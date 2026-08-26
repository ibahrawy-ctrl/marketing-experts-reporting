#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""تصحيح خطأين في مِشدّ الاختبار (لا في المنتج): مسار التدقيق ومعامل granularity."""
import json, urllib.request, urllib.error
BASE = "http://127.0.0.1:5092"; PW = "RcP123#Synthetic!2026"
IDS = json.load(open("/tmp/p123-rc/synth-ids.json", encoding="utf-8"))["ids"]

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

adm, mgr, emp = login("rc-admin"), login("rc-mgr"), login("rc-emp")
res = []

st, raw = call("GET", "/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-08", mgr)
res.append(("S-KPI-AGG2", "تجميع KPI بمعاملات صحيحة", st, raw[:200]))

st, raw = call("GET", "/api/audit-logs?page=1&pageSize=50", adm)
try:
    a = json.loads(raw); items = a.get("items", a if isinstance(a, list) else [])
except Exception:
    items = []
perm_hits = [i for i in items if "perm" in json.dumps(i, ensure_ascii=False).lower()]
res.append(("S-AUDIT-PERM2", f"سجلّ التدقيق (total={len(items)} permHits={len(perm_hits)})", st, raw[:300]))

st, raw = call("GET", "/api/audit-logs?page=1&pageSize=50", emp)
res.append(("S-AUDIT-DENY", "سجلّ التدقيق من موظّف", st, raw[:120]))

for sid, desc, st, body in res:
    print(f"{sid} | {desc} -> {st}")
    print("   ", body.replace("\n", " ")[:260])

if perm_hits:
    print("\nأمثلة قيود الصلاحيات:")
    for h in perm_hits[:4]:
        print("  -", json.dumps(h, ensure_ascii=False)[:300])
