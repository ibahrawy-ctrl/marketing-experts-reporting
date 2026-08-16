#!/usr/bin/env bash
# RECONCILE-PROD-DEVELOP-LINEAGE — بوّابة الدخان الموحّدة على TEST (الإصدار 3).
#
# ما الذي تغيّر عن `uni-smoke.sh` ولماذا (عيوب أداة لا عيوب منتج):
#  1) كان `CLIENT_ID` و`DOC_ID` يُنتقيان بجملتَي `limit 1` مستقلّتَين بلا ربط،
#     فكان المستند قد يخصّ عميلًا آخر. مُوِّه ذلك حين كان في القاعدة مستند واحد فقط؛
#     ومع 16 مستندًا صار الربط الخاطئ يُنتِج 404 مشروعًا. الآن يُنتقى الصفّ نفسه.
#  2) كان عدد المستندات المتوقَّع ثابتًا (1). الآن يُشتقّ من القاعدة لنفس العميل
#     **بعد استبعاد المحذوف منطقيًّا والمؤرشَف**، لأنّ الواجهة تُرجِع الحيّ فقط.
#     كذلك يُنتقى مستند حيّ لا مستند محذوف: عودة 404 لمستند محذوف سلوك صحيح
#     (سياسة «المرفوض يُرجِع 404 لا 403»)، فصار فحصًا موجبًا مستقلًّا بدل أن يُعَدّ فشلًا.
#  3) كان القسم 7 يقارن بثوابت مجمَّدة من 15 أغسطس. «حفظ البيانات» يعني المقارنة
#     بخطّ الأساس الفعليّ لما قبل النشر المستخرَج من نسخة `pg_dump` في Phase L.1،
#     مع استثناء واحد مقصود ومعلَن: عدد الهجرات 35 → 38.
set -uo pipefail

API="http://127.0.0.1:5091"
ENVF="/etc/khubara-reporting-test.env"
BASELINE="/root/backups/20260816-recon-l/baseline-counts.env"

ADMIN_EMAIL=$(grep -E '^Seed__AdminEmail=' "$ENVF" | cut -d= -f2- | tr -d '"')
ADMIN_PASS=$(grep -E '^Seed__AdminPassword=' "$ENVF" | cut -d= -f2- | tr -d '"')

CONN=$(grep -E '^ConnectionStrings__Default' "$ENVF" | cut -d= -f2- | tr -d '"')
export PGHOST=$(echo "$CONN" | tr ';' '\n' | grep -i '^Host=' | cut -d= -f2)
export PGPORT=$(echo "$CONN" | tr ';' '\n' | grep -i '^Port=' | cut -d= -f2)
export PGDATABASE=$(echo "$CONN" | tr ';' '\n' | grep -i '^Database=' | cut -d= -f2)
export PGUSER=$(echo "$CONN" | tr ';' '\n' | grep -i '^Username=' | cut -d= -f2)
export PGPASSWORD=$(echo "$CONN" | tr ';' '\n' | grep -i '^Password=' | cut -d= -f2)

PASS=0; FAIL=0
chk() { # chk <label> <actual> <expected>
  if [ "$2" = "$3" ]; then echo "  PASS  $1 -> $2"; PASS=$((PASS+1));
  else echo "  FAIL  $1 -> got=$2 expected=$3"; FAIL=$((FAIL+1)); fi
}
code() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

# خطّ الأساس لما قبل النشر (مستخرَج من نسخة pg_dump لا من ثوابت مكتوبة يدويًّا)
# shellcheck disable=SC1090
[ -f "$BASELINE" ] && . "$BASELINE"

echo "===== 0) BASELINE ====="
echo "  health=$(code $API/health)"
# الصفّ نفسه: العميل والمستند مترابطان بحُكم مصدرهما الواحد، والمستند حيّ.
read -r DOC_ID CLIENT_ID < <(psql -At -F' ' -c 'select "Id","ClientId" from client_documents where not "IsDeleted" and not "IsArchived" order by "Id" limit 1;')
DELETED_DOC_ID=$(psql -Atc "select \"Id\" from client_documents where \"IsDeleted\" and \"ClientId\"='$CLIENT_ID' order by \"Id\" limit 1;")
PROJECT_ID=$(psql -Atc 'select "Id" from projects order by "Id" limit 1;')
DOCS_EXPECTED=$(psql -Atc "select count(*) from client_documents where \"ClientId\"='$CLIENT_ID' and not \"IsDeleted\" and not \"IsArchived\";")
echo "  client_id=$CLIENT_ID"
echo "  document_id=$DOC_ID"
echo "  deleted_document_id=${DELETED_DOC_ID:-none}"
echo "  project_id=$PROJECT_ID"
echo "  documents_for_client=$DOCS_EXPECTED"
OUTBOX_BEFORE=$(psql -Atc 'select count(*) from email_outbox;' 2>/dev/null || echo "n/a")
echo "  email_outbox_before=$OUTBOX_BEFORE"

echo "===== 1) AUTH ====="
TOKEN=$(curl -s -X POST "$API/api/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASS\"}" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')
if [ -n "$TOKEN" ]; then echo "  PASS  admin login -> token acquired (len=${#TOKEN})"; PASS=$((PASS+1));
else echo "  FAIL  admin login -> no token"; FAIL=$((FAIL+1)); echo "SMOKE ABORTED"; exit 1; fi
AUTH="Authorization: Bearer $TOKEN"

echo "===== 2) CPW-R2 · CLIENT DOCUMENTS ====="
chk "GET /api/clients/{id}/documents"        "$(code -H "$AUTH" "$API/api/clients/$CLIENT_ID/documents")" "200"
chk "GET /api/clients/{id}/links"            "$(code -H "$AUTH" "$API/api/clients/$CLIENT_ID/links")" "200"
chk "GET /api/clients/{id}/documents/{docId}" "$(code -H "$AUTH" "$API/api/clients/$CLIENT_ID/documents/$DOC_ID")" "200"
chk "GET .../documents/storage-usage"        "$(code -H "$AUTH" "$API/api/clients/$CLIENT_ID/documents/storage-usage")" "200"
chk "GET .../documents/{docId}/download"     "$(code -H "$AUTH" "$API/api/clients/$CLIENT_ID/documents/$DOC_ID/download")" "200"
DOCS_JSON=$(curl -s -H "$AUTH" "$API/api/clients/$CLIENT_ID/documents")
DOC_COUNT=$(echo "$DOCS_JSON" | grep -o '"id"' | wc -l | tr -d ' ')
chk "document list matches live rows in db"  "$DOC_COUNT" "$DOCS_EXPECTED"
if [ -n "$DELETED_DOC_ID" ]; then
  chk "soft-deleted document -> 404 not 403"  "$(code -H "$AUTH" "$API/api/clients/$CLIENT_ID/documents/$DELETED_DOC_ID")" "404"
fi

echo "===== 3) CPW-R3 · PROJECT 360 ====="
chk "GET /api/projects/{id}/overview"        "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/overview")" "200"
STRAT=$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/strategy")
if [ "$STRAT" = "200" ] || [ "$STRAT" = "204" ]; then echo "  PASS  GET /api/projects/{id}/strategy -> $STRAT (204 = no strategy row yet, valid)"; PASS=$((PASS+1)); else echo "  FAIL  GET /api/projects/{id}/strategy -> $STRAT"; FAIL=$((FAIL+1)); fi
chk "GET /api/projects/{id}/strategy/schema" "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/strategy/schema")" "200"
chk "GET /api/projects/{id}/objectives"      "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/objectives")" "200"
chk "GET /api/projects/{id}/kpis"            "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/kpis")" "200"
chk "GET /api/projects/{id}/contract-deliverables" "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/contract-deliverables")" "200"
chk "GET /api/projects/{id}/risks"           "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/risks")" "200"
chk "GET /api/projects/{id}/decisions"       "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/decisions")" "200"
chk "GET /api/projects/{id}/notes"           "$(code -H "$AUTH" "$API/api/projects/$PROJECT_ID/notes")" "200"

echo "===== 4) CATALOG ====="
chk "GET /api/execution-taxonomy"            "$(code -H "$AUTH" "$API/api/execution-taxonomy")" "200"
chk "catalog scoped domains total"           "$(psql -Atc "select count(*) from execution_taxonomy_values where \"Domain\" in ('strategy_section','strategy_field','contract_deliverable');")" "38"
chk "catalog duplicates"                     "$(psql -Atc 'select count(*) from (select "Domain","Code" from execution_taxonomy_values group by 1,2 having count(*)>1) d;')" "0"

echo "===== 5) ANTI-ENUMERATION / SCOPE ====="
GHOST="00000000-0000-0000-0000-000000000001"
chk "unknown document -> 404 (not 403)"      "$(code -H "$AUTH" "$API/api/clients/$CLIENT_ID/documents/$GHOST")" "404"
chk "unknown client documents -> 404"        "$(code -H "$AUTH" "$API/api/clients/$GHOST/documents")" "404"
chk "unknown project overview -> 404"        "$(code -H "$AUTH" "$API/api/projects/$GHOST/overview")" "404"
chk "unknown project objectives -> 404"      "$(code -H "$AUTH" "$API/api/projects/$GHOST/objectives")" "404"
chk "unknown project decisions -> 404"       "$(code -H "$AUTH" "$API/api/projects/$GHOST/decisions")" "404"
chk "no token -> 401"                        "$(code "$API/api/clients/$CLIENT_ID/documents")" "401"
chk "no token project 360 -> 401"            "$(code "$API/api/projects/$PROJECT_ID/overview")" "401"
chk "bad token -> 401"                       "$(code -H 'Authorization: Bearer invalid.token.value' "$API/api/projects/$PROJECT_ID/overview")" "401"

echo "===== 6) EMAIL / REMINDER SILENCE ====="
grep -E '^(EmailNotifications__Mode|Email__Enabled|Reminders__Enabled)' "$ENVF" | sed 's/^/  /'
OUTBOX_AFTER=$(psql -Atc 'select count(*) from email_outbox;' 2>/dev/null || echo "n/a")
chk "email outbox unchanged"                 "$OUTBOX_AFTER" "$OUTBOX_BEFORE"
chk "email outbox vs pre-deploy baseline"    "$OUTBOX_AFTER" "${BL_email_outbox:-0}"
SENT=$(psql -Atc "select count(*) from email_outbox where \"SentAtUtc\" > now() - interval '1 hour';" 2>/dev/null || echo "0")
chk "no messages sent in last hour"          "$SENT" "0"
chk "reminder scheduler flag absent/false"   "$(grep -cE '^ReportReminderScheduler__Enabled=true' "$ENVF")" "0"

echo "===== 7) DATA PRESERVATION vs PRE-DEPLOY DUMP ====="
chk "client_documents"                       "$(psql -Atc 'select count(*) from client_documents;')" "${BL_client_documents:?}"
chk "client_document_versions"               "$(psql -Atc 'select count(*) from client_document_versions;')" "${BL_client_document_versions:?}"
chk "clients"                                "$(psql -Atc 'select count(*) from clients;')" "${BL_clients:?}"
chk "projects"                               "$(psql -Atc 'select count(*) from projects;')" "${BL_projects:?}"
chk "report_templates"                       "$(psql -Atc 'select count(*) from report_templates;')" "${BL_report_templates:?}"
chk "users"                                  "$(psql -Atc 'select count(*) from "AspNetUsers";')" "${BL_AspNetUsers:?}"
chk "submissions"                            "$(psql -Atc 'select count(*) from report_submissions;')" "${BL_report_submissions:?}"
chk "execution_taxonomy_values"              "$(psql -Atc 'select count(*) from execution_taxonomy_values;')" "${BL_execution_taxonomy_values:?}"
# الاستثناء المقصود الوحيد: ثلاث هجرات إضافيّة بحتة من نَسَب الإنتاج.
chk "migrations (baseline + 3 by design)"    "$(psql -Atc 'select count(*) from "__EFMigrationsHistory";')" "$(( ${BL___EFMigrationsHistory:?} + 3 ))"
chk "migrations head"                        "$(psql -Atc 'select max("MigrationId") from "__EFMigrationsHistory";')" "20260811142239_AddProject360Foundation"
chk "document storage fingerprint"           "$(find /var/lib/reporting-test/documents -type f | sort | xargs -r md5sum | md5sum | cut -d' ' -f1)" "$(cut -d' ' -f1 /root/backups/20260816-recon-l/storage-md5-before.txt)"

echo
echo "===== SMOKE SUMMARY ====="
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" -eq 0 ] && echo "SMOKE_GATE=PASS" || echo "SMOKE_GATE=FAIL"
