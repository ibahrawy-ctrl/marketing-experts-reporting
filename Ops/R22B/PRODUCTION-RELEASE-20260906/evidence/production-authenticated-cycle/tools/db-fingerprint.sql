\pset pager off
SELECT 'SUBS_TOTAL='||count(*) FROM report_submissions;
SELECT 'STATUS_DIST='||string_agg("Status"||':'||c, ' ' ORDER BY "Status") FROM (SELECT "Status", count(*) c FROM report_submissions GROUP BY "Status") x;
SELECT 'OTHER_SUBS_FP='||md5(string_agg(s."Id"::text||'|'||s."Status"||'|'||coalesce(s."UpdatedAtUtc"::text,'~')||'|'||coalesce(s."CurrentApproverId"::text,'~'), ',' ORDER BY s."Id")) FROM report_submissions s WHERE s."Id"<>'96613974-ed0d-41bc-8cae-78c4511a4004';
SELECT 'USERS_FP='||count(*)||'/'||md5(string_agg(u."Id"||'|'||u."IsActive"::text||'|'||coalesce(u."LockoutEnd"::text,'~'), ',' ORDER BY u."Id")) FROM "AspNetUsers" u;
SELECT 'OTHER_STEPS_FP='||count(*)||'/'||md5(string_agg(a."Id"::text||'|'||a."Status"||'|'||coalesce(a."DecidedAtUtc"::text,'~'), ',' ORDER BY a."Id")) FROM approval_steps a WHERE a."ReportSubmissionId"<>'96613974-ed0d-41bc-8cae-78c4511a4004';
SELECT 'LEDGER='||count(*) FROM employee_balance_ledger;
SELECT 'LEDGER_FP='||md5(coalesce(string_agg(l."Id"::text, ',' ORDER BY l."Id"),'~')) FROM employee_balance_ledger l;
SELECT 'KPI_EVAL='||count(*) FROM kpi_evaluations;
SELECT 'PROJECTS='||count(*)||' CLIENTS='||(SELECT count(*) FROM clients) FROM projects;
SELECT 'TPL_VERSIONS='||count(*)||'/'||count(*) FILTER (WHERE "IsPublished") FROM report_template_versions;
SELECT 'NOW_UTC='||to_char(now() AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS"Z"');
