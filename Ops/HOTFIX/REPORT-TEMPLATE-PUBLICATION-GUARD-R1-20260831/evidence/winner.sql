\pset border 2
\pset footer off
\echo '=== HYPOTHESIS: EF Include adds ORDER BY t.Id, v.Id  =>  winner = smallest version GUID among V4 ==='
WITH v4 AS (
  SELECT t."Title" AS title, v."Id" AS vid, v."VersionNumber" AS vnum, v."IsPublished" AS pub
  FROM report_template_versions v JOIN report_templates t ON t."Id"=v."ReportTemplateId"
  WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي','تقرير كاتب المحتوى الأسبوعي')
    AND EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
                AND f."FieldType"='ProjectRepeatableSection'
                AND replace(f."ConfigJson"::text,' ','') LIKE '%"catalogDomain":"%')
)
SELECT title,
       array_agg(vnum ORDER BY vid)                AS v4_versions_by_guid,
       array_agg(left(vid::text,8) ORDER BY vid)   AS v4_guids_sorted,
       (array_agg(vnum ORDER BY vid))[1]           AS guard_picks_vnum,
       max(vnum)                                   AS newest_v4_vnum
FROM v4 GROUP BY title ORDER BY title;
