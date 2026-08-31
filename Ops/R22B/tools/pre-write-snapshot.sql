\pset border 2
\echo '--- OPEN DRAFTS ---'
SELECT s."Id", t."Title", v."VersionNumber" AS ver, s."Status", s."PeriodKey",
       u."Email", s."UpdatedAtUtc", s."SubmittedAtUtc",
       (SELECT count(*) FROM submission_field_values fv WHERE fv."ReportSubmissionId" = s."Id") AS vals
FROM report_submissions s
JOIN report_template_versions v ON v."Id" = s."ReportTemplateVersionId"
JOIN report_templates t ON t."Id" = v."ReportTemplateId"
LEFT JOIN "AspNetUsers" u ON u."Id" = s."SubmitterId"
WHERE s."Status" = 'Draft' AND s."IsDeleted" = false
ORDER BY t."Title";

\echo '--- EMAIL COUNTERS ---'
SELECT (SELECT count(*) FROM email_notifications) AS email_notifications,
       (SELECT count(*) FROM email_outbox)       AS email_outbox;

\echo '--- NON-DRAFT SUBMISSION COUNT (HISTORICAL) ---'
SELECT count(*) AS historical FROM report_submissions WHERE "Status" <> 'Draft';
