#!/usr/bin/env python3
# R22B-REL §4 — تنظيف بيانات UAT على RC عبر الواجهات الرسميّة حصرًا.
# ممنوع: SQL كتابة · حذف صلب · تصفير كلمة مرور حساب حقيقيّ · حذف fixture غير مملوك · إبطال توكنات جماعيّ.
# المسموح: admin-delete ناعم للتسليمات · archive للمشاريع والعملاء · تعطيل الحسابات المؤقّتة
#          + reset-password للحسابات المؤقّتة وحدها (يُبطل توكناتها هي فقط).
import base64, json, secrets, ssl, sys, urllib.request, urllib.error

BASE = "https://rc-report.emarketingacademy.net"
BU, BP = open("/tmp/rel-secrets/rc-basic-auth").read().strip().split(":", 1)
BASIC = "Basic " + base64.b64encode(f"{BU}:{BP}".encode()).decode()
APW = open("/tmp/rel-secrets/rc-sysadmin-temp-pwd").read().strip()
DRY = "--apply" not in sys.argv
LOG = []


def call(method, path, token=None, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Authorization", BASIC if token is None else f"Bearer {token}")
    if token is not None:
        req.add_header("X-Basic", BASIC)
    data = None
    if body is not None:
        data = json.dumps(body, ensure_ascii=False).encode()
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, data, timeout=45, context=ssl.create_default_context()) as r:
            raw = r.read().decode("utf-8", "replace")
            return r.status, (json.loads(raw) if raw.strip().startswith(("{", "[")) else raw)
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        return e.code, (json.loads(raw) if raw.strip().startswith(("{", "[")) else raw)


def rec(step, status, note=""):
    ok = 200 <= status < 300
    LOG.append({"step": step, "status": status, "ok": ok, "note": str(note)[:200]})
    print(f"{'OK ' if ok else 'FAIL'} {status} {step} {str(note)[:90]}")
    return ok


# ── تسجيل دخول الأدمن (حساب قائم مسبقًا — لا يُغيَّر ولا تُصفَّر كلمة مروره) ──
st, b = call("POST", "/api/auth/login", None, {"email": "admin@marketingexperts.local", "password": APW})
assert st == 200, f"login failed {st} {b}"
TOK = b["accessToken"]
rec("login-admin", st)

TEMP_USERS = [
    ("e2257fd0-bf22-4818-bda8-2bccd632a435", "r22brel-am@rc-uat.local"),
    ("1d4fe116-3d62-446d-b697-972d1f6bb4e2", "r22brel-content@rc-uat.local"),
    ("a1fc4c78-02e9-408f-b53f-fd178347d711", "r22brel-lead@rc-uat.local"),
    ("22bdd125-fbf6-4d96-a500-c1bc4b14ddef", "r22brel-out@rc-uat.local"),
    ("f9df279f-91f1-4193-bd31-25c912f5d744", "r22brel-seo@rc-uat.local"),
]
TEMP_SUBMISSIONS = [
    "4d981b10-4876-4558-b35f-8c12f7c5603a", "de13df0f-5d56-42aa-ad98-2f77c7e48b72",
    "289621dd-b5ef-4284-b9a9-bf07b040b371", "c861d79a-0e57-4a8a-9e27-977ecee35109",
    "dc76b6c8-e7e5-41dd-869f-6b81acdd9419", "42dd90e5-d408-453e-98f9-6bc3528d166d",
]
TEMP_PROJECTS = ["a0668ebd-7086-4989-9188-b9d44ec21456", "ef4ab7ce-4721-441a-826f-2549ce76154d",
                 "d9497ea9-62ee-468a-9092-2f23b8da4367"]
TEMP_CLIENT = "6a728948-de62-492b-bb08-d0d07acd8241"
TEMP_TEAM = "65ef9351-dc2c-44ef-9ba3-0747a3a6667d"
TEMP_DEPT = "d9959c01-d187-4c00-90de-063338ddb8b0"
REASON = "R22B-REL 20260906 — تنظيف بيانات UAT مؤقّتة أنشأتها مهمّة الإصدار على RC."

if DRY:
    print("== DRY-RUN — لا كتابة. الخطط: ==")
    for s in TEMP_SUBMISSIONS:
        print(f"  POST /api/submissions/{s}/admin-delete  (حذف ناعم + سبب)")
    for p in TEMP_PROJECTS:
        print(f"  POST /api/projects/{p}/archive")
    print(f"  POST /api/clients/{TEMP_CLIENT}/archive")
    for uid, em in TEMP_USERS:
        print(f"  POST /api/directory/users/{uid}/reset-password  (إبطال توكنات {em} وحده)")
        print(f"  PUT  /api/directory/users/{uid}  IsActive=false")
    print(f"  PUT  /api/directory/teams/{TEMP_TEAM}  IsActive=false")
    print(f"  PUT  /api/directory/departments/{TEMP_DEPT}  IsActive=false")
    sys.exit(0)

# ── 1) التسليمات: حذف إداريّ ناعم بسبب إلزاميّ ──
for sid in TEMP_SUBMISSIONS:
    st, b = call("POST", f"/api/submissions/{sid}/admin-delete", TOK, {"reason": REASON})
    rec(f"admin-delete-submission-{sid[:8]}", st, b)

# ── 2) المشاريع والعميل: أرشفة ──
for pid in TEMP_PROJECTS:
    st, b = call("POST", f"/api/projects/{pid}/archive", TOK)
    rec(f"archive-project-{pid[:8]}", st, b)
st, b = call("POST", f"/api/clients/{TEMP_CLIENT}/archive", TOK)
rec("archive-client", st, b)

# ── 3) الحسابات المؤقّتة: إبطال توكناتها ثمّ تعطيلها ──
st, users = call("GET", "/api/directory/users?includeInactive=true&pageSize=200", TOK)
byid = {}
if st == 200:
    items = users.get("items", users) if isinstance(users, dict) else users
    byid = {u["id"]: u for u in items}
for uid, em in TEMP_USERS:
    st, b = call("POST", f"/api/directory/users/{uid}/reset-password", TOK,
                 {"newPassword": "Rc!" + secrets.token_urlsafe(24)})
    rec(f"revoke-tokens-{em.split('@')[0]}", st, b)
    cur = byid.get(uid, {})
    st, b = call("PUT", f"/api/directory/users/{uid}", TOK, {
        "fullName": cur.get("fullName") or "R22BREL UAT", "email": em, "isActive": False,
        "departmentId": cur.get("departmentId"), "teamId": cur.get("teamId"), "managerId": cur.get("managerId")})
    rec(f"deactivate-{em.split('@')[0]}", st, b)

# ── 4) الفريق والإدارة المؤقّتان: تعطيل ──
st, b = call("PUT", f"/api/directory/teams/{TEMP_TEAM}", TOK, {
    "nameAr": "R22BREL — فريق UAT", "nameEn": "R22BREL UAT Team", "departmentId": TEMP_DEPT,
    "teamLeaderId": None, "isActive": False, "syncMemberDepartments": False})
rec("deactivate-team", st, b)
st, b = call("PUT", f"/api/directory/departments/{TEMP_DEPT}", TOK, {
    "nameAr": "R22BREL — إدارة UAT", "nameEn": "R22BREL UAT Dept", "code": None,
    "managerId": None, "isActive": False})
rec("deactivate-department", st, b)

json.dump(LOG, open("/private/tmp/rel-uat/rc-cleanup-apply.json", "w"), ensure_ascii=False, indent=1)
fails = [x for x in LOG if not x["ok"]]
print(f"\nRC_CLEANUP_STEPS={len(LOG)}  RC_CLEANUP_FAILED={len(fails)}")
sys.exit(1 if fails else 0)
