\pset pager off
SELECT 'FP_SUBMISSIONS_TOTAL=' || count(*) FROM report_submissions;
SELECT 'FP_APPROVAL_STEPS_TOTAL=' || count(*) FROM approval_steps;
SELECT 'FP_STUCK_11_MD5=' || md5(string_agg(x,'|' ORDER BY x)) FROM (
  SELECT s."Id"::text || ':' || s."Status" || ':' || coalesce(s."CurrentApproverId"::text,'NULL') || ':' || s."IsDeleted"::text AS x
  FROM report_submissions s
  WHERE s."Status"='Submitted' AND (
    s."CurrentApproverId" IS NULL
    OR NOT EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId")
    OR EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId" AND u."IsActive"=false))) q;
SELECT 'FP_STEPS_OF_11_MD5=' || md5(string_agg(y,'|' ORDER BY y)) FROM (
  SELECT a."Id"::text || ':' || a."Level" || ':' || coalesce(a."ApproverId"::text,'NULL') || ':' || a."Status" AS y
  FROM approval_steps a WHERE a."ReportSubmissionId" IN (
    SELECT s."Id" FROM report_submissions s WHERE s."Status"='Submitted' AND (
      s."CurrentApproverId" IS NULL
      OR NOT EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId")
      OR EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=s."CurrentApproverId" AND u."IsActive"=false)))) q2;
SELECT 'FP_TEAMS_MD5=' || md5(string_agg(t."Id"::text || ':' || coalesce(t."TeamLeaderId"::text,'NULL') || ':' || t."IsActive"::text, '|' ORDER BY t."Id"::text)) FROM teams t;
SELECT 'FP_USERS_ACTIVE=' || count(*) FILTER (WHERE "IsActive") || ' FP_USERS_INACTIVE=' || count(*) FILTER (WHERE NOT "IsActive") FROM "AspNetUsers";
SELECT 'FP_AUDIT_ROWS=' || count(*) FROM audit_logs;
SELECT 'FP_TAKEN_AT_UTC=' || to_char(now() AT TIME ZONE 'UTC','YYYY-MM-DD HH24:MI:SS');
