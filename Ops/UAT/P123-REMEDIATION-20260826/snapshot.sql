-- §5 — بصمة السجلّات غير الاصطناعيّة قبل/بعد إنشاء بيانات UAT.
-- قراءة فقط. الاصطناعيّ = ما بريده ينتهي بـ@uat123.test أو اسمه يبدأ بـUAT-P123-.
WITH synth AS (
  SELECT "Id" FROM "AspNetUsers" WHERE "Email" LIKE '%@uat123.test'
)
SELECT 'users_nonsynth' AS k, count(*)::text AS v FROM "AspNetUsers" WHERE "Id" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'departments_nonsynth', count(*)::text FROM departments WHERE "NameAr" NOT LIKE 'UAT-P123-%'
UNION ALL SELECT 'teams_nonsynth', count(*)::text FROM teams WHERE "NameAr" NOT LIKE 'UAT-P123-%'
UNION ALL SELECT 'report_submissions_nonsynth', count(*)::text FROM report_submissions WHERE "SubmitterId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'kpi_evaluations_nonsynth', count(*)::text FROM kpi_evaluations WHERE "SubjectUserId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'attendance_incidents_nonsynth', count(*)::text FROM attendance_incidents WHERE "SubjectUserId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'leave_requests_nonsynth', count(*)::text FROM leave_requests WHERE "RequesterUserId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'employee_service_requests_nonsynth', count(*)::text FROM employee_service_requests WHERE "RequesterUserId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'employee_checklist_items_nonsynth', count(*)::text FROM employee_checklist_items WHERE "SubjectUserId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'governance_items_total', count(*)::text FROM governance_items
UNION ALL SELECT 'kpi_templates_total', count(*)::text FROM kpi_templates
UNION ALL SELECT 'report_templates_total', count(*)::text FROM report_templates
UNION ALL SELECT 'migrations_total', count(*)::text FROM "__EFMigrationsHistory"
-- بصمة محتوى (لا عدّ فقط): أيّ تعديل على صفّ قائم يغيّرها
UNION ALL SELECT 'digest_users_nonsynth', md5(coalesce(string_agg("Id"::text || '|' || "Email" || '|' || "PasswordHash", ',' ORDER BY "Id"), ''))
           FROM "AspNetUsers" WHERE "Id" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'digest_kpi_eval_nonsynth', md5(coalesce(string_agg("Id"::text || '|' || "Status"::text, ',' ORDER BY "Id"), ''))
           FROM kpi_evaluations WHERE "SubjectUserId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'digest_reports_nonsynth', md5(coalesce(string_agg("Id"::text || '|' || "Status"::text, ',' ORDER BY "Id"), ''))
           FROM report_submissions WHERE "SubmitterId" NOT IN (SELECT "Id" FROM synth)
UNION ALL SELECT 'digest_attendance_nonsynth', md5(coalesce(string_agg("Id"::text || '|' || "Status"::text, ',' ORDER BY "Id"), ''))
           FROM attendance_incidents WHERE "SubjectUserId" NOT IN (SELECT "Id" FROM synth)
ORDER BY 1;
