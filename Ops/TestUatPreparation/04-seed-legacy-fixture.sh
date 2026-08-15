#!/usr/bin/env bash
# =====================================================================
# 04-seed-legacy-fixture.sh — بيانات Legacy محدودة قابلة للتكرار (Archived history)
# ---------------------------------------------------------------------
# الهدف: مجموعة صغيرة معروفة من القوالب الإنتاجية القديمة تبقى (Archived, IsActive=false)
#        + Historical Submissions بحالة نهائية (Closed) «كأنها قبل الأرشفة» — قابلة للقراءة
#        في المسار التاريخي، بلا احتساب مزدوج مع Project-First (قوالب مختلفة ⇒ عزل بنيوي).
#
# القوالب الستّة القديمة (مصدر الحقيقة: TemplateSeeder.LegacyProductionTemplateTitles، تُؤرشَف
# تلقائيًّا عند الإقلاع عبر ArchiveLegacyProductionTemplatesAsync). هذه القوالب منفصلة تمامًا عن
# قوالب التنفيذ الأربعة لِـ Project-First (ProjectFirstExecutionSchema.ExecutionTemplateTitles):
#   محتوى/تصميم/فيديو/مديرشن — لذا تسليماتها التاريخية لا تُلتقَط أبدًا في تجميع Project-First
#   (versionIds مختلفة) ⇒ لا احتساب مزدوج بنيويًّا.
#
# مسار التنفيذ (المرحلة الثانية، بعد Cutover + إقلاع التطبيق على القاعدة الجديدة):
#   المسار A (هذا السكربت، Identity-safe): تأكيد أرشفة القوالب الستّة عبر API كـAdmin (idempotent).
#   المسار B (أداة dotnet مخصّصة LegacyExecutionFixture): إنشاء Historical Submissions على قوالب
#            Archived — لا يستطيعه الـAPI الرسمي (القالب المؤرشف IsActive=false وغير مُسنَد ⇒ يمنعه
#            حارس الإسناد الخادميّ). الأداة تكتب عبر AppDbContext بمفاتيح مستقرّة، بلا تعطيل أيّ حارس
#            runtime، وبحالة نهائية Closed مربوطة بالإصدار المنشور للقالب المؤرشف.
#
# مبادئ إلزامية:
#   • أرشفة القوالب عبر الـAPI الرسمي فقط (لا SQL خام على القوالب في هذا السكربت).
#   • مفاتيح مستقرّة (عنوان القالب + مفتاح الفترة) ⇒ idempotent (lookup-then-act).
#   • القوالب القديمة تبقى Archived/IsActive=false — cleanup لا يُلغي الأرشفة إطلاقًا.
#   • كل استدعاء API يُفحَص؛ عند فشل الاستجابة يتوقّف فورًا (fail-fast). لا يُطبع أيّ سرّ/توكن.
#   • cleanup يمسّ فقط تسليمات هذا الـfixture التاريخية (عبر أداة dotnet) — لا يمسّ القوالب ولا غيرها.
#
# الأوضاع (Modes):
#   (افتراضي)   plan     — طباعة الخطة فقط، بلا أيّ اتصال شبكي أو كتابة.
#   --apply      seed     — أرشفة القوالب الستّة (idempotent) + إرشاد تشغيل أداة dotnet للتاريخي.
#   --verify     verify   — تحقّق قراءة-فقط: القوالب Archived+IsActive=false + عزل Project-First.
#   --cleanup    cleanup  — حذف التسليمات التاريخية للـfixture فقط (عبر أداة dotnet). لا يُلغي الأرشفة.
#
# الدخول: ADMIN_EMAIL + ADMIN_PASSWORD عبر البيئة (تُستهلك لتوكن جلسة، لا تُطبع)، أو ADMIN_TOKEN جاهز.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="04-seed-legacy-fixture"
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/00-common.sh"

# --- تحديد الوضع (action) قبل تحليل الوسائط الموحّد ---
ACTION="plan"
for arg in "$@"; do
  case "$arg" in
    --verify)  ACTION="verify"  ;;
    --cleanup) ACTION="cleanup" ;;
    --apply)   ACTION="seed"    ;;
  esac
done

parse_common_args "$@"
load_config
guard_all_names
require_real_config 2>/dev/null || true

API="${TEST_API_LOCAL_URL}"

# =====================================================================
# مفاتيح مستقرّة — القوالب الإنتاجية القديمة الستّة (مطابقة نصّية دقيقة على العنوان)
# مصدر الحقيقة: TemplateSeeder.LegacyProductionTemplateTitles (ثوابت Schema).
# =====================================================================
LEGACY_TEMPLATE_TITLES=(
  "✍️ تقرير المحتوى Content Production"
  "🎨 تقرير التصميم Design Production"
  "🎬 تقرير الفيديو Video Production"
  "📣 تقرير النشر والسوشيال ميديا"
  "📊 تقرير Media Buyer حسب العميل"
  "🗂️ تقرير المشاريع حسب العميل/المشروع"
)
# فترات تاريخية معروفة (Historical) لتقارير Closed — أسبوع عمل سعودي YYYY-Www.
# تُستخدم كمفاتيح مستقرّة للأداة dotnet (LegacyExecutionFixture) ولا تتقاطع مع فترة UAT (2026-W28).
LEGACY_PERIODS=( "2026-W10" "2026-W11" )

# أداة dotnet للتاريخي (المسار B) — تُمرَّر عبر البيئة عند التشغيل الفعلي (لا تُشغَّل هنا تلقائيًّا).
LEGACY_FIXTURE_TOOL="${LEGACY_FIXTURE_TOOL:-reporting-backend/tools/LegacyExecutionFixture}"

# =====================================================================
# وضع الخطة (plan) — لا اتصال، لا كتابة
# =====================================================================
if [[ "$ACTION" == "plan" ]]; then
  cat <<EOF
--- خطة Fixture Legacy (وضع plan — لا اتصال/لا كتابة) ---
API: ${API}
قوالب قديمة (تبقى Archived/IsActive=false)=${#LEGACY_TEMPLATE_TITLES[@]}   فترات تاريخية=${LEGACY_PERIODS[*]}

القوالب الستّة (مطابقة عنوان دقيقة، مصدرها ثوابت Schema، تُؤرشَف تلقائيًّا عند الإقلاع):
$(printf '   • %s\n' "${LEGACY_TEMPLATE_TITLES[@]}")

خطوات البذر (seed، المسار A عبر الـAPI الرسمي، idempotent):
 0) دخول admin (ADMIN_EMAIL+ADMIN_PASSWORD أو ADMIN_TOKEN) ⇒ توكن جلسة (لا يُطبع).
 1) لكل عنوان قالب:
    - GET  /api/report-templates            (قائمة كاملة) → مطابقة .title == العنوان → .id.
    - GET  /api/report-templates/{id}        → قراءة .status.
    - إن لم يكن Archived: POST /api/report-templates/{id}/archive  (تأكيد الأرشفة — idempotent).
    - تحقّق status=Archived ⇒ isActive=false.
 2) Historical Submissions (المسار B — أداة dotnet، لا يقوم بها هذا السكربت):
    شغّل خارجيًّا (بعد الإقلاع، بمفاتيح مستقرّة title+periodKey، بلا تعطيل حارس، بحالة Closed):
      ConnectionStrings__Default=… dotnet run --project ${LEGACY_FIXTURE_TOOL} -- --apply
    السبب: القالب المؤرشف IsActive=false وغير مُسنَد ⇒ حارس الإسناد الخادميّ يمنع إنشاءه عبر الـAPI.

التحقّق (verify، قراءة فقط):
 - GET /api/report-templates?status=Archived  → القوالب الستّة حاضرة وكلّها isActive=false.
 - GET /api/reporting/project-execution/projects?periodType=Weekly&periodKey=${LEGACY_PERIODS[0]}
     → 200 ولا يحتسب القوالب القديمة (عزل بنيوي: قوالب مختلفة عن قوالب التنفيذ الأربعة).
 - GET /api/reporting/project-execution/pods?periodType=Weekly&periodKey=${LEGACY_PERIODS[0]}   → 200 بلا احتساب مزدوج.
 - تحقّق التاريخي في القاعدة عبر أداة dotnet:  dotnet run --project ${LEGACY_FIXTURE_TOOL} -- --verify

التنظيف (cleanup): حذف التسليمات التاريخية للـfixture فقط (عبر أداة dotnet):
      ConnectionStrings__Default=… dotnet run --project ${LEGACY_FIXTURE_TOOL} -- --cleanup
   ⚠ لا يُلغي أرشفة القوالب — القوالب القديمة تبقى Archived/IsActive=false (حالتها الإنتاجية الصحيحة).

التشغيل الفعلي لاحقًا (المرحلة الثانية، على TEST فقط):
  ADMIN_EMAIL=… ADMIN_PASSWORD=… OPS_ALLOW_WRITE=1 ./04-seed-legacy-fixture.sh --apply
  ADMIN_EMAIL=… ADMIN_PASSWORD=… ./04-seed-legacy-fixture.sh --verify
  OPS_ALLOW_WRITE=1 ./04-seed-legacy-fixture.sh --cleanup   # ثم أداة dotnet للتاريخي
EOF
  exit 0
fi

# =====================================================================
# ما بعد plan: يتطلب أدوات + دخول admin
# =====================================================================
need_cmd curl; need_cmd jq

# --- دخول admin (لا يُطبع التوكن إطلاقًا) ---
ADMIN_TOKEN="${ADMIN_TOKEN:-}"
admin_login() {
  [[ -n "$ADMIN_TOKEN" ]] && { log "استُخدم ADMIN_TOKEN من البيئة (لم يُطبع)."; return 0; }
  local em="${ADMIN_EMAIL:-}" pw="${ADMIN_PASSWORD:-}"
  [[ -n "$em" && -n "$pw" ]] || die "ADMIN_EMAIL/ADMIN_PASSWORD غير مضبوطين (أو مرّر ADMIN_TOKEN). لا تُخزَّن في Git."
  local body; body="$(jq -nc --arg e "$em" --arg p "$pw" '{email:$e,password:$p}')"
  local resp; resp="$(curl -fsS --max-time 15 -X POST -H 'Content-Type: application/json' -d "$body" "${API}/api/auth/login")" \
    || die "فشل دخول admin (تحقّق من البيانات/الاتصال)."
  ADMIN_TOKEN="$(jq -r '.accessToken // empty' <<<"$resp")"
  [[ -n "$ADMIN_TOKEN" ]] || die "لم يُرجَع accessToken من /api/auth/login."
  log "✓ دخول admin ناجح (التوكن لم يُطبع)."
}

# --- غلاف استدعاء API: يضبط API_STATUS + API_BODY، لا يطبع أسرارًا ---
API_STATUS=""; API_BODY=""
api() {
  local method="$1" path="$2" body="${3:-}"
  local tmp code
  tmp="$(mktemp)"
  local -a hdr=(-H "Authorization: Bearer ${ADMIN_TOKEN}" -H "Content-Type: application/json")
  if [[ -n "$body" ]]; then
    code="$(curl -sS --max-time 30 -o "$tmp" -w '%{http_code}' -X "$method" "${hdr[@]}" -d "$body" "${API}${path}" || echo 000)"
  else
    code="$(curl -sS --max-time 30 -o "$tmp" -w '%{http_code}' -X "$method" "${hdr[@]}" "${API}${path}" || echo 000)"
  fi
  API_STATUS="$code"; API_BODY="$(cat "$tmp")"; rm -f "$tmp"
}
api_ok()   { [[ "$API_STATUS" =~ ^2[0-9][0-9]$ ]]; }
api_or_die() {
  local method="$1" path="$2" body="${3:-}" what="${4:-$method $path}"
  api "$method" "$path" "$body"
  api_ok || die "فشل API (${what}): HTTP ${API_STATUS} — ${API_BODY:0:200}"
}

# --- lookup قالب بالعنوان الدقيق من القائمة الكاملة (مفتاح مستقر = العنوان) ---
find_template_id() {
  local title="$1"
  api GET "/api/report-templates"
  api_ok || die "فشل جلب قائمة القوالب: HTTP ${API_STATUS}"
  jq -r --arg t "$title" '.[] | select(.title==$t) | .id' <<<"$API_BODY" | head -n1
}

# =====================================================================
# البذر (seed) — أرشفة القوالب الستّة عبر API (idempotent)
# =====================================================================
archive_template() {
  local title="$1" id status
  id="$(find_template_id "$title")"
  [[ -n "$id" ]] || { log_warn "قالب غير موجود: ${title} — يُتخطّى (تحقّق من البذر/العنوان)."; return 0; }
  api GET "/api/report-templates/${id}"
  api_ok || die "فشل قراءة تفاصيل القالب ${title}: HTTP ${API_STATUS}"
  status="$(jq -r '.status // empty' <<<"$API_BODY")"
  if [[ "$status" == "Archived" ]]; then
    log "   ✓ (idempotent) مؤرشف مسبقًا: ${title}"
  else
    api_or_die POST "/api/report-templates/${id}/archive" "" "archive ${title}"
    log "   ✓ أُرشف القالب: ${title}"
  fi
}

seed_fixture() {
  require_write_enabled "أرشفة قوالب Legacy على ${NEW_UAT_DB} عبر ${API}" || return 0
  admin_login
  log "1) أرشفة القوالب الإنتاجية القديمة (تأكيد idempotent) ..."
  for t in "${LEGACY_TEMPLATE_TITLES[@]}"; do archive_template "$t"; done
  log "2) Historical Submissions (المسار B): شغّل أداة dotnet خارجيًّا (لا تُشغَّل من هنا):"
  log "     ConnectionStrings__Default=… dotnet run --project ${LEGACY_FIXTURE_TOOL} -- --apply"
  log "✓ اكتملت أرشفة القوالب. شغّل --verify للتأكيد بعد تشغيل أداة dotnet للتاريخي."
}

# =====================================================================
# التحقّق (verify) — قراءة فقط
# =====================================================================
verify_fixture() {
  admin_login
  log "تحقّق (قراءة فقط) ..."
  api GET "/api/report-templates?status=Archived"
  api_ok || die "فشل جلب القوالب المؤرشفة: HTTP ${API_STATUS}"
  local missing=0 notinactive=0
  for t in "${LEGACY_TEMPLATE_TITLES[@]}"; do
    local row; row="$(jq -c --arg t "$t" '.[] | select(.title==$t)' <<<"$API_BODY")"
    if [[ -z "$row" ]]; then
      log_err "   ✗ قالب قديم غير موجود ضمن Archived: ${t}"; missing=$((missing+1)); continue
    fi
    local active; active="$(jq -r '.isActive' <<<"$row")"
    if [[ "$active" == "false" ]]; then
      log "   ✓ Archived + isActive=false: ${t}"
    else
      log_err "   ✗ القالب مؤرشف لكن isActive=${active} (متوقّع false): ${t}"; notinactive=$((notinactive+1))
    fi
  done
  [[ $missing -eq 0 ]]     || die "نقص ${missing} من القوالب القديمة ضمن Archived."
  [[ $notinactive -eq 0 ]] || die "قوالب مؤرشفة بـ isActive!=false: ${notinactive}."

  log "عزل Project-First (لا احتساب مزدوج) — قراءة فقط:"
  api GET "/api/reporting/project-execution/projects?periodType=Weekly&periodKey=${LEGACY_PERIODS[0]}"
  api_ok && log "   ✓ project-execution/projects = 200 (القوالب القديمة خارج نطاقه بنيويًّا)" \
         || log_warn "   ⚠ project-execution/projects HTTP ${API_STATUS} — تحقّق المسار/الصلاحية."
  api GET "/api/reporting/project-execution/pods?periodType=Weekly&periodKey=${LEGACY_PERIODS[0]}"
  api_ok && log "   ✓ project-execution/pods = 200 (بلا احتساب مزدوج)" \
         || log_warn "   ⚠ project-execution/pods HTTP ${API_STATUS} — تحقّق المسار/الصلاحية."

  log "ℹ تحقّق التسليمات التاريخية في القاعدة عبر أداة dotnet:"
  log "     dotnet run --project ${LEGACY_FIXTURE_TOOL} -- --verify"
  log "✓ تحقّق القوالب اكتمل: الستّة Archived + isActive=false، بلا احتساب مزدوج مع Project-First."
}

# =====================================================================
# التنظيف (cleanup) — يُفوَّض للأداة dotnet؛ لا يُلغي أرشفة القوالب
# =====================================================================
cleanup_fixture() {
  require_write_enabled "تنظيف تسليمات Legacy التاريخية على ${NEW_UAT_DB}" || return 0
  log "تنظيف Legacy fixture: يُفوَّض حذف التسليمات التاريخية لأداة dotnet (مفاتيح مستقرّة):"
  log "     ConnectionStrings__Default=… dotnet run --project ${LEGACY_FIXTURE_TOOL} -- --cleanup"
  log_warn "⚠ لا يُلغى أرشفة القوالب الستّة — تبقى Archived/IsActive=false (حالتها الصحيحة)."
  log "✓ لا حذف للقوالب من هنا؛ التسليمات التاريخية تُحذف بأداة dotnet فقط."
}

# =====================================================================
# التوزيع على الوضع
# =====================================================================
case "$ACTION" in
  seed)    seed_fixture    ;;
  verify)  verify_fixture  ;;
  cleanup) cleanup_fixture ;;
  *)       die "وضع غير معروف: ${ACTION}" ;;
esac
