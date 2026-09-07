\pset pager off
SELECT 'STATUS_DIST=' || string_agg("Status" || ':' || c, ' ' ORDER BY "Status")
FROM (SELECT "Status", count(*) c FROM report_submissions GROUP BY "Status") x;

SELECT 'OPEN_TOTAL=' || count(*) FROM report_submissions WHERE "Status" NOT IN ('Closed','Draft');

SELECT 'NULL_APPROVER_OPEN=' || count(*) FROM report_submissions
WHERE "Status" NOT IN ('Closed','Draft') AND "CurrentApproverId" IS NULL;

SELECT 'INACTIVE_APPROVER_OPEN=' || count(*) FROM report_submissions s
JOIN "AspNetUsers" u ON u."Id" = s."CurrentApproverId"
WHERE s."Status" NOT IN ('Closed','Draft') AND u."IsActive" = false;

SELECT 'ORPHAN_APPROVER_OPEN=' || count(*) FROM report_submissions s
WHERE s."Status" NOT IN ('Closed','Draft') AND s."CurrentApproverId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id" = s."CurrentApproverId");

SELECT '---ROWS---';
SELECT left(s."Id"::text,8)
  || ' | st=' || s."Status"
  || ' | per=' || coalesce(s."PeriodType",'~') || '/' || coalesce(s."PeriodKey",'~')
  || ' | subm=' || left(coalesce(s."SubmitterId"::text,'~'),8)
  || ' | subm_act=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=s."SubmitterId"),'?')
  || ' | subm_mgr=' || left(coalesce((SELECT u."ManagerId"::text FROM "AspNetUsers" u WHERE u."Id"=s."SubmitterId"),'~'),8)
  || ' | subm_team=' || left(coalesce((SELECT u."TeamId"::text FROM "AspNetUsers" u WHERE u."Id"=s."SubmitterId"),'~'),8)
  || ' | appr=' || left(coalesce(s."CurrentApproverId"::text,'NULL'),8)
  || ' | appr_act=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId"),'-')
  || ' | subTeam=' || left(coalesce(s."TeamId"::text,'~'),8)
  || ' | dept=' || left(coalesce(s."DepartmentId"::text,'~'),8)
  || ' | proj=' || left(coalesce(s."ProjectId"::text,'~'),8)
  || ' | tplv=' || left(s."ReportTemplateVersionId"::text,8)
  || ' | subAt=' || coalesce(to_char(s."SubmittedAtUtc",'YYYY-MM-DD'),'~')
  || ' | upd=' || coalesce(to_char(s."UpdatedAtUtc",'YYYY-MM-DD'),'~')
  || ' | del=' || s."IsDeleted"::text
  || ' | ageDays=' || coalesce(extract(day from (now() - s."SubmittedAtUtc"))::int::text,'~')
FROM report_submissions s
WHERE s."Status" NOT IN ('Closed','Draft')
  AND (s."CurrentApproverId" IS NULL
       OR EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId" AND u."IsActive"=false)
       OR NOT EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId"))
ORDER BY s."SubmittedAtUtc" NULLS FIRST;
