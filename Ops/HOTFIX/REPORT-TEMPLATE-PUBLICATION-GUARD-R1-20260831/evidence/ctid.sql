\pset border 2
\pset footer off
\echo '=== PHYSICAL ROW ORDER (ctid) AS RETURNED WITHOUT ORDER BY ==='
SELECT t."Title" AS title, v.ctid AS ctid, v."VersionNumber" AS vnum, v."IsPublished" AS pub,
       EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
               AND f."FieldType"='ProjectRepeatableSection'
               AND replace(f."ConfigJson"::text,' ','') LIKE '%"catalogDomain":"%') AS is_v4
FROM report_template_versions v JOIN report_templates t ON t."Id"=v."ReportTemplateId"
WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي','تقرير كاتب المحتوى الأسبوعي');

\echo '=== NO-ORDER-BY SCAN ORDER (first v4 per template) ==='
WITH scan AS (
  SELECT t."Title" AS title, v."VersionNumber" AS vnum, row_number() OVER () AS rn,
         EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
                 AND f."FieldType"='ProjectRepeatableSection'
                 AND replace(f."ConfigJson"::text,' ','') LIKE '%"catalogDomain":"%') AS is_v4
  FROM report_template_versions v JOIN report_templates t ON t."Id"=v."ReportTemplateId"
  WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي','تقرير كاتب المحتوى الأسبوعي')
)
SELECT title, min(rn) FILTER (WHERE is_v4) AS first_v4_rn,
       (array_agg(vnum ORDER BY rn) FILTER (WHERE is_v4))[1] AS first_v4_vnum,
       array_agg(vnum ORDER BY rn) AS scan_order
FROM scan GROUP BY title ORDER BY title;
