#!/usr/bin/env bash
# =====================================================================
# 02-create-uat-db.sh — إنشاء قاعدة UAT نظيفة + دور تطبيق مستقل
# القاعدة: reporting_test_uat   الدور: reporting_test_uat_app
# - كلمة المرور تُولَّد وقت التنفيذ ولا تدخل Git ولا تُطبع كاملة.
# - أقل صلاحيات: CONNECT + امتلاك القاعدة الجديدة فقط. لا Superuser، لا CREATEDB، لا CREATEROLE.
# - يتوقّف (لا overwrite) إن كانت القاعدة موجودة.
# - لا يمسّ Production/RC/القاعدة الحالية إطلاقًا.
# افتراضيًّا PLAN. الإنشاء يتطلب --apply + OPS_ALLOW_WRITE=1 + تأكيد.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="02-create-uat-db"
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/00-common.sh"

parse_common_args "$@"
load_config
guard_all_names
require_real_config
guard_write_target_is_new_uat "${NEW_UAT_DB}"

# اعتماد إداريّ أوّليّ فقط (إنشاء الدور/القاعدة/الملكية/الصلاحيات) عبر Peer Authentication
# لمستخدم نظام التشغيل postgres — لا كلمة مرور، لا .pgpass، لا TCP كـpostgres، لا تعديل pg_hba.
# هذا الاعتماد لا يُستخدَم لاحقًا في التطبيق/Migrations/Seeders/Fixtures/Backup/الاتصال اليومي؛
# كلّ ذلك يعتمد حصريًّا على الدور ${NEW_UAT_ROLE}.
PSQL_SUPER=(sudo -n -u "${PG_SUPERUSER}" psql -v ON_ERROR_STOP=1 -d postgres)

plan "إنشاء الدور ${NEW_UAT_ROLE} (LOGIN، بلا صلاحيات عليا)"
plan "إنشاء القاعدة ${NEW_UAT_DB} (OWNER=${NEW_UAT_ROLE}، ENCODING/locale مشتقّ من ${CURRENT_TEST_DB})"
plan "منح CONNECT للدور على القاعدة الجديدة فقط"

if ! require_write_enabled "إنشاء القاعدة ${NEW_UAT_DB} والدور ${NEW_UAT_ROLE}"; then
  cat <<EOF
--- خطة SQL (لن تُنفَّذ الآن) ---
-- الاعتماد الإداريّ: Peer Authentication عبر: sudo -n -u ${PG_SUPERUSER} psql -v ON_ERROR_STOP=1 -d postgres
-- (لا PGPASSWORD، لا .pgpass، لا TCP كـ${PG_SUPERUSER}، لا تعديل pg_hba).
-- Preflight (قبل أيّ كتابة، وضع apply فقط): إثبات sudo -n -u ${PG_SUPERUSER} يعمل بلا كلمة مرور
--   وأن current_user=${PG_SUPERUSER} و rolsuper=t، وإلّا توقّف.
-- حارس: القاعدة يجب ألّا تكون موجودة مسبقًا (وإلا توقّف).
SELECT 1 FROM pg_database WHERE datname = '${NEW_UAT_DB}';   -- يجب أن يعيد صفرًا
-- توليد كلمة مرور قوية وقت التنفيذ (خارج Git، لا تُطبع، لا تدخل history/logs):
--   NEW_UAT_ROLE_PASSWORD="\$(openssl rand -base64 24)"
-- كلمة المرور تُمرَّر إلى psql عبر stdin (heredoc) بـ \\set + :'role_password'
-- ولا تظهر أبدًا في command-line arguments (process list):
CREATE ROLE ${NEW_UAT_ROLE} LOGIN PASSWORD :'role_password'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
-- الترميز/الـlocale يُشتقّان من ${CURRENT_TEST_DB} وقت التنفيذ لضمان التوافق:
CREATE DATABASE ${NEW_UAT_DB} OWNER ${NEW_UAT_ROLE}
  ENCODING '<من ${CURRENT_TEST_DB}>' LC_COLLATE '<من ${CURRENT_TEST_DB}>' LC_CTYPE '<من ${CURRENT_TEST_DB}>' TEMPLATE template0;
REVOKE ALL ON DATABASE ${NEW_UAT_DB} FROM PUBLIC;
GRANT CONNECT ON DATABASE ${NEW_UAT_DB} TO ${NEW_UAT_ROLE};
-- ملاحظة: EF Core (MigrateAsync) ينشئ الجداول عند إقلاع التطبيق بدور ${NEW_UAT_ROLE} المالك.
-- تسليم Connection String لـenv-file لاحقًا (يدويًّا، خارج Git):
--   ConnectionStrings__Default="Host=${PGHOST};Port=${PGPORT};Database=${NEW_UAT_DB};Username=${NEW_UAT_ROLE};Password=***"
EOF
  exit 0
fi

# --- وضع apply (المرحلة الثانية) ---
guard_expected_host
need_cmd psql; need_cmd openssl; need_cmd sudo

# 0) Preflight الاعتماد الإداريّ: إثبات Peer Authentication قبل أيّ كتابة.
#    - sudo -n (بلا كلمة مرور) لمستخدم ${PG_SUPERUSER}
#    - current_user == ${PG_SUPERUSER}  و  rolsuper == t
super_probe="$(sudo -n -u "${PG_SUPERUSER}" psql -v ON_ERROR_STOP=1 -d postgres -Atc \
  "SELECT current_user, rolsuper FROM pg_roles WHERE rolname = current_user;" 2>/dev/null || true)"
[[ "$super_probe" == "${PG_SUPERUSER}|t" ]] || die \
  "فشل Preflight الاعتماد الإداريّ (Peer): توقّع '${PG_SUPERUSER}|t' وحصلنا على '${super_probe:-<فارغ>}'. لا كتابة."
log "Preflight الاعتماد الإداريّ (Peer) ناجح: current_user=${PG_SUPERUSER}, rolsuper=t."

# 1) حارس: القاعدة لا يجب أن تكون موجودة
exists="$("${PSQL_SUPER[@]}" -Atc "SELECT 1 FROM pg_database WHERE datname = '${NEW_UAT_DB}';" || true)"
[[ -z "$exists" ]] || die "القاعدة ${NEW_UAT_DB} موجودة مسبقًا — أوقِف بدل overwrite. (احذفها يدويًّا بموافقة إن أردت إعادة الإنشاء.)"

# 2) توليد كلمة مرور (لا تُطبع، لا تُخزّن في Git)
NEW_UAT_ROLE_PASSWORD="${NEW_UAT_ROLE_PASSWORD:-$(openssl rand -base64 24)}"
log "تولّدت كلمة مرور الدور: $(mask "$NEW_UAT_ROLE_PASSWORD")"

# 3) إنشاء الدور (إن لم يوجد)
#    كلمة المرور تُمرَّر عبر stdin بـ \set + :'role_password' (اقتباس آمن كـstring literal)
#    فلا تظهر إطلاقًا في command-line arguments (process list) ولا في ملفّ على القرص.
role_exists="$("${PSQL_SUPER[@]}" -Atc "SELECT 1 FROM pg_roles WHERE rolname = '${NEW_UAT_ROLE}';" || true)"
if [[ -z "$role_exists" ]]; then
  "${PSQL_SUPER[@]}" <<SQL
\set role_password '${NEW_UAT_ROLE_PASSWORD}'
CREATE ROLE ${NEW_UAT_ROLE} LOGIN PASSWORD :'role_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
SQL
  log "أُنشئ الدور ${NEW_UAT_ROLE}."
else
  log_warn "الدور ${NEW_UAT_ROLE} موجود — سيُضبط password فقط (idempotent)."
  "${PSQL_SUPER[@]}" <<SQL
\set role_password '${NEW_UAT_ROLE_PASSWORD}'
ALTER ROLE ${NEW_UAT_ROLE} LOGIN PASSWORD :'role_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
SQL
fi

# 4) إنشاء القاعدة (CREATE DATABASE لا يعمل داخل معاملة)
#    الترميز/الـlocale يُشتقّان من القاعدة الحالية ${CURRENT_TEST_DB} لضمان التوافق (بلا hardcoding).
src_locale="$("${PSQL_SUPER[@]}" -Atc \
  "SELECT pg_encoding_to_char(encoding) || '|' || datcollate || '|' || datctype FROM pg_database WHERE datname = '${CURRENT_TEST_DB}';" || true)"
[[ -n "$src_locale" ]] || die "تعذّر اشتقاق ترميز/locale من ${CURRENT_TEST_DB} — لا كتابة."
SRC_ENCODING="${src_locale%%|*}"; rest="${src_locale#*|}"
SRC_COLLATE="${rest%%|*}"; SRC_CTYPE="${rest#*|}"
log "ترميز/locale المصدر (${CURRENT_TEST_DB}): ENCODING=${SRC_ENCODING} LC_COLLATE=${SRC_COLLATE} LC_CTYPE=${SRC_CTYPE}."
"${PSQL_SUPER[@]}" <<SQL
\set enc '${SRC_ENCODING}'
\set coll '${SRC_COLLATE}'
\set ctyp '${SRC_CTYPE}'
CREATE DATABASE ${NEW_UAT_DB} OWNER ${NEW_UAT_ROLE} ENCODING :'enc' LC_COLLATE :'coll' LC_CTYPE :'ctyp' TEMPLATE template0;
SQL
"${PSQL_SUPER[@]}" -c "REVOKE ALL ON DATABASE ${NEW_UAT_DB} FROM PUBLIC;"
"${PSQL_SUPER[@]}" -c "GRANT CONNECT ON DATABASE ${NEW_UAT_DB} TO ${NEW_UAT_ROLE};"
log "أُنشئت القاعدة ${NEW_UAT_DB} (OWNER=${NEW_UAT_ROLE}، ENCODING=${SRC_ENCODING}، LC_COLLATE=${SRC_COLLATE}، LC_CTYPE=${SRC_CTYPE})."

# 5) طباعة Connection String بدون كلمة المرور (تُدرَج يدويًّا في env-file)
cat <<EOF
--- سلّم Connection String لـenv-file يدويًّا (لا يُطبع الـpassword هنا) ---
ConnectionStrings__Default="Host=${PGHOST};Port=${PGPORT};Database=${NEW_UAT_DB};Username=${NEW_UAT_ROLE};Password=<الكلمة المولّدة أعلاه>"
كلمة المرور المولّدة محفوظة في ذاكرة الجلسة فقط: $(mask "$NEW_UAT_ROLE_PASSWORD")
انسخها الآن إلى env-file (chmod 600) — لن تُطبع مرة أخرى.
EOF
