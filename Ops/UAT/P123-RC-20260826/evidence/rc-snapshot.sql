\pset format unaligned
\pset tuples_only on
\pset fieldsep '|'
SELECT 'users_total', count(*) FROM "AspNetUsers";
SELECT 'users_synth_p123rc', count(*) FROM "AspNetUsers" WHERE "Email" LIKE '%@p123.rc.test';
SELECT 'users_nonsynth', count(*) FROM "AspNetUsers" WHERE "Email" NOT LIKE '%@p123.rc.test';
SELECT 'userclaims_perm_total', count(*) FROM "AspNetUserClaims" WHERE "ClaimType"='perm';
SELECT 'userclaims_perm_nonsynth', count(*) FROM "AspNetUserClaims" c JOIN "AspNetUsers" u ON u."Id"=c."UserId" WHERE c."ClaimType"='perm' AND u."Email" NOT LIKE '%@p123.rc.test';
SELECT 'departments_total', count(*) FROM departments;
SELECT 'departments_nonsynth', count(*) FROM departments WHERE "NameAr" NOT LIKE 'RC-P123-%' AND "Code" NOT LIKE 'RC-P123-%';
SELECT 'teams_total', count(*) FROM teams;
SELECT 'teams_nonsynth', count(*) FROM teams WHERE "NameAr" NOT LIKE 'RC-P123-%';
SELECT 'report_submissions_total', count(*) FROM report_submissions;
SELECT 'report_templates_total', count(*) FROM report_templates;
SELECT 'migrations', count(*) FROM "__EFMigrationsHistory";
SELECT 'md5_users_nonsynth', md5(string_agg("Id" || '|' || COALESCE("Email",'') || '|' || COALESCE("UserName",''), ',' ORDER BY "Id")) FROM "AspNetUsers" WHERE "Email" NOT LIKE '%@p123.rc.test';
SELECT 'md5_departments_nonsynth', md5(string_agg("Id"::text || '|' || "Code" || '|' || "NameAr", ',' ORDER BY "Id")) FROM departments WHERE "NameAr" NOT LIKE 'RC-P123-%' AND "Code" NOT LIKE 'RC-P123-%';
SELECT 'md5_teams_nonsynth', md5(string_agg("Id"::text || '|' || "DepartmentId"::text || '|' || "NameAr", ',' ORDER BY "Id")) FROM teams WHERE "NameAr" NOT LIKE 'RC-P123-%';
SELECT 'md5_submissions', md5(string_agg("Id"::text || '|' || "Status"::text || '|' || "PeriodKey", ',' ORDER BY "Id")) FROM report_submissions;
SELECT 'md5_perm_claims_nonsynth', COALESCE(md5(string_agg(c."Id"::text || '|' || c."UserId" || '|' || c."ClaimValue", ',' ORDER BY c."Id")), 'EMPTY') FROM "AspNetUserClaims" c JOIN "AspNetUsers" u ON u."Id"=c."UserId" WHERE c."ClaimType"='perm' AND u."Email" NOT LIKE '%@p123.rc.test';
