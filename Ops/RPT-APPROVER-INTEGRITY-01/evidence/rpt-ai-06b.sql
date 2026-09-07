\echo '== A) أحمد'
SELECT 'AHMED|'||u."Id"||'|active='||u."IsActive"||'|team='||coalesce(u."TeamId"::text,'-')
  ||'|dept='||coalesce(u."DepartmentId"::text,'-')||'|mgr='||coalesce(u."ManagerId"::text,'-')
  ||'|jobrole='||coalesce(u."JobRoleId"::text,'-')
FROM "AspNetUsers" u WHERE u."Id"::text LIKE 'f4e25122%';
\echo '== B) أدوار أحمد'
SELECT 'ROLE|'||r."Name" FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
WHERE ur."UserId"::text LIKE 'f4e25122%';
\echo '== C) عضويّات أحمد'
SELECT 'MEMBER|'||m."TeamId"||'|active='||m."IsActive"||'|type='||m."MembershipType"
FROM user_team_memberships m WHERE m."UserId"::text LIKE 'f4e25122%';
\echo '== D) الفريقان'
SELECT 'TEAM|'||t."Id"||'|'||t."NameAr"||'|active='||t."IsActive"||'|leader='||coalesce(t."TeamLeaderId"::text,'-')
FROM teams t WHERE t."Id"::text LIKE '698c5e0e%' OR t."Id"::text LIKE '87fbcea9%';
\echo '== E) أعمدة teams'
SELECT 'COL|'||column_name FROM information_schema.columns WHERE table_name='teams' ORDER BY ordinal_position;
\echo '== F) أعضاء الفريقين'
SELECT 'MEM|'||substr(u."TeamId"::text,1,8)||'|'||substr(u."Id"::text,1,8)||'|active='||u."IsActive"
  ||'|mgr='||coalesce(substr(u."ManagerId"::text,1,8),'-')
FROM "AspNetUsers" u WHERE u."TeamId"::text LIKE '698c5e0e%' OR u."TeamId"::text LIKE '87fbcea9%';
\echo '== G) أدوار مالكي التسليمات الست'
SELECT 'SUBROLE|'||substr(ur."UserId"::text,1,8)||'|'||r."Name"
FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
WHERE ur."UserId"::text LIKE '0e30b66a%' OR ur."UserId"::text LIKE '50884a37%'
   OR ur."UserId"::text LIKE 'f7c2f8bb%' OR ur."UserId"::text LIKE 'aedbfba9%'
   OR ur."UserId"::text LIKE '356916a7%' OR ur."UserId"::text LIKE '8be4ba0c%';
