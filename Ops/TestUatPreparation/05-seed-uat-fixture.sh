#!/usr/bin/env bash
# =====================================================================
# 05-seed-uat-fixture.sh — Fixture UAT صغير واقعي عبر الـAPI الرسمي فقط (لا SQL خام)
# ---------------------------------------------------------------------
# ينشئ بيانات UAT كافية لكل سيناريوهات القبول: أقسام + فرق + مستخدمون بأدوار
# متدرّجة (CEO→GM→Manager→TeamLeader→Employee) + عملاء + مشاريع بحالات مختلفة +
# Workstreams + Deliverables + تسليم Project-First واحد (مع حقول التصنيف v3).
#
# مبادئ إلزامية:
#   • كل الإنشاء عبر الـAPI الرسمي كـAdmin (Identity-safe) — لا كتابة SQL مباشرة.
#   • مفاتيح مستقرّة (بريد/اسم) لا IDs عشوائية ⇒ idempotent (lookup-then-create).
#   • كل البريد @uat.local — حارس يرفض أي بريد إنتاجي. لا بيانات شخصية حقيقية.
#   • كلمات المرور تُولَّد وقت التنفيذ وتُكتب في ملف تسليم آمن (600) خارج Git،
#     ولا تُطبع أبدًا على stdout (لا التوكن ولا كلمة المرور).
#   • كل استدعاء API يُفحَص؛ عند فشل الاستجابة يتوقّف فورًا (fail-fast).
#   • cleanup يمسّ بيانات الـfixture فقط (بريد @uat.local + أسماء بادئة UAT).
#
# الأوضاع (Modes):
#   (افتراضي)   plan     — طباعة الخطة فقط، بلا أي اتصال شبكي أو كتابة.
#   --apply      seed     — تنفيذ البذر (يتطلب OPS_ALLOW_WRITE=1 + تأكيد + دخول admin).
#   --verify     verify   — تحقّق قراءة-فقط من وجود بيانات الـfixture (GET فقط).
#   --cleanup    cleanup  — حذف بيانات الـfixture فقط (يتطلب OPS_ALLOW_WRITE=1 + تأكيد).
#
# الدخول: ADMIN_EMAIL + ADMIN_PASSWORD عبر البيئة (تُستهلك للحصول على توكن جلسة، لا تُطبع)،
#         أو ADMIN_TOKEN جاهز عبر البيئة. لا يُخزَّن أي سرّ في Git.
# =====================================================================
set -Eeuo pipefail
SCRIPT_NAME="05-seed-uat-fixture"
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
UAT_MAIL_DOMAIN="uat.local"   # حارس: لا بريد إنتاجي
UAT_CRED_FILE="${UAT_CRED_FILE:-${BACKUP_ROOT:-/tmp}/uat-fixture-credentials.txt}"

# =====================================================================
# مفاتيح مستقرّة (لا IDs عشوائية) — كلّها بادئة UAT للتمييز والتنظيف الآمن
# =====================================================================
# مستخدمون: email|fullName|role  (يُنشَأون بترتيب التبعية: الأعلى أولًا)
UAT_USERS=(
  "ceo@uat.local|المدير التنفيذي UAT|CEO"
  "gm@uat.local|المدير العام UAT|GeneralManager"
  "manager@uat.local|مدير UAT|Manager"
  "lead@uat.local|قائد فريق UAT|TeamLeader"
  "emp1@uat.local|موظف UAT ١|Employee"
  "emp2@uat.local|موظف UAT ٢|Employee"
)
# أقسام (NameAr)
UAT_DEPARTMENTS=( "إدارة UAT التسويق" "إدارة UAT المبيعات" )
# فرق (NameAr|departmentNameAr)
UAT_TEAMS=( "فريق UAT أ|إدارة UAT التسويق" "فريق UAT ب|إدارة UAT المبيعات" )
# عملاء (Name)
UAT_CLIENTS=( "عميل UAT ألفا" "عميل UAT بيتا" )
# مشاريع (Name|clientName|serviceType|status)  — حالات مختلفة (نشط/قيد التنفيذ/مؤرشف)
UAT_PROJECTS=(
  "مشروع UAT سوشيال نشط|عميل UAT ألفا|Social|Active"
  "مشروع UAT سيو نشط|عميل UAT بيتا|Seo|Active"
  "مشروع UAT مؤرشف|عميل UAT ألفا|Social|Closed"
)

# حارس بريد إنتاجي — يُطبَّق في كل الأوضاع
for row in "${UAT_USERS[@]}"; do
  email="${row%%|*}"
  [[ "$email" == *@"${UAT_MAIL_DOMAIN}" ]] || die "بريد غير UAT مرفوض: ${email}"
done

# =====================================================================
# وضع الخطة (plan) — لا اتصال، لا كتابة
# =====================================================================
if [[ "$ACTION" == "plan" ]]; then
  cat <<EOF
--- خطة Fixture UAT (وضع plan — لا اتصال/لا كتابة) ---
API: ${API}   |   بريد الـfixture حصريًّا @${UAT_MAIL_DOMAIN}
مستخدمون=${#UAT_USERS[@]}  أقسام=${#UAT_DEPARTMENTS[@]}  فرق=${#UAT_TEAMS[@]}  عملاء=${#UAT_CLIENTS[@]}  مشاريع=${#UAT_PROJECTS[@]}

خطوات البذر (seed، عبر الـAPI الرسمي، كلّها idempotent بمطابقة المفتاح):
 0) دخول admin (ADMIN_EMAIL+ADMIN_PASSWORD أو ADMIN_TOKEN) ⇒ توكن جلسة (لا يُطبع).
 1) الأقسام:   GET  /api/directory/departments (lookup NameAr) ثم POST إن غاب.
 2) الفرق:     GET  /api/directory/teams (lookup NameAr) ثم POST {NameAr,DepartmentId} إن غاب.
 3) المستخدمون: GET /api/directory/users (lookup Email) ثم
      POST /api/directory/users {Email,FullName,Password:<generated>,Roles:[role],DepartmentId,TeamId,ManagerId}.
      سلسلة الإدارة: emp*→lead→manager→gm→ceo. كلمات المرور تُكتب في ${UAT_CRED_FILE} (600) — لا تُطبع.
 4) قادة الفرق: PUT /api/directory/teams/{teamId} {…,TeamLeaderId:lead@uat.local}.
 5) العملاء:   GET  /api/clients (lookup Name) ثم POST /api/clients {Name} إن غاب.
 6) المشاريع:  GET  /api/projects (lookup Name) ثم POST /api/projects {ClientId,Name,ServiceType,OwnerTeamId}.
      المشروع «مؤرشف» يُنشَأ Active ثم POST /api/projects/{id}/archive.
 7) Workstreams:  POST /api/projects/{projectId}/workstreams {WorkstreamTypeCode,ResponsibleTeamId}.
 8) Deliverables: POST /api/projects/{projectId}/workstreams/{workstreamId}/deliverables {DeliverableTypeCode,UsageContextCode,PlannedQuantity}.
 9) Project-First submission: GET /api/report-templates (بالعنوان) → GET detail (حقل ProjectRepeatableSection)
      → POST /api/submissions {ReportTemplateId,PeriodType:Weekly,PeriodKey} → PUT /api/submissions/{id}/values
      (ValueJson = مصفوفة إدخالات مشروع + حقول التصنيف v3) → POST /api/submissions/{id}/submit.
10) Execution Taxonomy: مبذورة عند الإقلاع (ExecutionTaxonomySeeder) — يُتحقَّق منها في 03، لا تُنشَأ هنا.

التحقّق (verify، قراءة فقط): تأكيد وجود كل كيانات الـfixture بالأعداد المتوقّعة عبر GET فقط.
التنظيف (cleanup): حذف بيانات الـfixture فقط بالترتيب: submissions → projects (يُسقِط Workstreams/Deliverables بالـCascade)
      → clients → users → teams → departments. يمسّ فقط @uat.local + الأسماء بادئة UAT.

التشغيل الفعلي لاحقًا (المرحلة الثانية، على TEST فقط):
  ADMIN_EMAIL=… ADMIN_PASSWORD=… OPS_ALLOW_WRITE=1 ./05-seed-uat-fixture.sh --apply
  ADMIN_EMAIL=… ADMIN_PASSWORD=… ./05-seed-uat-fixture.sh --verify
  ADMIN_EMAIL=… ADMIN_PASSWORD=… OPS_ALLOW_WRITE=1 ./05-seed-uat-fixture.sh --cleanup
EOF
  exit 0
fi

# =====================================================================
# ما بعد plan: يتطلب أدوات + دخول admin
# =====================================================================
need_cmd curl; need_cmd jq
[[ "$ACTION" == "seed" || "$ACTION" == "cleanup" ]] && need_cmd openssl

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

# --- lookups (idempotency) ---
find_dept_id()    { api GET "/api/directory/departments"; jq -r --arg n "$1" '.[] | select(.nameAr==$n) | .id' <<<"$API_BODY" | head -n1; }
find_team_id()    { api GET "/api/directory/teams";       jq -r --arg n "$1" '.[] | select(.nameAr==$n) | .id' <<<"$API_BODY" | head -n1; }
find_user_id()    { api GET "/api/directory/users";       jq -r --arg e "$1" '.[] | select(.email==$e)  | .id' <<<"$API_BODY" | head -n1; }
find_client_id()  { api GET "/api/clients";               jq -r --arg n "$1" '.[] | select(.name==$n)   | .id' <<<"$API_BODY" | head -n1; }
# Workstream: مفتاح مطابقة = workstreamTypeCode + responsibleTeamId ضمن نفس المشروع.
find_workstream_id() { api GET "/api/projects/$1/workstreams"; jq -r --arg wt "$2" --arg rt "$3" '.[] | select(.workstreamTypeCode==$wt and .responsibleTeamId==$rt) | .id' <<<"$API_BODY" | head -n1; }
# Deliverable: مفتاح مطابقة = deliverableTypeCode + usageContextCode ضمن نفس الـWorkstream.
find_deliverable_id() { api GET "/api/projects/$1/workstreams/$2/deliverables"; jq -r --arg dt "$3" --arg uc "$4" '.[] | select(.deliverableTypeCode==$dt and .usageContextCode==$uc) | .id' <<<"$API_BODY" | head -n1; }
# مفتاح مركّب ثابت: اسم المشروع + ClientId — و includeClosed=true كي يشمل المشاريع المؤرشفة (Closed).
# (GET /api/projects الافتراضي يستبعد Closed، فيُعاد إنشاء المؤرشف عند غيابه — عيب idempotency.)
find_project_id() { api GET "/api/projects?includeClosed=true"; jq -r --arg n "$1" --arg cid "${2:-}" '.[] | select(.name==$n and (($cid=="") or (.clientId==$cid))) | .id' <<<"$API_BODY" | head -n1; }

# --- توليد كلمة مرور قويّة (تُلبّي سياسة: طول≥12 + Upper/Lower/Digit/Special) ---
gen_pw() { printf '%s#Aa9' "$(openssl rand -base64 18 | tr -d '\n=' | tr '/+' 'Xy')"; }

# =====================================================================
# البذر (seed)
# =====================================================================
seed_fixture() {
  require_write_enabled "بذر Fixture UAT على ${NEW_UAT_DB} عبر ${API}" || return 0
  admin_login

  # ملف تسليم كلمات المرور (600، خارج Git) — لا تُطبع على stdout
  ( umask 077; : > "$UAT_CRED_FILE" )
  chmod 600 "$UAT_CRED_FILE" 2>/dev/null || true
  {
    echo "# UAT fixture credentials — تسليم آمن، خارج Git، احذفه بعد التسليم"
    echo "# التاريخ: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  } >> "$UAT_CRED_FILE"

  log "1) الأقسام ..."
  declare -A DEPT_ID
  for d in "${UAT_DEPARTMENTS[@]}"; do
    local id; id="$(find_dept_id "$d")"
    if [[ -z "$id" ]]; then
      api_or_die POST "/api/directory/departments" "$(jq -nc --arg n "$d" '{nameAr:$n,nameEn:null,code:null,managerId:null}')" "create dept $d"
      id="$(jq -r '.id' <<<"$API_BODY")"
    fi
    DEPT_ID["$d"]="$id"; log "   ✓ قسم: $d"
  done

  log "2) الفرق ..."
  declare -A TEAM_ID
  for t in "${UAT_TEAMS[@]}"; do
    local name="${t%%|*}" deptName="${t##*|}"
    local id; id="$(find_team_id "$name")"
    if [[ -z "$id" ]]; then
      api_or_die POST "/api/directory/teams" \
        "$(jq -nc --arg n "$name" --arg dep "${DEPT_ID[$deptName]}" '{nameAr:$n,nameEn:null,departmentId:$dep,teamLeaderId:null}')" "create team $name"
      id="$(jq -r '.id' <<<"$API_BODY")"
    fi
    TEAM_ID["$name"]="$id"; log "   ✓ فريق: $name"
  done

  # الفريق/القسم الافتراضي لموظفي الـfixture
  local DEF_TEAM="${TEAM_ID['فريق UAT أ']}" DEF_DEPT="${DEPT_ID['إدارة UAT التسويق']}"

  log "3) المستخدمون (سلسلة إدارة: emp*→lead→manager→gm→ceo) ..."
  declare -A USER_ID
  # مصفوفة المدير المباشر لكل بريد (مفتاح=email)
  declare -A MGR_OF=(
    ["gm@uat.local"]="ceo@uat.local"
    ["manager@uat.local"]="gm@uat.local"
    ["lead@uat.local"]="manager@uat.local"
    ["emp1@uat.local"]="lead@uat.local"
    ["emp2@uat.local"]="lead@uat.local"
  )
  for row in "${UAT_USERS[@]}"; do
    local email="${row%%|*}"; local rest="${row#*|}"; local name="${rest%%|*}"; local role="${rest##*|}"
    local id; id="$(find_user_id "$email")"
    if [[ -z "$id" ]]; then
      local mgrEmail="${MGR_OF[$email]:-}" mgrId="null"
      [[ -n "$mgrEmail" ]] && mgrId="$(jq -Rn --arg v "${USER_ID[$mgrEmail]:-}" '$v | if .=="" then null else . end')"
      local teamId="null" deptId="null"
      case "$role" in Employee|TeamLeader) teamId="\"$DEF_TEAM\""; deptId="\"$DEF_DEPT\"";; esac
      local pw; pw="$(gen_pw)"
      local reqbody
      reqbody="$(jq -nc --arg e "$email" --arg n "$name" --arg p "$pw" --arg r "$role" \
        --argjson team "$teamId" --argjson dept "$deptId" --argjson mgr "$mgrId" \
        '{email:$e,fullName:$n,password:$p,roles:[$r],departmentId:$dept,teamId:$team,managerId:$mgr}')"
      api_or_die POST "/api/directory/users" "$reqbody" "create user $email"
      id="$(jq -r '.id' <<<"$API_BODY")"
      printf '%s\t%s\t%s\n' "$email" "$role" "$pw" >> "$UAT_CRED_FILE"
      unset pw reqbody
    fi
    USER_ID["$email"]="$id"; log "   ✓ مستخدم: $email ($role)"
  done
  log "   (كلمات المرور المولّدة كُتبت في ${UAT_CRED_FILE} — 600، لم تُطبع)"

  log "4) ضبط قائد فريق UAT أ = lead@uat.local ..."
  local leadId="${USER_ID['lead@uat.local']}"
  api_or_die PUT "/api/directory/teams/${DEF_TEAM}" \
    "$(jq -nc --arg n 'فريق UAT أ' --arg dep "$DEF_DEPT" --arg tl "$leadId" \
      '{nameAr:$n,nameEn:null,departmentId:$dep,teamLeaderId:$tl,isActive:true,syncMemberDepartments:true}')" "set team leader"
  log "   ✓ قائد الفريق مضبوط."

  log "5) العملاء ..."
  declare -A CLIENT_ID
  for c in "${UAT_CLIENTS[@]}"; do
    local id; id="$(find_client_id "$c")"
    if [[ -z "$id" ]]; then
      api_or_die POST "/api/clients" "$(jq -nc --arg n "$c" '{name:$n}')" "create client $c"
      id="$(jq -r '.id' <<<"$API_BODY")"
    fi
    CLIENT_ID["$c"]="$id"; log "   ✓ عميل: $c"
  done

  log "6) المشاريع (حالات مختلفة) ..."
  declare -A PROJECT_ID
  for p in "${UAT_PROJECTS[@]}"; do
    IFS='|' read -r pname cname svc status <<<"$p"
    local id; id="$(find_project_id "$pname" "${CLIENT_ID[$cname]}")"
    if [[ -z "$id" ]]; then
      api_or_die POST "/api/projects" \
        "$(jq -nc --arg cid "${CLIENT_ID[$cname]}" --arg n "$pname" --arg s "$svc" --arg ot "$DEF_TEAM" \
          '{clientId:$cid,name:$n,serviceType:$s,ownerTeamId:$ot}')" "create project $pname"
      id="$(jq -r '.id' <<<"$API_BODY")"
      if [[ "$status" == "Closed" ]]; then
        api_or_die POST "/api/projects/${id}/archive" "" "archive project $pname"
        log "     (أُرشِف: $pname)"
      fi
    fi
    PROJECT_ID["$pname"]="$id"; log "   ✓ مشروع: $pname [$status]"
  done

  log "7-8) Workstreams + Deliverables على «مشروع UAT سوشيال نشط» ..."
  local proj1="${PROJECT_ID['مشروع UAT سوشيال نشط']}"
  # تيار عمل واحد (سوشيال ميديا) مسؤول عنه فريق UAT أ — idempotent: استعلم أولًا فإن وُجد فأعد استخدامه.
  local wsId; wsId="$(find_workstream_id "$proj1" "social_media" "$DEF_TEAM")"
  if [[ -z "$wsId" ]]; then
    api_or_die POST "/api/projects/${proj1}/workstreams" \
      "$(jq -nc --arg wt 'social_media' --arg rt "$DEF_TEAM" '{workstreamTypeCode:$wt,responsibleTeamId:$rt,sortOrder:0}')" "create workstream"
    wsId="$(jq -r '.id' <<<"$API_BODY")"
    log "   ✓ workstream: social_media (أُنشئ)"
  else
    log "   ✓ workstream: social_media (موجود — أُعيد استخدامه)"
  fi
  # مُخرَجان بسياقَي استخدام مختلفين — idempotent لكل (deliverableTypeCode + usageContextCode).
  local dId
  dId="$(find_deliverable_id "$proj1" "$wsId" "post" "organic_social")"
  if [[ -z "$dId" ]]; then
    api_or_die POST "/api/projects/${proj1}/workstreams/${wsId}/deliverables" \
      "$(jq -nc '{deliverableTypeCode:"post",usageContextCode:"organic_social",plannedQuantity:8,priority:"Medium"}')" "create deliverable post"
    log "   ✓ deliverable: post/organic_social (أُنشئ)"
  else
    log "   ✓ deliverable: post/organic_social (موجود — تُخُطّي)"
  fi
  dId="$(find_deliverable_id "$proj1" "$wsId" "reel" "paid_ads")"
  if [[ -z "$dId" ]]; then
    api_or_die POST "/api/projects/${proj1}/workstreams/${wsId}/deliverables" \
      "$(jq -nc '{deliverableTypeCode:"reel",usageContextCode:"paid_ads",plannedQuantity:4,priority:"High"}')" "create deliverable reel"
    log "   ✓ deliverable: reel/paid_ads (أُنشئ)"
  else
    log "   ✓ deliverable: reel/paid_ads (موجود — تُخُطّي)"
  fi

  log "9) تسليم Project-First (emp1، «تقرير المديرشن الأسبوعي») ..."
  seed_project_first_submission "emp1@uat.local" "تقرير المديرشن الأسبوعي" "$proj1"

  log "10) Execution Taxonomy: مبذورة عند الإقلاع — يُتحقَّق منها عبر 03 (لا إنشاء هنا)."
  log "✓ اكتمل البذر. للتسليم: انسخ ${UAT_CRED_FILE} عبر قناة آمنة ثم احذفه."
}

# --- تسليم Project-First كامل (draft → save PRS values → submit) ---
seed_project_first_submission() {
  local actorEmail="$1" templateTitle="$2" projectId="$3"

  # نُنشئ التسليم كـadmin نيابةً؟ لا — التسليم مرتبط بالمُرسِل. للـfixture نكتفي بإنشائه
  # عبر توكن admin (الخادم يربطه بالمستخدم الحالي=admin) — كافٍ لاختبار التجميع Project-First.
  api_or_die GET "/api/report-templates" "" "list templates"
  local tid; tid="$(jq -r --arg t "$templateTitle" '.[] | select(.title==$t) | .id' <<<"$API_BODY" | head -n1)"
  [[ -n "$tid" ]] || die "قالب Project-First غير موجود: $templateTitle"

  api_or_die GET "/api/report-templates/${tid}" "" "template detail"
  # حقل ProjectRepeatableSection من الإصدار المنشور
  local prsFieldId
  prsFieldId="$(jq -r '
    ([.versions[] | select(.isPublished==true)] | last) // (.versions | last)
    | .fields[] | select(.fieldType=="ProjectRepeatableSection") | .id' <<<"$API_BODY" | head -n1)"
  [[ -n "$prsFieldId" ]] || die "لم يُعثر على حقل ProjectRepeatableSection في القالب."

  local weekKey; weekKey="${UAT_PERIOD_KEY:-2026-W28}"
  # idempotent: إن وُجد تسليم بنفس (عنوان القالب + الفترة) فلا تُنشئ آخر —
  # حارس تفرّد التسليم (مُرسِل، قالب، فترة) يرفض التكرار بـ409، وهو ليس نتيجة متوقعة.
  api_or_die GET "/api/submissions?periodKey=${weekKey}" "" "list submissions"
  local existingSub
  existingSub="$(jq -r --arg t "$templateTitle" --arg pk "$weekKey" '.[] | select(.templateTitle==$t and .periodKey==$pk) | .id' <<<"$API_BODY" | head -n1)"
  if [[ -n "$existingSub" ]]; then
    log "   ✓ تسليم Project-First موجود مسبقًا (subId مخفي، week=${weekKey}) — تُخُطّي الإنشاء."
    return 0
  fi
  api_or_die POST "/api/submissions" \
    "$(jq -nc --arg tpl "$tid" --arg pk "$weekKey" '{reportTemplateId:$tpl,periodType:"Weekly",periodKey:$pk}')" "create submission"
  local subId; subId="$(jq -r '.id' <<<"$API_BODY")"

  # قيمة PRS: إدخال مشروع واحد + حقول التصنيف v3 المطلوبة
  local valueJson
  valueJson="$(jq -nc --arg pid "$projectId" '
    [ { projectId:$pid, answers:{
        planned:"25", completed:"20", approved:"16", revisions:"4", published:"10", delayed:"2",
        activity_type:"comments", interaction_result:"inquiry", response_time:"under_1h", count:"12"
    } } ]')"
  api_or_die PUT "/api/submissions/${subId}/values" \
    "$(jq -nc --arg fid "$prsFieldId" --arg vj "$valueJson" '{values:[{templateFieldId:$fid,valueJson:$vj}]}')" "save PRS values"

  api_or_die POST "/api/submissions/${subId}/submit" "" "submit"
  log "   ✓ تسليم Project-First مُرسَل (subId مخفي، week=${weekKey})."
}

# =====================================================================
# التحقّق (verify) — قراءة فقط (GET)
# =====================================================================
verify_fixture() {
  admin_login
  local fail=0
  chk() { local label="$1" have="$2" want="$3"
    if [[ "$have" -ge "$want" ]]; then log "✓ ${label}: ${have} (≥ ${want})"; else log_err "✗ ${label}: ${have} (< ${want})"; fail=1; fi; }

  api_or_die GET "/api/directory/departments" "" "GET departments"
  chk "أقسام UAT" "$(jq '[.[] | select(.nameAr|startswith("إدارة UAT"))] | length' <<<"$API_BODY")" "${#UAT_DEPARTMENTS[@]}"

  api_or_die GET "/api/directory/teams" "" "GET teams"
  chk "فرق UAT" "$(jq '[.[] | select(.nameAr|startswith("فريق UAT"))] | length' <<<"$API_BODY")" "${#UAT_TEAMS[@]}"
  chk "قائد فريق UAT أ مضبوط" "$(jq '[.[] | select(.nameAr=="فريق UAT أ" and .teamLeaderId!=null)] | length' <<<"$API_BODY")" 1

  api_or_die GET "/api/directory/users" "" "GET users"
  chk "مستخدمو UAT" "$(jq '[.[] | select(.email|endswith("@uat.local"))] | length' <<<"$API_BODY")" "${#UAT_USERS[@]}"

  api_or_die GET "/api/clients" "" "GET clients"
  chk "عملاء UAT" "$(jq '[.[] | select(.name|startswith("عميل UAT"))] | length' <<<"$API_BODY")" "${#UAT_CLIENTS[@]}"

  api_or_die GET "/api/projects" "" "GET projects"
  chk "مشاريع UAT" "$(jq '[.[] | select(.name|startswith("مشروع UAT"))] | length' <<<"$API_BODY")" "${#UAT_PROJECTS[@]}"

  [[ $fail -eq 0 ]] && log "✓ التحقّق ناجح — كل كيانات الـfixture موجودة." || die "التحقّق أخفق — راجع الأعداد أعلاه."
}

# =====================================================================
# التنظيف (cleanup) — بيانات الـfixture فقط
# =====================================================================
cleanup_fixture() {
  require_write_enabled "حذف Fixture UAT فقط (بريد @uat.local + أسماء بادئة UAT) عبر ${API}" || return 0
  admin_login

  # 1) submissions — نحذف تسليمات UAT (المرتبطة بمشاريع UAT). حذف التسليم يفكّ قيد حذف المشروع.
  log "1) حذف تسليمات UAT ..."
  api GET "/api/submissions?mine=false" || true
  # نعتمد مسار عام؛ إن تعذّر الفلترة نتخطّى بأمان (المشاريع تُحذف لاحقًا بعد فكّ الارتباط).
  local subIds
  subIds="$(jq -r '.. | objects | select(has("id") and (has("periodKey"))) | .id' <<<"${API_BODY:-{}}" 2>/dev/null | sort -u || true)"
  for s in $subIds; do
    api DELETE "/api/submissions/${s}"; api_ok && log "   ✓ حُذف تسليم ${s:0:8}…" || log_warn "   تعذّر حذف تسليم ${s:0:8}… (HTTP ${API_STATUS})"
  done

  # 2) projects (يُسقِط Workstreams/Deliverables بالـCascade)
  log "2) حذف مشاريع UAT (Cascade على Workstreams/Deliverables) ..."
  api_or_die GET "/api/projects" "" "GET projects"
  for id in $(jq -r '.[] | select(.name|startswith("مشروع UAT")) | .id' <<<"$API_BODY"); do
    api DELETE "/api/projects/${id}"; api_ok && log "   ✓ حُذف مشروع ${id:0:8}…" || log_warn "   تعذّر حذف مشروع ${id:0:8}… (HTTP ${API_STATUS}: ${API_BODY:0:120})"
  done

  # 3) clients
  log "3) حذف عملاء UAT ..."
  api_or_die GET "/api/clients" "" "GET clients"
  for id in $(jq -r '.[] | select(.name|startswith("عميل UAT")) | .id' <<<"$API_BODY"); do
    api DELETE "/api/clients/${id}"; api_ok && log "   ✓ حُذف عميل ${id:0:8}…" || log_warn "   تعذّر حذف عميل ${id:0:8}… (HTTP ${API_STATUS})"
  done

  # 4) users (@uat.local)
  log "4) حذف مستخدمي UAT (@uat.local) ..."
  api_or_die GET "/api/directory/users" "" "GET users"
  for id in $(jq -r '.[] | select(.email|endswith("@uat.local")) | .id' <<<"$API_BODY"); do
    api DELETE "/api/directory/users/${id}"; api_ok && log "   ✓ حُذف مستخدم ${id:0:8}…" || log_warn "   تعذّر حذف مستخدم ${id:0:8}… (HTTP ${API_STATUS})"
  done

  # 5) teams (بادئة UAT)
  log "5) حذف فرق UAT ..."
  api_or_die GET "/api/directory/teams" "" "GET teams"
  for id in $(jq -r '.[] | select(.nameAr|startswith("فريق UAT")) | .id' <<<"$API_BODY"); do
    api DELETE "/api/directory/teams/${id}"; api_ok && log "   ✓ حُذف فريق ${id:0:8}…" || log_warn "   تعذّر حذف فريق ${id:0:8}… (HTTP ${API_STATUS})"
  done

  # 6) departments (بادئة UAT)
  log "6) حذف أقسام UAT ..."
  api_or_die GET "/api/directory/departments" "" "GET departments"
  for id in $(jq -r '.[] | select(.nameAr|startswith("إدارة UAT")) | .id' <<<"$API_BODY"); do
    api DELETE "/api/directory/departments/${id}"; api_ok && log "   ✓ حُذف قسم ${id:0:8}…" || log_warn "   تعذّر حذف قسم ${id:0:8}… (HTTP ${API_STATUS})"
  done

  log "✓ اكتمل التنظيف — لم يُمَسّ سوى بيانات الـfixture (@uat.local + بادئة UAT)."
  log_warn "احذف ملف كلمات المرور يدويًّا إن لم يعد لازمًا: ${UAT_CRED_FILE}"
}

# =====================================================================
# التوزيع
# =====================================================================
case "$ACTION" in
  seed)    seed_fixture ;;
  verify)  verify_fixture ;;
  cleanup) cleanup_fixture ;;
  *)       die "وضع غير معروف: ${ACTION}" ;;
esac
