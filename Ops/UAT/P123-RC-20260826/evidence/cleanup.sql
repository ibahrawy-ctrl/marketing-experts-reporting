-- تنظيف المرحلة 11 — يحذف ما أنشأته هذه الجولة حصرًا (نطاق p123.rc.test وبادئة RC-P123-).
-- لا يمسّ أيّ صفّ غير اصطناعيّ، ولا يحذف قيود التدقيق (أدلّة لازمة).
BEGIN;

CREATE TEMP TABLE synth_users AS
SELECT "Id" FROM "AspNetUsers" WHERE "Email" LIKE '%@p123.rc.test';

CREATE TEMP TABLE synth_incidents AS
SELECT i."Id" FROM attendance_incidents i
WHERE i."SubjectUserId" IN (SELECT "Id" FROM synth_users)
   OR i."ReportedByUserId" IN (SELECT "Id" FROM synth_users);

DELETE FROM attendance_incident_events WHERE "IncidentId" IN (SELECT "Id" FROM synth_incidents);
DELETE FROM attendance_incident_attachments WHERE "IncidentId" IN (SELECT "Id" FROM synth_incidents);
DELETE FROM attendance_incidents WHERE "Id" IN (SELECT "Id" FROM synth_incidents);

DELETE FROM employee_checklist_items WHERE "SubjectUserId" IN (SELECT "Id" FROM synth_users);

UPDATE "AspNetUsers" SET "DepartmentId"=NULL, "TeamId"=NULL, "ManagerId"=NULL
WHERE "Id" IN (SELECT "Id" FROM synth_users);

UPDATE departments SET "ManagerId"=NULL
WHERE "ManagerId" IN (SELECT "Id" FROM synth_users);
UPDATE teams SET "TeamLeaderId"=NULL
WHERE "TeamLeaderId" IN (SELECT "Id" FROM synth_users);

DELETE FROM teams WHERE "NameAr" LIKE 'RC-P123-%';
DELETE FROM departments WHERE "NameAr" LIKE 'RC-P123-%' OR "Code" LIKE 'RC-P123-%';

DELETE FROM "AspNetUserClaims" WHERE "UserId" IN (SELECT "Id" FROM synth_users);
DELETE FROM "AspNetUserRoles"  WHERE "UserId" IN (SELECT "Id" FROM synth_users);
DELETE FROM "AspNetUserTokens" WHERE "UserId" IN (SELECT "Id" FROM synth_users);
DELETE FROM "AspNetUserLogins" WHERE "UserId" IN (SELECT "Id" FROM synth_users);
DELETE FROM "AspNetUsers"      WHERE "Id"     IN (SELECT "Id" FROM synth_users);

COMMIT;
