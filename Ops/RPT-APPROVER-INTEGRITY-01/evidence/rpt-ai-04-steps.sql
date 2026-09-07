\pset pager off
SELECT '---STEPS_OF_THE_SIX---';
SELECT 'STEP sub=' || left(st."ReportSubmissionId"::text,8)
  || ' | lvl=' || st."Level"::text
  || ' | status=' || st."Status"
  || ' | appr=' || left(coalesce(st."ApproverId"::text,'NULL'),8)
  || ' | appr_exists=' || (EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=st."ApproverId"))::text
  || ' | appr_act=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=st."ApproverId"),'-')
  || ' | decided=' || coalesce(to_char(st."DecidedAtUtc",'YYYY-MM-DD'),'~')
FROM approval_steps st
WHERE st."ReportSubmissionId" IN (SELECT "Id" FROM report_submissions WHERE left("Id"::text,8) IN ('52455d79','ea04ce56','65179e5c','902bb0e9','e72a7916','a167a5bf'))
ORDER BY st."ReportSubmissionId", st."Level";

SELECT '---ORPHAN_6a00ccc0_ANY_TRACE---';
SELECT 'A=' || to_char(a."CreatedAtUtc",'YYYY-MM-DD HH24:MI') || ' | ' || a."Action" || ' | ent=' || left(coalesce(a."EntityId"::text,'~'),8) || ' | actor=' || left(coalesce(a."ActorId"::text,'~'),8)
FROM audit_logs a
WHERE a."EntityId"::text LIKE '6a00ccc0%' OR a."DataJson" LIKE '%6a00ccc0%'
ORDER BY a."CreatedAtUtc" LIMIT 20;

SELECT '---DEACTIVATION_AUDIT_FOR_356916a7_8be4ba0c---';
SELECT 'A=' || to_char(a."CreatedAtUtc",'YYYY-MM-DD HH24:MI') || ' | ' || a."Action" || ' | ent=' || left(coalesce(a."EntityId"::text,'~'),8) || ' | actor=' || left(coalesce(a."ActorId"::text,'~'),8)
FROM audit_logs a
WHERE a."EntityId"::text LIKE '356916a7%' OR a."EntityId"::text LIKE '8be4ba0c%' OR a."EntityId"::text LIKE '87fbcea9%' OR a."EntityId"::text LIKE 'aedbfba9%' OR a."EntityId"::text LIKE 'f7c2f8bb%'
ORDER BY a."CreatedAtUtc" LIMIT 40;
