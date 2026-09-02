#!/usr/bin/env python3
# R22B CLOSURE — تنظيف بيئة UAT على TEST عبر الـAPI الرسميّة حصرًا.
#
# مبدأ حاكم: `DELETE` للمستخدم في هذا النظام **حذف صلب** ⟹ ممنوع هنا. التنظيف = تعطيل
# (`isActive=false`) عبر `PUT /api/directory/users/{id}` مع الحفاظ على بقيّة الحقول كما هي.
#
# ما يُنظَّف بالضبط:
#   (1) الحسابات الخمسة التي أنشأتها هذه الجلسة (`closure-state.json`) ⟵ تُعطَّل.
#   (2) حسابات R22C السبعة التي **أعادت** هذه الجلسة تفعيلها ⟵ تُعاد إلى حالتها السابقة
#       المسجَّلة حرفيًّا في `reactivated.json` (`wasActive`). ما كان نشطًا قبلنا يبقى نشطًا.
#
# ما لا يُمَسّ إطلاقًا: العميل والمشروعات والقوالب والتقارير (المرسَلة والمعتمَدة) وأيّ حساب
# لم تُنشئه أو تُفعّله هذه الجلسة.
#
# التشغيل: تشغيل جافّ افتراضًا. للتطبيق: R22B_APPLY=1
import json, os, sys, urllib.request, urllib.error

API = os.environ.get("R22B_API", "http://127.0.0.1:15091")
APPLY = os.environ.get("R22B_APPLY") == "1"
MODE = "APPLY" if APPLY else "DRY-RUN"


def call(method, path, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw.strip() else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, (json.loads(raw) if raw.strip() else None)
        except Exception:
            return e.code, {"raw": raw[:400]}


adm_pw = open("/tmp/r22b-uat/.admin-pw").read().strip()
st, b = call("POST", "/api/auth/login", None,
             {"email": "r22b-hotfix-admin@r22uat.test", "password": adm_pw})
if st != 200:
    sys.exit(f"login failed {st}")
TOK = b["accessToken"]

st, users = call("GET", "/api/directory/users?includeInactive=true&pageSize=500", TOK)
if st != 200:
    sys.exit(f"list failed {st}")
byid = {u["id"]: u for u in users}
print(f"== MODE={MODE} · directory={len(users)} حساب (شامل المعطَّلين)")

plan = []  # (userId, email, currentActive, targetActive, reason)

S = json.load(open("/tmp/r22b-uat/closure-state.json"))
for slug, e in S["employees"].items():
    u = byid.get(e["userId"])
    if not u:
        print(f"!!  MISSING {e['email']}")
        continue
    plan.append((u["id"], u["email"], u.get("isActive"), False, f"أنشأتها هذه الجلسة ({slug})"))

RE = json.load(open("/tmp/r22b-uat/reactivated.json"))
for r in (RE if isinstance(RE, list) else list(RE.values())):
    uid = r.get("userId") or r.get("id")
    u = byid.get(uid)
    if not u:
        print(f"!!  MISSING {uid}")
        continue
    plan.append((u["id"], u["email"], u.get("isActive"), bool(r.get("wasActive")),
                 "استعادة الحالة السابقة لحساب R22C"))

print(f"\n{'الحساب':44s} {'الآن':>6s} {'الهدف':>7s}  {'إجراء':>10s}  السبب")
changes = 0
for uid, email, cur, tgt, why in plan:
    act = "لا تغيير" if bool(cur) == bool(tgt) else ("تعطيل" if not tgt else "تفعيل")
    if bool(cur) != bool(tgt):
        changes += 1
    print(f"{email:44s} {str(cur):>6s} {str(tgt):>7s}  {act:>10s}  {why}")
print(f"\nPLANNED_CHANGES = {changes} / {len(plan)}")

if not APPLY:
    print("DRY-RUN — لم يُكتب شيء. للتطبيق: R22B_APPLY=1")
    sys.exit(0)

applied, failed = 0, 0
for uid, email, cur, tgt, why in plan:
    if bool(cur) == bool(tgt):
        continue
    u = byid[uid]
    st, _ = call("PUT", f"/api/directory/users/{uid}", TOK, {
        "fullName": u.get("fullName"), "email": u.get("email"), "isActive": tgt,
        "departmentId": u.get("departmentId"), "teamId": u.get("teamId"),
        "managerId": u.get("managerId"),
    })
    ok = 200 <= st < 300
    applied += ok
    failed += (not ok)
    print(("OK  " if ok else "!!  ") + f"{email:44s} -> isActive={tgt}  {st}")

st, after = call("GET", "/api/directory/users?includeInactive=true&pageSize=500", TOK)
ab = {u["id"]: u for u in after}
mismatch = [e for uid, e, cur, tgt, why in plan if bool(ab.get(uid, {}).get("isActive")) != bool(tgt)]
print(f"\nAPPLIED={applied} FAILED={failed} VERIFY_MISMATCH={len(mismatch)} {mismatch}")
sys.exit(1 if (failed or mismatch) else 0)
