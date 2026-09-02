#!/usr/bin/env python3
# R22B CLOSURE — تزويد بيئة UAT على TEST عبر الـAPI الرسميّة حصرًا (لا SQL خام · لا حذف صلب).
#
# لماذا حسابات جديدة بدل حسابات R22C: تقارير W36 لحسابات R22C **مغلقة** (Closed)، والإغلاق
# حالة نهائيّة لا يعيدها حتّى الأدمن (409) — وهو سلوك حوكمة صحيح. والدورة التالية مرفوضة
# بـ`calendar.cycle_not_open`. فلا سبيل لمسودّة قابلة للتحرير في الدورة الحاليّة إلّا بموظّف
# لم يقدّم فيها بعد. الحسابات الجديدة ترث **نفس** المسمّى الوظيفيّ والفريق والإدارة، فالاستحقاق
# والقالب والمشروع كما هي — يتغيّر الشخص لا المسار.
#
# التنظيف (المرحلة D): تعطيل الحسابات (`isActive=false`) لا حذفها — الحذف صلب في هذا النظام.
import json, os, sys, urllib.request, urllib.error

API = os.environ.get("R22B_API", "http://127.0.0.1:15091")
OUT = os.environ.get("R22B_OUT", "/tmp/r22b-uat")
SUFFIX = os.environ.get("R22B_SUFFIX", "c1")
SLUGS = ["content", "design", "video", "moderation", "seo"]


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


def must(name, st, note=""):
    ok = isinstance(st, int) and 200 <= st < 300
    print(("OK  " if ok else "!!  ") + f"{name:44s} {st}  {str(note)[:110]}")
    if not ok:
        sys.exit(1)
    return True


adm_pw = open("/tmp/r22b-uat/.admin-pw").read().strip()
usr_pw = open("/tmp/r22b-uat/.user-pw").read().strip()
R22C = json.load(open("/tmp/r22c-state.json"))

st, b = call("POST", "/api/auth/login", None,
             {"email": "r22b-hotfix-admin@r22uat.test", "password": adm_pw})
must("login-admin", st)
TOK = b["accessToken"]

# الدليل كاملًا (بما فيه المعطَّلون) لاستخراج المسمّى الوظيفيّ والانتماء من حسابات R22C.
st, users = call("GET", "/api/directory/users?includeInactive=true&pageSize=500", TOK)
must("list-users", st, len(users))
byid = {u["id"]: u for u in users}

S = {"suffix": SUFFIX, "employees": {}, "clientId": R22C["clientId"],
     "reviewerId": R22C["reviewerId"], "accountManagerId": R22C["accountManagerId"],
     "outOfScopeProjectId": R22C["outOfScopeProjectId"],
     "outOfScopeProjectName": R22C["outOfScopeProjectName"]}

# المشروعات القائمة لعميل UAT — تُعاد استعمالًا كما هي (لا مشروعات جديدة بلا حاجة).
st, projects = call("GET", f"/api/projects?clientId={R22C['clientId']}", TOK)
must("list-projects", st, len(projects if isinstance(projects, list) else []))
pbyname = {p["name"]: p for p in projects}

for slug in SLUGS:
    src = byid[R22C["employees"][slug]["userId"]]
    email = f"r22bc-{slug}-{SUFFIX}@r22uat.test"
    existing = next((u for u in users if u["email"] == email), None)
    if existing:
        uid = existing["id"]
        st, _ = call("PUT", f"/api/directory/users/{uid}", TOK,
                     {"fullName": existing["fullName"], "email": email, "isActive": True,
                      "departmentId": src.get("departmentId"), "teamId": src.get("teamId"),
                      "managerId": src.get("managerId")})
        must(f"reactivate-{slug}", st, uid)
        st, _ = call("POST", f"/api/directory/users/{uid}/reset-password", TOK, {"newPassword": usr_pw})
        must(f"reset-pw-{slug}", st)
    else:
        st, u = call("POST", "/api/directory/users", TOK,
                     {"email": email, "fullName": f"R22B إغلاق — موظّف {slug} (UAT مؤقّت)",
                      "password": usr_pw, "roles": src.get("roles") or ["Employee"],
                      "departmentId": src.get("departmentId"), "teamId": src.get("teamId"),
                      "managerId": src.get("managerId")})
        must(f"create-{slug}", st, (u or {}).get("id"))
        uid = u["id"]

    # المسمّى الوظيفيّ هو ما يقود الاستحقاق (لا الدور الأمنيّ) — يُنسخ حرفيًّا من حساب R22C.
    st, _ = call("PATCH", f"/api/directory/users/{uid}/job-role", TOK,
                 {"jobRoleId": src.get("jobRoleId"), "notes": "R22B closure UAT"})
    must(f"job-role-{slug}", st, src.get("jobRoleId"))

    pname = R22C["employees"][slug]["projectName"]
    S["employees"][slug] = {
        "email": email, "userId": uid, "jobRoleId": src.get("jobRoleId"),
        "templateId": R22C["employees"][slug]["templateId"],
        "templateTitle": R22C["employees"][slug]["templateTitle"],
        "projectId": pbyname.get(pname, {}).get("id") or R22C["employees"][slug]["projectId"],
        "projectName": pname,
    }
    st, _ = call("POST", "/api/auth/login", None, {"email": email, "password": usr_pw})
    must(f"login-{slug}", st)

json.dump(S, open(os.path.join(OUT, "closure-state.json"), "w"), ensure_ascii=False, indent=1)
print("WROTE", os.path.join(OUT, "closure-state.json"))
