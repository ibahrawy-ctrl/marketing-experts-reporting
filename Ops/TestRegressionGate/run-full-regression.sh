#!/usr/bin/env bash
# =============================================================================
# RECONCILE-PROD-DEVELOP-LINEAGE — بوّابة الانحدار الكامل (بروتوكول cand10)
# -----------------------------------------------------------------------------
# يرفض التشغيل ما لم تتحقّق كلّ شروط العزل. الغرض منع تكرار الأخطاء الإجرائيّة
# الأربعة الموثّقة في Docs/Planning/RECONCILE-LINEAGE-R1-15-…-MEASUREMENT-PROTOCOL.md §3.2:
#   1) قاعدة مُعاد استعمالها/ملوّثة   ⟹ رقم باطل 67 ومدّة 5 س 11 د
#   2) تمهيد قاعدة PFE مفقود          ⟹ رقم باطل 29 (13 فشلًا وهميًّا)
#   3) ترقيع مفاتيح الفترات مُتلَف     ⟹ رقم باطل 16 (15 فشلًا وهميًّا)
#   4) TEST_DB_CONNECTION غير مضبوط  ⟹ التمهيد يذهب صامتًا إلى reporting_test
#
# الاستعمال:
#   export TEST_DB_CONNECTION="Host=localhost;Database=<main>;Username=<user>"
#   export TEST_DB_CONNECTION_CAL="Host=localhost;Database=<cal>;Username=<user>"
#   export TEST_DB_CONNECTION_PFE="Host=localhost;Database=<pfe>;Username=<user>"
#   Ops/TestRegressionGate/run-full-regression.sh
#
# لا يحوي هذا الملفّ أيّ اسم قاعدة محلّيّة ولا مستخدمًا ولا سرًّا.
# =============================================================================
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND="$REPO_ROOT/reporting-backend"
TESTS_PROJ="tests/Reporting.IntegrationTests/Reporting.IntegrationTests.csproj"
EXPECTED_TABLE_COUNT="${EXPECTED_TABLE_COUNT:-78}"

# القواعد المحظورة صراحةً — أيّ قياس عليها باطل بحكم البروتوكول.
FORBIDDEN_DBS="reporting_test reporting_prod reporting_rc reporting_test_uat"

fail() { echo "GATE REFUSED — $*" >&2; exit 2; }

# --- (1) وجود المتغيّرات الثلاثة -------------------------------------------
for v in TEST_DB_CONNECTION TEST_DB_CONNECTION_CAL TEST_DB_CONNECTION_PFE; do
  [ -n "${!v:-}" ] || fail "متغيّر البيئة $v غير مضبوط. البروتوكول يوجب قواعد معزولة للقواعد الثلاث."
done

# --- (2) استخراج الحقول والتحقّق منها --------------------------------------
field() { # field <conn> <Key>
  printf '%s' "$1" | tr ';' '\n' | awk -F= -v k="$2" 'tolower($1)==tolower(k){print $2}' | head -1
}

declare -a DBS=() HOSTS=()
for v in TEST_DB_CONNECTION TEST_DB_CONNECTION_CAL TEST_DB_CONNECTION_PFE; do
  conn="${!v}"
  db="$(field "$conn" Database)"; host="$(field "$conn" Host)"
  [ -n "$db" ]   || fail "$v لا يحوي Database=."
  [ -n "$host" ] || fail "$v لا يحوي Host=."
  for bad in $FORBIDDEN_DBS; do
    [ "$db" = "$bad" ] && fail "$v يشير إلى القاعدة المحظورة '$bad'. لا يُقاس انحدار على قاعدة مشتركة أو حيّة."
  done
  case "$host" in
    localhost|127.0.0.1|::1) ;;
    *) fail "$v يشير إلى مضيف بعيد '$host'. ممنوع الاتّصال بـTEST/RC/Production من بوّابة الانحدار." ;;
  esac
  DBS+=("$db"); HOSTS+=("$host")
done

# --- (3) تمايز القواعد الثلاث ----------------------------------------------
uniq_count="$(printf '%s\n' "${DBS[@]}" | sort -u | wc -l | tr -d ' ')"
[ "$uniq_count" -eq 3 ] || fail "القواعد الثلاث ليست متمايزة (${DBS[*]}). التوازي يُسبّب تسابق هجرات."

# --- (4) حارس مفاتيح الفترات المستقبليّة ------------------------------------
# مفتاح فترة مستقبليّ يعبر بوّابات التقويم = فشل وهميّ. الاستثناء المشروع الوحيد
# TemplateVersionManagementTests لأنّه يُدرِج مباشرةً عبر AppDbContext بلا بوّابة.
future_keys="$(grep -rn '"20[3-9][0-9]-' "$BACKEND/tests/Reporting.IntegrationTests" --include='*.cs' \
  | grep -v 'TemplateVersionManagementTests' || true)"
[ -z "$future_keys" ] || { echo "$future_keys" >&2; fail "مفاتيح فترات مستقبليّة مكتشفة (أعلاه)."; }

echo "GATE PRECHECK OK — main=${DBS[0]} cal=${DBS[1]} pfe=${DBS[2]}"

cd "$BACKEND"

# --- (5) تمهيد متتابع للقواعد الثلاث — بالترتيب لا بالتوازي -----------------
echo "=== تمهيد main ==="; dotnet test "$TESTS_PROJ" -c Debug --no-build --filter "FullyQualifiedName~HealthTests" 2>&1 | tail -2
echo "=== تمهيد cal  ==="; dotnet test "$TESTS_PROJ" -c Debug --no-build --filter "FullyQualifiedName~DailyCalendarTests" 2>&1 | tail -2
echo "=== تمهيد pfe  ==="; dotnet test "$TESTS_PROJ" -c Debug --no-build --filter "FullyQualifiedName~ProjectFirstExecutionAggregationTests" 2>&1 | tail -2

# --- (6) التحقّق من التمهيد بالمخطّط لا بنجاح الاختبار ----------------------
for i in 0 1 2; do
  n="$(psql -h "${HOSTS[$i]}" -d "${DBS[$i]}" -tAc \
        "select count(*) from information_schema.tables where table_schema='public'")"
  [ "$n" = "$EXPECTED_TABLE_COUNT" ] \
    || fail "القاعدة '${DBS[$i]}' فيها $n جدولًا والمتوقّع $EXPECTED_TABLE_COUNT. التمهيد ناقص — نجاح اختبار التمهيد ليس دليلًا."
done

# --- (7) عبارة التصنيف على main حصرًا — يجب UPDATE 5 ------------------------
upd="$(psql -h "${HOSTS[0]}" -d "${DBS[0]}" -tAc \
  "UPDATE report_templates SET \"Classification\"='Supplementary' WHERE \"Title\" IN (
     'تقرير متابعة مقالات SEO الأسبوعي','تقرير كاتب المحتوى الأسبوعي',
     'تقرير فريق التصميم','تقرير فريق الفيديو','تقرير المديرشن الأسبوعي');" 2>&1 | tr -d ' ')"
[ "$upd" = "UPDATE5" ] || fail "عبارة التصنيف أرجعت '$upd' والمتوقّع 'UPDATE 5'. أعِد إنشاء القاعدة."

echo "GATE READY — تشغيل الانحدار الكامل"
date
dotnet test Reporting.sln -c Debug --no-build
date
