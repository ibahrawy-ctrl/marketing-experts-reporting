-- R22B — إثبات توافر مجالات الكتالوج (catalogDomain) المطلوبة لقوالب التحويل (قراءة فقط).
SELECT "Domain",
       count(*) FILTER (WHERE "IsActive") AS active_values,
       count(*)                           AS total_values,
       md5(string_agg("Code" || '~' || "NameAr", '|' ORDER BY "SortOrder", "Code")
           FILTER (WHERE "IsActive"))     AS active_md5
FROM execution_taxonomy_values
WHERE "Domain" IN ('video_type','edit_type','video_duration','video_status',
                   'design_type','design_status','design_tool',
                   'activity_type','interaction_result','response_time',
                   'content_type','content_goal','work_status')
GROUP BY "Domain"
ORDER BY "Domain";
