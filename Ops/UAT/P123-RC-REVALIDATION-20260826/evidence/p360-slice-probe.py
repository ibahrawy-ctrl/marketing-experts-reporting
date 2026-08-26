#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Phase 9c — تغطية 63b7d42 على RC الحيّ: تنقّل تقارير P360 وعزل شريحة المشروع.
المسار الجديد `GET /api/projects/{id}/reports/{submissionId}`.
قراءة فقط: لا يُنشَأ ولا يُعدَّل ولا يُحذَف أيّ صفّ. الفاعلون اصطناعيّون @p123.rc.test
عدا القراءة الإيجابيّة التي تحتاج صفة تملك رؤية المشروع.
"""
import json, urllib.request, urllib.error

BASE = "http://127.0.0.1:5092"
PW = "RcP123#Synthetic!2026"
IDS = json.load(open("/tmp/p123-rc/synth-ids.json", encoding="utf-8"))["ids"]
PAIRS = json.load(open("/tmp/p123-rc/p360-pairs.json", encoding="utf-8"))
R = []


def call(method, path, token=None):
    req = urllib.request.Request(BASE + path, method=method)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")


def js(raw):
    try:
        return json.loads(raw)
    except Exception:
        return None


def login(local):
    import urllib.request as u
    body = json.dumps({"email": f"{local}@p123.rc.test", "password": PW}).encode()
    req = u.Request(BASE + "/api/auth/login", data=body, method="POST")
    req.add_header("Content-Type", "application/json")
    with u.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())["accessToken"]


def rec(sid, desc, expected, measured, ok):
    R.append({"id": sid, "desc": desc, "expected": expected, "measured": measured,
              "status": "PASS" if ok else "FAIL"})
    print(f"[{'PASS' if ok else 'FAIL'}] {sid} {desc} -> {measured}")
    return ok


T = {k: login(k) for k in ["rc-admin", "rc-hr", "rc-mgr", "rc-emp", "rc-other"]}

SUB = PAIRS["multiProjectSubmission"]      # تسليم واحد يحمل عدّة مشاريع
MINE = PAIRS["projectInSubmission"]         # مشروع داخل ذلك التسليم
SIBLINGS = PAIRS["siblingProjects"]         # بقيّة مشاريع التسليم نفسه
FOREIGN = PAIRS["projectNotInSubmission"]   # مشروع حقيقيّ لا يظهر في ذلك التسليم
GHOST = "00000000-0000-0000-0000-000000000001"

P = f"/api/projects/{MINE}/reports/{SUB}"

# 1) المسار موجود ومفعَّل على المرشّح المنشور (ليس 404 توجيه)
st_anon, raw_anon = call("GET", P)
rec("P360-S1", "المسار الجديد مُفعَّل ويطلب مصادقة", "401 لا 404 توجيه",
    f"HTTP {st_anon}", st_anon == 401)

# 2) القراءة الإيجابيّة — صفة تملك رؤية المشروع
st, raw = call("GET", P, T["rc-admin"])
d = js(raw) or {}
rec("P360-S2", "صفة مخوَّلة تقرأ شريحة المشروع", "200 + شريحة",
    f"HTTP {st} projectId={d.get('projectId')} fields={len(d.get('fields') or [])}",
    st == 200 and d.get("projectId") == MINE)

# 3) العزل الجوهريّ: لا يظهر أيّ مشروع شقيق داخل الحمولة
body_text = raw
leaked = [p for p in SIBLINGS if p in body_text]
rec("P360-S3", "الحمولة لا تحتوي أيّ مشروع شقيق من التسليم نفسه", "صفر تسريب",
    f"siblings={len(SIBLINGS)} leaked={len(leaked)} {leaked[:2]}", not leaked)

# 4) الحمولة الكاملة للتسليم لا تُرسَل: عدد العناصر = عناصر هذا المشروع فقط
entries = sum(len(f.get("entries") or []) for f in (d.get("fields") or []))
rec("P360-S4", "عدد العناصر يساوي عناصر هذا المشروع فقط", f"{PAIRS['expectedEntries']}",
    f"entries={entries}", entries == PAIRS["expectedEntries"])

# 5) اقتران مغلوط: مشروع حقيقيّ + تسليم لا يخصّه ⇒ رفض بلا إفشاء
st, raw = call("GET", f"/api/projects/{FOREIGN}/reports/{SUB}", T["rc-admin"])
rec("P360-S5", "مشروع لا يخصّ التسليم يُرفَض", "404",
    f"HTTP {st}", st == 404)

# 6) IDOR: فاعلون بلا نطاق على المشروع لا يكتشفون شيئًا — ونفس الرمز لا يميّز
codes = {}
for who in ["rc-emp", "rc-other", "rc-mgr", "rc-hr"]:
    st, raw = call("GET", P, T[who])
    codes[who] = st
rec("P360-S6", "فاعلون خارج نطاق المشروع لا يكتشفون الشريحة", "404 للجميع",
    f"{codes}", all(v == 404 for v in codes.values()))

# 7) لا تمييز بين «ممنوع» و«غير موجود» — معرّف وهميّ يعطي نفس الرمز والرسالة
st_ghost, raw_ghost = call("GET", f"/api/projects/{GHOST}/reports/{SUB}", T["rc-emp"])
st_real, raw_real = call("GET", P, T["rc-emp"])
same = (st_ghost == st_real) and ((js(raw_ghost) or {}).get("detail") == (js(raw_real) or {}).get("detail"))
rec("P360-S7", "المعرّف الوهميّ والمشروع الحقيقيّ المحجوب يتطابقان (لا قناة اكتشاف)",
    "نفس الرمز ونفس الرسالة", f"ghost={st_ghost} real={st_real} sameDetail={same}", same)

# 8) تسليم وهميّ على مشروع مخوَّل ⇒ 404 لا 500
st, raw = call("GET", f"/api/projects/{MINE}/reports/{GHOST}", T["rc-admin"])
rec("P360-S8", "تسليم غير موجود يُرفَض بلطف", "404",
    f"HTTP {st}", st == 404)

json.dump(R, open("/tmp/p123-rc/p360-slice-results.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
p = sum(1 for x in R if x["status"] == "PASS")
print(f"\nTOTAL={len(R)} PASS={p} FAIL={len(R)-p}")
