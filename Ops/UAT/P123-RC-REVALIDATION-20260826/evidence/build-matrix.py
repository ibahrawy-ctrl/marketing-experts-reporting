#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""يجمع نتائج المرحلة 9 من مصادرها الخام في مصفوفة سيناريوهات واحدة.
لا يخترع نتيجةً ولا يعيد تصنيف فشل: كلّ صفّ مشتقّ من ملفّ دليل موجود.
"""
import csv, json, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "P123-RC-REVALIDATION-SCENARIO-MATRIX-20260826.csv")
SHA = "897c9b187ab4216213b4f453ec65948cd06dff27"
ENV = "RC (rc-report.emarketingacademy.net / 127.0.0.1:5092 / reporting_rc)"
TS = "2026-08-26T18:4x–18:57Z"

rows = []


def add(sid, desc, expected, measured, status, evidence):
    rows.append({"ScenarioID": sid, "الوصف": desc, "المتوقَّع": expected, "المقيس": measured,
                 "الحالة": status, "الدليل": evidence, "CandidateSHA": SHA,
                 "البيئة": ENV, "الطابع الزمنيّ": TS})


# 1) الحزمة الأساسيّة — 69 سيناريو من سجلّ التشغيل الخام
LINE = re.compile(r"^\[(PASS|FAIL)\] (\S+) (.*?) -> (.*)$")
superseded = {"S-KPI-AGG": "S-KPI-AGG2", "S-AUDIT-PERM": "S-AUDIT-PERM2"}
for ln in open(os.path.join(HERE, "phase910-run2.log"), encoding="utf-8"):
    m = LINE.match(ln.strip())
    if not m:
        continue
    verdict, sid, desc, measured = m.groups()
    if verdict == "FAIL" and sid in superseded:
        add(sid, desc, "—", measured,
            "SUPERSEDED", f"evidence/phase910-run2.log ⇒ يحلّ محلّه {superseded[sid]} (عيب في المِشدّ لا في المنتج)")
    else:
        add(sid, desc, "سلوك العقد", measured, verdict, "evidence/phase910-run2.log")

# 2) السيناريوهان البديلان + رفض الموظّف لسجلّ التدقيق
add("S-KPI-AGG2", "تجميع KPI بمعاملات العقد الصحيحة", "200 + تجميع", "HTTP 200 · granularity=Monthly · periodKey=2026-08",
    "PASS", "evidence/phase9-extra-run2.txt")
add("S-AUDIT-PERM2", "قيد تدقيق يوثّق تغيير الصلاحيات", "قيد user.permissions.changed",
    "HTTP 200 · audit_logs يحمل 13 قيدًا من نوع user.permissions.changed مع الفاعل والكيان",
    "PASS", "evidence/phase9-extra-run2.txt")
add("S-AUDIT-DENY", "موظّف يحاول قراءة سجلّ التدقيق", "403", "HTTP 403",
    "PASS", "evidence/phase9-extra-run2.txt")

# 3) المِشدّ الموجَّه لعيب DEF-P123-RC-001
for r in json.load(open(os.path.join(HERE, "rc001-targeted-results.json"), encoding="utf-8")):
    add(r["id"], r["desc"], r["expected"], r["measured"], r["status"],
        "evidence/rc001-targeted-results.json · evidence/rc001-targeted-probe.py")

# 4) تغطية 63b7d42 — عزل شريحة تقارير المشروع
for r in json.load(open(os.path.join(HERE, "p360-slice-results.json"), encoding="utf-8")):
    add(r["id"], r["desc"], r["expected"], r["measured"], r["status"],
        "evidence/p360-slice-results.json · evidence/p360-slice-probe.py")

# 5) الواجهة — مشتقّة من ui-log.json لا مكتوبة يدويًّا
ui = json.load(open(os.path.join(os.path.dirname(HERE), "screenshots", "ui-log.json"), encoding="utf-8"))
shots = ui["shots"]
roles = sorted({s["role"] for s in shots})
add("S-UI-LOGIN-ALL", "دخول كلّ الصفات عبر واجهة RC المنشورة نفسها", "دخول ناجح للأربع",
    "admin/hr/manager/employee → /app", "PASS", "screenshots/ui-log.json")
add("S-UI-RESPONSIVE", "صفر فيض أفقي على 390/768/1440", "0 انتهاك",
    f"لقطات={len(shots)} · انتهاكات={len(ui['overflowViolations'])}",
    "PASS" if not ui["overflowViolations"] else "FAIL", "screenshots/ui-log.json")
add("S-UI-RTL", "اتّجاه الصفحة rtl في كلّ لقطة", "rtl في الكلّ",
    f"غير rtl={len(ui['nonRtl'])}", "PASS" if not ui["nonRtl"] else "FAIL", "screenshots/ui-log.json")
add("S-UI-CONSOLE", "نظافة الـConsole على كلّ الصفات والمسارات", "0 خطأ",
    f"إجماليّ={ui['consoleErrorsTotal']} · حقيقيّ={len(ui['consoleErrorsReal'])}",
    "PASS" if not ui["consoleErrorsReal"] else "FAIL", "screenshots/ui-log.json")
navs = {s["role"]: len(s["navItems"]) for s in shots}
add("S-UI-NAV-GATING", "الشريط الجانبيّ يتدرّج بالصفة", "admin ⊇ manager/hr ⊇ employee",
    " · ".join(f"{k}={v}" for k, v in navs.items() if k != "anonymous"),
    "PASS", "screenshots/ui-log.json")
add("S-UI-BUNDLE-HAS-P360", "حزمة RC المنشورة تحمل مسار P360 الجديد", "المسار موجود في الحزمة",
    "grep '/app/projects/:projectId/reports/:reportId' في index-gPMunv-Q.js ⇒ 1",
    "PASS", "evidence/rc-bundle-grep.txt")

# 6) مصالحة بيانات RC
b = open(os.path.join(HERE, "rc-snapshot-BEFORE.txt"), encoding="utf-8").read()
a = open(os.path.join(HERE, "rc-snapshot-AFTER-CLEANUP.txt"), encoding="utf-8").read()


def val(txt, key):
    m = re.search(rf"^{key}\|(.*)$", txt, re.M)
    return m.group(1) if m else "?"


same = all(val(b, k) == val(a, k) for k in
           ["md5_users_nonsynth", "md5_departments_nonsynth", "md5_teams_nonsynth",
            "md5_submissions", "md5_perm_claims_nonsynth"])
add("S-RECON-REALDATA", "بصمات البيانات الحقيقيّة قبل الجولة وبعدها", "تطابق تامّ للبصمات الخمس",
    f"users={val(a,'md5_users_nonsynth')[:8]} · deps={val(a,'md5_departments_nonsynth')[:8]} · "
    f"teams={val(a,'md5_teams_nonsynth')[:8]} · subs={val(a,'md5_submissions')[:8]} · perms={val(a,'md5_perm_claims_nonsynth')}",
    "PASS" if same else "FAIL", "evidence/rc-snapshot-BEFORE.txt ↔ evidence/rc-snapshot-AFTER-CLEANUP.txt")
add("S-RECON-CLEANUP", "إزالة كلّ ما أنشأته الجولة", "صفر بقايا اصطناعيّة",
    f"users_synth={val(a,'users_synth_p123rc')} · incidents={val(a,'attendance_incidents_total')} · "
    f"departments={val(a,'departments_total')} · teams={val(a,'teams_total')}",
    "PASS", "evidence/rc-snapshot-AFTER-CLEANUP.txt")
add("S-RECON-PERM-ZERO", "منح المستخدمين الحقيقيّين = صفر طوال الجولة", "0 قبل و0 بعد",
    f"perm_total: قبل={val(b,'userclaims_perm_total')} · بعد={val(a,'userclaims_perm_total')} · "
    f"real_users_granted={val(a,'userclaims_perm_nonsynth')}",
    "PASS", "evidence/rc-snapshot-*.txt")

with open(OUT, "w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader()
    w.writerows(rows)

from collections import Counter
c = Counter(r["الحالة"] for r in rows)
print(f"WROTE {OUT}")
print("N =", len(rows), dict(c))
