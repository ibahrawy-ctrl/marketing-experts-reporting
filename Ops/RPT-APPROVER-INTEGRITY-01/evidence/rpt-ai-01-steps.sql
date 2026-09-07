\pset pager off
SELECT '---COHORT11_STEPS---';
SELECT left(a."ReportSubmissionId"::text,8) || ' | L' || a."Level"
  || ' | appr=' || left(coalesce(a."ApproverId"::text,'NULL'),8)
  || ' | apprActive=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=a."ApproverId"),'MISSING')
  || ' | st=' || a."Status"
  || ' | decided=' || coalesce(to_char(a."DecidedAtUtc",'YYYY-MM-DD'),'~')
  || ' | created=' || to_char(a."CreatedAtUtc",'YYYY-MM-DD')
  || ' | commentLen=' || coalesce(length(a."Comment"),0)
FROM approval_steps a
WHERE left(a."ReportSubmissionId"::text,8) IN ('52455d79','416de42c','4c883e36','2ada052d','e840df55','c25673e1','ea04ce56','65179e5c','902bb0e9','e72a7916','a167a5bf')
ORDER BY a."ReportSubmissionId", a."Level", a."CreatedAtUtc";

SELECT '---RETURNED_NULL_IS_BY_DESIGN---';
SELECT 'RETURNED_TOTAL=' || count(*) || ' RETURNED_NULL_APPROVER=' || count(*) FILTER (WHERE "CurrentApproverId" IS NULL)
FROM report_submissions WHERE "Status"='Returned';
SELECT 'SUBMITTED_NOTDELETED_NULL_APPROVER=' || count(*) FROM report_submissions
WHERE "Status"='Submitted' AND "IsDeleted"=false AND "CurrentApproverId" IS NULL;
SELECT 'SUBMITTED_DELETED_NULL_APPROVER=' || count(*) FROM report_submissions
WHERE "Status"='Submitted' AND "IsDeleted"=true AND "CurrentApproverId" IS NULL;
SELECT 'DELETED_ALL_STATUSES=' || count(*) FROM report_submissions WHERE "IsDeleted"=true;
SELECT 'APPROVEDBYDM_APPROVER_NULL_OR_INACTIVE=' || count(*) FROM report_submissions s
WHERE s."Status"='ApprovedByDirectManager'
  AND (s."CurrentApproverId" IS NULL OR NOT EXISTS(SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId" AND u."IsActive"));

SELECT '---ORPHAN_6a00ccc0_FOOTPRINT---';
SELECT 'steps_with_orphan=' || count(*) FROM approval_steps a WHERE left(coalesce(a."ApproverId"::text,''),8)='6a00ccc0';
SELECT 'subs_with_orphan=' || count(*) FROM report_submissions s WHERE left(coalesce(s."CurrentApproverId"::text,''),8)='6a00ccc0';
SELECT 'users_managed_by_orphan=' || count(*) FROM "AspNetUsers" u WHERE left(coalesce(u."ManagerId"::text,''),8)='6a00ccc0';
SELECT 'teams_led_by_orphan=' || count(*) FROM teams t WHERE left(coalesce(t."TeamLeaderId"::text,''),8)='6a00ccc0';

SELECT '---MEMBERSHIPS_777_AND_NINJAS---';
SELECT left(m."TeamId"::text,8) || ' | user=' || left(m."UserId"::text,8)
  || ' | userActive=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=m."UserId"),'MISSING')
  || ' | mActive=' || m."IsActive"::text
  || ' | type=' || coalesce(m."MembershipType",'~')
  || ' | start=' || coalesce(to_char(m."StartDateUtc",'YYYY-MM-DD'),'~')
  || ' | end=' || coalesce(to_char(m."EndDateUtc",'YYYY-MM-DD'),'~')
FROM user_team_memberships m
WHERE left(m."TeamId"::text,8) IN ('698c5e0e','87fbcea9')
ORDER BY m."TeamId", m."UserId";
SELECT 'MEMBERSHIPS_TOTAL=' || count(*) FROM user_team_memberships;

SELECT '---TEAM777_MEMBERS_DIRECTORY---';
SELECT left(u."Id"::text,8) || ' | ' || left(u."FullName",3) || repeat('*', greatest(length(u."FullName")-3,0))
  || ' | active=' || u."IsActive"::text
  || ' | mgr=' || left(coalesce(u."ManagerId"::text,'NULL'),8)
  || ' | job=' || coalesce((SELECT j."NameAr" FROM job_roles j WHERE j."Id"=u."JobRoleId"),'~')
  || ' | roles=' || coalesce((SELECT string_agg(r."Name",'+') FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id"),'~')
FROM "AspNetUsers" u WHERE left(coalesce(u."TeamId"::text,''),8) IN ('698c5e0e','87fbcea9')
ORDER BY u."TeamId", u."IsActive" DESC, u."FullName";

SELECT '---ACTIVE_LEADERS_IN_DEPT_4dc72160---';
SELECT left(u."Id"::text,8) || ' | ' || left(u."FullName",3) || repeat('*', greatest(length(u."FullName")-3,0))
  || ' | active=' || u."IsActive"::text
  || ' | roles=' || coalesce((SELECT string_agg(r."Name",'+') FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id"),'~')
  || ' | job=' || coalesce((SELECT j."NameAr" FROM job_roles j WHERE j."Id"=u."JobRoleId"),'~')
  || ' | team=' || left(coalesce(u."TeamId"::text,'~'),8)
FROM "AspNetUsers" u
WHERE left(coalesce(u."DepartmentId"::text,''),8)='4dc72160' AND u."IsActive"
  AND EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id" AND r."Name" IN ('TeamLeader','Manager','GeneralManager'))
ORDER BY u."FullName";
