BEGIN;
INSERT INTO "AspNetUsers"
("Id","FullName","IsActive","CreatedAtUtc","UserName","NormalizedUserName","Email","NormalizedEmail",
 "EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed",
 "TwoFactorEnabled","LockoutEnabled","AccessFailedCount","BypassTeamLeaderApproval")
VALUES ('973c0925-e54b-428a-a994-b318159b2f02'::uuid,'RC-P123 مدير نظام اصطناعيّ',true,now(),'rc-admin@p123.rc.test','RC-ADMIN@P123.RC.TEST','rc-admin@p123.rc.test','RC-ADMIN@P123.RC.TEST',
 true,'AQAAAAIAAYagAAAAEKkLlBIT8tYtnYri3infk1cI23Lrga0sd1jfd/pdM51wpAyGojdzwuLBt7CMpid/gg==','c56ef571-db19-4d60-a6ad-7a69dd634082','d78f6d1f-375b-43ac-ac8d-8d11e9ae4425',false,false,true,0,false);
INSERT INTO "AspNetUserRoles" ("UserId","RoleId")
SELECT '973c0925-e54b-428a-a994-b318159b2f02'::uuid, r."Id" FROM "AspNetRoles" r WHERE r."Name"='Admin';
INSERT INTO "AspNetUsers"
("Id","FullName","IsActive","CreatedAtUtc","UserName","NormalizedUserName","Email","NormalizedEmail",
 "EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed",
 "TwoFactorEnabled","LockoutEnabled","AccessFailedCount","BypassTeamLeaderApproval")
VALUES ('7f0e7894-a5f9-4884-b999-9cbb4a0f1403'::uuid,'RC-P123 موارد بشريّة اصطناعيّ',true,now(),'rc-hr@p123.rc.test','RC-HR@P123.RC.TEST','rc-hr@p123.rc.test','RC-HR@P123.RC.TEST',
 true,'AQAAAAIAAYagAAAAEEwDO9FHL32wkievYUXlR1X/RvPMjbcTr29gNVQMIbHNC3juWPgdHheny8diL2ay4w==','06cdae15-579c-4dc1-b83c-abc573d715ce','c3282d44-60eb-4926-8399-36e84918f05d',false,false,true,0,false);
INSERT INTO "AspNetUserRoles" ("UserId","RoleId")
SELECT '7f0e7894-a5f9-4884-b999-9cbb4a0f1403'::uuid, r."Id" FROM "AspNetRoles" r WHERE r."Name"='HR';
INSERT INTO "AspNetUsers"
("Id","FullName","IsActive","CreatedAtUtc","UserName","NormalizedUserName","Email","NormalizedEmail",
 "EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed",
 "TwoFactorEnabled","LockoutEnabled","AccessFailedCount","BypassTeamLeaderApproval")
VALUES ('7cd90a3c-14a5-4167-8748-b156fbb586db'::uuid,'RC-P123 مدير اصطناعيّ',true,now(),'rc-mgr@p123.rc.test','RC-MGR@P123.RC.TEST','rc-mgr@p123.rc.test','RC-MGR@P123.RC.TEST',
 true,'AQAAAAIAAYagAAAAEBSjgTjSBcm/hX/gkYbiq9lawYhP4FMy+bwcwPmLaMB2GGpG1bPHQJZD2GV84JKayw==','bb66381a-2839-42f1-b701-b8da8b0180ea','19b6de03-a92f-488a-b80f-7580e19ef812',false,false,true,0,false);
INSERT INTO "AspNetUserRoles" ("UserId","RoleId")
SELECT '7cd90a3c-14a5-4167-8748-b156fbb586db'::uuid, r."Id" FROM "AspNetRoles" r WHERE r."Name"='Manager';
INSERT INTO "AspNetUsers"
("Id","FullName","IsActive","CreatedAtUtc","UserName","NormalizedUserName","Email","NormalizedEmail",
 "EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed",
 "TwoFactorEnabled","LockoutEnabled","AccessFailedCount","BypassTeamLeaderApproval")
VALUES ('b334d9a5-73fa-41a1-b2a3-0b017a9e97d0'::uuid,'RC-P123 موظّف اصطناعيّ',true,now(),'rc-emp@p123.rc.test','RC-EMP@P123.RC.TEST','rc-emp@p123.rc.test','RC-EMP@P123.RC.TEST',
 true,'AQAAAAIAAYagAAAAED5tI4rIQX86Ib161j1GJwgWREFRFQDL0RsgEC7evFvhSNUmr9VnVu6BgCK8ZiIlpQ==','311c7182-cdf9-42e6-b6a2-2147ce126959','6234e386-a37d-493f-add2-018c931c1239',false,false,true,0,false);
INSERT INTO "AspNetUserRoles" ("UserId","RoleId")
SELECT 'b334d9a5-73fa-41a1-b2a3-0b017a9e97d0'::uuid, r."Id" FROM "AspNetRoles" r WHERE r."Name"='Employee';
INSERT INTO "AspNetUsers"
("Id","FullName","IsActive","CreatedAtUtc","UserName","NormalizedUserName","Email","NormalizedEmail",
 "EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed",
 "TwoFactorEnabled","LockoutEnabled","AccessFailedCount","BypassTeamLeaderApproval")
VALUES ('ad71c008-78e4-4758-a54d-a9ffe663d21d'::uuid,'RC-P123 موظّف آخر اصطناعيّ',true,now(),'rc-other@p123.rc.test','RC-OTHER@P123.RC.TEST','rc-other@p123.rc.test','RC-OTHER@P123.RC.TEST',
 true,'AQAAAAIAAYagAAAAEGj5pOu/bRjeymhR8jWzHa3DOg68nZLkHG9cK7KYNgUPTm5Mgq1SmfdMbLWxZaCxQQ==','39ce24b2-edbf-4127-9760-b0a60ae9fdda','9287c84e-bfb6-4ff3-861f-a8243daae672',false,false,true,0,false);
INSERT INTO "AspNetUserRoles" ("UserId","RoleId")
SELECT 'ad71c008-78e4-4758-a54d-a9ffe663d21d'::uuid, r."Id" FROM "AspNetRoles" r WHERE r."Name"='Employee';
COMMIT;