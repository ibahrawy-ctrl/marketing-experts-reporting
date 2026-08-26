#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""إثبات معزول: تسريب المسودّة إلى الموظّف (الموضوع) في قائمة /api/attendance
مقابل حجبها الصحيح في /api/attendance/{id}. RC فقط، بيانات اصطناعيّة فقط."""
import json, urllib.request, urllib.error, datetime

BASE = "http://127.0.0.1:5092"
PW = "RcP123#Synthetic!2026"
IDS = json.load(open("/tmp/p123-rc/synth-ids.json", encoding="utf-8"))["ids"]
OUT = {}

def call(m, p, tok=None, body=None):
    d = json.dumps(body, ensure_ascii=False).encode() if body is not None else None
    r = urllib.request.Request(BASE + p, data=d, method=m)
    r.add_header("Content-Type", "application/json")
    if tok: r.add_header("Authorization", "Bearer " + tok)
    try:
        with urllib.request.urlopen(r, timeout=60) as x:
            return x.status, x.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")

def login(l):
    st, raw = call("POST", "/api/auth/login", body={"email": f"{l}@p123.rc.test", "password": PW})
    return json.loads(raw)["accessToken"]

mgr, emp = login("rc-mgr"), login("rc-emp")
st, raw = call("GET", "/api/attendance/types", mgr)
tid = next(t["id"] for t in json.loads(raw) if t.get("requiresTimes"))

st, raw = call("POST", "/api/attendance", mgr, {
    "subjectUserId": IDS["rc-emp"], "incidentTypeId": tid,
    "incidentDate": (datetime.date.today() - datetime.timedelta(days=3)).isoformat(),
    "startTime": "09:40:00", "returnTime": "10:10:00",
    "description": "RC-P123 إعادة إنتاج معزولة — مسودّة لم تُرسَل قطّ",
    "submitImmediately": False})
inc = json.loads(raw)
IID, CS = inc["id"], inc.get("concurrencyStamp", 0)
OUT["created_status"] = inc.get("status")
print("CREATED", st, IID, "status =", inc.get("status"))

st_d, raw_d = call("GET", f"/api/attendance/{IID}", emp)
OUT["detail_as_subject"] = {"status": st_d, "body": raw_d[:300]}
print("DETAIL as subject ->", st_d, raw_d[:160])

st_l, raw_l = call("GET", "/api/attendance", emp)
lst = json.loads(raw_l)
item = next((i for i in lst.get("items", []) if i["id"] == IID), None)
OUT["list_as_subject"] = {"status": st_l, "total": lst.get("totalCount"), "leaked_item": item}
print("LIST as subject ->", st_l, "leaked =", item is not None)
if item:
    print(json.dumps(item, ensure_ascii=False, indent=2))

st_o, raw_o = call("GET", "/api/attendance", login("rc-other"))
lo = json.loads(raw_o)
OUT["list_as_other"] = {"status": st_o,
                        "leaked": any(i["id"] == IID for i in lo.get("items", []))}
print("LIST as unrelated employee -> leaked =", OUT["list_as_other"]["leaked"])

# تنظيف فوريّ: إلغاء/حذف المسودّة
st_c, raw_c = call("DELETE", f"/api/attendance/{IID}?concurrencyStamp={CS}", mgr)
print("CLEANUP DELETE ->", st_c, raw_c[:160])
OUT["cleanup"] = {"status": st_c, "body": raw_c[:200], "incidentId": IID}

json.dump(OUT, open("/tmp/p123-rc/repro-draft-leak.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=2)
