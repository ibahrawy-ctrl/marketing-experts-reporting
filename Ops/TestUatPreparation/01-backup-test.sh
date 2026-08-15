#!/usr/bin/env bash
# =====================================================================
# 01-backup-test.sh — نسخ احتياطي شامل لبيئة TEST قبل أي خطوة
# القاعدة الحالية + Backend runtime + Frontend dist + env-file + Nginx
# + DataProtection keys + uploads + migration history + service status
# + health + بصمات SHA256 + Manifest.
# READ-ONLY على النظام الحيّ (يقرأ وينسخ فقط). لا يعدّل أي قاعدة/خدمة.
# افتراضيًّا PLAN. الكتابة (إنشاء ملفات النسخ) تتطلب --apply.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="01-backup-test"
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/00-common.sh"

parse_common_args "$@"
load_config
guard_all_names
require_real_config

# DataProtection key ring اختياري: الـruntime يعتمد JWT Bearer فقط ولا يستخدم DataProtection persistence.
# قيمة فارغة آمنة تمنع فشل unbound variable تحت set -u، ولا تُعدّ Blocker عند غيابها.
DATAPROTECTION_KEYPATH="${DATAPROTECTION_KEYPATH:-}"

STAMP="$(date -u +%Y%m%d-%H%M%S)"
DEST="${BACKUP_ROOT%/}/${RELEASE_ID}-${STAMP}"
MANIFEST="${DEST}/MANIFEST.txt"

plan "وجهة النسخ: ${DEST}"
plan "المكوّنات: DB(${CURRENT_TEST_DB}) + publish + dist + env-file + nginx + dpkeys + uploads + history + status + health + SHA256"

# --- في وضع plan: اعرض الخطة فقط ثم اخرج ---
if ! require_write_enabled "إنشاء مجلد النسخ الاحتياطي وكتابة الملفات فيه"; then
  cat <<EOF
--- خطة الأوامر (لن تُنفَّذ الآن) ---
mkdir -p "${DEST}"
# 1) قاعدة البيانات (custom format، مضغوط) — الاعتماد من سلسلة اتصال التطبيق في env-file (لا postgres superuser، لا sudo، لا peer):
#    resolve_app_db_conn  ⇐ يقرأ ${APP_CONN_ENV_KEY:-ConnectionStrings__Default} من ${TEST_ENV_FILE}
PGPASSWORD="***(من env-file)" pg_dump -h "<APPDB_HOST>" -p "<APPDB_PORT>" -U "<APPDB_USER>" -Fc "<APPDB_NAME=${CURRENT_TEST_DB}>" > "${DEST}/db-${CURRENT_TEST_DB}.dump"
# 2) Backend runtime:
tar czf "${DEST}/backend-publish.tgz" -C "$(dirname "${TEST_PUBLISH_DIR}")" "$(basename "${TEST_PUBLISH_DIR}")"
# 3) Frontend dist:
tar czf "${DEST}/frontend-dist.tgz" -C "$(dirname "${TEST_FRONTEND_DIST}")" "$(basename "${TEST_FRONTEND_DIST}")"
# 4) env-file (600):
cp -a "${TEST_ENV_FILE}" "${DEST}/env-file.bak" && chmod 600 "${DEST}/env-file.bak"
# 5) Nginx conf:
cp -a "${TEST_NGINX_CONF}" "${DEST}/nginx.conf.bak"
# 6) DataProtection keys: $( [[ -n "${DATAPROTECTION_KEYPATH}" && -d "${DATAPROTECTION_KEYPATH}" ]] && echo "tar czf ${DEST}/dpkeys.tgz -C $(dirname "${DATAPROTECTION_KEYPATH}") $(basename "${DATAPROTECTION_KEYPATH}")" || echo "not configured / not applicable (JWT Bearer only) — يُتخطّى" )
# 7) uploads/files (إن وُجد):
[ -d "${TEST_UPLOADS_DIR}" ] && tar czf "${DEST}/uploads.tgz" -C "$(dirname "${TEST_UPLOADS_DIR}")" "$(basename "${TEST_UPLOADS_DIR}")"
# 8) migration history (بنفس اعتماد التطبيق، لا postgres superuser):
PGPASSWORD="***(من env-file)" psql -h "<APPDB_HOST>" -p "<APPDB_PORT>" -U "<APPDB_USER>" -d "<APPDB_NAME>" -Atc 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' > "${DEST}/migration-history.txt"
# 9) service status:
systemctl status "${TEST_SERVICE_NAME}" --no-pager > "${DEST}/service-status.txt" 2>&1 || true
# 10) health:
curl -fsS "${TEST_API_LOCAL_URL}/health" > "${DEST}/health.json" 2>&1 || true
# 11) بصمات SHA256 لملفات backend + frontend bundle:
find "${TEST_PUBLISH_DIR}" -name '*.dll' -maxdepth 2 -exec sha256sum {} \; > "${DEST}/backend-assemblies.sha256"
find "${TEST_FRONTEND_DIST}/assets" -name 'index-*.js' -exec sha256sum {} \; > "${DEST}/frontend-bundle.sha256"
EOF
  exit 0
fi

# --- وضع apply (المرحلة الثانية فقط) ---
guard_expected_host
need_cmd pg_dump; need_cmd psql; need_cmd tar; need_cmd curl
# اعتماد قاعدة البيانات = سلسلة اتصال التطبيق نفسها (لا postgres superuser)
resolve_app_db_conn
mkdir -p "${DEST}"; chmod 700 "${DEST}"

log "1/11 نسخ قاعدة ${APPDB_NAME} (اعتماد التطبيق) ..."
PGPASSWORD="${APPDB_PASSWORD}" pg_dump -h "${APPDB_HOST}" -p "${APPDB_PORT}" -U "${APPDB_USER}" -Fc "${APPDB_NAME}" > "${DEST}/db-${CURRENT_TEST_DB}.dump"

log "2/11 backend publish ..."
tar czf "${DEST}/backend-publish.tgz" -C "$(dirname "${TEST_PUBLISH_DIR}")" "$(basename "${TEST_PUBLISH_DIR}")"

log "3/11 frontend dist ..."
tar czf "${DEST}/frontend-dist.tgz" -C "$(dirname "${TEST_FRONTEND_DIST}")" "$(basename "${TEST_FRONTEND_DIST}")"

log "4/11 env-file ..."
cp -a "${TEST_ENV_FILE}" "${DEST}/env-file.bak"; chmod 600 "${DEST}/env-file.bak"

log "5/11 nginx conf ..."
cp -a "${TEST_NGINX_CONF}" "${DEST}/nginx.conf.bak" || log_warn "تعذّر نسخ nginx conf."

log "6/11 DataProtection keys ..."
DP_STATUS="not configured / not applicable (JWT Bearer only)"
if [[ -n "${DATAPROTECTION_KEYPATH}" && -d "${DATAPROTECTION_KEYPATH}" ]]; then
  tar czf "${DEST}/dpkeys.tgz" -C "$(dirname "${DATAPROTECTION_KEYPATH}")" "$(basename "${DATAPROTECTION_KEYPATH}")"
  DP_STATUS="captured: ${DATAPROTECTION_KEYPATH}"
else
  log "DataProtection key ring: ${DP_STATUS} — يُتخطّى."
fi

log "7/11 uploads ..."
[[ -d "${TEST_UPLOADS_DIR}" ]] && tar czf "${DEST}/uploads.tgz" -C "$(dirname "${TEST_UPLOADS_DIR}")" "$(basename "${TEST_UPLOADS_DIR}")" || log "لا uploads — يُتخطّى."

log "8/11 migration history ..."
PGPASSWORD="${APPDB_PASSWORD}" psql -h "${APPDB_HOST}" -p "${APPDB_PORT}" -U "${APPDB_USER}" -d "${APPDB_NAME}" -Atc \
  'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' > "${DEST}/migration-history.txt"

log "9/11 service status ..."
systemctl status "${TEST_SERVICE_NAME}" --no-pager > "${DEST}/service-status.txt" 2>&1 || true

log "10/11 health ..."
curl -fsS "${TEST_API_LOCAL_URL}/health" > "${DEST}/health.json" 2>&1 || log_warn "health غير متاح."

log "11/11 بصمات SHA256 ..."
find "${TEST_PUBLISH_DIR}" -maxdepth 2 -name '*.dll' -exec sha256sum {} \; > "${DEST}/backend-assemblies.sha256" 2>/dev/null || true
find "${TEST_FRONTEND_DIST}/assets" -name 'index-*.js' -exec sha256sum {} \; > "${DEST}/frontend-bundle.sha256" 2>/dev/null || true

# --- Manifest ---
{
  echo "RELEASE_ID: ${RELEASE_ID}"
  echo "TIMESTAMP_UTC: ${STAMP}"
  echo "EXPECTED_GIT_REF: ${EXPECTED_GIT_REF}"
  echo "CURRENT_TEST_DB: ${CURRENT_TEST_DB}"
  echo "HOST: $(hostname)"
  echo "DB_AUTH: application connection string (non-superuser) host=${APPDB_HOST} port=${APPDB_PORT} db=${APPDB_NAME} user=${APPDB_USER}"
  echo "DataProtection key ring: ${DP_STATUS}"
  echo "--- files ---"
  ( cd "${DEST}" && for f in *; do [[ "$f" == MANIFEST.txt ]] && continue; printf '%s  %s\n' "$(sha256_of "$f")" "$f"; done )
} > "${MANIFEST}"
chmod 600 "${MANIFEST}"

log "اكتمل النسخ الاحتياطي في ${DEST}. Manifest: ${MANIFEST}"
