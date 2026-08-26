#!/usr/bin/env python3
"""تجهيز بيانات UAT لتذكرة PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2 — على TEST وحده.

كلّ كيان ببادئة `P360R2-` ليكون الحذف لاحقًا قاطعًا بلا تخمين.
لا تُطبع كلمة مرور ولا رمز وصول في أيّ مخرج.
"""
import json
import sys
import urllib.request
import urllib.error
import urllib.parse

BASE = "https://test.emarketingacademy.net/api"
PW = open("/tmp/p360r2/.pw").read().strip()
ADMIN_PW = open("/tmp/p360r2/.adminpw").read().strip()
TAG = "P360R2"
TEMPLATE_ID = "aed0016c-398d-4e27-a901-43a2c9097fe8"
SECTION_FIELD = "0de34722-b3bb-422c-9c09-23ed2be68763"
GENERAL_FIELD = "2f83c326-fd59-4a80-a58d-d70e562cf133"
GENERAL_NOTE = "ملخّص-عامّ-لا-ينتمي-لمشروع-R2X9"


def call(method, path, token=None, body=None, raw=False):
    data = json.dumps(body, ensure_ascii=False).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            txt = r.read().decode()
            return r.status, (txt if raw else (json.loads(txt) if txt else None))
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:500]


def must(status, payload, label):
    if status >= 300:
        print(f"FAIL {label}: http={status} {payload}")
        sys.exit(1)
    return payload


def login(email, pw):
    st, d = call("POST", "/auth/login", body={"email": email, "password": pw})
    must(st, d, "login " + email)
    return d["accessToken"], d["userId"]


admin_tok = login("admin@marketingexperts.local", ADMIN_PW)[0]
out = {"users": {}, "templateId": TEMPLATE_ID,
       "sectionFieldId": SECTION_FIELD, "generalFieldId": GENERAL_FIELD,
       "generalNote": GENERAL_NOTE}

USERS = [
    ("admin",    f"{TAG}-مدير النظام",       "p360r2.admin@r2uat.test",    ["Admin"]),
    ("acctmgr",  f"{TAG}-مدير الحساب",       "p360r2.acctmgr@r2uat.test",  ["Employee", "AccountPortfolioReader"]),
    ("owner",    f"{TAG}-مالك المشروع",      "p360r2.owner@r2uat.test",    ["Manager"]),
    ("lead",     f"{TAG}-قائد الفريق",       "p360r2.lead@r2uat.test",     ["TeamLeader"]),
    ("emp",      f"{TAG}-موظّف داخل النطاق", "p360r2.emp@r2uat.test",      ["Employee"]),
    ("outsider", f"{TAG}-موظّف خارج النطاق", "p360r2.outsider@r2uat.test", ["Employee"]),
]

for key, name, email, roles in USERS:
    st, d = call("POST", "/directory/users", admin_tok,
                 {"email": email, "fullName": name, "password": PW, "roles": roles,
                  "departmentId": None, "teamId": None, "managerId": None})
    if st >= 300:
        st2, lst = call("GET", "/directory/users?search=" + urllib.parse.quote(email), admin_tok)
        must(st2, lst, "lookup " + email)
        rows = lst["items"] if isinstance(lst, dict) and "items" in lst else lst
        hit = [r for r in rows if r.get("email", "").lower() == email.lower()]
        if not hit:
            print(f"FAIL create+lookup {email}: http={st} {d}")
            sys.exit(1)
        uid = hit[0]["id"]
    else:
        uid = d["id"] if isinstance(d, dict) else d
    out["users"][key] = {"id": uid, "email": email, "roles": roles, "fullName": name}
    print(f"user {key:9s} {email:30s} {uid}")

U = out["users"]

# ---- إدارة + فريقان (داخل النطاق / خارج النطاق) ----
st, depts = call("GET", "/directory/departments", admin_tok)
must(st, depts, "list departments")
dep = next((d for d in depts if d.get("nameAr") == f"{TAG}-إدارة"), None)
if dep is None:
    st, dep = call("POST", "/directory/departments", admin_tok,
                   {"nameAr": f"{TAG}-إدارة", "nameEn": "P360R2 Dept", "code": "P360R2", "managerId": U["owner"]["id"]})
    must(st, dep, "create department")
out["departmentId"] = dep["id"]

st, teams = call("GET", "/directory/teams", admin_tok)
must(st, teams, "list teams")


def team_of(name, leader):
    t = next((x for x in teams if x.get("nameAr") == name), None)
    if t is None:
        s, t = call("POST", "/directory/teams", admin_tok,
                    {"nameAr": name, "nameEn": None, "departmentId": out["departmentId"], "teamLeaderId": leader})
        must(s, t, "create team " + name)
    return t["id"]


out["teamId"] = team_of(f"{TAG}-فريق داخل النطاق", U["lead"]["id"])
out["outsiderTeamId"] = team_of(f"{TAG}-فريق خارج النطاق", None)
print("department", out["departmentId"], "team", out["teamId"], "outsiderTeam", out["outsiderTeamId"])

for key in ("acctmgr", "owner", "lead", "emp", "outsider"):
    tid = out["outsiderTeamId"] if key == "outsider" else out["teamId"]
    st, r = call("PUT", f"/directory/users/{U[key]['id']}", admin_tok, {
        "fullName": U[key]["fullName"], "email": U[key]["email"], "isActive": True,
        "departmentId": out["departmentId"], "teamId": tid, "managerId": None}, raw=True)
    must(st, r, "update user " + key)
print("users placed in teams")

# ---- عميل + مشروعان (idempotent بالاسم) ----
st, clients = call("GET", "/clients", admin_tok)
must(st, clients, "list clients")
crows = clients["items"] if isinstance(clients, dict) and "items" in clients else clients
cl = next((c for c in crows if c.get("name") == f"{TAG}-عميل بنود العمل"), None)
if cl is None:
    st, cl = call("POST", "/clients", admin_tok,
                  {"name": f"{TAG}-عميل بنود العمل", "accountManagerId": U["acctmgr"]["id"]})
    must(st, cl, "create client")
out["clientId"] = cl["id"]
print("client", cl["id"])

st, projs = call("GET", "/projects", admin_tok)
must(st, projs, "list projects")
prows = projs["items"] if isinstance(projs, dict) and "items" in projs else projs

for label, nm in (("A", f"{TAG}-مشروع أ"), ("B", f"{TAG}-مشروع ب")):
    p = next((x for x in prows if x.get("name") == nm), None)
    body = {"clientId": out["clientId"], "name": nm, "serviceType": "MediaBuying",
            "ownerTeamId": out["teamId"], "accountManagerId": U["acctmgr"]["id"],
            "projectOwnerId": U["owner"]["id"], "teamLeaderId": U["lead"]["id"], "status": "Active"}
    if p is None:
        st, p = call("POST", "/projects", admin_tok, body)
        must(st, p, "create project " + label)
    else:
        st, r = call("PUT", f"/projects/{p['id']}", admin_tok, body, raw=True)
        must(st, r, "update project " + label)
    out["project" + label] = p["id"]
    print("project", label, p["id"])

st, asg = call("POST", f"/report-templates/{TEMPLATE_ID}/assignments", admin_tok, {
    "scopeType": "Employee", "scopeId": U["emp"]["id"], "kind": "Include", "notes": f"{TAG} UAT"})
if st == 409:
    print("template assignment already exists (idempotent)")
else:
    must(st, asg, "assign template")
    print("template assigned to emp")

emp_tok = login("p360r2.emp@r2uat.test", PW)[0]
st, cycles = call("GET", "/reporting-calendar/my-cycles", emp_tok)
must(st, cycles, "my-cycles")
period_key = cycles.get("currentCycleKey") if isinstance(cycles, dict) else None
if not period_key:
    print("cycles payload:", json.dumps(cycles, ensure_ascii=False)[:600])
    sys.exit("FAIL: تعذّر استخراج مفتاح الفترة الأسبوعيّة")
out["periodKey"] = period_key
print("periodKey", period_key)

st, sub = call("POST", "/submissions", emp_tok, {
    "reportTemplateId": TEMPLATE_ID, "periodType": "Weekly",
    "periodKey": period_key, "projectId": out["projectA"]})
if st == 409:
    st2, mine = call("GET", "/submissions?mine=true", emp_tok)
    must(st2, mine, "list my submissions")
    mrows = mine["items"] if isinstance(mine, dict) and "items" in mine else mine
    hit = [x for x in mrows if x.get("periodKey") == period_key
           and str(x.get("reportTemplateId", "")).lower() == TEMPLATE_ID]
    if not hit:
        print("submissions payload:", json.dumps(mrows, ensure_ascii=False)[:600])
        sys.exit("FAIL: تعذّر إيجاد التسليم القائم")
    sub = hit[0]
else:
    must(st, sub, "create submission")
out["submissionId"] = sub["id"]
print("submission", sub["id"])

# سيناريو A/B الحقيقيّ المطلوب في §13:
#   مشروع أ = Carousel + Static Post + Reel Script   (ثلاثة بنود عمل داخل بطاقة مشروع واحدة)
#   مشروع ب = Article + SEO Activity                 (بندان)
entries = [
    {"projectId": out["projectA"],
     "answers": {"project_goal": "Awareness", "project_notes": "بطاقة مشروع واحدة تضمّ ثلاثة أنواع عمل"},
     "workItems": [
         {"answers": {"content_type": "Carousel", "work_status": "Published", "count": 3, "item_notes": "R2-A-CAROUSEL"}},
         {"answers": {"content_type": "Static Post", "work_status": "Approved", "count": 5, "item_notes": "R2-A-STATIC"}},
         {"answers": {"content_type": "Reel Script", "work_status": "Draft", "count": 2, "item_notes": "R2-A-REEL"}},
     ]},
    {"projectId": out["projectB"],
     "answers": {"project_goal": "Educational", "project_notes": "مشروع ب — بندان مختلفان"},
     "workItems": [
         {"answers": {"content_type": "Article", "work_status": "Published", "count": 4, "item_notes": "R2-B-ARTICLE"}},
         {"answers": {"content_type": "SEO Activity", "work_status": "Approved", "count": 7, "item_notes": "R2-B-SEO"}},
     ]},
]

st, r = call("PUT", f"/submissions/{out['submissionId']}/values", emp_tok, {"values": [
    {"templateFieldId": SECTION_FIELD, "valueText": None, "valueNumber": None,
     "valueDate": None, "valueBool": None, "valueJson": json.dumps(entries, ensure_ascii=False)},
    {"templateFieldId": GENERAL_FIELD, "valueText": GENERAL_NOTE, "valueNumber": None,
     "valueDate": None, "valueBool": None, "valueJson": None},
]}, raw=True)
must(st, r, "save values")
print("values saved")

out["markers"] = {"A": ["R2-A-CAROUSEL", "R2-A-STATIC", "R2-A-REEL"],
                  "B": ["R2-B-ARTICLE", "R2-B-SEO"]}
json.dump(out, open("/tmp/p360r2/fixture.json", "w"), ensure_ascii=False, indent=1)
print("OK fixture written")
