\pset pager off
SELECT 'COLS_APPROVAL_STEPS=' || string_agg(column_name,' ' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='approval_steps';
SELECT 'COLS_AUDIT_LOGS=' || string_agg(column_name,' ' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='audit_logs';
SELECT '---TEAMS_OF_INTEREST---';
SELECT 'T=' || left(t."Id"::text,8)
  || ' | name=' || t."NameAr"
  || ' | active=' || t."IsActive"::text
  || ' | leader=' || left(coalesce(t."TeamLeaderId"::text,'NULL'),8)
  || ' | leader_act=' || coalesce((SELECT u."IsActive"::text FROM "AspNetUsers" u WHERE u."Id"=t."TeamLeaderId"),'-')
  || ' | leader_name=' || coalesce((SELECT left(u."FullName",3)||'***' FROM "AspNetUsers" u WHERE u."Id"=t."TeamLeaderId"),'-')
  || ' | members_active=' || (SELECT count(*) FROM "AspNetUsers" u WHERE u."TeamId"=t."Id" AND u."IsActive")
  || ' | members_inactive=' || (SELECT count(*) FROM "AspNetUsers" u WHERE u."TeamId"=t."Id" AND NOT u."IsActive")
FROM teams t
WHERE left(t."Id"::text,8) IN ('a7ef8832','698c5e0e','87fbcea9','34b4928d','87611efc')
ORDER BY t."NameAr";
