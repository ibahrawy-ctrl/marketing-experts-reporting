#!/usr/bin/env bash
# =====================================================================
# 00-common.sh — مكتبة مشتركة (guards / logging / confirm / helpers)
# تُصدَّر (source) من كل السكربتات. لا تُشغَّل مباشرةً.
# لا تكتب على أي بيئة. لا تطبع أسرارًا.
# =====================================================================
set -Eeuo pipefail

# --- منع التشغيل المباشر ---
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  echo "00-common.sh مكتبة مشتركة تُصدَّر عبر source؛ لا تُشغَّل مباشرةً." >&2
  exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[1]}")" && pwd)"

# --- الوضع الافتراضي: PLAN (لا كتابة) ---
OPS_MODE="plan"                 # plan | apply
OPS_ALLOW_WRITE="${OPS_ALLOW_WRITE:-0}"
OPS_ASSUME_YES="${OPS_ASSUME_YES:-0}"

# --- تحميل الإعدادات ---
# ترتيب البحث: $OPS_CONFIG ثم config.env بجوار السكربت (يجب أن يكون خارج Git إن حوى قيمًا حقيقية)
load_config() {
  local cfg="${OPS_CONFIG:-}"
  if [[ -z "$cfg" ]]; then
    if [[ -f "${SCRIPT_DIR}/config.env" ]]; then
      cfg="${SCRIPT_DIR}/config.env"
    else
      cfg="${SCRIPT_DIR}/config.env.template"
      log_warn "لا يوجد config.env — تحميل config.env.template (قيم placeholder، صالح للفحص الساكن فقط)."
    fi
  fi
  [[ -f "$cfg" ]] || die "ملف الإعداد غير موجود: $cfg"
  # shellcheck disable=SC1090
  source "$cfg"
  OPS_CONFIG_RESOLVED="$cfg"
}

# --- تسجيل ---
_ts() { date -u +"%Y-%m-%dT%H:%M:%SZ"; }
log()      { printf '%s [INFO ] %s\n' "$(_ts)" "$*"; }
log_warn() { printf '%s [WARN ] %s\n' "$(_ts)" "$*" >&2; }
log_err()  { printf '%s [ERROR] %s\n' "$(_ts)" "$*" >&2; }
die()      { log_err "$*"; exit 1; }
plan()     { printf '%s [PLAN ] %s\n' "$(_ts)" "$*"; }

# --- توقيت وتلخيص النتيجة ---
OPS_T0="$(date +%s)"
on_exit() {
  local rc=$?
  local dt=$(( $(date +%s) - OPS_T0 ))
  if [[ $rc -eq 0 ]]; then
    log "انتهى ${SCRIPT_NAME:-script} بنجاح (mode=${OPS_MODE}) خلال ${dt}s."
  else
    log_err "فشل ${SCRIPT_NAME:-script} (rc=${rc}, mode=${OPS_MODE}) بعد ${dt}s."
  fi
}
trap on_exit EXIT
trap 'log_err "خطأ عند السطر ${LINENO}."' ERR

# --- تحليل الوسائط الموحّد ---
parse_common_args() {
  for arg in "$@"; do
    case "$arg" in
      --apply) OPS_MODE="apply" ;;
      --plan|--dry-run) OPS_MODE="plan" ;;
      --yes) OPS_ASSUME_YES="1" ;;
      -h|--help) print_help 2>/dev/null || true; exit 0 ;;
    esac
  done
}

# =====================================================================
# الحُرّاس (Guards) — تمنع أي تنفيذ على Production/RC أو خادم خاطئ
# =====================================================================

# يرفض أي قيمة تطابق النمط المحظور (prod/RC/دومين الإنتاج ...)
guard_name_not_forbidden() {
  local label="$1" value="$2"
  [[ -n "${FORBIDDEN_NAME_REGEX:-}" ]] || die "FORBIDDEN_NAME_REGEX غير معرّف في الإعداد."
  if grep -Eiq "${FORBIDDEN_NAME_REGEX}" <<<"$value"; then
    die "حارس أمان: القيمة (${label}='${value}') تطابق نمطًا محظورًا (Production/RC). أُوقِف التنفيذ."
  fi
}

# يتأكّد أن كل الأسماء الحسّاسة آمنة قبل أي عملية
guard_all_names() {
  guard_name_not_forbidden "TEST_DOMAIN"     "${TEST_DOMAIN:-}"
  guard_name_not_forbidden "TEST_SERVICE"    "${TEST_SERVICE_NAME:-}"
  guard_name_not_forbidden "NEW_UAT_DB"      "${NEW_UAT_DB:-}"
  guard_name_not_forbidden "NEW_UAT_ROLE"    "${NEW_UAT_ROLE:-}"
  # القاعدة الحالية أرشيف: يجب ألا تكون هدف كتابة، لكنها قد تحمل اسم rc — لا نمرّرها لحارس الاسم
  [[ "${NEW_UAT_DB:-}" == "reporting_test_uat" ]] || log_warn "NEW_UAT_DB ليس reporting_test_uat (=${NEW_UAT_DB:-}) — تأكّد أنه المقصود."
}

# يمنع أي هدف كتابة غير القاعدة الجديدة
guard_write_target_is_new_uat() {
  local target="$1"
  [[ "$target" == "${NEW_UAT_DB}" ]] || die "حارس أمان: هدف الكتابة (${target}) ليس القاعدة الجديدة (${NEW_UAT_DB}). أُوقِف."
  guard_name_not_forbidden "write-target" "$target"
  [[ "$target" != "${CURRENT_TEST_DB:-__none__}" ]] || die "حارس أمان: مُنِعت الكتابة على القاعدة الحالية/الأرشيف (${target})."
}

# يمنع تنفيذ خطوة كتابية إلا في وضع apply + تصريح + تأكيد تفاعلي
require_write_enabled() {
  local what="$1"
  if [[ "$OPS_MODE" != "apply" ]]; then
    plan "سيُنفَّذ لاحقًا (apply): ${what}"
    return 1   # في وضع plan: لا تنفيذ
  fi
  [[ "$OPS_ALLOW_WRITE" == "1" ]] || die "الكتابة تتطلب OPS_ALLOW_WRITE=1 (غير مضبوط) — رُفِض: ${what}"
  confirm "تأكيد الكتابة: ${what}"
}

# تأكيد تفاعلي بكتابة رمز صريح (يمكن تخطّيه بـ --yes فقط مع OPS_ALLOW_WRITE)
confirm() {
  local prompt="$1"
  if [[ "$OPS_ASSUME_YES" == "1" ]]; then
    log "تأكيد تلقائي (--yes): ${prompt}"
    return 0
  fi
  local token="EXECUTE"
  read -r -p "${prompt}
اكتب '${token}' للمتابعة: " ans
  [[ "$ans" == "$token" ]] || die "أُلغِيت العملية (لم يُكتب ${token})."
}

# =====================================================================
# مساعدات
# =====================================================================
need_cmd() { command -v "$1" >/dev/null 2>&1 || die "الأمر المطلوب غير موجود: $1"; }

mask() { # يُخفي القيمة السرّية عند الطباعة
  local v="${1:-}"; [[ -z "$v" ]] && { echo "(فارغ)"; return; }
  echo "***(${#v} chars)"
}

# =====================================================================
# قراءة سلسلة اتصال التطبيق (بدل افتراض superuser postgres)
# ---------------------------------------------------------------------
# المصدر: env-file التطبيق نفسه (APP_ENV_FILE، وإلا TEST_ENV_FILE) عبر
# المفتاح APP_CONN_ENV_KEY (افتراضيًّا ConnectionStrings__Default).
# يفكّ سلسلة .NET (Host=..;Port=..;Database=..;Username=..;Password=..)
# ويملأ المتغيّرات: APPDB_HOST / APPDB_PORT / APPDB_NAME / APPDB_USER / APPDB_PASSWORD.
# لا يفترض postgres، لا sudo، لا peer، لا .pgpass، لا تعديل pg_hba.
# يعمل بلا تغيير على TEST/RC/Production — يكفي أن يشير env-file لكل بيئة
# إلى سلسلة اتصالها. لا يطبع أي سرّ (كلمة المرور تبقى في المتغيّر فقط).
# =====================================================================
resolve_app_db_conn() {
  local env_file="${APP_ENV_FILE:-${TEST_ENV_FILE:-}}"
  local key="${APP_CONN_ENV_KEY:-ConnectionStrings__Default}"
  [[ -n "$env_file" ]] || die "لا مصدر لسلسلة الاتصال: APP_ENV_FILE/TEST_ENV_FILE غير محدّد."
  [[ -f "$env_file" ]] || die "env-file غير موجود: ${env_file}"

  # استخرج قيمة المفتاح فقط (أوّل تطابق)، مع دعم بادئة export، بلا طباعة السرّ
  local raw
  raw="$(grep -E "^[[:space:]]*(export[[:space:]]+)?${key}=" "$env_file" | head -n1 || true)"
  raw="${raw#"${raw%%[![:space:]]*}"}"           # إزالة المسافات البادئة
  raw="${raw#export }"                            # إزالة export إن وُجد
  raw="${raw#"$key"=}"                            # إزالة KEY=
  [[ -n "$raw" ]] || die "لم يُعثر على المفتاح ${key} داخل env-file."
  # إزالة اقتباس محيط إن وُجد
  raw="${raw%\"}"; raw="${raw#\"}"
  raw="${raw%\'}"; raw="${raw#\'}"

  APPDB_HOST=""; APPDB_PORT=""; APPDB_NAME=""; APPDB_USER=""; APPDB_PASSWORD=""
  local pair k v old_ifs="$IFS"
  IFS=';'
  for pair in $raw; do
    [[ -z "$pair" ]] && continue
    k="${pair%%=*}"; v="${pair#*=}"
    k="$(printf '%s' "$k" | tr '[:upper:]' '[:lower:]' | tr -d '[:space:]')"
    case "$k" in
      host|server)              APPDB_HOST="$v" ;;
      port)                     APPDB_PORT="$v" ;;
      database|db)              APPDB_NAME="$v" ;;
      username|userid|user)     APPDB_USER="$v" ;;
      password|pwd)             APPDB_PASSWORD="$v" ;;
    esac
  done
  IFS="$old_ifs"

  : "${APPDB_PORT:=5432}"
  [[ -n "$APPDB_HOST" ]]     || die "سلسلة الاتصال بلا Host."
  [[ -n "$APPDB_NAME" ]]     || die "سلسلة الاتصال بلا Database."
  [[ -n "$APPDB_USER" ]]     || die "سلسلة الاتصال بلا Username."
  [[ -n "$APPDB_PASSWORD" ]] || die "سلسلة الاتصال بلا Password."

  # حارس اتّساق: القاعدة في سلسلة الاتصال يجب أن تطابق القاعدة المقصودة للنسخ
  if [[ -n "${CURRENT_TEST_DB:-}" && "$APPDB_NAME" != "${CURRENT_TEST_DB}" ]]; then
    die "حارس أمان: قاعدة سلسلة الاتصال (${APPDB_NAME}) ≠ CURRENT_TEST_DB (${CURRENT_TEST_DB})."
  fi

  export APPDB_HOST APPDB_PORT APPDB_NAME APPDB_USER APPDB_PASSWORD
  log "سلسلة اتصال التطبيق: host=${APPDB_HOST} port=${APPDB_PORT} db=${APPDB_NAME} user=${APPDB_USER} password=$(mask "$APPDB_PASSWORD")"
}

sha256_of() { # طباعة SHA256 لملف (لينكس: sha256sum، ماك: shasum -a 256)
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | awk '{print $1}';
  elif command -v shasum >/dev/null 2>&1; then shasum -a 256 "$1" | awk '{print $1}';
  else die "لا sha256sum ولا shasum متاح."; fi
}

# فحص أن اسم مضيف الخادم مطابق للمتوقّع (يُستدعى فقط عند التنفيذ على الخادم)
guard_expected_host() {
  local actual; actual="$(hostname 2>/dev/null || echo unknown)"
  if [[ -n "${OPS_EXPECTED_HOSTNAME:-}" && "${OPS_EXPECTED_HOSTNAME}" != REPLACE_ME_* ]]; then
    [[ "$actual" == "${OPS_EXPECTED_HOSTNAME}" ]] || die "حارس خادم: hostname=${actual} ≠ المتوقّع ${OPS_EXPECTED_HOSTNAME}."
  else
    log_warn "OPS_EXPECTED_HOSTNAME غير مضبوط — تخطّي فحص اسم الخادم (فحص ساكن)."
  fi
}

# يرفض القيم placeholder عند التنفيذ الفعلي (يُسمح بها في الفحص الساكن)
require_real_config() {
  [[ "$OPS_MODE" == "apply" ]] || return 0
  local bad=0
  for k in OPS_SERVER_HOST TEST_SERVICE_NAME TEST_ENV_FILE TEST_DOMAIN CURRENT_TEST_DB BACKUP_ROOT; do
    local v="${!k:-}"
    if [[ -z "$v" || "$v" == REPLACE_ME_* ]]; then log_err "الإعداد غير مكتمل: ${k}"; bad=1; fi
  done
  [[ $bad -eq 0 ]] || die "أوقِف: config.env يحوي قيم placeholder — لا تنفيذ فعليّ بها."
}
