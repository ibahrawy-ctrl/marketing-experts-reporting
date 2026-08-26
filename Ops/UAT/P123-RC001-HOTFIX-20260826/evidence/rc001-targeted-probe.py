#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""DEF-P123-RC-001 — مِشدّ موجَّه على RC الحيّ (127.0.0.1:5092) يغطّي ما لم تغطّه حزمة phase910:
العدّاد، والملغاة السابقة للإرسال، والترقيم، ورؤية المراجع المخوّل، وخارج النطاق.
مستخدمون اصطناعيّون @p123.rc.test حصرًا، وكلّ ما يُنشأ يُحذف في نهاية التشغيل."""
import json, urllib.request, urllib.error, datetime

BASE = "http://127.0.0.1:5092"
PW = "RcP123#Synthetic!2026"
IDS = json.load(open("/tmp/p123-rc/synth-ids.json", encoding="utf-8"))["ids"]
R = []


def call(method, path, token=None, body=None):
    data = json.dumps(body, ensure_ascii=False).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
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
    st, raw = call("POST", "/api/auth/login", body={"email": f"{local}@p123.rc.test", "password": PW})
    return (js(raw) or {}).get("accessToken")


def rec(sid, desc, expected, measured, ok):
    R.append({"id": sid, "desc": desc, "expected": expected, "measured": measured,
              "status": "PASS" if ok else "FAIL"})
    print(f"[{'PASS' if ok else 'FAIL'}] {sid} {desc} -> {measured}")
    return ok


def listing(token, query=""):
    st, raw = call("GET", "/api/attendance" + query, token)
    d = js(raw) or {}
    items = d.get("items", []) if isinstance(d, dict) else []
    return st, [i.get("id") for i in items], d.get("totalCount")


T = {k: login(k) for k in ["rc-admin", "rc-hr", "rc-mgr", "rc-emp", "rc-other"]}

st, raw = call("GET", "/api/attendance/types", T["rc-mgr"])
types = js(raw) or []
# النوع الذي يشترط التوقيتَين هو ما يقبله عقد الإنشاء بحقلَي startTime/returnTime.
type_id = next(t["id"] for t in types if t.get("requiresTimes"))

created = []
stamps = {}


def report(day_offset, submit):
    # اسم الحقل في العقد `incidentTypeId` لا `typeId`؛ خطؤه يُنتج 404 «نوع الحادثة غير معروف»
    # فتبدو كلّ فحوص الحجب ناجحةً زورًا لأنّ شيئًا لم يُنشَأ أصلًا.
    body = {"subjectUserId": IDS["rc-emp"], "incidentTypeId": type_id,
            "incidentDate": (datetime.date.today() - datetime.timedelta(days=day_offset)).isoformat(),
            "startTime": "09:40:00", "returnTime": "10:10:00",
            "description": "RC-P123-RC001 اصطناعيّ", "submitImmediately": submit}
    st, raw = call("POST", "/api/attendance", T["rc-mgr"], body)
    d = js(raw) or {}
    if not d.get("id"):
        raise SystemExit(f"CREATE FAILED http={st} body={raw[:300]}")
    created.append(d["id"])
    stamps[d["id"]] = d.get("concurrencyStamp", 0)
    return d["id"], d.get("status")


# 1) مسودّة — العدّاد لا يُفشيها
st0, ids0, total0 = listing(T["rc-emp"])
draft_id, draft_status = report(2, False)
st1, ids1, total1 = listing(T["rc-emp"])
rec("RC001-S1", "المسودّة لا تدخل items للموضوع", "غائبة",
    f"status={draft_status} · seen={draft_id in ids1}", draft_id not in ids1)
rec("RC001-S2", "totalCount لا يزيد بالمسودّة", f"{total0}",
    f"before={total0} after={total1}", total0 == total1)
st, _ = call("GET", f"/api/attendance/{draft_id}", T["rc-emp"])
rec("RC001-S3", "التفاصيل تردّ 404 للموضوع", "404", f"HTTP {st}", st == 404)

# 2) المُبلِّغ يرى مسودّته
_, rep_ids, _ = listing(T["rc-mgr"], f"?subjectUserId={IDS['rc-emp']}")
rec("RC001-S4", "المُبلِّغ يرى مسودّته في القائمة", "حاضرة",
    f"seen={draft_id in rep_ids}", draft_id in rep_ids)

# 3) خارج النطاق لا يكتشفها
st, _ = call("GET", f"/api/attendance/{draft_id}", T["rc-other"])
_, oth_ids, _ = listing(T["rc-other"])
rec("RC001-S5", "غير المرتبط لا يكتشف المسودّة", "404 + غائبة",
    f"detail={st} inList={draft_id in oth_ids}", st == 404 and draft_id not in oth_ids)

# 4) الترقيم لا يُسرِّبها
_, p_ids, p_total = listing(T["rc-emp"], "?page=1&pageSize=100")
rec("RC001-S6", "الترقيم بحجم كبير لا يُسرِّب المسودّة", "غائبة",
    f"pageSize=100 seen={draft_id in p_ids} totalCount={p_total}", draft_id not in p_ids)

# 5) الإلغاء يبقيها محجوبة
# عقد الإلغاء `DELETE /api/attendance/{id}?concurrencyStamp=N` لا `POST .../cancel` (الأخير 404
# فيبدو S7 ناجحًا زورًا وهو يفحص مسودّة لا ملغاة). لذلك نتحقّق أوّلًا أنّ الحالة صارت Cancelled فعلًا.
st, raw = call("DELETE", f"/api/attendance/{draft_id}?concurrencyStamp={stamps[draft_id]}", T["rc-mgr"])
_, rawd = call("GET", f"/api/attendance/{draft_id}", T["rc-mgr"])
cancelled_status = (js(rawd) or {}).get("status")
_, c_ids, c_total = listing(T["rc-emp"])
st2, _ = call("GET", f"/api/attendance/{draft_id}", T["rc-emp"])
rec("RC001-S7", "الملغاة السابقة للإرسال تبقى محجوبة عن الموضوع", "Cancelled + غائبة + 404",
    f"cancel={st} status={cancelled_status} inList={draft_id in c_ids} detail={st2}",
    cancelled_status == "Cancelled" and draft_id not in c_ids and st2 == 404)

# 6) بعد الإرسال يراها الموضوع
sub_id, sub_status = report(3, True)
_, s_ids, s_total = listing(T["rc-emp"])
st, _ = call("GET", f"/api/attendance/{sub_id}", T["rc-emp"])
rec("RC001-S8", "بعد الإرسال يراها الموضوع في القائمة والتفاصيل", "حاضرة + 200",
    f"status={sub_status} inList={sub_id in s_ids} detail={st}",
    sub_id in s_ids and st == 200 and sub_status not in ("Draft", "Cancelled"))

# 7) المراجع المخوّل يرى المسودّة داخل نطاقه
call("PUT", f"/api/directory/users/{IDS['rc-hr']}/permissions", T["rc-admin"],
     {"permissions": ["Attendance.Review"]})
hr = login("rc-hr")
d2_id, _ = report(4, False)
_, hr_ids, _ = listing(hr, f"?subjectUserId={IDS['rc-emp']}")
st, _ = call("GET", f"/api/attendance/{d2_id}", hr)
rec("RC001-S9", "المراجع بمفتاح صريح يرى المسودّة", "حاضرة + 200",
    f"inList={d2_id in hr_ids} detail={st}", d2_id in hr_ids and st == 200)

# 8) الثابت: القائمة ⊆ التفاصيل لكلّ صفة
drift = []
for name, tok in [("الموضوع", T["rc-emp"]), ("المُبلِّغ", T["rc-mgr"]),
                  ("المراجع", hr), ("غريب", T["rc-other"])]:
    _, seen, _ = listing(tok, f"?subjectUserId={IDS['rc-emp']}&pageSize=100")
    for iid in created:
        if iid in seen:
            st, _ = call("GET", f"/api/attendance/{iid}", tok)
            if st != 200:
                drift.append(f"{name}:{iid}:{st}")
rec("RC001-S10", "الثابت القائمة ⊆ التفاصيل على كلّ الصفات", "صفر انحراف",
    f"drift={len(drift)} {drift[:3]}", not drift)

call("PUT", f"/api/directory/users/{IDS['rc-hr']}/permissions", T["rc-admin"], {"permissions": []})

json.dump(R, open("/tmp/p123-rc/rc001-targeted-results.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
p = sum(1 for x in R if x["status"] == "PASS")
print(f"\nTOTAL={len(R)} PASS={p} FAIL={len(R)-p}")
print("CREATED_INCIDENTS=" + json.dumps(created))
