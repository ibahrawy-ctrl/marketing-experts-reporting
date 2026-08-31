-- R22B — تصدير مسودّة واحدة كاملة بصيغة JSON (قراءة فقط).
-- الاستعمال: psql -d <db> -v sid='<submission-guid>' -At -f dump-draft.sql
SELECT jsonb_pretty(jsonb_build_object(
  'database', current_database(),
  'capturedAtUtc', to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
  'submission', (SELECT to_jsonb(x) FROM (
      SELECT s."Id", s."Status", s."PeriodKey", s."SubmitterId", s."ProjectId",
             s."ReportTemplateVersionId", s."CreatedAtUtc", s."UpdatedAtUtc",
             s."SubmittedAtUtc", s."ClosedAtUtc", s."IsDeleted",
             t."Id" AS "TemplateId", t."Title" AS "TemplateTitle", v."VersionNumber"
      FROM report_submissions s
      JOIN report_template_versions v ON v."Id" = s."ReportTemplateVersionId"
      JOIN report_templates t ON t."Id" = v."ReportTemplateId"
      WHERE s."Id" = :'sid') x),
  'values', (SELECT coalesce(jsonb_agg(to_jsonb(y) ORDER BY y."Order"), '[]'::jsonb) FROM (
      SELECT fv."Id", fv."TemplateFieldId", tf."Key", tf."Label", tf."FieldType", tf."Order",
             fv."ValueText", fv."ValueNumber", fv."ValueDate", fv."ValueBool", fv."ValueJson"
      FROM submission_field_values fv
      JOIN template_fields tf ON tf."Id" = fv."TemplateFieldId"
      WHERE fv."ReportSubmissionId" = :'sid') y),
  'approvalSteps', (SELECT count(*) FROM approval_steps a WHERE a."ReportSubmissionId" = :'sid')
));
