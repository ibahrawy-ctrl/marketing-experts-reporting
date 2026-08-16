#!/usr/bin/env bash
# =============================================================================
#  Migration History Bridge — جسر سجلّ الهجرات
#  تذكرة: RECONCILE-PROD-DEVELOP-LINEAGE
#
#  الغرض:
#    نَسَب الإنتاج/RC طبّق هجرتين بمعرّفَين زمنيَّين مختلفَين عن معرّفَي develop
#    المعتمدَين، مع **محتوى SQL متطابق حرفيًّا** (أُثبِت بـmd5 بعد تطبيع المعرّفات):
#      20260622144900_KpiTemplateAssignmentsPhaseT1 ⟵ يكافئ ⟶ 20260622140138_KpiTemplateAssignmentsPhaseT1
#      20260626135944_AddReportViewGrants           ⟵ يكافئ ⟶ 20260626124527_AddReportViewGrants
#    لذلك تظهر الهجرتان المعتمدتان لدى EF Core على أنّهما «معلَّقتان»، فتحاول
#    CREATE TABLE على جدولَين موجودَين ⟹ 42P07 وتوقّف الخدمة عند الإقلاع.
#
#  ما تفعله هذه الأداة — وما لا تفعله:
#    ✔ تُدرِج **صفَّي سجلّ (Alias) فقط** في "__EFMigrationsHistory" بالمعرّفَين المعتمدَين.
#    ✘ لا تنفّذ أيّ DDL، ولا تلمس أيّ جدول أعمال، ولا تعدّل ولا تحذف أيّ صفّ بيانات.
#    ✘ لا تحذف الصفَّين التاريخيَّين (يبقيان شاهدَين على التاريخ الفعليّ للبيئة).
#
#  الوضع الافتراضيّ **Dry Run**. التنفيذ الحقيقيّ يحتاج ‎--apply‎ صراحةً،
#  وبيئة الإنتاج تحتاج ‎--allow-production‎ إضافيًّا.
#
#  الاتصال: عبر متغيّرات libpq القياسيّة (PGHOST/PGPORT/PGUSER/PGPASSWORD/PGPASSFILE)
#  أو تنفيذ محلّيّ بمصادقة peer. **لا تُمرَّر كلمات السرّ في سطر الأوامر ولا تُطبَع أبدًا.**
# =============================================================================
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FINGERPRINT_SQL="$SCRIPT_DIR/fingerprint.sql"
DEFAULT_FP_FILE="$SCRIPT_DIR/expected-fingerprint-prod-rc.txt"

# ---- المعرّفات المعتمدة (Canonical) والمعرّفات التاريخيّة (Legacy) ----------
CANON_1="20260622140138_KpiTemplateAssignmentsPhaseT1"
LEGACY_1="20260622144900_KpiTemplateAssignmentsPhaseT1"
TABLE_1="kpi_template_assignments"

CANON_2="20260626124527_AddReportViewGrants"
LEGACY_2="20260626135944_AddReportViewGrants"
TABLE_2="report_view_grants"

# ---- الوسائط ----------------------------------------------------------------
DB=""; ENVNAME=""; APPLY=0; ALLOW_PROD=0; FP_FILE="$DEFAULT_FP_FILE"
EXPECTED_COMMIT=""; SKIP_FP=0; OUT_DIR="${BRIDGE_OUT_DIR:-/tmp}"

usage() {
  cat <<'USAGE'
usage: bridge.sh --db <dbname> --env <test|rc|production> [options]
  --db NAME              اسم قاعدة البيانات المستهدَفة (إلزاميّ)
  --env NAME             هويّة البيئة: test | rc | production (إلزاميّ، قائمة سماح مغلقة)
  --apply                تنفيذ حقيقيّ داخل معاملة واحدة (الافتراضيّ Dry Run)
  --allow-production     مطلوب إضافةً إلى --apply عندما تكون البيئة production
  --fingerprint-file F   ملفّ البصمة المرجعيّة (الافتراضيّ: expected-fingerprint-prod-rc.txt)
  --expected-commit SHA  التحقّق من أنّ الكود المنشور على الخادم يطابق هذا الالتزام
  --skip-fingerprint     تخطّي مطابقة البصمة (للنسخ التجريبيّة فقط؛ يُسجَّل تحذير)
  --out-dir DIR          مجلّد سجلّ التنفيذ وسكربت التراجع (الافتراضيّ /tmp)
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --db) DB="${2:-}"; shift 2 ;;
    --env) ENVNAME="${2:-}"; shift 2 ;;
    --apply) APPLY=1; shift ;;
    --allow-production) ALLOW_PROD=1; shift ;;
    --fingerprint-file) FP_FILE="${2:-}"; shift 2 ;;
    --expected-commit) EXPECTED_COMMIT="${2:-}"; shift 2 ;;
    --skip-fingerprint) SKIP_FP=1; shift ;;
    --out-dir) OUT_DIR="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "وسيط غير معروف: $1" >&2; usage; exit 2 ;;
  esac
done

TS="$(date -u +%Y%m%dT%H%M%SZ)"
LOG="$OUT_DIR/bridge-${DB:-nodb}-$TS.log"
ROLLBACK="$OUT_DIR/bridge-rollback-${DB:-nodb}-$TS.sql"

log()  { printf '%s\n' "$*" | tee -a "$LOG"; }
fail() { log "REFUSED: $*"; log "RESULT = REFUSED"; exit 3; }

q() { psql -d "$DB" -At -v ON_ERROR_STOP=1 -c "$1"; }

mkdir -p "$OUT_DIR"; : > "$LOG"
log "=== Migration History Bridge — $TS (UTC) ==="
log "db=$DB env=$ENVNAME mode=$([[ $APPLY -eq 1 ]] && echo APPLY || echo DRY-RUN)"

# ---- 1) هويّة البيئة وقائمة السماح ------------------------------------------
[[ -n "$DB" ]] || fail "--db مطلوب."
case "$ENVNAME" in
  test|rc|production) ;;
  *) fail "بيئة خارج قائمة السماح: '${ENVNAME:-<فارغ>}' (المسموح: test|rc|production)." ;;
esac
if [[ "$ENVNAME" == "production" && $APPLY -eq 1 && $ALLOW_PROD -ne 1 ]]; then
  fail "التنفيذ على الإنتاج يحتاج --allow-production صراحةً."
fi
log "[1/9] هويّة البيئة …………………… OK"

# ---- 2) الاتصال ووجود القاعدة -----------------------------------------------
command -v psql >/dev/null || fail "psql غير متاح في PATH."
SRV="$(q "select version();" | cut -d, -f1)" || fail "تعذّر الاتصال بالقاعدة '$DB'."
log "[2/9] الاتصال ………………………… OK ($SRV)"

# ---- 3) الالتزام المتوقَّع (اختياريّ) ----------------------------------------
if [[ -n "$EXPECTED_COMMIT" ]]; then
  ACTUAL="$(git -C "${BRIDGE_REPO_DIR:-$SCRIPT_DIR/../..}" rev-parse HEAD 2>/dev/null || echo '')"
  [[ -n "$ACTUAL" ]] || fail "تعذّر قراءة HEAD للتحقّق من الالتزام المتوقَّع."
  [[ "$ACTUAL" == "$EXPECTED_COMMIT"* ]] || fail "الالتزام لا يطابق المتوقَّع (متوقَّع $EXPECTED_COMMIT)."
  log "[3/9] الالتزام المتوقَّع ………… OK ($EXPECTED_COMMIT)"
else
  log "[3/9] الالتزام المتوقَّع ………… تُخطّي (لم يُمرَّر)"
fi

# ---- 4) لا معاملات معلّقة على القاعدة ---------------------------------------
IDLE="$(q "select count(*) from pg_stat_activity where datname = current_database() and pid <> pg_backend_pid() and state in ('idle in transaction','idle in transaction (aborted)');")"
[[ "$IDLE" == "0" ]] || fail "توجد $IDLE جلسة/جلسات بمعاملة معلّقة على '$DB' — أوقف الخدمة أوّلًا."
log "[4/9] لا معاملات معلّقة ………… OK"

# ---- 5) جدول سجلّ الهجرات ومجموعة الهجرات المطبَّقة --------------------------
HAS_HIST="$(q "select count(*) from information_schema.tables where table_schema='public' and table_name='__EFMigrationsHistory';")"
[[ "$HAS_HIST" == "1" ]] || fail "\"__EFMigrationsHistory\" غير موجود — القاعدة ليست قاعدة EF مهاجَرة."
APPLIED_N="$(q "select count(*) from \"__EFMigrationsHistory\";")"
log "[5/9] سجلّ الهجرات ……………… OK (المطبَّق = $APPLIED_N)"

# ---- 6) وجود صفَّي المصدر التاريخيَّين ---------------------------------------
for m in "$LEGACY_1" "$LEGACY_2"; do
  n="$(q "select count(*) from \"__EFMigrationsHistory\" where \"MigrationId\" = '$m';")"
  [[ "$n" == "1" ]] || fail "الصفّ التاريخيّ '$m' غير موجود (وُجد $n) — هذه البيئة ليست من نَسَب الإنتاج."
done
log "[6/9] الصفّان التاريخيّان ……… OK"

# ---- 7) وجود الجدولَين فعليًّا ------------------------------------------------
for t in "$TABLE_1" "$TABLE_2"; do
  n="$(q "select count(*) from information_schema.tables where table_schema='public' and table_name='$t';")"
  [[ "$n" == "1" ]] || fail "الجدول '$t' غير موجود — لا مبرّر للجسر (الهجرة المعتمدة يجب أن تُنفَّذ فعليًّا)."
done
log "[7/9] وجود الجدولَين ………………… OK"

# ---- 8) غياب الصفَّين المعتمدَين (وإلّا: الأداة مُنفَّذة سلفًا ⟹ خروج نظيف) ----
P1="$(q "select count(*) from \"__EFMigrationsHistory\" where \"MigrationId\" = '$CANON_1';")"
P2="$(q "select count(*) from \"__EFMigrationsHistory\" where \"MigrationId\" = '$CANON_2';")"
if [[ "$P1" == "1" && "$P2" == "1" ]]; then
  log "[8/9] الصفّان المعتمدان …………… موجودان سلفًا"
  log "لا شيء لتنفيذه — الأداة idempotent."
  log "RESULT = ALREADY-BRIDGED (no-op)"
  exit 0
fi
[[ "$P1" == "0" && "$P2" == "0" ]] || fail "حالة جزئيّة: $CANON_1=$P1 و $CANON_2=$P2 — تدخّل يدويّ مطلوب."
log "[8/9] الصفّان المعتمدان …………… غائبان (كما هو متوقَّع)"

# ---- 9) بصمة المخطَّط الكاملة -------------------------------------------------
if [[ $SKIP_FP -eq 1 ]]; then
  log "[9/9] بصمة المخطَّط ……………………… تُخطّي (--skip-fingerprint) ⚠"
else
  [[ -f "$FINGERPRINT_SQL" ]] || fail "ملفّ الاستعلام $FINGERPRINT_SQL غير موجود."
  [[ -f "$FP_FILE" ]] || fail "ملفّ البصمة المرجعيّة $FP_FILE غير موجود."
  ACT_FP="$OUT_DIR/bridge-actual-fp-$DB-$TS.txt"
  psql -d "$DB" -At -v ON_ERROR_STOP=1 -f "$FINGERPRINT_SQL" > "$ACT_FP"
  if ! diff -q "$FP_FILE" "$ACT_FP" >/dev/null; then
    log "فروقات البصمة (أوّل 20 سطرًا):"
    diff "$FP_FILE" "$ACT_FP" | head -20 | tee -a "$LOG"
    fail "بصمة المخطَّط لا تطابق المرجع — البيئة ليست في الحالة المتوقَّعة."
  fi
  log "[9/9] بصمة المخطَّط ……………………… OK (متطابقة سطرًا بسطر)"
fi

# ---- لقطة «قبل» --------------------------------------------------------------
BEFORE_N="$APPLIED_N"
BEFORE_TBL="$(q "select count(*) from information_schema.tables where table_schema='public' and table_type='BASE TABLE';")"
log "قبل: صفوف السجلّ = $BEFORE_N | جداول = $BEFORE_TBL"

if [[ $APPLY -ne 1 ]]; then
  log ""
  log "DRY RUN — لن يُكتب أيّ شيء. العمليّة المزمعة:"
  log "  INSERT \"__EFMigrationsHistory\"(\"MigrationId\",\"ProductVersion\") ← '$CANON_1'"
  log "  INSERT \"__EFMigrationsHistory\"(\"MigrationId\",\"ProductVersion\") ← '$CANON_2'"
  log "  (بنفس ProductVersion المأخوذ من الصفّ التاريخيّ المقابل)"
  log "المتوقَّع بعد التنفيذ: صفوف السجلّ = $((BEFORE_N + 2)) | جداول = $BEFORE_TBL (بلا تغيير)"
  log "RESULT = DRY-RUN OK (ready to apply)"
  exit 0
fi

# ---- سكربت التراجع (يُكتب قبل التنفيذ) ---------------------------------------
cat > "$ROLLBACK" <<SQL
-- تراجع جسر سجلّ الهجرات — قاعدة: $DB — وقت التوليد: $TS (UTC)
-- يحذف حصريًّا الصفَّين اللذين أضافهما الجسر. لا يمسّ أيّ جدول أعمال ولا أيّ بيانات.
BEGIN;
DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" IN ('$CANON_1','$CANON_2');
-- تحقَّق من أنّ العدد عاد إلى $BEFORE_N قبل COMMIT:
SELECT count(*) AS history_rows FROM "__EFMigrationsHistory";
COMMIT;
SQL
log "سكربت التراجع: $ROLLBACK"

# ---- التنفيذ داخل معاملة واحدة ----------------------------------------------
psql -d "$DB" -v ON_ERROR_STOP=1 <<SQL >>"$LOG" 2>&1
BEGIN;
INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
SELECT '$CANON_1', "ProductVersion" FROM "__EFMigrationsHistory" WHERE "MigrationId"='$LEGACY_1';
INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
SELECT '$CANON_2', "ProductVersion" FROM "__EFMigrationsHistory" WHERE "MigrationId"='$LEGACY_2';
COMMIT;
SQL

AFTER_N="$(q "select count(*) from \"__EFMigrationsHistory\";")"
AFTER_TBL="$(q "select count(*) from information_schema.tables where table_schema='public' and table_type='BASE TABLE';")"
log "بعد: صفوف السجلّ = $AFTER_N | جداول = $AFTER_TBL"
[[ "$AFTER_N" == "$((BEFORE_N + 2))" ]] || fail "عدد صفوف السجلّ غير متوقَّع (متوقَّع $((BEFORE_N + 2)))."
[[ "$AFTER_TBL" == "$BEFORE_TBL" ]] || fail "تغيّر عدد الجداول — يجب ألّا يحدث."

if [[ $SKIP_FP -ne 1 ]]; then
  psql -d "$DB" -At -v ON_ERROR_STOP=1 -f "$FINGERPRINT_SQL" > "$OUT_DIR/bridge-postfp-$DB-$TS.txt"
  diff -q "$FP_FILE" "$OUT_DIR/bridge-postfp-$DB-$TS.txt" >/dev/null \
    || fail "تغيّرت بصمة المخطَّط بعد التنفيذ — يجب ألّا يحدث."
  log "بصمة المخطَّط بعد التنفيذ …… بلا تغيير OK"
fi

log "RESULT = APPLIED (2 alias rows)"
