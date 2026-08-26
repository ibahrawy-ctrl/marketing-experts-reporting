#!/usr/bin/env python3
"""مصفوفة تحقّق API متعدّدة الأدوار — §8 الاكتشاف · §9 عقد الشريحة · §10 النطاق و404.

لا تُطبع كلمات مرور ولا رموز وصول. المخرج: /tmp/p360r2/api-matrix.json
"""
import json
import sys
import urllib.request
import urllib.error

BASE = "https://test.emarketingacademy.net/api"
PW = open("/tmp/p360r2/.pw").read().strip()
F = json.load(open("/tmp/p360r2/fixture.json", encoding="utf-8"))
A, B, SUB = F["projectA"], F["projectB"], F["submissionId"]
TAMPERED = "00000000-0000-0000-0000-0000000000ff"
MA, MB = F["markers"]["A"], F["markers"]["B"]
GN = F["generalNote"]

results = []


def call(method, path, token=None, body=None):
    data = json.dumps(body, ensure_ascii=False).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            txt = r.read().decode()
            return r.status, (json.loads(txt) if txt else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw[:300]


def rec(cid, role, desc, ok, detail):
    results.append({"id": cid, "role": role, "desc": desc,
                    "result": "PASS" if ok else "FAIL", "detail": str(detail)[:300]})
    print(f"{'PASS' if ok else 'FAIL'}  {cid:<26} {role:<10} {str(detail)[:80]}")


def login(email):
    st, d = call("POST", "/auth/login", body={"email": email, "password": PW})
    if st >= 300:
        sys.exit(f"FAIL login {email}: {st}")
    return d["accessToken"]


def rows_of(payload):
    if isinstance(payload, dict):
        for k in ("items", "reports", "data"):
            if k in payload and isinstance(payload[k], list):
                return payload[k]
        return []
    return payload if isinstance(payload, list) else []


TOK = {k: login(v["email"]) for k, v in F["users"].items()}

INSIDE = ["admin", "acctmgr", "owner", "lead", "emp"]

for key in INSIDE:
    t = TOK[key]
    # §8 — الاكتشاف: التقرير يظهر في المشروعين معًا (A بالربط العلويّ، B بالربط المتداخل فقط)
    for label, pid in (("A", A), ("B", B)):
        st, d = call("GET", f"/projects/{pid}/reports", t)
        rows = rows_of(d)
        ids = [str(r.get("submissionId") or r.get("id")).lower() for r in rows]
        hits = ids.count(SUB.lower())
        rec(f"DISCOVERY-{label}-{key}", key,
            f"التقرير يظهر في قائمة تقارير مشروع {label} مرّة واحدة بلا تكرار",
            st == 200 and hits == 1, f"http={st} rows={len(rows)} hits={hits}")

    # §9 — عقد الشريحة: بنود مشروع A فقط، ولا تسريب لعلامات B ولا للملخّص العامّ
    st, d = call("GET", f"/projects/{A}/reports/{SUB}", t)
    blob = json.dumps(d, ensure_ascii=False)
    a_hits = sum(1 for m in MA if m in blob)
    b_hits = sum(1 for m in MB if m in blob)
    rec(f"SLICE-A-{key}", key, "شريحة مشروع A تُظهر بنوده الثلاثة فقط",
        st == 200 and a_hits == 3 and b_hits == 0 and GN not in blob,
        f"http={st} A={a_hits}/3 B={b_hits}/0 general_leak={GN in blob}")

    st, d = call("GET", f"/projects/{B}/reports/{SUB}", t)
    blob = json.dumps(d, ensure_ascii=False)
    a_hits = sum(1 for m in MA if m in blob)
    b_hits = sum(1 for m in MB if m in blob)
    rec(f"SLICE-B-{key}", key, "شريحة مشروع B تُظهر بنديه فقط",
        st == 200 and b_hits == 2 and a_hits == 0 and GN not in blob,
        f"http={st} B={b_hits}/2 A={a_hits}/0 general_leak={GN in blob}")

    # §10 — عبث بالمعرّف ⇒ 404 موحّد
    st, d = call("GET", f"/projects/{TAMPERED}/reports/{SUB}", t)
    rec(f"TAMPER-PROJECT-{key}", key, "معرّف مشروع مُعبَث به يُرجِع 404", st == 404, f"http={st}")
    st, d = call("GET", f"/projects/{A}/reports/{TAMPERED}", t)
    rec(f"TAMPER-SUBMISSION-{key}", key, "معرّف تسليم مُعبَث به يُرجِع 404", st == 404, f"http={st}")

    # §8 — الملخّص يحتسب التقرير المرتبط بالتداخل
    st, d = call("GET", f"/projects/{B}/summary", t)
    total = None
    if isinstance(d, dict):
        for k in ("totalReports", "reportsCount", "submissionsCount", "total"):
            if k in d:
                total = d[k]
                break
    rec(f"SUMMARY-B-{key}", key, "ملخّص مشروع B يحتسب التقرير المرتبط بالتداخل",
        st == 200 and (total is None or total >= 1), f"http={st} total={total} keys={list(d)[:8] if isinstance(d, dict) else None}")

# §10 — خارج النطاق: لا قائمة ولا شريحة
t = TOK["outsider"]
for label, pid in (("A", A), ("B", B)):
    st, d = call("GET", f"/projects/{pid}/reports", t)
    rec(f"OUTSIDER-LIST-{label}", "outsider", f"خارج النطاق لا يرى قائمة تقارير مشروع {label}",
        st in (403, 404), f"http={st}")
    st, d = call("GET", f"/projects/{pid}/reports/{SUB}", t)
    rec(f"OUTSIDER-SLICE-{label}", "outsider", f"خارج النطاق لا يرى شريحة مشروع {label}",
        st in (403, 404), f"http={st}")

# بلا مصادقة إطلاقًا
st, d = call("GET", f"/projects/{A}/reports/{SUB}")
rec("ANON-SLICE", "anon", "طلب بلا مصادقة يُرفَض", st == 401, f"http={st}")

fails = [r for r in results if r["result"] == "FAIL"]
json.dump({"total": len(results), "pass": len(results) - len(fails), "fail": len(fails),
           "cases": results}, open("/tmp/p360r2/api-matrix.json", "w"), ensure_ascii=False, indent=1)
print(f"\nTOTAL={len(results)} PASS={len(results)-len(fails)} FAIL={len(fails)}")
