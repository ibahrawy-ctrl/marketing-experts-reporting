#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import json, urllib.request, urllib.error
B = "http://127.0.0.1:5092"; PW = "RcP123#Synthetic!2026"

def call(p, tok=None, m="GET", body=None):
    d = json.dumps(body).encode() if body is not None else None
    r = urllib.request.Request(B + p, data=d, method=m)
    r.add_header("Content-Type", "application/json")
    if tok: r.add_header("Authorization", "Bearer " + tok)
    try:
        with urllib.request.urlopen(r, timeout=60) as x:
            return x.status, x.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()

st, raw = call("/api/auth/login", m="POST", body={"email": "rc-admin@p123.rc.test", "password": PW})
tok = json.loads(raw)["accessToken"]
st, raw = call("/api/audit-logs?page=1&pageSize=200", tok)
d = json.loads(raw)
items = d if isinstance(d, list) else d.get("items", [])
print("HTTP", st, "count", len(items))
acts = {}
for i in items:
    acts[i.get("action")] = acts.get(i.get("action"), 0) + 1
print("actions:", json.dumps(acts, ensure_ascii=False))
for i in items:
    a = (i.get("action") or "").lower()
    if any(k in a for k in ("perm", "role", "user", "depart", "team")):
        print(" *", i.get("action"), "|", i.get("actorName"), "|", i.get("entityType"),
              "|", (i.get("dataJson") or "")[:200])
