#!/bin/bash
# لقطة مصالحة بيانات RC — للقراءة فقط، لا تكتب صفًّا واحدًا.
# الاستعمال: rc-snapshot.sh "<عنوان اللقطة>"
# تفصل الصفوف الاصطناعيّة (نطاق p123.rc.test / بادئة RC-P123-) عن الحقيقيّة،
# وتبصم الحقيقيّة بـmd5 حتّى يُثبَت أنّ جولة الاختبار لم تمسّها.
set -euo pipefail
TITLE="${1:-RC SNAPSHOT}"
echo "=== ${TITLE} ($(date -u +%Y-%m-%dT%H:%M:%SZ) UTC) ==="
sudo -u postgres psql -d reporting_rc -t -A -F'|' <<'SQL'
SELECT 'users_total', count(*) FROM "AspNetUsers";
SELECT 'users_synth_p123rc', count(*) FROM "AspNetUsers" WHERE "Email" LIKE '%@p123.rc.test';
SELECT 'users_nonsynth', count(*) FROM "AspNetUsers" WHERE "Email" NOT LIKE '%@p123.rc.test';
SELECT 'userclaims_perm_total', count(*) FROM "AspNetUserClaims" WHERE "ClaimType"='perm';
SELECT 'userclaims_perm_nonsynth', count(*) FROM "AspNetUserClaims" c
  JOIN "AspNetUsers" u ON u."Id"=c."UserId"
  WHERE c."ClaimType"='perm' AND u."Email" NOT LIKE '%@p123.rc.test';
SELECT 'departments_total', count(*) FROM departments;
SELECT 'departments_nonsynth', count(*) FROM departments WHERE coalesce("NameAr",'') NOT LIKE 'RC-P123-%' AND coalesce("Code",'') NOT LIKE 'RC-P123-%';
SELECT 'teams_total', count(*) FROM teams;
SELECT 'teams_nonsynth', count(*) FROM teams WHERE coalesce("NameAr",'') NOT LIKE 'RC-P123-%';
SELECT 'attendance_incidents_total', count(*) FROM attendance_incidents;
SELECT 'attendance_incidents_synth', count(*) FROM attendance_incidents i
  JOIN "AspNetUsers" u ON u."Id"=i."SubjectUserId" WHERE u."Email" LIKE '%@p123.rc.test';
SELECT 'report_submissions_total', count(*) FROM report_submissions;
SELECT 'report_templates_total', count(*) FROM report_templates;
SELECT 'migrations', count(*) FROM "__EFMigrationsHistory";
SELECT 'md5_users_nonsynth', coalesce(md5(string_agg("Id"::text||'|'||coalesce("Email",'')||'|'||coalesce("FullName",'')||'|'||"IsActive"::text, ',' ORDER BY "Id")),'EMPTY')
  FROM "AspNetUsers" WHERE "Email" NOT LIKE '%@p123.rc.test';
SELECT 'md5_departments_nonsynth', coalesce(md5(string_agg("Id"::text||'|'||coalesce("NameAr",''), ',' ORDER BY "Id")),'EMPTY')
  FROM departments WHERE coalesce("NameAr",'') NOT LIKE 'RC-P123-%' AND coalesce("Code",'') NOT LIKE 'RC-P123-%';
SELECT 'md5_teams_nonsynth', coalesce(md5(string_agg("Id"::text||'|'||coalesce("NameAr",''), ',' ORDER BY "Id")),'EMPTY')
  FROM teams WHERE coalesce("NameAr",'') NOT LIKE 'RC-P123-%';
SELECT 'md5_submissions', coalesce(md5(string_agg("Id"::text||'|'||"Status"::text, ',' ORDER BY "Id")),'EMPTY') FROM report_submissions;
SELECT 'md5_perm_claims_nonsynth', coalesce(md5(string_agg(c."UserId"::text||'|'||coalesce(c."ClaimValue",''), ',' ORDER BY c."Id")),'EMPTY')
  FROM "AspNetUserClaims" c JOIN "AspNetUsers" u ON u."Id"=c."UserId"
  WHERE c."ClaimType"='perm' AND u."Email" NOT LIKE '%@p123.rc.test';
SQL
