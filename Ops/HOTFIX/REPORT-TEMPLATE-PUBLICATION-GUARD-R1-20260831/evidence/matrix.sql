\pset border 2
\pset footer off

-- ===== (1) هويّة القوالب الخمسة المعنيّة + كلّ إصداراتها =====
\echo '=== SECTION 1: VERSIONS MATRIX ==='
SELECT
  left(t."Id"::text,8)                          AS tpl,
  t."Title"                                     AS title,
  t."Status"                                    AS tpl_status,
  left(v."Id"::text,8)                          AS ver,
  v."VersionNumber"                             AS vnum,
  v."IsPublished"                               AS pub,
  to_char(v."CreatedAtUtc",'MM-DD HH24:MI:SS')  AS created,
  to_char(v."PublishedAtUtc",'MM-DD HH24:MI:SS') AS published,
  to_char(v."UpdatedAtUtc",'MM-DD HH24:MI:SS')  AS updated,
  (SELECT count(*) FROM template_fields f WHERE f."ReportTemplateVersionId" = v."Id") AS fields,
  -- محدِّدات الحارس الثلاثة، محسوبة بنفس منطق TemplateSeeder
  EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
          AND f."FieldType"='ProjectRepeatableSection'
          AND replace(f."ConfigJson"::text,' ','') LIKE '%"key":"delayed"%')      AS is_projfirst,
  EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
          AND f."FieldType"='ProjectRepeatableSection'
          AND replace(f."ConfigJson"::text,' ','') LIKE '%"type":"Select"%')      AS is_v3,
  EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
          AND f."FieldType"='ProjectRepeatableSection'
          AND replace(f."ConfigJson"::text,' ','') LIKE '%"catalogDomain":"%')    AS is_v4,
  EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
          AND replace(f."ConfigJson"::text,' ','') LIKE '%"schemaVersion":2%')    AS sv2,
  EXISTS (SELECT 1 FROM template_fields f WHERE f."ReportTemplateVersionId"=v."Id"
          AND replace(f."ConfigJson"::text,' ','') LIKE '%"workItems"%')          AS workitems
FROM report_templates t
JOIN report_template_versions v ON v."ReportTemplateId" = t."Id"
WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي',
                    'تقرير كاتب المحتوى الأسبوعي','تقرير متابعة مقالات SEO الأسبوعي')
ORDER BY t."Title", v."VersionNumber";

-- ===== (2) الفائز حسب عقد التشغيل: أعلى VersionNumber بين المنشورة =====
\echo '=== SECTION 2: RUNTIME WINNER (highest published VersionNumber) ==='
SELECT t."Title" AS title,
       (SELECT v2."VersionNumber" FROM report_template_versions v2
        WHERE v2."ReportTemplateId"=t."Id" AND v2."IsPublished"
        ORDER BY v2."VersionNumber" DESC LIMIT 1) AS runtime_winner_vnum,
       (SELECT count(*) FROM report_template_versions v3
        WHERE v3."ReportTemplateId"=t."Id" AND v3."IsPublished") AS published_count,
       (SELECT max(v4."VersionNumber") FROM report_template_versions v4
        WHERE v4."ReportTemplateId"=t."Id") AS max_vnum
FROM report_templates t
WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي',
                    'تقرير كاتب المحتوى الأسبوعي','تقرير متابعة مقالات SEO الأسبوعي')
ORDER BY t."Title";

-- ===== (3) التسليمات المرتبطة بكل إصدار حسب الحالة والفترة =====
\echo '=== SECTION 3: SUBMISSIONS PER VERSION BY STATUS ==='
SELECT t."Title" AS title, v."VersionNumber" AS vnum, v."IsPublished" AS pub,
       s."Status" AS status, count(*) AS cnt,
       min(s."PeriodKey") AS period_min, max(s."PeriodKey") AS period_max
FROM report_templates t
JOIN report_template_versions v ON v."ReportTemplateId"=t."Id"
JOIN report_submissions s ON s."ReportTemplateVersionId"=v."Id"
WHERE t."Title" IN ('تقرير فريق الفيديو','تقرير فريق التصميم','تقرير المديرشن الأسبوعي',
                    'تقرير كاتب المحتوى الأسبوعي','تقرير متابعة مقالات SEO الأسبوعي')
GROUP BY t."Title", v."VersionNumber", v."IsPublished", s."Status"
ORDER BY t."Title", v."VersionNumber", s."Status";

-- ===== (4) إجماليّات عامّة =====
\echo '=== SECTION 4: GLOBAL COUNTS ==='
SELECT count(*) AS total_versions,
       count(*) FILTER (WHERE "IsPublished") AS published_versions
FROM report_template_versions;

\echo '=== SECTION 5: TEMPLATES WITH >1 PUBLISHED VERSION ==='
SELECT left(t."Id"::text,8) AS tpl, t."Title" AS title, count(*) AS pub_cnt
FROM report_templates t JOIN report_template_versions v ON v."ReportTemplateId"=t."Id"
WHERE v."IsPublished" GROUP BY t."Id", t."Title" HAVING count(*)>1 ORDER BY t."Title";

\echo '=== SECTION 6: TEMPLATES WITH ZERO PUBLISHED VERSION ==='
SELECT left(t."Id"::text,8) AS tpl, t."Title" AS title, t."Status" AS st
FROM report_templates t
WHERE NOT EXISTS (SELECT 1 FROM report_template_versions v
                  WHERE v."ReportTemplateId"=t."Id" AND v."IsPublished")
ORDER BY t."Title";
