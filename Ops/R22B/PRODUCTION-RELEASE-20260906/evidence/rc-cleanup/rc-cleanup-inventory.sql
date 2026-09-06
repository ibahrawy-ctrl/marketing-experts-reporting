-- R22B-REL §4 — جرد بيانات UAT على RC (قراءة فقط، لا كتابة إطلاقًا).
\echo == TEMP_USERS ==
SELECT u."Id", u."Email", u."IsActive"
FROM "AspNetUsers" u WHERE u."Email" LIKE 'r22brel-%@rc-uat.local' ORDER BY u."Email";

\echo == TEMP_ACTIVE_REFRESH_TOKENS ==
SELECT u."Email", count(*) AS active_tokens
FROM refresh_tokens t JOIN "AspNetUsers" u ON u."Id" = t."UserId"
WHERE u."Email" LIKE 'r22brel-%@rc-uat.local' AND t."RevokedAtUtc" IS NULL
GROUP BY u."Email" ORDER BY u."Email";

\echo == TEMP_SUBMISSIONS ==
SELECT s."Id", u."Email", s."Status", s."PeriodKey", s."IsDeleted"
FROM report_submissions s JOIN "AspNetUsers" u ON u."Id" = s."SubmitterId"
WHERE u."Email" LIKE 'r22brel-%@rc-uat.local' ORDER BY u."Email", s."CreatedAtUtc";

\echo == TEMP_PROJECTS ==
SELECT p."Id", p."Name", p."Status" FROM projects p WHERE p."Name" LIKE 'R22BREL%' ORDER BY p."Name";

\echo == TEMP_CLIENTS ==
SELECT c."Id", c."Name", c."Status" FROM clients c WHERE c."Name" LIKE 'R22BREL%' ORDER BY c."Name";

\echo == TEMP_TEAMS_DEPTS ==
SELECT 'team' AS kind, t."Id", t."NameAr", t."IsActive" FROM teams t WHERE t."NameAr" LIKE 'R22BREL%'
UNION ALL
SELECT 'dept', d."Id", d."NameAr", d."IsActive" FROM departments d WHERE d."NameAr" LIKE 'R22BREL%';

\echo == REAL_ACCOUNTS_BASELINE ==
SELECT count(*) AS total_users,
       count(*) FILTER (WHERE "IsActive") AS active_users,
       count(*) FILTER (WHERE "Email" LIKE 'r22brel-%@rc-uat.local') AS temp_users
FROM "AspNetUsers";
SELECT md5(string_agg("Id"::text || ':' || coalesce("Email",'') || ':' || "IsActive"::text, ',' ORDER BY "Id")) AS real_users_md5
FROM "AspNetUsers" WHERE "Email" NOT LIKE 'r22brel-%@rc-uat.local';

\echo == REAL_DATA_BASELINE ==
SELECT (SELECT count(*) FROM report_submissions s JOIN "AspNetUsers" u ON u."Id"=s."SubmitterId"
        WHERE u."Email" NOT LIKE 'r22brel-%@rc-uat.local') AS real_submissions,
       (SELECT count(*) FROM report_submissions s JOIN "AspNetUsers" u ON u."Id"=s."SubmitterId"
        WHERE u."Email" NOT LIKE 'r22brel-%@rc-uat.local' AND s."IsDeleted") AS real_submissions_deleted,
       (SELECT count(*) FROM projects WHERE "Name" NOT LIKE 'R22BREL%') AS real_projects,
       (SELECT count(*) FROM clients  WHERE "Name" NOT LIKE 'R22BREL%') AS real_clients;

\echo == SEO_TEMPLATE_V7_GUARD ==
SELECT v."ReportTemplateId", v."VersionNumber", v."IsPublished", md5(v::text) AS row_md5
FROM report_template_versions v
WHERE v."ReportTemplateId" = '46e100e3-b3ea-4b38-8447-3105d38b7bc7'
ORDER BY v."VersionNumber" DESC LIMIT 3;
