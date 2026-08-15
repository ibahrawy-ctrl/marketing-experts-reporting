#!/usr/bin/env bash
# =====================================================================
# 06-cutover-test-to-uat.sh — تحويل بيئة TEST إلى القاعدة الجديدة + Staging
# التسلسل: Preflight → تأكيد Backup → جاهزية القاعدة → 30 هجرة →
#          Seeders/Fixtures → نسخ env-file الحالي → تركيب env-file الجديد ذرّيًّا →
#          Restart لخدمة TEST فقط → Health → Auth → API → SignalR → Email safety →
#          Project-First smoke → Legacy smoke → Rollup smoke.
# يتوقّف ويستدعي/يوصي بـRollback عند فشل Health أو Auth.
# افتراضيًّا PLAN. التنفيذ يتطلب --apply + OPS_ALLOW_WRITE=1 + تأكيد.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="06-cutover-test-to-uat"
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/00-common.sh"

parse_common_args "$@"
load_config
guard_all_names
require_real_config
guard_write_target_is_new_uat "${NEW_UAT_DB}"

API="${TEST_API_LOCAL_URL}"
NEW_ENV_SRC="${NEW_ENV_SRC:-}"   # env-file الجديد المُجهَّز (مبني من staging.env.template، خارج Git)
STAMP="$(date -u +%Y%m%d-%H%M%S)"
ENV_PREV="${TEST_ENV_FILE}.pre-uat-${STAMP}"

plan "خطة Cutover لخدمة ${TEST_SERVICE_NAME} على ${TEST_DOMAIN}"
plan "env السابق سيُحفظ في: ${ENV_PREV}"
plan "التركيب الذرّي: install -m600 <new> ${TEST_ENV_FILE} ثم systemctl restart ${TEST_SERVICE_NAME}"

if [[ "$OPS_MODE" != "apply" ]]; then
  cat <<EOF
--- تسلسل Cutover (لن يُنفَّذ الآن) ---
[Preflight]
  - guard_expected_host + guard_all_names + guard_write_target_is_new_uat
  - تأكيد وجود Backup manifest حديث (من 01) — وإلا توقّف.
  - تأكيد وجود القاعدة ${NEW_UAT_DB} + الدور ${NEW_UAT_ROLE}.
  - تأكيد ملف env جديد \$NEW_ENV_SRC موجود + بوّابات الإقلاع كلها (وإلا توقّف):
    · ASPNETCORE_ENVIRONMENT=Staging
    · Cors__AllowedOrigins__0 غير فارغ، بلا wildcard/localhost
    · Jwt__Key ≥32 محرفًا وبلا 'dev-only'
    · Seed__AdminEmail موجود + Seed__AdminPassword قويّ (≥12، كبير/صغير/رقم/رمز،
      ليس Admin#12345، ليس placeholder — لا يُطبع)
    · Connection String يشير للقاعدة ${NEW_UAT_DB}، وبلا أي اسم محظور (prod/rc)
[Cutover]
  1) cp -a ${TEST_ENV_FILE} ${ENV_PREV}         # نسخة رجوع فورية
  2) install -m 600 \$NEW_ENV_SRC ${TEST_ENV_FILE}   # تركيب ذرّي
  3) systemctl restart ${TEST_SERVICE_NAME}      # إعادة تشغيل خدمة TEST فقط
     3.1) انتظار الخدمة active (مهلة \${SERVICE_TIMEOUT_SECS:-45}s)
     3.2) انتظار Health=ok (مهلة \${HEALTH_TIMEOUT_SECS:-60}s، إعادة محاولة كل \${POLL_INTERVAL_SECS:-3}s)
     (عند الإقلاع: MigrateAsync يطبّق الـ30 هجرة + Catalog Seeders؛ OrgSeeder لا يعمل)
[Validate]  (عبر 07-health-validation.sh)
  Health → Auth(login admin) → API(GET /api/report-templates) → SignalR(/hubs/notifications) →
  Email safety(email_outbox=0 / Enabled=false) → Project-First(/api/reporting/project-execution/projects) →
  Legacy(/api/report-templates?status=Archived) → Rollup(/api/reporting/project-execution/pods)
[سلوك الفشل]
  · فشل إقلاع الخدمة / Health ضمن المهلة / فشل Auth حرج  ⇒ Auto-Rollback آمن (08).
  · فشل وظيفي غير حرج (07 rc=2)                          ⇒ توقّف + عرض النتائج بلا رجوع (قرار بشري).
EOF
  exit 0
fi

# --- وضع apply (المرحلة الثانية) ---
guard_expected_host
need_cmd systemctl; need_cmd install; need_cmd psql; need_cmd curl

# Preflight
[[ -n "$NEW_ENV_SRC" && -f "$NEW_ENV_SRC" ]] || die "NEW_ENV_SRC غير موجود — جهّز env-file الجديد أولًا."
grep -q '^ASPNETCORE_ENVIRONMENT=Staging' "$NEW_ENV_SRC" || die "env الجديد لا يحوي ASPNETCORE_ENVIRONMENT=Staging."

# --- بوّابة CORS (مطابقة لحارس Program.cs في Staging: غير فارغ، بلا wildcard/localhost) ---
cors_val="$(grep -m1 '^Cors__AllowedOrigins__0=' "$NEW_ENV_SRC" | sed 's/^Cors__AllowedOrigins__0=//')"
[[ -n "$cors_val" ]] || die "بوّابة CORS: Cors__AllowedOrigins__0 مفقود أو فارغ."
[[ "$cors_val" != *'*'* ]] || die "بوّابة CORS: wildcard (*) مرفوض في Staging."
grep -Eiq 'localhost|127\.0\.0\.1' <<<"$cors_val" && die "بوّابة CORS: localhost/127.0.0.1 مرفوض في Staging." || true

# --- بوّابة JWT (مطابقة لحارس Program.cs: ≥32 محرفًا وبلا 'dev-only') ---
jwt_val="$(grep -m1 '^Jwt__Key=' "$NEW_ENV_SRC" | sed 's/^Jwt__Key=//' | tr -d '\n')"
[[ "${#jwt_val}" -ge 32 ]] || die "بوّابة JWT: Jwt__Key أقصر من 32 محرفًا."
grep -qi 'dev-only' <<<"$jwt_val" && die "بوّابة JWT: Jwt__Key يحوي 'dev-only' (يرفضه Program.cs في Staging)." || true

# --- بوّابة Seed Admin: البريد + كلمة المرور (لا يُطبع أي منهما) ---
grep -q '^Seed__AdminEmail=' "$NEW_ENV_SRC" || die "Seed__AdminEmail مفقود (Staging يتطلّبه)."
seed_pw="$(grep -m1 '^Seed__AdminPassword=' "$NEW_ENV_SRC" | sed 's/^Seed__AdminPassword=//')"
[[ -n "$seed_pw" ]] || die "بوّابة Seed Admin: Seed__AdminPassword مفقود أو فارغ (Staging لن يبذر admin بلا كلمة صريحة)."
case "$seed_pw" in
  __SET_AT_RUNTIME__|__*__|REPLACE_ME_*) die "بوّابة Seed Admin: Seed__AdminPassword ما زال placeholder — ضع كلمة حقيقية." ;;
  'Admin#12345') die "بوّابة Seed Admin: Seed__AdminPassword = الافتراضية القديمة (Admin#12345) — مرفوض." ;;
esac
[[ "${#seed_pw}" -ge 12 ]] || die "بوّابة Seed Admin: كلمة المرور أقصر من 12 محرفًا."
grep -q '[A-Z]'          <<<"$seed_pw" || die "بوّابة Seed Admin: كلمة المرور بلا حرف كبير."
grep -q '[a-z]'          <<<"$seed_pw" || die "بوّابة Seed Admin: كلمة المرور بلا حرف صغير."
grep -q '[0-9]'          <<<"$seed_pw" || die "بوّابة Seed Admin: كلمة المرور بلا رقم."
grep -q '[^A-Za-z0-9]'   <<<"$seed_pw" || die "بوّابة Seed Admin: كلمة المرور بلا رمز خاص."
log "✓ بوّابة Seed Admin: كلمة مرور قوية (لم تُطبع، طول=$(mask "$seed_pw"))."

grep -q "Database=${NEW_UAT_DB}" "$NEW_ENV_SRC" || die "Connection String لا يشير للقاعدة الجديدة ${NEW_UAT_DB}."
grep -Eiq "${FORBIDDEN_NAME_REGEX}" "$NEW_ENV_SRC" && die "env الجديد يحوي اسمًا محظورًا (Production/RC)." || true

exists="$(psql -Atc "SELECT 1 FROM pg_database WHERE datname='${NEW_UAT_DB}';" -h "${PGHOST}" -p "${PGPORT}" -U "${PG_SUPERUSER}" -d postgres || true)"
[[ -n "$exists" ]] || die "القاعدة ${NEW_UAT_DB} غير موجودة — شغّل 02 أولًا."

[[ -d "${BACKUP_ROOT}" ]] && ls -1 "${BACKUP_ROOT}" | grep -q "${RELEASE_ID}" || die "لا يوجد Backup للحزمة ${RELEASE_ID} — شغّل 01 أولًا."

require_write_enabled "Cutover: تركيب env جديد + restart ${TEST_SERVICE_NAME}"

log "1) حفظ env السابق: ${ENV_PREV}"
cp -a "${TEST_ENV_FILE}" "${ENV_PREV}"; chmod 600 "${ENV_PREV}"

log "2) تركيب env الجديد ذرّيًّا ..."
install -m 600 "$NEW_ENV_SRC" "${TEST_ENV_FILE}"

# مُهَل قابلة للضبط (ثوانٍ)
SERVICE_TIMEOUT_SECS="${SERVICE_TIMEOUT_SECS:-45}"
HEALTH_TIMEOUT_SECS="${HEALTH_TIMEOUT_SECS:-60}"
POLL_INTERVAL_SECS="${POLL_INTERVAL_SECS:-3}"

# Auto-Rollback آمن: يُستدعى فقط عند فشل الإقلاع/الصحّة/المصادقة الحرجة
# (حالات لا يخفي فيها الرجوع دليلًا — env الجديد لم يُقلِع أصلًا).
auto_rollback() {
  local reason="$1"
  log_err "فشل حرج: ${reason} ⇒ Auto-Rollback آمن (env السابق: ${ENV_PREV})."
  if OPS_MODE=apply OPS_ALLOW_WRITE=1 OPS_ASSUME_YES=1 OPS_CONFIG="${OPS_CONFIG_RESOLVED}" \
       ENV_PREV="${ENV_PREV}" bash "${SCRIPT_DIR}/08-rollback-test.sh" --apply --yes; then
    die "Cutover فشل (${reason}) — تمّ الرجوع الآلي للبيئة السابقة."
  else
    die "Cutover فشل (${reason}) وفشل Auto-Rollback — تدخّل يدوي عاجل (استعادة ${ENV_PREV})."
  fi
}

log "3) restart ${TEST_SERVICE_NAME} ..."
systemctl restart "${TEST_SERVICE_NAME}"

log "3.1) انتظار أن تصبح الخدمة active (مهلة ${SERVICE_TIMEOUT_SECS}s) ..."
svc_ok=0; elapsed=0
while [[ $elapsed -lt $SERVICE_TIMEOUT_SECS ]]; do
  if systemctl is-active --quiet "${TEST_SERVICE_NAME}"; then svc_ok=1; break; fi
  sleep "$POLL_INTERVAL_SECS"; elapsed=$((elapsed + POLL_INTERVAL_SECS))
done
[[ $svc_ok -eq 1 ]] || auto_rollback "الخدمة لم تصبح active خلال ${SERVICE_TIMEOUT_SECS}s"
log "✓ الخدمة active."

log "3.2) انتظار Health (مهلة ${HEALTH_TIMEOUT_SECS}s) ..."
health_ok=0; elapsed=0
while [[ $elapsed -lt $HEALTH_TIMEOUT_SECS ]]; do
  if curl -fsS --max-time 5 "${API}/health" 2>/dev/null | grep -q '"status":"ok"'; then health_ok=1; break; fi
  sleep "$POLL_INTERVAL_SECS"; elapsed=$((elapsed + POLL_INTERVAL_SECS))
done
[[ $health_ok -eq 1 ]] || auto_rollback "Health لم يصبح ok خلال ${HEALTH_TIMEOUT_SECS}s"
log "✓ Health ok."

log "4) تشغيل التحقّق الكامل (07) ..."
# 07 يُرجِع: 0=سليم، 1=فشل حرج (Health/Auth)⇒Auto-Rollback آمن،
#            2=فشل وظيفي غير حرج⇒توقّف بلا رجوع آلي (قرار بشري).
set +e
OPS_MODE=apply OPS_CONFIG="${OPS_CONFIG_RESOLVED}" bash "${SCRIPT_DIR}/07-health-validation.sh" --apply --yes
v_rc=$?
set -e
case "$v_rc" in
  0) log "اكتمل Cutover بنجاح. env السابق محفوظ في ${ENV_PREV}. القاعدة القديمة لم تُمَسّ." ;;
  1) auto_rollback "فشل حرج في 07 (Health/Auth)" ;;
  2) log_err "07 أبلغ فشلًا وظيفيًّا غير حرج — لا Auto-Rollback (كي لا يُخفى الدليل)."
     log_err "الخدمة تعمل على env الجديد. راجع مخرجات 07 أعلاه ثم قرّر يدويًّا:"
     log_err "  - للرجوع: ENV_PREV=${ENV_PREV} bash 08-rollback-test.sh --apply"
     die "Cutover متوقّف على فشل وظيفي — يتطلّب قرارًا بشريًّا." ;;
  *) auto_rollback "07 خرج برمز غير متوقّع (${v_rc})" ;;
esac
