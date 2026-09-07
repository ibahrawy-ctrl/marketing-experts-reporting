\pset pager off
SELECT '---TEAMS_OF_INTEREST---';
SELECT 'T=' || left(t."Id"::text,8)
  || ' | name=' || t."Name"
  || ' | active=' || t."IsActive"::text
  || ' | leader=' || left(coalesce(t."TeamLeaderId"::text,'NULL'),8)
  || ' | leader_act=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=t."TeamLeaderId"),'-')
  || ' | leader_name=' || coalesce((SELECT left(u."FullName",3)||'***' FROM "AspNetUsers" u WHERE u."Id"=t."TeamLeaderId"),'-')
  || ' | dept=' || left(coalesce(t."DepartmentId"::text,'~'),8)
  || ' | members_active=' || (SELECT count(*) FROM "AspNetUsers" u WHERE u."TeamId"=t."Id" AND u."IsActive")
  || ' | members_inactive=' || (SELECT count(*) FROM "AspNetUsers" u WHERE u."TeamId"=t."Id" AND NOT u."IsActive")
FROM teams t
WHERE left(t."Id"::text,8) IN ('a7ef8832','698c5e0e','87fbcea9','34b4928d','87611efc')
ORDER BY t."Name";

SELECT '---SUBMITTERS_OF_THE_SIX---';
SELECT 'U=' || left(u."Id"::text,8)
  || ' | name=' || left(u."FullName",3) || '***'
  || ' | active=' || u."IsActive"::text
  || ' | team=' || left(coalesce(u."TeamId"::text,'~'),8)
  || ' | mgr=' || left(coalesce(u."ManagerId"::text,'~'),8)
  || ' | mgr_act=' || coalesce((SELECT m."IsActive"::text FROM "AspNetUsers" m WHERE m."Id"=u."ManagerId"),'-')
  || ' | override=' || left(coalesce(u."ReportApproverOverrideUserId"::text,'~'),8)
  || ' | bypassTL=' || u."BypassTeamLeaderApproval"::text
  || ' | exit=' || coalesce(to_char(u."ExitDate",'YYYY-MM-DD'),'~')
  || ' | roles=' || coalesce((SELECT string_agg(r."Name",',') FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id"),'NONE')
FROM "AspNetUsers" u
WHERE left(u."Id"::text,8) IN ('0e30b66a','50884a37','f7c2f8bb','aedbfba9','356916a7','8be4ba0c','f4e25122')
ORDER BY u."IsActive" DESC, u."FullName";

SELECT '---ORPHAN_APPROVER_6a00ccc0_TRACES---';
SELECT 'STEP=' || left(st."Id"::text,8) || ' | sub=' || left(st."ReportSubmissionId"::text,8)
  || ' | order=' || st."StepOrder"::text || ' | status=' || st."Status"
  || ' | appr=' || left(coalesce(st."ApproverId"::text,'NULL'),8)
  || ' | exists=' || (EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=st."ApproverId"))::text
FROM approval_steps st
WHERE st."ReportSubmissionId" IN (SELECT "Id" FROM report_submissions WHERE left("Id"::text,8) IN ('52455d79','ea04ce56','65179e5c','902bb0e9','e72a7916','a167a5bf'))
ORDER BY st."ReportSubmissionId", st."StepOrder";

SELECT '---AUDIT_6a00ccc0---';
SELECT 'A=' || to_char(a."CreatedAtUtc",'YYYY-MM-DD HH24:MI') || ' | ' || a."Action" || ' | ent=' || left(coalesce(a."EntityId"::text,'~'),8)
FROM audit_logs a
WHERE a."EntityId"::text LIKE '6a00ccc0%' OR a."Details" LIKE '%6a00ccc0%'
ORDER BY a."CreatedAtUtc" LIMIT 20;
