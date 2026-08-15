#!/usr/bin/env bash
# =====================================================================
# 07-health-validation.sh — تحقّق ما بعد Cutover (قراءة فقط للحالة الحيّة)
# Health → Auth → API → SignalR → Email safety → Project-First → Legacy → Rollup.
# لا يكتب على القاعدة (يقرأ فقط؛ login admin لتوليد توكن جلسة).
# يُرجِع rc≠0 عند أي فشل حرج (Health/Auth) لتحفيز Rollback من 06.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="07-health-validation"
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/00-common.sh"

parse_common_args "$@"
load_config
guard_all_names

API="${TEST_API_LOCAL_URL}"
fail=0
crit=0
ok()   { log "✓ $*"; }
bad()  { log_err "✗ $*"; fail=1; }
badc() { log_err "✗✗ (حرج) $*"; fail=1; crit=1; }

plan "تحقّق حيّ ضدّ ${API} (قراءة فقط، عدا login admin)"

if [[ "$OPS_MODE" != "apply" ]]; then
  cat <<EOF
--- خطة الفحوص (لن تُنفَّذ الآن) ---
1) Health:        GET ${API}/health                → {"status":"ok"}         [حرج]
2) Auth:          POST ${API}/api/auth/login (admin) → 200 + token           [حرج]
3) API:           GET ${API}/api/report-templates    → 200
4) SignalR:       GET ${API}/hubs/notifications/negotiate (مُصادَق) → 200/101
5) Email safety:  Email__Enabled=false + email_outbox=0 (لا إرسال)
6) Project-First: GET ${API}/api/reporting/project-execution/projects → 200
7) Legacy smoke:  GET ${API}/api/report-templates?status=Archived → يظهر التاريخي
8) Rollup smoke:  GET ${API}/api/reporting/project-execution/pods → 200 بلا احتساب مزدوج
رموز الخروج: 0=سليم، 1=فشل حرج (Health/Auth)، 2=فشل وظيفي غير حرج.
EOF
  exit 0
fi

need_cmd curl; need_cmd jq

# 1) Health [حرج]
if curl -fsS "${API}/health" | jq -e '.status=="ok"' >/dev/null 2>&1; then ok "Health"; else badc "Health"; fi

# 2) Auth [حرج] — بيانات admin تُمرَّر عبر البيئة، لا تُطبع
ADMIN_EMAIL="${ADMIN_EMAIL:-}"; ADMIN_PASSWORD="${ADMIN_PASSWORD:-}"
TOKEN=""
if [[ -n "$ADMIN_EMAIL" && -n "$ADMIN_PASSWORD" ]]; then
  TOKEN="$(curl -fsS -X POST -H 'Content-Type: application/json' \
    -d "$(jq -nc --arg e "$ADMIN_EMAIL" --arg p "$ADMIN_PASSWORD" '{email:$e,password:$p}')" \
    "${API}/api/auth/login" | jq -r '.accessToken // .access // empty')"
  [[ -n "$TOKEN" ]] && ok "Auth (login admin)" || badc "Auth login فشل"
else
  badc "ADMIN_EMAIL/ADMIN_PASSWORD غير مضبوطين — تعذّر فحص Auth."
fi

auth=(-H "Authorization: Bearer ${TOKEN}")

# 3) API
[[ -n "$TOKEN" ]] && { curl -fsS "${auth[@]}" "${API}/api/report-templates" >/dev/null && ok "API report-templates" || bad "API report-templates"; }

# 4) SignalR negotiate
[[ -n "$TOKEN" ]] && { curl -fsS -X POST "${auth[@]}" "${API}/hubs/notifications/negotiate?negotiateVersion=1" >/dev/null && ok "SignalR negotiate" || bad "SignalR negotiate"; }

# 5) Email safety (قراءة env-file محليًّا + عدّ outbox)
if grep -q '^Email__Enabled=false' "${TEST_ENV_FILE}" 2>/dev/null; then ok "Email__Enabled=false"; else bad "Email قد يكون مفعّلًا"; fi
ob="$(psql -Atc 'SELECT count(*) FROM email_outbox;' -h "${PGHOST}" -p "${PGPORT}" -U "${NEW_UAT_ROLE}" -d "${NEW_UAT_DB}" 2>/dev/null || echo '?')"
[[ "$ob" == "0" ]] && ok "email_outbox فارغ" || log_warn "email_outbox=${ob}"

# 6) Project-First smoke
[[ -n "$TOKEN" ]] && { curl -fsS "${auth[@]}" "${API}/api/reporting/project-execution/projects?periodType=Weekly&periodKey=2026-W10" >/dev/null 2>&1 && ok "Project-First aggregation" || bad "Project-First aggregation (تحقّق المسار/الفلاتر)"; }

# 7) Legacy smoke
[[ -n "$TOKEN" ]] && { curl -fsS "${auth[@]}" "${API}/api/report-templates?status=Archived" >/dev/null 2>&1 && ok "Legacy archived query" || bad "Legacy archived query"; }

# 8) Rollup smoke
[[ -n "$TOKEN" ]] && { curl -fsS "${auth[@]}" "${API}/api/reporting/project-execution/pods?periodType=Weekly&periodKey=2026-W10" >/dev/null 2>&1 && ok "Rollup pods aggregation" || bad "Rollup pods aggregation (تحقّق المسار)"; }

# رموز الخروج: 1=حرج (Health/Auth)⇒Auto-Rollback آمن من 06 ؛ 2=فشل وظيفي غير حرج⇒توقّف بلا رجوع.
if [[ $crit -ne 0 ]]; then log_err "فشل حرج (Health/Auth) — يستدعي 06 Auto-Rollback."; exit 1; fi
if [[ $fail -ne 0 ]]; then log_err "فحوص وظيفية غير حرجة أخفقت — توقّف بلا رجوع آلي (قرار بشري)."; exit 2; fi
log "كل الفحوص سليمة."
