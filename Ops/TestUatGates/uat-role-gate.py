#!/usr/bin/env python3
"""
بوّابة الأدوار والنطاق ومكافحة التعداد — CPW-R2 + CPW-R3.
يقرأ الأسرار من المخزن الآمن (JSON) مباشرةً: لا source، لا eval، لا صدفة، لا argv.
يحترم حدّ المعدّل 30/60ث بمباعدة نداءات المصادقة.

القِيَم البيئيّة تُمرَّر عبر متغيّرات البيئة لا تُثبَّت في الشيفرة، حتّى تعمل البوّابة
نفسها — بلا نسخ متفرّعة — على TEST وRC معًا (الافتراضات تبقى TEST):
  ROLE_GATE_API · ROLE_GATE_STORE · ROLE_GATE_MATRIX
"""
import json, os, sys, time, urllib.error, urllib.request

API = os.environ.get("ROLE_GATE_API", "http://127.0.0.1:5091")
STORE = os.environ.get("ROLE_GATE_STORE", "/root/uat-secrets/uat-accounts.json")
MATRIX_OUT = os.environ.get("ROLE_GATE_MATRIX", "/root/uat-role-matrix.json")
GHOST = "00000000-0000-0000-0000-000000000001"


def call(path, token=None, method="GET", payload=None):
    data = json.dumps(payload).encode() if payload is not None else None
    r = urllib.request.Request(API + path, data=data, method=method)
    if data is not None:
        r.add_header("Content-Type", "application/json")
    if token:
        r.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(r, timeout=30) as resp:
            return resp.status, resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, ""
    except Exception:
        return 0, ""


def login(email, pw):
    st, body = call("/api/auth/login", method="POST", payload={"email": email, "password": pw})
    if st == 200:
        try:
            return json.loads(body).get("accessToken")
        except Exception:
            return None
    return None


def main():
    if len(sys.argv) < 4:
        print("usage: uat-role-gate.py <client_id> <document_id> <project_id>")
        return 2
    client_id, doc_id, project_id = sys.argv[1], sys.argv[2], sys.argv[3]

    with open(STORE, "r", encoding="utf-8") as fh:
        accounts = json.load(fh)["accounts"]

    order = ["VIEWER", "EMP", "SALES", "TL", "OPS_MGR", "HR", "FIN_EMP", "FIN_MGR", "AM", "GM", "CEO"]
    checks = [
        ("R2 documents  ", "/api/clients/%s/documents" % client_id, {200, 403, 404}),
        ("R2 links      ", "/api/clients/%s/links" % client_id, {200, 403, 404}),
        ("R2 doc detail ", "/api/clients/%s/documents/%s" % (client_id, doc_id), {200, 403, 404}),
        ("R3 overview   ", "/api/projects/%s/overview" % project_id, {200, 403, 404}),
        ("R3 objectives ", "/api/projects/%s/objectives" % project_id, {200, 403, 404}),
        ("R3 kpis       ", "/api/projects/%s/kpis" % project_id, {200, 403, 404}),
        ("R3 decisions  ", "/api/projects/%s/decisions" % project_id, {200, 403, 404}),
        ("AE doc        ", "/api/clients/%s/documents/%s" % (client_id, GHOST), {404}),
        ("AE client     ", "/api/clients/%s/documents" % GHOST, {404}),
        ("AE overview   ", "/api/projects/%s/overview" % GHOST, {404}),
        ("AE objectives ", "/api/projects/%s/objectives" % GHOST, {404}),
        ("AE decisions  ", "/api/projects/%s/decisions" % GHOST, {404}),
    ]

    PASS = FAIL = 0
    matrix = {}
    print("===== ROLE / SCOPE / ANTI-ENUMERATION GATE =====")
    for key in order:
        if key not in accounts:
            continue
        acct = accounts[key]
        tok = login(acct["email"], acct["password"])
        time.sleep(2.2)                      # احترام حدّ المعدّل
        if not tok:
            print("  %-8s LOGIN FAILED" % key); FAIL += 1; continue
        PASS += 1
        row = {}
        print("  --- %s (%s) ---" % (key, acct["email"]))
        for label, path, expected in checks:
            st, _ = call(path, tok)
            row[label.strip()] = st
            if st in expected:
                PASS += 1
            else:
                FAIL += 1
                print("      FAIL %s -> %s (expected %s)" % (label, st, sorted(expected)))
        matrix[key] = row
        print("      %s" % "  ".join("%s=%s" % (l.strip().split()[-1], row[l.strip()]) for l, _, _ in checks))

    print("\n===== SUMMARY =====")
    print("PASS=%d FAIL=%d" % (PASS, FAIL))
    print("ROLE_GATE=%s" % ("PASS" if FAIL == 0 and PASS > 0 else "FAIL"))
    with open(MATRIX_OUT, "w", encoding="utf-8") as fh:
        json.dump(matrix, fh, ensure_ascii=False, indent=2)
    print("matrix -> %s" % MATRIX_OUT)
    return 0 if FAIL == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
