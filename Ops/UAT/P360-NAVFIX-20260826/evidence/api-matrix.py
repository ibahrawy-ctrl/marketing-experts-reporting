#!/usr/bin/env python3
"""مصفوفة قياس خادميّة للشريحة المقيَّدة بالمشروع — تُقرأ من نصّ الاستجابة الخام لا من كائن مُفكَّك."""
import json
import sys
import urllib.request
import urllib.error
import uuid

BASE = "https://test.emarketingacademy.net/api"
PW = open("/tmp/p360nav/.pw").read().strip()
F = json.load(open("/tmp/p360nav/fixture.json"))
A, B, SUB = F["projectA"], F["projectB"], F["submissionId"]
MA, MB, GN = F["markers"]["A"], F["markers"]["B"], F["markers"]["general"]


def get(path, token):
    req = urllib.request.Request(BASE + path, method="GET")
    req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def login(email):
    body = json.dumps({"email": email, "password": PW}).encode()
    req = urllib.request.Request(BASE + "/auth/login", data=body, method="POST")
    req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())["accessToken"]


toks = {k: login(v["email"]) for k, v in F["users"].items()}
rows = []


def check(cid, desc, cond, detail=""):
    rows.append({"id": cid, "desc": desc, "result": "PASS" if cond else "FAIL", "detail": detail})
    print(f"{'PASS' if cond else 'FAIL'}  {cid:14s} {desc[:56]:56s} {detail[:70]}")


for actor in ("admin", "acctmgr", "owner", "lead", "emp"):
    st, raw = get(f"/projects/{A}/reports/{SUB}", toks[actor])
    check(f"SLICE-A-{actor}", f"{actor}: شريحة أ = 200 وتحوي {MA} فقط",
          st == 200 and MA in raw and MB not in raw and B not in raw and GN not in raw,
          f"http={st} MA={MA in raw} MB={MB in raw} B_id={B in raw} general={GN in raw}")
    st, raw = get(f"/projects/{B}/reports/{SUB}", toks[actor])
    check(f"SLICE-B-{actor}", f"{actor}: شريحة ب = 200 وتحوي {MB} فقط",
          st == 200 and MB in raw and MA not in raw and A not in raw and GN not in raw,
          f"http={st} MB={MB in raw} MA={MA in raw} A_id={A in raw} general={GN in raw}")

st, raw = get(f"/projects/{A}/reports/{SUB}", toks["outsider"])
check("SLICE-A-outsider", "خارج النطاق: 404 بلا أيّ بصمة", st == 404 and MA not in raw and MB not in raw, f"http={st}")
st, raw2 = get(f"/projects/{B}/reports/{SUB}", toks["outsider"])
check("SLICE-B-outsider", "خارج النطاق على ب: 404", st == 404, f"http={st}")

st1, r1 = get(f"/projects/{uuid.uuid4()}/reports/{SUB}", toks["emp"])
st2, r2 = get(f"/projects/{A}/reports/{uuid.uuid4()}", toks["emp"])


def fp(txt):
    try:
        d = json.loads(txt)
        return f'{d.get("title")}|{d.get("detail")}|{d.get("type")}'
    except Exception:
        return txt[:80]


check("TAMPER-IDS", "العبث بالمعرّفين: 404/404 وبصمة رفض واحدة",
      st1 == 404 and st2 == 404 and fp(r1) == fp(r2), f"{st1}/{st2} fp={fp(r1)}")

st, raw = get(f"/submissions/{SUB}", toks["outsider"])
check("GENERAL-POLICY", "المسار العامّ للتسليم بقي رافضًا لخارج النطاق", st in (403, 404), f"http={st}")

st, raw = get(f"/submissions/{SUB}", toks["emp"])
check("GENERAL-OWNER", "صاحب التقرير ما زال يرى تقريره الكامل (لا تضييق غير مقصود)",
      st == 200 and MA in raw and MB in raw, f"http={st}")

st, raw = get(f"/projects/{A}/reports", toks["emp"])
check("LIST-A", "قائمة تقارير مشروع أ تُدرِج التسليم", st == 200 and SUB in raw, f"http={st}")

json.dump(rows, open("/tmp/p360nav/api-matrix.json", "w"), ensure_ascii=False, indent=1)
fails = [r for r in rows if r["result"] == "FAIL"]
print(f"\nTOTAL={len(rows)} PASS={len(rows)-len(fails)} FAIL={len(fails)}")
sys.exit(1 if fails else 0)
