#!/usr/bin/env python3
# R22B/RECONCILIATION — تزويد بيئة UAT على TEST عبر الـAPI الرسميّة حصرًا (لا SQL خام، لا كتابة مباشرة).
# البادئة R22R تميّز تجهيزات هذه الجولة عن جولات R22C السابقة كي يكون التنظيف قابلًا للتمييز بيقين.
# ينشئ: إدارة · 5 مسمّيات · فريق · مراجِع (TeamLeader) · 5 موظّفين جدد · عميل · 5 مشروعات،
# ويضبط JobRoleId على القوالب الخمسة مع تسجيل القيمة السابقة (تُعاد في التنظيف).
import json, os, sys, urllib.request, urllib.error

API = "http://127.0.0.1:5091"
OUT = "/root/r22r"
STATE = os.path.join(OUT, "state.json")
PREFIX = "R22R"

EMPLOYEES = [
    # slug, email, عنوان القالب على TEST, اسم المسمّى الوظيفيّ, اسم المشروع
    ("content",    "r22r-content@r22uat.test",
     "تقرير كاتب المحتوى الأسبوعي",       "R22R — كاتب محتوى (UAT)", "R22R — مشروع المحتوى"),
    ("design",     "r22r-design@r22uat.test",
     "تقرير فريق التصميم",                 "R22R — مصمّم (UAT)",      "R22R — مشروع التصميم"),
    ("video",      "r22r-video@r22uat.test",
     "تقرير فريق الفيديو",                 "R22R — منتج فيديو (UAT)", "R22R — مشروع الفيديو"),
    ("moderation", "r22r-moderation@r22uat.test",
     "تقرير المديرشن الأسبوعي",            "R22R — مديرشن (UAT)",     "R22R — مشروع المديرشن"),
    ("seo",        "r22r-seo@r22uat.test",
     "تقرير متابعة مقالات SEO الأسبوعي",   "R22R — محرّر SEO (UAT)",  "R22R — مشروع SEO"),
]

LOG = []


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
            return e.code, json.loads(raw) if raw.strip() else None
        except Exception:
            return e.code, {"raw": raw[:400]}


def step(name, st, note=""):
    ok = 200 <= st < 300 if isinstance(st, int) else False
    LOG.append({"step": name, "status": st, "note": str(note)[:200]})
    print(f"{'OK ' if ok else '!! '}{name:40s} {st}  {str(note)[:110]}")
    return ok


def must(name, st, note=""):
    if not step(name, st, note):
        os.makedirs(OUT, exist_ok=True)
        json.dump({"log": LOG}, open(os.path.join(OUT, "provision-FAILED.json"), "w"),
                  ensure_ascii=False, indent=1)
        sys.exit(1)


os.makedirs(OUT, exist_ok=True)
adm_pw = open("/root/.r22c-admin-pw").read().strip()
usr_pw = open("/root/.r22c-user-pw").read().strip()

st, b = call("POST", "/api/auth/login",
             body={"email": "r22b-hotfix-admin@r22uat.test", "password": adm_pw})
must("login-admin", st)
TOK = b["accessToken"]

S = {"prefix": PREFIX, "userPasswordFile": "/root/.r22c-user-pw", "employees": {}}

# ---- بصمة ما قبل التزويد: كلّ ما هو قائم الآن ليس من صنع هذه الجولة ----
for res in ("users", "clients", "projects"):
    st, b = call("GET", f"/api/directory/{res}" if res == "users" else f"/api/{res}", TOK)
    items = b if isinstance(b, list) else (b or {}).get("items", [])
    S.setdefault("preExisting", {})[res] = sorted(
        [i.get("id") for i in items if isinstance(i, dict) and i.get("id")])
    step(f"fingerprint-{res}", st, len(S["preExisting"][res]))

# ---- 1) الإدارة ----
st, b = call("POST", "/api/directory/departments", TOK,
             {"nameAr": "R22R — إدارة UAT", "nameEn": "R22R UAT Dept",
              "code": "R22RUAT", "managerId": None})
must("create-department", st, (b or {}).get("id"))
S["departmentId"] = b["id"]

# ---- 2) المسمّيات الوظيفيّة ----
for slug, _e, _t, jr_name, _p in EMPLOYEES:
    st, b = call("POST", "/api/directory/job-roles", TOK,
                 {"nameAr": jr_name, "nameEn": None,
                  "code": f"R22R-{slug.upper()}", "departmentId": S["departmentId"]})
    must(f"create-jobrole-{slug}", st, (b or {}).get("id"))
    S["employees"].setdefault(slug, {})["jobRoleId"] = b["id"]

# ---- 3) الفريق ----
st, b = call("POST", "/api/directory/teams", TOK,
             {"nameAr": "R22R — فريق UAT", "nameEn": "R22R UAT Team",
              "departmentId": S["departmentId"], "teamLeaderId": None})
must("create-team", st, (b or {}).get("id"))
S["teamId"] = b["id"]

# ---- 4) المراجِع (قائد الفريق) + قارئ مستقلّ مسموح له (مدير) ----
st, b = call("POST", "/api/directory/users", TOK,
             {"email": "r22r-lead@r22uat.test", "fullName": "R22R — قائد فريق UAT (مؤقّت)",
              "password": usr_pw, "roles": ["TeamLeader"],
              "departmentId": S["departmentId"], "teamId": S["teamId"], "managerId": None})
must("create-reviewer", st, (b or {}).get("id"))
S["reviewerId"] = b["id"]
S["reviewerEmail"] = "r22r-lead@r22uat.test"

st, b = call("POST", "/api/directory/users", TOK,
             {"email": "r22r-manager@r22uat.test", "fullName": "R22R — مدير UAT (قارئ مستقلّ)",
              "password": usr_pw, "roles": ["Manager"],
              "departmentId": S["departmentId"], "teamId": None, "managerId": None})
must("create-independent-reader", st, (b or {}).get("id"))
S["readerId"] = b["id"]
S["readerEmail"] = "r22r-manager@r22uat.test"

# ---- 5) ربط قائد الفريق ----
st, _ = call("PUT", f"/api/directory/teams/{S['teamId']}", TOK,
             {"nameAr": "R22R — فريق UAT", "nameEn": "R22R UAT Team",
              "departmentId": S["departmentId"], "teamLeaderId": S["reviewerId"],
              "isActive": True, "syncMemberDepartments": True})
must("set-team-leader", st, S["reviewerId"])

# ---- 6) الموظّفون الخمسة (حسابات جديدة بالكامل) ----
for slug, email, _t, _jr, _p in EMPLOYEES:
    st, b = call("POST", "/api/directory/users", TOK,
                 {"email": email, "fullName": f"R22R — موظّف {slug} (UAT)",
                  "password": usr_pw, "roles": ["Employee"],
                  "departmentId": S["departmentId"], "teamId": S["teamId"],
                  "managerId": S["reviewerId"]})
    must(f"create-user-{slug}", st, (b or {}).get("id"))
    S["employees"][slug].update({"email": email, "userId": b["id"]})
    st, _ = call("PATCH", f"/api/directory/users/{b['id']}/job-role", TOK,
                 {"jobRoleId": S["employees"][slug]["jobRoleId"], "notes": "R22R UAT"})
    must(f"set-user-jobrole-{slug}", st)

# ---- 7) القوالب: اكتشاف + ضبط JobRoleId مع حفظ الحالة السابقة ----
st, b = call("GET", "/api/report-templates", TOK)
must("list-templates", st)
items = b if isinstance(b, list) else (b or {}).get("items", [])
by_title = {t["title"]: t for t in items}

for slug, _e, title, _jr, _p in EMPLOYEES:
    t = by_title.get(title)
    if t is None:
        must(f"find-template-{slug}", 404, title)
    S["employees"][slug].update({
        "templateId": t["id"], "templateTitle": title,
        "jobRoleIdBefore": t.get("jobRoleId"),
        "classificationBefore": t.get("classification"),
        "descriptionBefore": t.get("description"),
        "defaultPeriodTypeBefore": t.get("defaultPeriodType"),
    })
    st, _ = call("PUT", f"/api/report-templates/{t['id']}", TOK,
                 {"title": title, "description": t.get("description"),
                  "jobRoleId": S["employees"][slug]["jobRoleId"],
                  "defaultPeriodType": t.get("defaultPeriodType", "Weekly"),
                  "classification": t.get("classification", "Primary")})
    must(f"set-template-jobrole-{slug}", st, t["id"])

# ---- 8) العميل والمشروعات ----
st, b = call("POST", "/api/clients", TOK,
             {"name": "R22R — عميل UAT (مؤقّت)", "accountManagerId": None})
must("create-client", st, (b or {}).get("id"))
S["clientId"] = b["id"]

st, b = call("GET", "/api/projects", TOK)
step("list-projects", st)
sample = (b if isinstance(b, list) else (b or {}).get("items", []) or [{}])[0]
S["serviceType"] = sample.get("serviceType") or "SocialMedia"

for slug, _e, _t, _jr, pname in EMPLOYEES:
    st, b = call("POST", "/api/projects", TOK,
                 {"clientId": S["clientId"], "name": pname, "serviceType": S["serviceType"],
                  "ownerTeamId": S["teamId"], "accountManagerId": None,
                  "status": "Active", "teamLeaderId": S["reviewerId"]})
    must(f"create-project-{slug}", st, (b or {}).get("id"))
    S["employees"][slug].update({"projectId": b["id"], "projectName": pname})

S["log"] = LOG
json.dump(S, open(STATE, "w"), ensure_ascii=False, indent=1)
print("\nSTATE=" + STATE)
