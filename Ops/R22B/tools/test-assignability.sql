-- R22B — قراءة فقط: كيف يصل الموظّف إلى القوالب الأربعة في هذه البيئة.
\echo '--- TEMPLATE -> JOB ROLE ---'
SELECT t."Title", t."IsActive", t."Status", j."NameAr" AS job_role, t."JobRoleId"
FROM report_templates t
LEFT JOIN job_roles j ON j."Id" = t."JobRoleId"
WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي',
                    'تقرير متابعة مقالات SEO الأسبوعي','تقرير كاتب المحتوى الأسبوعي')
ORDER BY t."Title";

\echo '--- ACTIVE ASSIGNMENTS ON THOSE TEMPLATES ---'
SELECT t."Title", a."ScopeType", a."Mode", a."IsActive", a."UserId", a."JobRoleId", a."TeamId", a."DepartmentId"
FROM report_template_assignments a
JOIN report_templates t ON t."Id" = a."ReportTemplateId"
WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي',
                    'تقرير متابعة مقالات SEO الأسبوعي','تقرير كاتب المحتوى الأسبوعي')
ORDER BY t."Title";

\echo '--- PROJECTS AVAILABLE ---'
SELECT p."Id", p."Name", c."Name" AS client, p."Status"
FROM projects p LEFT JOIN clients c ON c."Id" = p."ClientId"
ORDER BY p."Name" LIMIT 15;
