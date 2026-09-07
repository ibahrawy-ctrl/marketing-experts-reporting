\pset pager off
SELECT 'ACTIVE_GM=' || string_agg(left(u."Id"::text,8) || '(' || left(u."FullName",3) || '***)', ' ' ORDER BY u."Id"::text)
FROM "AspNetUsers" u
JOIN "AspNetUserRoles" ur ON ur."UserId"=u."Id"
JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
WHERE u."IsActive" AND r."Name"='GeneralManager';
SELECT 'ACTIVE_ADMIN_CEO=' || string_agg(left(u."Id"::text,8) || '/' || r."Name", ' ' ORDER BY u."Id"::text)
FROM "AspNetUsers" u
JOIN "AspNetUserRoles" ur ON ur."UserId"=u."Id"
JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
WHERE u."IsActive" AND r."Name" IN ('Admin','CEO');
SELECT 'S=' || left(s."Id"::text,8) || ' tmpl=' || left(coalesce(v."ReportTemplateId"::text,'~'),8) || ' period=' || s."PeriodType" || '/' || s."PeriodKey"
  || ' proj=' || left(coalesce(s."ProjectId"::text,'NONE'),8) || ' team=' || left(coalesce(s."TeamId"::text,'NONE'),8)
  || ' submitter=' || left(s."SubmitterId"::text,8) || ' subActive=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=s."SubmitterId"),'MISSING')
  || ' subMgr=' || left(coalesce((SELECT u."ManagerId"::text FROM "AspNetUsers" u WHERE u."Id"=s."SubmitterId"),'NULL'),8)
  || ' submitted=' || to_char(s."SubmittedAtUtc",'YYYY-MM-DD')
  || ' ageDays=' || extract(day from (now() - s."SubmittedAtUtc"))::int
  || ' del=' || s."IsDeleted"::text
FROM report_submissions s
LEFT JOIN report_template_versions v ON v."Id"=s."ReportTemplateVersionId"
WHERE s."Status"='Submitted' AND (
  s."CurrentApproverId" IS NULL
  OR NOT EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId")
  OR EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId" AND u."IsActive"=false))
ORDER BY s."SubmittedAtUtc";
SELECT 'TEMPLATE_NAMES=' || string_agg(DISTINCT left(t."Id"::text,8) || ':' || t."NameAr", ' | ') FROM report_templates t;
