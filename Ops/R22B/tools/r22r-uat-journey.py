#!/usr/bin/env python3
# R22B/RECONCILIATION — رحلة UAT للأسطح الخمسة على TEST عبر الـAPI الرسميّة حصرًا.
# لكلّ قالب من الخمسة: إنشاء تقرير ⟹ تعبئة ⟹ إرسال ⟹ إعادة بتعليق من ثلاثة أسطر مميّزة،
# ثمّ إعادة القراءة الباردة من الـAPI للموظّف وللقارئ المستقلّ، وقراءة الجرس.
# القالب الأوّل يمرّ بدورة اعتماد ثانية بتعليق يحوي محرفَي حقن (<script> و&) ثمّ حذف إداريّ
# ناعم لإثبات سطح «أرشيف الإدارة». معاينة بريد HTML عبر نقطة المعاينة الرسميّة (بلا إرسال).
import json, os, sys, urllib.request, urllib.error

API = "http://127.0.0.1:5091"
OUT = "/root/r22r"
STATE = os.path.join(OUT, "state.json")
EV = os.path.join(OUT, "uat-evidence.json")

PERIOD_CANDIDATES = ["2026-W36", "2026-W35", "2026-W34"]

# تعليقات مميّزة لكلّ قالب — ثلاثة أسطر لكلّ واحد، بلا تكرار بين القوالب.
COMMENTS = {
    "content":    "س1/محتوى: الأدلّة ناقصة على مقالين.\nس2/محتوى: صحّح عدد الكلمات في الجدول.\nس3/محتوى: أعد الإرسال قبل نهاية اليوم.",
    "design":     "س1/تصميم: ملفّ المصدر غير مرفق.\nس2/تصميم: وحّد مقاسات البانرات.\nس3/تصميم: أعد الإرسال بعد التصحيح.",
    "video":      "س1/فيديو: مدّة المقطع تتجاوز المتّفق عليه.\nس2/فيديو: أضف الترجمة العربيّة.\nس3/فيديو: أعد الرفع بدقّة أعلى.",
    "moderation": "س1/مديرشن: زمن الاستجابة غير مسجّل.\nس2/مديرشن: صنّف الشكاوى حسب النوع.\nس3/مديرشن: أعد الإرسال مع الأدلّة.",
    "seo":        "س1/سيو: الكلمات المفتاحيّة غير محدّثة.\nس2/سيو: أضف روابط داخليّة للمقالين.\nس3/سيو: أعد الإرسال بعد المراجعة.",
}
# دورة الاعتماد الثانية للقالب الأوّل — تجمع الأسطر المتعدّدة مع محاولة حقن.
APPROVE_COMMENT = ("اعتماد/س1: شكرًا، صُحِّحت الملاحظات.\n"
                   "اعتماد/س2: <script>alert('xss')</script> & \"اقتباس\"\n"
                   "اعتماد/س3: أُغلق التقرير للفترة.")

LOG = []
E = {"period": None, "surfaces": {}, "templates": {}, "log": LOG}


def call(method, path, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw.strip() else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw) if raw.strip() else None
        except Exception:
            return e.code, {"raw": raw[:400]}


def step(name, st, note=""):
    ok = isinstance(st, int) and 200 <= st < 300
    LOG.append({"step": name, "status": st, "note": str(note)[:200]})
    print(f"{'OK ' if ok else '!! '}{name:44s} {st}  {str(note)[:100]}")
    return ok


def die(name, st, note=""):
    step(name, st, note)
    json.dump(E, open(EV, "w"), ensure_ascii=False, indent=1)
    print("\nEVIDENCE=" + EV)
    sys.exit(1)


def login(email, pw):
    st, b = call("POST", "/api/auth/login", body={"email": email, "password": pw})
    if not step(f"login {email}", st):
        die("login-failed", st, email)
    return b["accessToken"], b.get("refreshToken")


NUMERIC = ("Number", "Decimal", "Integer")


def sub_field_value(f):
    """قيمة صالحة لحقل فرعيّ داخل القسم المتكرّر — مقادة بتعريف القالب لا بترميز صلب."""
    opts = f.get("options")
    if opts:
        return opts[0]
    if f.get("type") in NUMERIC:
        lo = f.get("min")
        return float(lo) if lo is not None else 2
    return "قيمة R22R"


def repeatable_json(cfg, project_id):
    """بطاقة مشروع واحدة مستوفية لكلّ الحقول الإلزاميّة المُعلَنة في ConfigJson."""
    answers = {f["key"]: sub_field_value(f) for f in (cfg.get("fields") or []) if f.get("required")}
    entry = {"projectId": project_id, "answers": answers}
    wi = cfg.get("workItems")
    if wi:
        n = max(int(wi.get("minItems") or 0), 1)
        item = {"answers": {f["key"]: sub_field_value(f)
                            for f in (wi.get("fields") or []) if f.get("required")}}
        entry["workItems"] = [dict(item) for _ in range(n)]
    return json.dumps([entry], ensure_ascii=False)


def fill_values(sub, project_id):
    """يبني قيمًا صالحة لكلّ حقل إلزاميّ + لأقسام المشاريع المتكرّرة (لها تحقّق خاصّ)."""
    out = []
    for f in sub.get("fieldValues", []):
        ft = str(f.get("fieldType"))
        v = {"templateFieldId": f["templateFieldId"], "valueText": None, "valueNumber": None,
             "valueDate": None, "valueBool": None, "valueJson": None}
        if ft == "ProjectRepeatableSection":
            cfg = json.loads(f.get("configJson") or "{}")
            v["valueJson"] = repeatable_json(cfg, project_id)
        elif not f.get("isRequired") or ft == "SectionHeader":
            continue
        elif ft in NUMERIC:
            v["valueNumber"] = 3
        elif ft == "Date":
            v["valueDate"] = "2026-08-30T00:00:00Z"
        elif ft in ("Bool", "Boolean"):
            v["valueBool"] = True
        else:
            v["valueText"] = "قيمة اختبار R22R"
        out.append(v)
    return out


S = json.load(open(STATE))
usr_pw = open(S["userPasswordFile"]).read().strip()
adm_pw = open("/root/.r22c-admin-pw").read().strip()

ADM, _ = login("r22b-hotfix-admin@r22uat.test", adm_pw)
REV, _ = login(S["reviewerEmail"], usr_pw)

# ============ 1) دورة كلّ قالب: إنشاء ⟹ تعبئة ⟹ إرسال ⟹ إعادة بتعليق ثلاثيّ ============
for slug, emp in S["employees"].items():
    r = {"email": emp["email"], "templateId": emp["templateId"], "templateTitle": emp["templateTitle"]}
    E["templates"][slug] = r
    tok, _ = login(emp["email"], usr_pw)

    st, b = call("GET", "/api/report-templates?assignedOnly=true", tok)
    items = b if isinstance(b, list) else (b or {}).get("items", [])
    r["assignedCount"] = len(items)
    r["seesOwnTemplate"] = any(t["id"] == emp["templateId"] for t in items)
    step(f"eligibility-{slug}", st, f"assigned={len(items)} own={r['seesOwnTemplate']}")

    sub = None
    for pk in PERIOD_CANDIDATES:
        st, b = call("POST", "/api/submissions", tok,
                     {"reportTemplateId": emp["templateId"], "periodType": "Weekly",
                      "periodKey": pk, "projectId": emp["projectId"]})
        if 200 <= st < 300 and isinstance(b, dict):
            sub, r["periodKey"] = b, pk
            E["period"] = E["period"] or pk
            break
        r.setdefault("createAttempts", []).append({"periodKey": pk, "status": st,
                                                   "body": json.dumps(b, ensure_ascii=False)[:200]})
    if sub is None:
        die(f"create-{slug}", 400, json.dumps(r.get("createAttempts"), ensure_ascii=False)[:300])
    r["submissionId"] = sub["id"]
    step(f"create-{slug}", 200, f"{sub['id']} {r['periodKey']}")

    vals = fill_values(sub, emp["projectId"])
    r["requiredFieldCount"] = len(vals)
    if vals:
        st, _ = call("PUT", f"/api/submissions/{sub['id']}/values", tok, {"values": vals})
        if not step(f"save-values-{slug}", st, len(vals)):
            die(f"save-values-{slug}", st)

    st, b = call("POST", f"/api/submissions/{sub['id']}/submit", tok)
    if not step(f"submit-{slug}", st, (b or {}).get("status")):
        die(f"submit-{slug}", st, json.dumps(b, ensure_ascii=False)[:250])
    r["statusAfterSubmit"] = (b or {}).get("status")

    # ---- المراجِع يُعيد التقرير بتعليق من ثلاثة أسطر ----
    st, b = call("POST", f"/api/submissions/{sub['id']}/return", REV, {"comment": COMMENTS[slug]})
    if not step(f"return-{slug}", st, (b or {}).get("status")):
        die(f"return-{slug}", st, json.dumps(b, ensure_ascii=False)[:250])
    r["statusAfterReturn"] = (b or {}).get("status")
    r["sentComment"] = COMMENTS[slug]
    r["sentLineCount"] = COMMENTS[slug].count("\n") + 1

    # ---- قراءة باردة: الموظّف يقرأ من الـAPI بعد انتهاء المعاملة ----
    st, b = call("GET", f"/api/submissions/{sub['id']}", tok)
    steps = (b or {}).get("approvalSteps", []) if isinstance(b, dict) else []
    got = next((s.get("comment") for s in steps if s.get("comment")), None)
    r["apiRereadStatus"] = st
    r["apiRereadComment"] = got
    r["apiRereadLineCount"] = (got.count("\n") + 1) if got else 0
    r["apiRereadExact"] = (got == COMMENTS[slug])
    step(f"api-reread-{slug}", st, f"lines={r['apiRereadLineCount']} exact={r['apiRereadExact']}")

    # ---- الجرس: نصّ الإشعار الواصل للموظّف ----
    st, b = call("GET", "/api/notifications", tok)
    ns = b if isinstance(b, list) else (b or {}).get("items", [])
    body = next((n.get("body") for n in ns if n.get("body") and COMMENTS[slug][:12] in n.get("body")), None)
    r["bellStatus"] = st
    r["bellBody"] = body
    r["bellLineCount"] = (body.count("\n") + 1) if body else 0
    r["bellContainsFullComment"] = bool(body and COMMENTS[slug] in body)
    step(f"bell-{slug}", st, f"lines={r['bellLineCount']} full={r['bellContainsFullComment']}")

    # ---- قارئ مستقلّ مسموح له ----
    st, b = call("GET", f"/api/submissions/{sub['id']}", ADM)
    steps = (b or {}).get("approvalSteps", []) if isinstance(b, dict) else []
    got2 = next((s.get("comment") for s in steps if s.get("comment")), None)
    r["readerStatus"] = st
    r["readerExact"] = (got2 == COMMENTS[slug])
    step(f"independent-reader-{slug}", st, f"exact={r['readerExact']}")

# ============ 2) دورة اعتماد ثانية + حذف إداريّ ناعم ⟹ سطح أرشيف الإدارة ============
first = list(S["employees"].keys())[0]
emp = S["employees"][first]
sid = E["templates"][first]["submissionId"]
tok, _ = login(emp["email"], usr_pw)

st, b = call("POST", f"/api/submissions/{sid}/submit", tok)
step("resubmit-after-return", st, (b or {}).get("status"))
st, b = call("POST", f"/api/submissions/{sid}/approve", REV, {"comment": APPROVE_COMMENT})
step("approve-with-injection-comment", st, (b or {}).get("status"))
E["approve"] = {"submissionId": sid, "sent": APPROVE_COMMENT, "status": (b or {}).get("status")}

st, b = call("GET", f"/api/submissions/{sid}", tok)
steps = (b or {}).get("approvalSteps", []) if isinstance(b, dict) else []
got = next((s.get("comment") for s in steps if s.get("comment") and "اعتماد/س1" in s.get("comment")), None)
E["approve"]["apiRereadExact"] = (got == APPROVE_COMMENT)
E["approve"]["apiRereadLineCount"] = (got.count("\n") + 1) if got else 0
step("approve-api-reread", st, f"exact={E['approve']['apiRereadExact']}")

st, b = call("POST", f"/api/submissions/{sid}/admin-delete", ADM,
             {"reason": "R22R — حذف إداريّ ناعم مؤقّت لإثبات سطح أرشيف الإدارة (يُستعاد في التنظيف)"})
step("admin-soft-delete", st)
E["archive"] = {"submissionId": sid, "deleteStatus": st}

st, b = call("GET", f"/api/admin/archive/report/{sid}", ADM)
ws = (b or {}).get("workflowSteps", []) if isinstance(b, dict) else []
acs = [s.get("comment") for s in ws if s.get("comment")]
E["archive"]["detailStatus"] = st
E["archive"]["stepCount"] = len(ws)
E["archive"]["comments"] = acs
E["archive"]["approveCommentExact"] = APPROVE_COMMENT in acs
E["archive"]["returnCommentExact"] = COMMENTS[first] in acs
step("archive-detail", st, f"steps={len(ws)} approveExact={E['archive']['approveCommentExact']} "
                          f"returnExact={E['archive']['returnCommentExact']}")

# ============ 3) سطح البريد: معاينة HTML رسميّة بلا إرسال ============
st, b = call("GET", "/api/email-control/templates", ADM)
tpls = b if isinstance(b, list) else (b or {}).get("items", [])
E["email"] = {"listStatus": st, "templateCount": len(tpls)}
if tpls:
    key = tpls[0].get("key") or tpls[0].get("templateKey")
    E["email"]["key"] = key
    st, b = call("POST", f"/api/email-control/templates/{key}/preview", ADM,
                 {"subjectTemplate": "R22R — عيّنة عنوان",
                  "bodyTemplate": APPROVE_COMMENT})
    html = (b or {}).get("bodyHtml") if isinstance(b, dict) else None
    E["email"]["previewStatus"] = st
    E["email"]["bodyHtml"] = html
    if html:
        E["email"]["brCount"] = html.count("<br />")
        E["email"]["scriptEncoded"] = ("&lt;script&gt;" in html) and ("<script>" not in html)
        E["email"]["ampEncoded"] = "&amp;" in html
    step("email-preview", st, f"br={E['email'].get('brCount')} "
                              f"scriptEncoded={E['email'].get('scriptEncoded')}")

json.dump(E, open(EV, "w"), ensure_ascii=False, indent=1)
print("\nEVIDENCE=" + EV)
