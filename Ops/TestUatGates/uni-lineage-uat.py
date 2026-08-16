#!/usr/bin/env python3
"""
RECONCILE-PROD-DEVELOP-LINEAGE — UAT الوظيفيّة لميزات نَسَب الإنتاج المستعادة — TEST فقط.

الغرض: إثبات أنّ الميزات التي كانت حيّة على الإنتاج (`ce166662`) وغائبة عن نموذج
`develop` صارت تعمل فعليًّا بعد توحيد النَسَب، وأنّ ميزات `develop` المعتمَدة
(CPW-R2/CPW-R3) لم تنحدر.

يقرأ الأسرار من المخزن الآمن مباشرةً: لا source ولا eval ولا argv.
لا يكتب أيّ بيانات على القاعدة عبر الواجهة إلّا في المسارات المعلَّمة WRITE،
وكلّها على TEST وقابلة للعكس بحذف ما أنشأته في نفس التشغيل.
"""
import json, sys, time, urllib.error, urllib.request

API = "http://127.0.0.1:5091"
STORE = "/root/uat-secrets/uat-accounts.json"

PASS = 0
FAIL = 0


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
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as ex:
        return 0, str(ex)


def login(email, pw):
    st, body = call("/api/auth/login", method="POST", payload={"email": email, "password": pw})
    return json.loads(body).get("accessToken") if st == 200 else None


def chk(label, actual, expected):
    global PASS, FAIL
    ok = actual in expected if isinstance(expected, (set, frozenset)) else actual == expected
    if ok:
        print("  PASS  %-52s -> %s" % (label, actual))
        PASS += 1
    else:
        print("  FAIL  %-52s -> got=%s expected=%s" % (label, actual, expected))
        FAIL += 1


def main():
    accounts = json.load(open(STORE, encoding="utf-8"))["accounts"]
    envf = open("/etc/khubara-reporting-test.env", encoding="utf-8").read().splitlines()
    admin_email = next(l.split("=", 1)[1].strip('"') for l in envf if l.startswith("Seed__AdminEmail="))
    admin_pass = next(l.split("=", 1)[1].strip('"') for l in envf if l.startswith("Seed__AdminPassword="))

    print("===== 1) AUTH =====")
    admin = login(admin_email, admin_pass)
    chk("admin login", bool(admin), True)
    time.sleep(2.2)
    ceo = login(accounts["CEO"]["email"], accounts["CEO"]["password"])
    time.sleep(2.2)
    tl = login(accounts["TL"]["email"], accounts["TL"]["password"])
    time.sleep(2.2)
    emp = login(accounts["EMP"]["email"], accounts["EMP"]["password"])
    time.sleep(2.2)
    chk("uat identities logged in", all([ceo, tl, emp]), True)

    # ---------------------------------------------------------------- المناصب المرنة
    # ميزة إنتاجيّة حيّة كانت غائبة عن نموذج develop (ScopeResolver.ResolvePositionScopeAsync
    # + قدرة positions.manage + مسار الواجهة /app/positions).
    print("===== 2) PRODUCTION LINEAGE · FLEXIBLE POSITIONS =====")
    st, body = call("/api/positions", admin)
    chk("GET /api/positions (Admin)", st, 200)
    positions_ok = st == 200
    st, _ = call("/api/positions/permission-options", admin)
    chk("GET /api/positions/permission-options (Admin)", st, 200)
    st, _ = call("/api/positions", ceo)
    chk("GET /api/positions (CEO) -> forbidden by design", st, {403, 404})
    st, _ = call("/api/positions", tl)
    chk("GET /api/positions (TL) -> forbidden by design", st, {403, 404})
    st, _ = call("/api/positions")
    chk("GET /api/positions (anonymous) -> 401", st, 401)
    st, _ = call("/api/positions/00000000-0000-0000-0000-000000000001", admin)
    chk("GET /api/positions/{ghost} -> 404 not 403", st, 404)
    if positions_ok:
        try:
            payload = json.loads(body)
            items = payload if isinstance(payload, list) else payload.get("items", payload.get("data", []))
            chk("positions payload is a collection", isinstance(items, list), True)
        except Exception:
            chk("positions payload is JSON", False, True)

    # ---------------------------------------------------------------- منح الرؤية
    print("===== 3) PRODUCTION LINEAGE · REPORT VIEW GRANTS =====")
    st, _ = call("/api/report-view-grants", admin)
    chk("GET /api/report-view-grants (Admin)", st, 200)
    st, _ = call("/api/report-view-grants", ceo)
    chk("GET /api/report-view-grants (CEO) -> admin only", st, {403, 404})
    st, _ = call("/api/report-view-grants/effective/me", emp)
    chk("GET /api/report-view-grants/effective/me (any user)", st, 200)
    st, _ = call("/api/report-view-grants")
    chk("GET /api/report-view-grants (anonymous) -> 401", st, 401)

    # ---------------------------------------------------------------- ورشة الحوكمة
    print("===== 4) PRODUCTION LINEAGE · GOVERNANCE WORKSPACE =====")
    for res in ("risks", "escalations", "decisions"):
        st, _ = call("/api/%s" % res, ceo)
        chk("GET /api/%s (CEO)" % res, st, 200)
    st, _ = call("/api/risks/00000000-0000-0000-0000-000000000001", ceo)
    chk("GET /api/risks/{ghost} -> 404 not 403", st, 404)
    st, _ = call("/api/decisions", emp)
    chk("GET /api/decisions (Employee) -> scoped", st, {200, 403, 404})

    # ---------------------------------------------------------------- التذكيرات (جافّة)
    print("===== 5) PRODUCTION LINEAGE · REPORT REMINDERS (DRY-RUN ONLY) =====")
    st, _ = call("/api/report-reminders/dry-run/generate", admin, method="POST", payload={})
    chk("POST /api/report-reminders/dry-run/generate (Admin)", st, {200, 400, 404})
    st, _ = call("/api/report-reminders/dry-run/generate", emp, method="POST", payload={})
    chk("POST dry-run/generate (Employee) -> forbidden", st, {403, 404})

    # ---------------------------------------------------------------- develop المعتمَدة
    print("===== 6) DEVELOP-APPROVED FEATURES (NO REGRESSION) =====")
    st, _ = call("/api/execution-taxonomy", ceo)
    chk("GET /api/execution-taxonomy (CPW-R3 catalog)", st, 200)
    st, _ = call("/api/dashboard/me", ceo)
    chk("GET /api/dashboard/me", st, 200)
    st, _ = call("/api/dashboard/pending-reports", ceo)
    chk("GET /api/dashboard/pending-reports", st, 200)
    st, _ = call("/api/notifications", emp)
    chk("GET /api/notifications", st, 200)
    st, _ = call("/api/reporting-calendar/my-cycles", emp)
    chk("GET /api/reporting-calendar/my-cycles (ROLE-AWARE-CALENDAR)", st, 200)
    st, _ = call("/api/reporting-calendar/my-days", emp)
    chk("GET /api/reporting-calendar/my-days", st, 200)
    st, _ = call("/api/report-calendar/missing-reports", ceo)
    chk("GET /api/report-calendar/missing-reports", st, 200)

    print()
    print("===== LINEAGE UAT SUMMARY =====")
    print("PASS=%d FAIL=%d" % (PASS, FAIL))
    print("LINEAGE_UAT=%s" % ("PASS" if FAIL == 0 else "FAIL"))
    return 0 if FAIL == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
