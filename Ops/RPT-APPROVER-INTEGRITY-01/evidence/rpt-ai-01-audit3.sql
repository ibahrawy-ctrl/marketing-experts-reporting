\pset pager off
SELECT '---ENTITY_TYPES---';
SELECT coalesce(a."EntityType",'~') || ' x' || count(*) FROM audit_logs a GROUP BY a."EntityType" ORDER BY 1;
SELECT '---ORG_CHANGE_EVENTS---';
SELECT to_char(a."CreatedAtUtc",'YYYY-MM-DD HH24:MI') || ' | ' || a."Action"
  || ' | type=' || coalesce(a."EntityType",'~')
  || ' | ent=' || left(coalesce(a."EntityId"::text,'~'),8)
  || ' | actor=' || left(coalesce(a."ActorId"::text,'~'),8)
  || ' | data=' || left(coalesce(a."DataJson"::text,'-'),260)
FROM audit_logs a
WHERE a."Action" IN ('user.org.changed','team.updated','team.deleted','team.moved','team.additional_member.added','team.additional_member.removed','leave_request.team_leader_stuck_remediated')
ORDER BY a."CreatedAtUtc" DESC LIMIT 30;
SELECT '---STUCK_SUBMISSION_AUDIT_TRAIL---';
SELECT to_char(a."CreatedAtUtc",'YYYY-MM-DD HH24:MI') || ' | ' || a."Action" || ' | ent=' || left(coalesce(a."EntityId"::text,'~'),8) || ' | actor=' || left(coalesce(a."ActorId"::text,'~'),8)
FROM audit_logs a
WHERE left(coalesce(a."EntityId"::text,''),8) IN ('52455d79','ea04ce56','65179e5c','902bb0e9','e72a7916','a167a5bf','416de42c','4c883e36','2ada052d','e840df55','c25673e1')
ORDER BY a."CreatedAtUtc";
SELECT '---NOTIFICATIONS_TO_INACTIVE_APPROVERS---';
SELECT 'notif_to_8be4ba0c=' || count(*) FROM notifications n WHERE left(coalesce(n."UserId"::text,''),8)='8be4ba0c';
SELECT 'notif_to_356916a7=' || count(*) FROM notifications n WHERE left(coalesce(n."UserId"::text,''),8)='356916a7';
