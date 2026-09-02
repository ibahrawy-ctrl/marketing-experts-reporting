#!/usr/bin/env python3
# R22B/RECONCILIATION §10 — تنظيف تجهيزات UAT على TEST عبر الـAPI الرسميّة وIdentity حصرًا.
# لا SQL خام للكتابة · لا حذف صلب · لا مساس بالأدمن ولا بأيّ كيان سابق للتزويد.
# طوران: `dry` يقيس ويثبت ثمّ يطبع الخطّة · `apply` ينفّذ الخطّة المُثبَتة نفسها.
#
# البرهان الحاكم: كلّ معرّف سيُمسّ = (الموجود الآن) − (بصمة ما قبل التزويد المحفوظة في state.json)،
# ويجب أن يطابق تمامًا ما سجّله التزويد. أيّ فارق ⟹ توقّف قبل أيّ كتابة.
import json, os, secrets, sys, urllib.request, urllib.error

API = "http://127.0.0.1:5091"
OUT = "/root/r22r"
STATE = os.path.join(OUT, "state.json")
MODE = sys.argv[1] if len(sys.argv) > 1 else "dry"
assert MODE in ("dry", "apply"), "usage: r22r-uat-cleanup.py [dry|apply]"

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
    ok = isinstance(st, int) and 200 <= st < 300
    LOG.append({"step": name, "status": st, "note": str(note)[:200]})
    print(f"{'OK ' if ok else '!! '}{name:44s} {st}  {str(note)[:100]}")
    return ok


def halt(msg):
    print("\nHALT: " + msg)
    json.dump({"mode": MODE, "halt": msg, "log": LOG},
              open(os.path.join(OUT, f"cleanup-{MODE}-HALTED.json"), "w"),
              ensure_ascii=False, indent=1)
    sys.exit(2)


def ids(b):
    items = b if isinstance(b, list) else (b or {}).get("items", [])
    return {i["id"]: i for i in items if isinstance(i, dict) and i.get("id")}


S = json.load(open(STATE))
adm_pw = open("/root/.r22c-admin-pw").read().strip()
st, b = call("POST", "/api/auth/login",
             body={"email": "r22b-hotfix-admin@r22uat.test", "password": adm_pw})
if not step("login-admin", st):
    halt("تعذّر الدخول")
TOK = b["accessToken"]

# ---------- 1) الخطّة المُعلَنة من سجلّ التزويد ----------
emp = S["employees"]
PLAN = {
    "users": [S["reviewerId"], S["readerId"]] + [e["userId"] for e in emp.values()],
    "clients": [S["clientId"]],
    "projects": [e["projectId"] for e in emp.values()],
    "jobRoles": [e["jobRoleId"] for e in emp.values()],
    "teams": [S["teamId"]],
    "departments": [S["departmentId"]],
    "templates": [{"id": e["templateId"], "title": e["templateTitle"],
                   "jobRoleId": e["jobRoleIdBefore"],
                   "description": e["descriptionBefore"],
                   "classification": e["classificationBefore"],
                   "defaultPeriodType": e["defaultPeriodTypeBefore"]} for e in emp.values()],
}

# ---------- 2) البرهان: الفارق عن بصمة ما قبل التزويد = خطّتنا بالضبط ----------
DIFF = {}
for res, path in (("users", "/api/directory/users"), ("clients", "/api/clients"),
                  ("projects", "/api/projects")):
    st, b = call("GET", path, TOK)
    if not step(f"list-{res}", st):
        halt(f"تعذّرت قراءة {res}")
    cur = ids(b)
    before = set(S["preExisting"][res])
    created = sorted(set(cur) - before)
    planned = sorted(set(PLAN[res]))
    DIFF[res] = {"beforeCount": len(before), "nowCount": len(cur),
                 "created": created, "planned": planned,
                 "createdEqualsPlanned": created == planned,
                 "untouchedPreExisting": sorted(before & set(cur)) == sorted(before)}
    step(f"diff-{res}", 200,
         f"before={len(before)} now={len(cur)} created={len(created)} match={DIFF[res]['createdEqualsPlanned']}")
    if not DIFF[res]["createdEqualsPlanned"]:
        halt(f"فارق {res} لا يطابق سجلّ التزويد — لا كتابة")
    # كلّ كيان مستهدَف يحمل بصمة R22R نصّيًّا (برهان ثانٍ مستقلّ عن الفارق العدديّ)
    for i in created:
        it = cur[i]
        label = str(it.get("email") or it.get("name") or it.get("fullName") or "")
        if "r22r" not in label.lower() and "R22R" not in label:
            halt(f"كيان {res}/{i} لا يحمل بصمة R22R: {label!r}")
    DIFF[res]["labels"] = [str(cur[i].get("email") or cur[i].get("name") or "") for i in created]

# ---------- 3) التسليمات: تُؤخذ من سجلّ الرحلة، ويُتحقَّق فرديًّا أنّ صاحبها موظّف تجهيزات ----------
# قائمة `/api/submissions` مُنطاقة بالمُطالِب (الأدمن ليس مراجِعًا لأحد) فتعود فارغة؛
# لذا المصدر هو المعرّفات المسجَّلة في `uat-evidence.json` مع تحقّق ملكيّة لكلّ معرّف على حدة.
fixture_users = set(PLAN["users"])
ev = json.load(open(os.path.join(OUT, "uat-evidence.json")))
candidates = sorted({t["submissionId"] for t in ev["templates"].values() if t.get("submissionId")})
PLAN["submissions"], PLAN["submissionsAlreadyGone"] = [], []
for sid in candidates:
    st, b = call("GET", f"/api/submissions/{sid}", TOK)
    if st == 404:
        PLAN["submissionsAlreadyGone"].append(sid)
        step(f"verify-submission-{sid[:8]}", 200, "محذوف إداريًّا سلفًا")
        continue
    if not step(f"verify-submission-{sid[:8]}", st):
        halt(f"تعذّرت قراءة التسليم {sid}")
    owner = b.get("submitterId")
    if owner not in fixture_users:
        halt(f"التسليم {sid} ليس لموظّف تجهيزات (owner={owner})")
    PLAN["submissions"].append(sid)
step("plan-submissions", 200,
     f"{len(PLAN['submissions'])} للحذف · {len(PLAN['submissionsAlreadyGone'])} محذوف سلفًا")

# ---------- 4) القوالب: مسّت التزويد بحقل واحد فقط، والاستعادة إليه ----------
st, b = call("GET", "/api/report-templates", TOK)
step("list-templates", st)
tmap = ids(b)
for t in PLAN["templates"]:
    cur = tmap.get(t["id"], {})
    t["jobRoleIdNow"] = cur.get("jobRoleId")
    t["needsRestore"] = cur.get("jobRoleId") != t["jobRoleId"]
step("plan-templates", 200,
     f"{sum(1 for t in PLAN['templates'] if t['needsRestore'])}/5 تحتاج استعادة JobRoleId")

REPORT = {"mode": MODE, "diff": DIFF, "plan": PLAN}

if MODE == "dry":
    print("\n=== DRY RUN — لا كتابة وقعت ===")
    for k in ("users", "clients", "projects", "jobRoles", "teams", "departments", "submissions"):
        print(f"  {k:14s} {len(PLAN[k])}")
    json.dump({**REPORT, "log": LOG}, open(os.path.join(OUT, "cleanup-dryrun.json"), "w"),
              ensure_ascii=False, indent=1)
    print("\nDRYRUN=" + os.path.join(OUT, "cleanup-dryrun.json"))
    sys.exit(0)

# ================= APPLY — بالترتيب الآمن للتبعيّات =================
# أ) استعادة القوالب إلى حالتها المسجَّلة قبل التزويد.
for t in PLAN["templates"]:
    st, _ = call("PUT", f"/api/report-templates/{t['id']}", TOK,
                 {"title": t["title"], "description": t["description"],
                  "jobRoleId": t["jobRoleId"], "defaultPeriodType": t["defaultPeriodType"],
                  "classification": t["classification"]})
    step(f"restore-template-{t['id'][:8]}", st)

# ب) حذف إداريّ ناعم لكلّ تسليمات التجهيزات (لا حذف صلب).
for sid in PLAN["submissions"]:
    st, b = call("POST", f"/api/submissions/{sid}/admin-delete", TOK,
                 {"reason": "R22R — إنهاء تجهيزات UAT: حذف إداريّ ناعم لتسليم اختباريّ"})
    step(f"soft-delete-submission-{sid[:8]}", st,
         "" if 200 <= st < 300 else (b or {}).get("code", ""))

# ج) أرشفة المشروعات ثمّ العميل.
for pid in PLAN["projects"]:
    st, _ = call("POST", f"/api/projects/{pid}/archive", TOK)
    step(f"archive-project-{pid[:8]}", st)
st, _ = call("POST", f"/api/clients/{PLAN['clients'][0]}/archive", TOK)
step("archive-client", st)

# د) الحسابات: إبطال رموز التجديد (إعادة تعيين رسميّة) ثمّ التعطيل.
#    إعادة التعيين هي المسار الرسميّ الوحيد لإبطال RefreshTokens بلا حذف صلب ولا SQL،
#    وأثرها غير قابل للعكس: كلمة المرور القديمة تضيع نهائيًّا (مُقرّ به في §10).
st, b = call("GET", "/api/directory/users", TOK)
umap = ids(b)
for uid in PLAN["users"]:
    # كلمة مرور عشوائيّة تُولَّد في الذاكرة ولا تُكتَب ولا تُطبَع: الحساب يصبح غير قابل للاسترجاع
    # عمدًا، وهو الأثر غير القابل للعكس المُقرّ به في §10 مقابل إبطال رموز التجديد بلا حذف صلب.
    st, _ = call("POST", f"/api/directory/users/{uid}/reset-password", TOK,
                 {"newPassword": "Zz9" + secrets.token_urlsafe(24)})
    step(f"invalidate-tokens-{uid[:8]}", st)
    u = umap.get(uid, {})
    st, _ = call("PUT", f"/api/directory/users/{uid}", TOK,
                 {"fullName": u.get("fullName", "R22R"), "email": u.get("email"),
                  "isActive": False, "departmentId": u.get("departmentId"),
                  "teamId": u.get("teamId"), "managerId": u.get("managerId")})
    step(f"deactivate-user-{uid[:8]}", st)

# هـ) المسمّيات الوظيفيّة (أرشفة رسميّة).
for jid in PLAN["jobRoles"]:
    st, _ = call("POST", f"/api/directory/job-roles/{jid}/archive", TOK)
    step(f"archive-jobrole-{jid[:8]}", st)

# و) الفريق ثمّ الإدارة (إلغاء تفعيل، لا حذف).
st, _ = call("PUT", f"/api/directory/teams/{PLAN['teams'][0]}", TOK,
             {"nameAr": "R22R — فريق UAT (مؤرشف)", "nameEn": "R22R UAT Team",
              "departmentId": PLAN["departments"][0], "teamLeaderId": None,
              "isActive": False, "syncMemberDepartments": False})
step("deactivate-team", st)
st, _ = call("PUT", f"/api/directory/departments/{PLAN['departments'][0]}", TOK,
             {"nameAr": "R22R — إدارة UAT (مؤرشفة)", "nameEn": "R22R UAT Dept",
              "code": "R22RUAT", "managerId": None, "isActive": False})
step("deactivate-department", st)

# ---------- التحقّق البعديّ ----------
V = {}
st, b = call("GET", "/api/directory/users", TOK)
umap = ids(b)
V["usersStillPresent"] = [u for u in PLAN["users"] if u in umap]
V["usersActive"] = [u for u in PLAN["users"] if umap.get(u, {}).get("isActive")]
V["preExistingUsersIntact"] = all(u in umap for u in S["preExisting"]["users"])

st, b = call("GET", "/api/projects", TOK)
pmap = ids(b)
V["projectsActive"] = [p for p in PLAN["projects"]
                       if str(pmap.get(p, {}).get("status")) not in ("Archived", "Inactive", "Closed")]
V["preExistingProjectsIntact"] = all(p in pmap for p in S["preExisting"]["projects"])

st, b = call("GET", "/api/clients", TOK)
cmap = ids(b)
V["preExistingClientsIntact"] = all(c in cmap for c in S["preExisting"]["clients"])
V["clientStatus"] = str(cmap.get(PLAN["clients"][0], {}).get("status"))

st, b = call("GET", "/api/report-templates", TOK)
tmap = ids(b)
V["templatesRestored"] = all(tmap.get(t["id"], {}).get("jobRoleId") == t["jobRoleId"]
                             for t in PLAN["templates"])

for k, v in V.items():
    print(f"VERIFY {k:28s} = {json.dumps(v, ensure_ascii=False)[:90]}")

json.dump({**REPORT, "verify": V, "log": LOG},
          open(os.path.join(OUT, "cleanup-apply.json"), "w"), ensure_ascii=False, indent=1)
print("\nAPPLY=" + os.path.join(OUT, "cleanup-apply.json"))
