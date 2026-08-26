#!/usr/bin/env python3
"""إنشاء بيانات اصطناعيّة على RC حصرًا — نطاق @p123.rc.test وبادئة RC-P123-.
لا يمسّ أيّ صفّ قائم. كلّ ما يُنشأ هنا يُحذف في مرحلة التنظيف."""
import base64, hashlib, os, subprocess, uuid, json

PW = "RcP123#Synthetic!2026"

def identity_v3_hash(password, iterations=100000):
    salt = os.urandom(16)
    subkey = hashlib.pbkdf2_hmac("sha512", password.encode("utf-8"), salt, iterations, 32)
    out = bytearray()
    out.append(0x01)
    out += (2).to_bytes(4, "big")           # PRF = HMACSHA512
    out += iterations.to_bytes(4, "big")
    out += (16).to_bytes(4, "big")          # salt length
    out += salt
    out += subkey
    return base64.b64encode(bytes(out)).decode()

def q(s):
    return "'" + s.replace("'", "''") + "'"

USERS = [
    ("rc-admin",   "Admin",      "RC-P123 مدير نظام اصطناعيّ"),
    ("rc-hr",      "HR",         "RC-P123 موارد بشريّة اصطناعيّ"),
    ("rc-mgr",     "Manager",    "RC-P123 مدير اصطناعيّ"),
    ("rc-emp",     "Employee",   "RC-P123 موظّف اصطناعيّ"),
    ("rc-other",   "Employee",   "RC-P123 موظّف آخر اصطناعيّ"),
]

stmts = ["BEGIN;"]
ids = {}
for local, role, full in USERS:
    uid = str(uuid.uuid4())
    ids[local] = uid
    email = f"{local}@p123.rc.test"
    h = identity_v3_hash(PW)
    stmts.append(
        f"""INSERT INTO "AspNetUsers"
("Id","FullName","IsActive","CreatedAtUtc","UserName","NormalizedUserName","Email","NormalizedEmail",
 "EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed",
 "TwoFactorEnabled","LockoutEnabled","AccessFailedCount","BypassTeamLeaderApproval")
VALUES ({q(uid)}::uuid,{q(full)},true,now(),{q(email)},{q(email.upper())},{q(email)},{q(email.upper())},
 true,{q(h)},{q(str(uuid.uuid4()))},{q(str(uuid.uuid4()))},false,false,true,0,false);"""
    )
    stmts.append(
        f"""INSERT INTO "AspNetUserRoles" ("UserId","RoleId")
SELECT {q(uid)}::uuid, r."Id" FROM "AspNetRoles" r WHERE r."Name"={q(role)};"""
    )
stmts.append("COMMIT;")

open("/tmp/p123-rc/seed_synth.sql", "w", encoding="utf-8").write("\n".join(stmts))
open("/tmp/p123-rc/synth-ids.json", "w", encoding="utf-8").write(json.dumps({"password": PW, "ids": ids}, ensure_ascii=False, indent=2))
print("SQL written; users:", json.dumps(ids, indent=2))
