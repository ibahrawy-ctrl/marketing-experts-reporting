#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""المرحلة 9 + 10 — سيناريوهات حيّة على RC (127.0.0.1:5092) بمستخدمين اصطناعيّين @p123.rc.test فقط.
لا يُمسّ أيّ صفّ حقيقيّ: كلّ ما يُنشأ هنا يحمل بادئة RC-P123- أو يخصّ نطاق p123.rc.test.
"""
import json, urllib.request, urllib.error, datetime, sys

BASE = "http://127.0.0.1:5092"
PW = "RcP123#Synthetic!2026"
IDS = json.load(open("/tmp/p123-rc/synth-ids.json", encoding="utf-8"))["ids"]

RESULTS = []
CREATED = {"departments": [], "teams": [], "incidents": []}


def call(method, path, token=None, body=None):
    data = json.dumps(body, ensure_ascii=False).encode("utf-8") if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            raw = r.read().decode("utf-8", "replace")
            return r.status, raw, dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace"), dict(e.headers)
    except Exception as e:
        return -1, str(e), {}


def js(raw):
    try:
        return json.loads(raw)
    except Exception:
        return None


def rec(sid, desc, expected, status, raw, ok, extra=""):
    RESULTS.append({
        "id": sid, "desc": desc, "expected": expected,
        "measured": f"HTTP {status}" + (f" | {extra}" if extra else ""),
        "status": "PASS" if ok else "FAIL",
        "sample": (raw or "")[:220],
    })
    print(f"[{'PASS' if ok else 'FAIL'}] {sid} {desc} -> {status} {extra}")
    return ok


def login(local):
    st, raw, _ = call("POST", "/api/auth/login", body={"email": f"{local}@p123.rc.test", "password": PW})
    d = js(raw) or {}
    return st, d.get("accessToken"), d.get("refreshToken"), raw


T = {}

# ═══════ المرحلة 9 — تسجيل الدخول والهويّة ═══════
for local in ["rc-admin", "rc-hr", "rc-mgr", "rc-emp", "rc-other"]:
    st, tok, rtok, raw = login(local)
    T[local] = tok
    rec(f"S-AUTH-{local}", f"تسجيل دخول {local}", "200 + accessToken", st, raw, st == 200 and bool(tok))
    if tok:
        st2, raw2, _ = call("GET", "/api/auth/me", tok)
        me = js(raw2) or {}
        roles = me.get("roles") or me.get("Roles") or []
        rec(f"S-ME-{local}", f"/auth/me لـ{local}", "200 + الأدوار", st2, raw2, st2 == 200,
            f"roles={roles}")

st, raw, _ = call("POST", "/api/auth/login", body={"email": "rc-admin@p123.rc.test", "password": "WrongPassword#1"})
rec("S-AUTH-BAD", "دخول بكلمة مرور خاطئة", "401", st, raw, st == 401)

st, raw, _ = call("GET", "/api/auth/me")
rec("S-AUTH-ANON", "/auth/me بلا رمز", "401", st, raw, st == 401)

# ═══════ المرحلة 10 (أ) — المنع الافتراضيّ قبل أيّ منح ═══════
for local, role in [("rc-admin", "Admin"), ("rc-hr", "HR"), ("rc-mgr", "Manager"), ("rc-emp", "Employee")]:
    st, raw, _ = call("GET", "/api/hr-operations/dashboard", T[local])
    rec(f"S-PERM-DENY-{local}", f"لوحة HR قبل المنح ({role})", "403 منع افتراضيّ", st, raw, st == 403)

st, raw, _ = call("GET", "/api/directory/hr/users", T["rc-admin"])
rec("S-PERM-HRDIR-ADMIN", "دليل HR للمدير (سياسة دور لا مطالبة)", "200", st, raw, st == 200)

# ═══════ بنية تنظيميّة اصطناعيّة عبر مسار المنتج ═══════
st, raw, _ = call("POST", "/api/directory/departments", T["rc-admin"], {
    "nameAr": "RC-P123-إدارة اصطناعيّة", "nameEn": "RC-P123 Synthetic Dept",
    "code": "RC-P123-DEP", "managerId": IDS["rc-mgr"]})
dep = js(raw) or {}
DEP_ID = dep.get("id") or dep.get("Id")
if DEP_ID:
    CREATED["departments"].append(DEP_ID)
rec("S-DIR-DEP-CREATE", "إنشاء إدارة اصطناعيّة", "200/201", st, raw, st in (200, 201) and bool(DEP_ID))

# تفرّد الاسم — 409 ProblemDetails
st, raw, hdr = call("POST", "/api/directory/departments", T["rc-admin"], {
    "nameAr": "RC-P123-إدارة اصطناعيّة", "nameEn": "dup", "code": "RC-P123-DEP2", "managerId": None})
pd = js(raw) or {}
ok = st == 409 and "application/problem+json" in (hdr.get("Content-Type", "")) and "title" in pd
rec("S-DIR-UNIQ-NAME", "تكرار اسم إدارة", "409 + ProblemDetails", st, raw, ok,
    f"ct={hdr.get('Content-Type','')}")

# تفرّد الرمز
st, raw, hdr = call("POST", "/api/directory/departments", T["rc-admin"], {
    "nameAr": "RC-P123-إدارة اصطناعيّة ٢", "nameEn": "dup code", "code": "RC-P123-DEP", "managerId": None})
ok = st == 409 and "application/problem+json" in (hdr.get("Content-Type", ""))
rec("S-DIR-UNIQ-CODE", "تكرار رمز إدارة", "409 + ProblemDetails", st, raw, ok)

st, raw, _ = call("POST", "/api/directory/teams", T["rc-admin"], {
    "nameAr": "RC-P123-فريق اصطناعيّ", "nameEn": "RC-P123 Synthetic Team",
    "departmentId": DEP_ID, "teamLeaderId": IDS["rc-mgr"]})
tm = js(raw) or {}
TEAM_ID = tm.get("id") or tm.get("Id")
if TEAM_ID:
    CREATED["teams"].append(TEAM_ID)
rec("S-DIR-TEAM-CREATE", "إنشاء فريق اصطناعيّ", "200/201", st, raw, st in (200, 201) and bool(TEAM_ID))

st, raw, hdr = call("POST", "/api/directory/teams", T["rc-admin"], {
    "nameAr": "RC-P123-فريق اصطناعيّ", "nameEn": "dup", "departmentId": DEP_ID, "teamLeaderId": None})
ok = st == 409 and "application/problem+json" in (hdr.get("Content-Type", ""))
rec("S-DIR-UNIQ-TEAM", "تكرار اسم فريق داخل الإدارة", "409 + ProblemDetails", st, raw, ok)

for local in ["rc-mgr", "rc-emp", "rc-other"]:
    st, raw, _ = call("PATCH", f"/api/directory/users/{IDS[local]}/org-assignment", T["rc-admin"], {
        "departmentId": DEP_ID, "teamId": TEAM_ID,
        "managerId": None if local == "rc-mgr" else IDS["rc-mgr"],
        "notes": "RC-P123 synthetic"})
    rec(f"S-DIR-ORG-{local}", f"إسناد تنظيميّ لـ{local}", "200/204", st, raw, st in (200, 204))

# إعادة تسجيل الدخول بعد تغيّر الإسناد
for local in ["rc-mgr", "rc-emp", "rc-other"]:
    _, T[local], _, _ = login(local)

# ═══════ المرحلة 10 (ب) — المنح عبر مسار المنتج ═══════
HR_KEYS = ["HrOperations.View", "Attendance.Review"]
st, raw, _ = call("PUT", f"/api/directory/users/{IDS['rc-hr']}/permissions", T["rc-admin"],
                  {"permissions": HR_KEYS})
rec("S-PERM-GRANT", "منح المفاتيح الأدنى لـrc-hr", "200/204", st, raw, st in (200, 204))

st, raw, _ = call("GET", f"/api/directory/users/{IDS['rc-hr']}/permissions", T["rc-admin"])
got = sorted((js(raw) or {}).get("permissions", []))
rec("S-PERM-READBACK", "قراءة صلاحيات rc-hr", "المفتاحان فقط", st, raw,
    st == 200 and got == sorted(HR_KEYS), f"perms={got}")

# الرمز القديم لا يزال بلا مطالبة (المطالبات في الرمز لا في القاعدة)
st, raw, _ = call("GET", "/api/hr-operations/dashboard", T["rc-hr"])
rec("S-PERM-OLDTOKEN", "الرمز القديم بعد المنح", "403 حتّى إعادة الدخول", st, raw, st == 403)

_, T["rc-hr"], RT_HR, _ = login("rc-hr")
st, raw, _ = call("GET", "/api/hr-operations/dashboard", T["rc-hr"])
rec("S-PERM-ALLOW", "لوحة HR بعد إعادة الدخول", "200", st, raw, st == 200)

st, raw, _ = call("GET", "/api/hr-operations/queues/attendance-awaiting-hr", T["rc-hr"])
rec("S-PERM-QUEUE", "طابور HR بمفتاح الرؤية", "200", st, raw, st == 200)

# التصدير مفتاح مستقلّ — لم يُمنَح
st, raw, _ = call("GET", "/api/hr-operations/queues/attendance-awaiting-hr/export", T["rc-hr"])
rec("S-PERM-EXPORT-DENY", "تصدير HR بلا مفتاح التصدير", "403 (فصل المفاتيح)", st, raw, st == 403)

# مستخدم غير مصرَّح
st, raw, _ = call("GET", "/api/hr-operations/dashboard", T["rc-emp"])
rec("S-PERM-UNAUTH", "لوحة HR لموظّف بلا مفاتيح", "403", st, raw, st == 403)

# ═══════ المرحلة 9 — مسار الحضور الكامل ═══════
st, raw, _ = call("GET", "/api/attendance/types", T["rc-mgr"])
types = js(raw) or []
if not isinstance(types, list):
    types = []
rec("S-ATT-TYPES", "أنواع الوقائع", "200 + قائمة", st, raw, st == 200 and len(types) > 0,
    f"count={len(types)}")
late = next((t for t in types if t.get("requiresTimes")), (types[0] if types else None))
TYPE_ID = late.get("id") if late else None
TYPE_CODE = late.get("code") if late else None

INC_DATE = (datetime.date.today() - datetime.timedelta(days=2)).isoformat()
st, raw, _ = call("POST", "/api/attendance", T["rc-mgr"], {
    "subjectUserId": IDS["rc-emp"], "incidentTypeId": TYPE_ID, "incidentDate": INC_DATE,
    "startTime": "09:35:00", "returnTime": "10:05:00",
    "description": "RC-P123 واقعة تأخّر اصطناعيّة للتحقّق فقط", "submitImmediately": False})
inc = js(raw) or {}
INC_ID = inc.get("id") or inc.get("Id")
if INC_ID:
    CREATED["incidents"].append(INC_ID)
rec("S-ATT-DRAFT", f"إنشاء مسودّة واقعة ({TYPE_CODE})", "200/201 + Draft", st, raw,
    st in (200, 201) and bool(INC_ID), f"status={inc.get('status')}")

# إخفاء المسودّة عن الموظّف
st, raw, _ = call("GET", "/api/attendance", T["rc-emp"])
lst = js(raw) or {}
items = lst.get("items", []) if isinstance(lst, dict) else []
seen = any((i.get("id") == INC_ID) for i in items)
rec("S-ATT-DRAFT-HIDDEN", "المسودّة مخفيّة عن الموظّف", "غير مدرجة", st, raw,
    st == 200 and not seen, f"items={len(items)} seen={seen}")

st, raw, _ = call("GET", f"/api/attendance/{INC_ID}", T["rc-emp"])
rec("S-ATT-DRAFT-DIRECT", "وصول مباشر للمسودّة من الموظّف", "403/404", st, raw, st in (403, 404))

st, raw, _ = call("POST", f"/api/attendance/{INC_ID}/submit", T["rc-mgr"], {})
d = js(raw) or {}
rec("S-ATT-SUBMIT", "إرسال البلاغ", "200 + AwaitingEmployee", st, raw, st == 200,
    f"status={d.get('status')}")

st, raw, _ = call("GET", "/api/attendance", T["rc-emp"])
lst = js(raw) or {}
items = lst.get("items", []) if isinstance(lst, dict) else []
seen = any((i.get("id") == INC_ID) for i in items)
rec("S-ATT-VISIBLE", "ظهور البلاغ للموظّف بعد الإرسال", "مدرَج", st, raw, st == 200 and seen)

st, raw, _ = call("GET", f"/api/attendance/{INC_ID}", T["rc-emp"])
det = js(raw) or {}
CS = det.get("concurrencyStamp", 0)
rec("S-ATT-DETAIL-EMP", "تفاصيل الواقعة للموظّف", "200 بلا hrNote", st, raw,
    st == 200 and "hrNote" not in det, f"keys_hrNote={'hrNote' in det}")

st, raw, _ = call("POST", f"/api/attendance/{INC_ID}/acknowledge", T["rc-emp"],
                  {"response": "RC-P123 إقرار اصطناعيّ", "concurrencyStamp": CS})
d = js(raw) or {}
rec("S-ATT-ACK", "إقرار الموظّف", "200 + AwaitingHr", st, raw, st == 200, f"status={d.get('status')}")

# عزل النطاق: rc-other لا يرى واقعة rc-emp
st, raw, _ = call("GET", f"/api/attendance/{INC_ID}", T["rc-other"])
rec("S-IDOR-ATT", "موظّف آخر يطلب الواقعة", "403/404", st, raw, st in (403, 404))

# مراجعة HR بلا مفتاح ثمّ به
st, raw, _ = call("GET", f"/api/attendance/{INC_ID}", T["rc-hr"])
det = js(raw) or {}
CS = det.get("concurrencyStamp", CS)
st, raw, _ = call("POST", f"/api/attendance/{INC_ID}/hr-review", T["rc-emp"],
                  {"decision": 1, "note": "x", "concurrencyStamp": CS})
rec("S-ATT-HRREVIEW-DENY", "مراجعة HR من موظّف بلا مفتاح", "403", st, raw, st == 403)

st, raw, _ = call("POST", f"/api/attendance/{INC_ID}/hr-review", T["rc-hr"],
                  {"decision": 1, "note": "RC-P123 تأكيد اصطناعيّ", "concurrencyStamp": CS})
d = js(raw) or {}
rec("S-ATT-HRREVIEW", "تأكيد HR بمفتاح Attendance.Review", "200 + Confirmed", st, raw,
    st == 200, f"status={d.get('status')}")

st, raw, _ = call("GET", f"/api/attendance/{INC_ID}", T["rc-hr"])
det = js(raw) or {}
CS = det.get("concurrencyStamp", CS)
st, raw, _ = call("POST", f"/api/attendance/{INC_ID}/close", T["rc-hr"],
                  {"reason": "RC-P123 إغلاق اصطناعيّ", "concurrencyStamp": CS})
d = js(raw) or {}
rec("S-ATT-CLOSE", "إغلاق الواقعة", "200 + Closed", st, raw, st == 200, f"status={d.get('status')}")

st, raw, _ = call("GET", f"/api/attendance/{INC_ID}/events", T["rc-hr"])
ev = js(raw) or []
rec("S-ATT-EVENTS", "سجلّ أحداث الواقعة", "≥4 أحداث", st, raw, st == 200 and len(ev) >= 4,
    f"events={len(ev)}")

# ═══════ Employee 360 وقائمة الالتزام ═══════
st, raw, _ = call("GET", "/api/employees/me/profile-360", T["rc-emp"])
rec("S-360-ME", "ملفّ 360 الشخصيّ", "200", st, raw, st == 200)

st, raw, _ = call("GET", f"/api/employees/{IDS['rc-other']}/profile-360", T["rc-emp"])
rec("S-360-IDOR", "ملفّ 360 لموظّف آخر", "403/404", st, raw, st in (403, 404))

st, raw, _ = call("GET", f"/api/employees/{IDS['rc-emp']}/profile-360", T["rc-mgr"])
rec("S-360-MGR", "ملفّ 360 لمرؤوس من المدير", "200", st, raw, st == 200)

st, raw, _ = call("GET", "/api/employees/me/checklist", T["rc-emp"])
rec("S-CHK-ME", "قائمة الالتزام الشخصيّة", "200", st, raw, st == 200)

st, raw, _ = call("PUT", f"/api/employees/{IDS['rc-emp']}/checklist/PersonalPhoto", T["rc-mgr"],
                  {"isDone": True, "note": "RC-P123"})
rec("S-CHK-MANAGE-DENY", "تحرير بند الالتزام بلا مفتاح", "403", st, raw, st == 403)

# ═══════ التقارير وKPI والأرصدة والطلبات ═══════
for sid, desc, path, tok, exp in [
    ("S-RPT-TPL", "قوالب التقارير", "/api/report-templates", T["rc-emp"], (200,)),
    ("S-RPT-MINE", "تسليماتي", "/api/submissions/mine", T["rc-emp"], (200,)),
    ("S-RPT-DUE", "حالة استحقاقي", "/api/reports/due/my-status", T["rc-emp"], (200,)),
    ("S-RPT-PENDING", "بانتظار اعتمادي (مدير)", "/api/submissions/pending-approvals", T["rc-mgr"], (200,)),
    ("S-KPI-AGG", "تجميع KPI", "/api/kpi-evaluations/aggregate", T["rc-mgr"], (200,)),
    ("S-KPI-PERF", "أداء KPI", "/api/kpi/performance", T["rc-mgr"], (200, 400)),
    ("S-BAL-ME", "أرصدتي", "/api/me/balances", T["rc-emp"], (200,)),
    ("S-LEAVE-MY", "طلبات إجازتي", "/api/leave-requests/my", T["rc-emp"], (200,)),
    ("S-LEAVE-PEND", "إجازات بانتظار المدير", "/api/leave-requests/pending", T["rc-mgr"], (200,)),
    ("S-ESR-MY", "طلباتي الخدميّة", "/api/employee-service-requests/my", T["rc-emp"], (200,)),
    ("S-DASH-ME", "لوحتي", "/api/dashboard/me", T["rc-emp"], (200,)),
    ("S-DASH-MGR", "لوحة المدير", "/api/dashboard/me", T["rc-mgr"], (200,)),
]:
    st, raw, _ = call("GET", path, tok)
    rec(sid, desc, "/".join(str(e) for e in exp), st, raw, st in exp)

# لوحة المدير لا تستدعي governance-summary
st, raw, _ = call("GET", "/api/reports/governance-summary", T["rc-mgr"])
rec("S-GOV-DENY", "governance-summary من مدير", "403", st, raw, st == 403)

# عزل نطاق الدليل
st, raw, _ = call("GET", "/api/directory/users", T["rc-emp"])
rec("S-DIR-USERS-EMP", "دليل المستخدمين لموظّف", "200 (بيانات عامّة)", st, raw, st == 200)

st, raw, _ = call("GET", f"/api/directory/users/{IDS['rc-emp']}/permissions", T["rc-emp"])
rec("S-PERM-READ-DENY", "قراءة صلاحيات من موظّف", "403", st, raw, st == 403)

# ═══════ التدقيق ═══════
st, raw, _ = call("GET", "/api/audit?page=1&pageSize=50", T["rc-admin"])
aud = js(raw) or {}
aitems = aud.get("items", []) if isinstance(aud, dict) else (aud if isinstance(aud, list) else [])
hit = [a for a in aitems if "perm" in json.dumps(a, ensure_ascii=False).lower()]
rec("S-AUDIT-PERM", "أثر تدقيق لتغيير الصلاحيات", "قيد يوثّق التغيير", st, raw,
    st == 200 and len(hit) > 0, f"total={len(aitems)} permHits={len(hit)}")

# ═══════ السحب ═══════
st, raw, _ = call("PUT", f"/api/directory/users/{IDS['rc-hr']}/permissions", T["rc-admin"],
                  {"permissions": []})
rec("S-PERM-REVOKE", "سحب كلّ المفاتيح من rc-hr", "200/204", st, raw, st in (200, 204))

_, T["rc-hr"], _, _ = login("rc-hr")
st, raw, _ = call("GET", "/api/hr-operations/dashboard", T["rc-hr"])
rec("S-PERM-REVOKE-EFFECT", "لوحة HR بعد السحب وإعادة الدخول", "403", st, raw, st == 403)

st, raw, _ = call("GET", f"/api/directory/users/{IDS['rc-hr']}/permissions", T["rc-admin"])
got = (js(raw) or {}).get("permissions", [])
rec("S-PERM-REVOKE-READBACK", "قراءة الصلاحيات بعد السحب", "قائمة فارغة", st, raw,
    st == 200 and len(got) == 0, f"perms={got}")

json.dump({"results": RESULTS, "created": CREATED,
           "dep": DEP_ID, "team": TEAM_ID, "incident": INC_ID},
          open("/tmp/p123-rc/phase910-results.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=2)

p = sum(1 for r in RESULTS if r["status"] == "PASS")
print(f"\nTOTAL={len(RESULTS)} PASS={p} FAIL={len(RESULTS)-p}")
for r in RESULTS:
    if r["status"] == "FAIL":
        print("  FAIL:", r["id"], r["desc"], "| expected", r["expected"], "| got", r["measured"], "|", r["sample"][:160])
