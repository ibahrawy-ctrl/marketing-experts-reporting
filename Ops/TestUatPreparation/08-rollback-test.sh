#!/usr/bin/env bash
# =====================================================================
# 08-rollback-test.sh — الرجوع الفوري لبيئة TEST إلى ما قبل Cutover
# يعيد: env-file السابق (⇒ Connection String + Environment السابقة؛ المصادقة JWT Bearer فقط)
#       ثم restart لخدمة TEST فقط ثم انتظار active + Health بمهلة.
# القاعدة القديمة لم تُحذف/تُعدَّل ⇒ الرجوع = تبديل env + restart فقط (ثوانٍ).
# افتراضيًّا PLAN. التنفيذ يتطلب --apply + OPS_ALLOW_WRITE=1 + تأكيد.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="08-rollback-test"
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/00-common.sh"

parse_common_args "$@"
load_config
guard_all_names
require_real_config

API="${TEST_API_LOCAL_URL}"
# ملف env السابق: إمّا يُمرَّر صراحةً، أو يُلتقط أحدث *.pre-uat-* بجوار TEST_ENV_FILE
ENV_PREV="${ENV_PREV:-}"
if [[ -z "$ENV_PREV" ]]; then
  ENV_PREV="$(ls -1t "${TEST_ENV_FILE}".pre-uat-* 2>/dev/null | head -n1 || true)"
fi

plan "الرجوع بـenv السابق: ${ENV_PREV:-(غير محدّد)} ثم restart ${TEST_SERVICE_NAME}"

if [[ "$OPS_MODE" != "apply" ]]; then
  cat <<EOF
--- خطة Rollback (لن تُنفَّذ الآن) ---
1) تحقّق وجود env السابق: ${ENV_PREV:-<TEST_ENV_FILE>.pre-uat-<stamp>}  (وإلا توقّف).
2) install -m 600 <env-prev> ${TEST_ENV_FILE}   # استعادة ذرّية
3) systemctl restart ${TEST_SERVICE_NAME}
4) GET ${API}/health → {"status":"ok"}
5) تحقّق frontend bundle (index-*.js) مطابق لما قبل (اختياري).
ملاحظة: القاعدة القديمة (${CURRENT_TEST_DB}) لم تُمَسّ ⇒ الرجوع فوري وآمن.
EOF
  exit 0
fi

# --- وضع apply ---
guard_expected_host
need_cmd systemctl; need_cmd install; need_cmd curl
[[ -n "$ENV_PREV" && -f "$ENV_PREV" ]] || die "ملف env السابق غير موجود — مرّر ENV_PREV=<path>."

require_write_enabled "Rollback: استعادة ${ENV_PREV} + restart ${TEST_SERVICE_NAME}"

SERVICE_TIMEOUT_SECS="${SERVICE_TIMEOUT_SECS:-45}"
HEALTH_TIMEOUT_SECS="${HEALTH_TIMEOUT_SECS:-60}"
POLL_INTERVAL_SECS="${POLL_INTERVAL_SECS:-3}"

log "1) استعادة env السابق ..."
install -m 600 "$ENV_PREV" "${TEST_ENV_FILE}"

log "2) restart ${TEST_SERVICE_NAME} ..."
systemctl restart "${TEST_SERVICE_NAME}"

log "2.1) انتظار أن تصبح الخدمة active (مهلة ${SERVICE_TIMEOUT_SECS}s) ..."
svc_ok=0; elapsed=0
while [[ $elapsed -lt $SERVICE_TIMEOUT_SECS ]]; do
  if systemctl is-active --quiet "${TEST_SERVICE_NAME}"; then svc_ok=1; break; fi
  sleep "$POLL_INTERVAL_SECS"; elapsed=$((elapsed + POLL_INTERVAL_SECS))
done
[[ $svc_ok -eq 1 ]] || die "الخدمة لم تصبح active بعد Rollback خلال ${SERVICE_TIMEOUT_SECS}s — تدخّل يدوي عاجل."
log "✓ الخدمة active."

log "3) انتظار Health (مهلة ${HEALTH_TIMEOUT_SECS}s) ..."
health_ok=0; elapsed=0
while [[ $elapsed -lt $HEALTH_TIMEOUT_SECS ]]; do
  if curl -fsS --max-time 5 "${API}/health" 2>/dev/null | grep -q '"status":"ok"'; then health_ok=1; break; fi
  sleep "$POLL_INTERVAL_SECS"; elapsed=$((elapsed + POLL_INTERVAL_SECS))
done
[[ $health_ok -eq 1 ]] && log "✓ الرجوع تمّ — الخدمة سليمة على البيئة السابقة." \
  || die "Health أخفق بعد Rollback خلال ${HEALTH_TIMEOUT_SECS}s — تدخّل يدوي عاجل مطلوب."
