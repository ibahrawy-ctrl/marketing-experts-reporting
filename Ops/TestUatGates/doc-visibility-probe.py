#!/usr/bin/env python3
"""
RECONCILE-PROD-DEVELOP-LINEAGE — استقصاء تفاصيل المستند لكلّ دور تشغيليّ.
سبب الاستقصاء: بوّابة الأدوار سجّلت لـAM قيمة `R2 doc detail = 404` اليوم مقابل `200`
في مصفوفة CPW-UNIFIED-UAT (التقرير 05). البوّابة نفسها لا تحسم ذلك لأنّ توقّعها
لفحوص النطاق هو {200,403,404} أي مقبول دائمًا؛ فالحسم يحتاج ربط الحالة بخصائص
المستند (التصنيف/الرؤية) لا بتشغيل واحد.
يقرأ الأسرار من المخزن الآمن مباشرةً: لا source ولا eval ولا argv.
"""
import json, os, sys, time, urllib.error, urllib.request

# القِيَم البيئيّة تُمرَّر لا تُثبَّت، لتعمل الأداة نفسها على TEST وRC (الافتراضات TEST):
#   PROBE_API · PROBE_STORE · PROBE_DOCMETA
API = os.environ.get("PROBE_API", "http://127.0.0.1:5091")
STORE = os.environ.get("PROBE_STORE", "/root/uat-secrets/uat-accounts.json")
DOCMETA = os.environ.get("PROBE_DOCMETA", "/tmp/docmeta.json")


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
    return json.loads(body).get("accessToken") if st == 200 else None


def main():
    client_id = sys.argv[1]
    docs = json.load(open(DOCMETA, encoding="utf-8"))
    accounts = json.load(open(STORE, encoding="utf-8"))["accounts"]
    print("%-40s %-18s %-16s %s" % ("document", "confidentiality", "visibility", "  ".join("%-8s" % r for r in ("OPS_MGR", "AM", "HR", "FIN_MGR", "CEO"))))
    tokens = {}
    for role in ("OPS_MGR", "AM", "HR", "FIN_MGR", "CEO"):
        tokens[role] = login(accounts[role]["email"], accounts[role]["password"])
        time.sleep(2.2)
    for d in docs:
        cells = []
        for role in ("OPS_MGR", "AM", "HR", "FIN_MGR", "CEO"):
            st, _ = call("/api/clients/%s/documents/%s" % (client_id, d["id"]), tokens[role])
            cells.append("%-8s" % st)
        print("%-40s %-18s %-16s %s" % (d["id"], d["conf"], d["vis"], "  ".join(cells)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
