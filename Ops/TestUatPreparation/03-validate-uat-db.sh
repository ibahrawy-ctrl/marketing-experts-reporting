#!/usr/bin/env bash
# =====================================================================
# 03-validate-uat-db.sh — التحقّق بعد إقلاع التطبيق على القاعدة الجديدة
# READ-ONLY تمامًا (SELECT فقط). لا يكتب على أي قاعدة إطلاقًا.
# لا --apply ولا OPS_ALLOW_WRITE — لا علاقة لهذا السكربت بالكتابة.
# الأوضاع:
#   (افتراضي)      : يطبع خطة الاستعلامات فقط (لا اتصال بالقاعدة).
#   --run          : ينفّذ الاستعلامات فعليًّا (SELECT فقط) على القاعدة الجديدة.
# دفاع في العمق: الاتصال بـ default_transaction_read_only=on + حارس select_only
# يرفض أي استعلام لا يبدأ بـ SELECT.
# يتحقّق من:
#   - 30 هجرة + آخر هجرة الصحيحة.
#   - Catalog Seeders: Taxonomy = 170 صفًّا / 19 بُعدًا (execution_taxonomy_values."Domain").
#   - قوالب التقارير > 0 مع وجود Published (نشِط) وArchived (Legacy مؤرشف) معًا.
#   - admin مبذور موجود (AspNetUsers >= 1).
#   - عدم بذر OrgSeeder في Staging: لا مستخدمي ديمو @marketingexperts.local.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="03-validate-uat-db"
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/00-common.sh"

# --- تحليل وسائط خاص (لا apply/كتابة هنا إطلاقًا) ---
RUN_MODE="plan"     # plan | run
for arg in "$@"; do
  case "$arg" in
    --run)         RUN_MODE="run" ;;
    --plan)        RUN_MODE="plan" ;;
    -h|--help)
      cat <<'H'
الاستخدام: bash 03-validate-uat-db.sh [--run]
  (بلا وسائط)  يطبع خطة الاستعلامات (SELECT فقط) دون اتصال.
  --run        ينفّذ التحقّق READ-ONLY على القاعدة الجديدة.
هذا السكربت لا يكتب على أي قاعدة ولا يقبل --apply/OPS_ALLOW_WRITE.
H
      exit 0 ;;
  esac
done

load_config
guard_all_names

EXPECTED_MIGRATION_COUNT=30
LAST_MIGRATION="20260709231845_AddWorkstreamDeliverables"
EXPECTED_TAXONOMY_ROWS=170
EXPECTED_TAXONOMY_DOMAINS=19

# القاعدة الجديدة كـ read-only role + منع أي معاملة كتابة على مستوى الجلسة.
PSQL_RO=(env PGOPTIONS='-c default_transaction_read_only=on' \
  psql -v ON_ERROR_STOP=1 -h "${PGHOST}" -p "${PGPORT}" -U "${NEW_UAT_ROLE}" -d "${NEW_UAT_DB}")

# حارس: يرفض أي استعلام لا يبدأ بـ SELECT (بعد إزالة الفراغات).
select_only() {
  local sql="$1"
  local trimmed; trimmed="$(printf '%s' "$sql" | sed -e 's/^[[:space:]]*//')"
  if ! printf '%s' "$trimmed" | grep -Eiq '^select[[:space:](]'; then
    die "حارس READ-ONLY: رُفِض استعلام غير SELECT: ${sql}"
  fi
}

run_select() {  # يُنفّذ SELECT واحدًا ويطبع القيمة القِشرية (-Atc)
  local sql="$1"
  select_only "$sql"
  "${PSQL_RO[@]}" -Atc "$sql"
}

plan "READ-ONLY validation ضدّ ${NEW_UAT_DB} كـ${NEW_UAT_ROLE}"
plan "متوقّع: ${EXPECTED_MIGRATION_COUNT} هجرة، آخرها ${LAST_MIGRATION}، taxonomy=${EXPECTED_TAXONOMY_ROWS}/${EXPECTED_TAXONOMY_DOMAINS}"

if [[ "$RUN_MODE" != "run" ]]; then
  cat <<EOF
--- خطة الاستعلامات (SELECT فقط، لن تُنفَّذ الآن — مرّر --run للتنفيذ READ-ONLY) ---
SELECT count(*) FROM "__EFMigrationsHistory";                                    -- = ${EXPECTED_MIGRATION_COUNT}
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;  -- = ${LAST_MIGRATION}
SELECT count(*) FROM execution_taxonomy_values;                                 -- = ${EXPECTED_TAXONOMY_ROWS}
SELECT count(DISTINCT "Domain") FROM execution_taxonomy_values;                 -- = ${EXPECTED_TAXONOMY_DOMAINS}
SELECT count(*) FROM report_templates;                                          -- > 0 (TemplateSeeder)
SELECT count(*) FROM report_templates WHERE "Status" = 'Published';             -- > 0 (قوالب نشطة)
SELECT count(*) FROM report_templates WHERE "Status" = 'Archived';              -- > 0 (Legacy مؤرشف)
SELECT count(*) FROM "AspNetUsers";                                             -- >= 1 (admin مبذور)
SELECT count(*) FROM "AspNetUsers" WHERE lower("Email") LIKE '%@marketingexperts.local';  -- = 0 (OrgSeeder لم يعمل)
EOF
  exit 0
fi

need_cmd psql

fail=0
check_eq() { local label="$1" got="$2" want="$3"; if [[ "$got" == "$want" ]]; then log "✓ ${label}: ${got}"; else log_err "✗ ${label}: got=${got} want=${want}"; fail=1; fi; }
check_ge() { local label="$1" got="$2" min="$3"; if [[ "${got:-0}" -ge "$min" ]]; then log "✓ ${label}: ${got}"; else log_err "✗ ${label}: got=${got} min=${min}"; fail=1; fi; }
check_gt() { local label="$1" got="$2"; if [[ "${got:-0}" -gt 0 ]]; then log "✓ ${label}: ${got}"; else log_err "✗ ${label}: ${got} (=0)"; fail=1; fi; }

# 1) الهجرات
cnt="$(run_select 'SELECT count(*) FROM "__EFMigrationsHistory";')"
check_eq "عدد الهجرات" "$cnt" "$EXPECTED_MIGRATION_COUNT"

last="$(run_select 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
check_eq "آخر هجرة" "$last" "$LAST_MIGRATION"

# 2) Execution Taxonomy (السكيمة الفعلية: execution_taxonomy_values."Domain")
tx_rows="$(run_select 'SELECT count(*) FROM execution_taxonomy_values;')"
check_eq "صفوف Taxonomy" "$tx_rows" "$EXPECTED_TAXONOMY_ROWS"

tx_dom="$(run_select 'SELECT count(DISTINCT "Domain") FROM execution_taxonomy_values;')"
check_eq "أبعاد Taxonomy" "$tx_dom" "$EXPECTED_TAXONOMY_DOMAINS"

# 3) القوالب: إجمالي + نشِط (Published) + مؤرشف (Archived)
tmpl="$(run_select 'SELECT count(*) FROM report_templates;')"
check_gt "قوالب التقارير (إجمالي)" "$tmpl"

active="$(run_select "SELECT count(*) FROM report_templates WHERE \"Status\" = 'Published';")"
check_gt "قوالب نشطة (Published)" "$active"

archived="$(run_select "SELECT count(*) FROM report_templates WHERE \"Status\" = 'Archived';")"
check_gt "قوالب مؤرشفة (Archived/Legacy)" "$archived"

# 4) admin مبذور
users="$(run_select 'SELECT count(*) FROM "AspNetUsers";')"
check_ge "مستخدمون (admin مبذور)" "$users" 1

# 5) OrgSeeder لم يعمل في Staging: لا مستخدمي ديمو @marketingexperts.local
demo="$(run_select "SELECT count(*) FROM \"AspNetUsers\" WHERE lower(\"Email\") LIKE '%@marketingexperts.local';")"
check_eq "مستخدمو ديمو OrgSeeder (يجب 0)" "$demo" "0"

[[ $fail -eq 0 ]] || die "فشل التحقّق — لا تتابع Cutover."
log "اجتاز التحقّق READ-ONLY الكامل للقاعدة الجديدة."
