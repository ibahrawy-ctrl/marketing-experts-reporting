-- R22B — الجرد النهائيّ (قراءة فقط): كلّ قالب، وهل فيه قسم مشاريع متكرّر، وحالته.
SELECT current_database() AS db;

\echo '--- COUNTS ---'
SELECT count(*) AS templates_total,
       count(*) FILTER (WHERE "IsActive") AS templates_active
FROM report_templates;

\echo '--- TEMPLATES WITH ProjectRepeatableSection (effective published version) ---'
WITH eff AS (
  SELECT DISTINCT ON (v."ReportTemplateId")
         v."ReportTemplateId", v."Id" AS vid, v."VersionNumber"
  FROM report_template_versions v
  WHERE v."IsPublished"
  ORDER BY v."ReportTemplateId", v."VersionNumber" DESC
)
SELECT t."Title", t."IsActive" AS act, eff."VersionNumber" AS v,
       (f."ConfigJson"::jsonb ->> 'schemaVersion') AS sv,
       (f."ConfigJson"::jsonb -> 'workItems' IS NOT NULL) AS wi,
       jsonb_array_length(coalesce(f."ConfigJson"::jsonb -> 'fields', '[]'::jsonb)) AS proj_fields,
       md5(f."ConfigJson"::text) AS config_md5
FROM eff
JOIN report_templates t ON t."Id" = eff."ReportTemplateId"
JOIN template_fields f ON f."ReportTemplateVersionId" = eff.vid
WHERE f."FieldType" = 'ProjectRepeatableSection'
ORDER BY t."Title";

\echo '--- ACTIVE TEMPLATES WITH NO ProjectRepeatableSection IN ANY VERSION ---'
SELECT count(*) AS active_no_project_section
FROM report_templates t
WHERE t."IsActive"
  AND NOT EXISTS (
    SELECT 1 FROM report_template_versions v
    JOIN template_fields f ON f."ReportTemplateVersionId" = v."Id"
    WHERE v."ReportTemplateId" = t."Id" AND f."FieldType" = 'ProjectRepeatableSection');

\echo '--- INACTIVE TEMPLATES THAT DO HAVE A ProjectRepeatableSection SOMEWHERE ---'
SELECT t."Title"
FROM report_templates t
WHERE NOT t."IsActive"
  AND EXISTS (
    SELECT 1 FROM report_template_versions v
    JOIN template_fields f ON f."ReportTemplateVersionId" = v."Id"
    WHERE v."ReportTemplateId" = t."Id" AND f."FieldType" = 'ProjectRepeatableSection')
ORDER BY t."Title";
