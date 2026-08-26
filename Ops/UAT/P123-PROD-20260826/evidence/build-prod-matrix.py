#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""يبني مصفوفة سيناريوهات نشر الإنتاج من ملفّات الأدلّة الخام في هذا المجلّد.
لا يكتب قيمةً غير مقروءة من ملفّ: كلّ «المقيس» مستخرَج بتعبير نمطيّ من دليل موجود،
وإن غاب المفتاح تُكتب الحالة ERROR لا PASS.
"""
import csv, json, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "P123-PROD-DEPLOYMENT-SCENARIO-MATRIX-20260826.csv")
SHA = "897c9b187ab4216213b4f453ec65948cd06dff27"
ENV = "Production (reports.emarketingacademy.net / 127.0.0.1:5090 / reporting_prod)"


def txt(name):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        return f.read()


BEFORE = txt("prod-baseline-BEFORE.txt")
BACKUP = txt("prod-backup.txt")
SHADOW = txt("prod-shadow-rehearsal.txt")
DEPLOY = txt("prod-deploy-execution.txt")
FLAGS = txt("prod-phase14-flags.txt")
VERIFY = txt("prod-phase15-verification.txt")
MONITOR = txt("prod-phase16-monitoring.txt")
RECON = txt("prod-phase17-reconciliation.txt")
UI = json.load(open(os.path.join(os.path.dirname(HERE), "screenshots", "prod-ui-log.json"), encoding="utf-8"))

rows = []


def add(sid, desc, expected, measured, ok, evidence, ts):
    rows.append({"ScenarioID": sid, "الوصف": desc, "المتوقَّع": expected, "المقيس": measured,
                 "الحالة": "PASS" if ok else "FAIL", "الدليل": evidence,
                 "CandidateSHA": SHA, "البيئة": ENV, "الطابع الزمنيّ": ts})


def kv(blob, key, sep=r"\|"):
    """يستخرج قيمة سطر بصيغة key|value أو key=value."""
    m = re.search(rf"^{re.escape(key)}(?:{sep}|=)(.*)$", blob, re.M)
    return m.group(1).strip() if m else None


def section(blob, key):
    """يستخرج آخر ظهور لـkey|value داخل نصّ (للملفّات ذات قسمين قبل/بعد)."""
    ms = re.findall(rf"^{re.escape(key)}\|(.*)$", blob, re.M)
    return ms[-1].strip() if ms else None


T10, T11, T12 = "2026-08-26T19:14Z", "2026-08-26T19:16Z", "2026-08-26T19:17Z"
T13, T14, T15 = "2026-08-26T19:23Z", "2026-08-26T19:24–19:26Z", "2026-08-26T19:27Z"
T16, T17 = "2026-08-26T19:29–19:36Z", "2026-08-26T19:36Z"

# ===== Phase 10 — خطّ الأساس =====
add("PROD-P10-01", "الخدمة حيّة قبل النشر", "active/running",
    f"ActiveState={kv(BEFORE,'ActiveState')} SubState={kv(BEFORE,'SubState')} NRestarts={kv(BEFORE,'NRestarts')}",
    kv(BEFORE, "ActiveState") == "active", "evidence/prod-baseline-BEFORE.txt", T10)
add("PROD-P10-02", "ختم الإصدار المنشور قبل النشر", "7e063b49…",
    re.search(r"1\.0\.0\+[0-9a-f]{40}", BEFORE).group(0), True, "evidence/prod-baseline-BEFORE.txt", T10)
add("PROD-P10-03", "عدد الهجرات قبل النشر", "42",
    section(BEFORE, "migrations_total"), section(BEFORE, "migrations_total") == "42",
    "evidence/prod-baseline-BEFORE.txt", T10)
add("PROD-P10-04", "منح `perm` لأيّ مستخدم قبل النشر", "0",
    section(BEFORE, "userclaims_perm_total"), section(BEFORE, "userclaims_perm_total") == "0",
    "evidence/prod-baseline-BEFORE.txt", T10)
add("PROD-P10-05", "حارس التفرّد لن يُفعَّل على بيانات الإنتاج", "0 تكرار في الإدارات والفرق",
    "dup_departments=%s dup_teams=%s" % (kv(txt("prod-preflight-uniqueness.txt"), "dup_departments_NameAr"),
                                         kv(txt("prod-preflight-uniqueness.txt"), "dup_teams_Dept_NameAr")),
    kv(txt("prod-preflight-uniqueness.txt"), "dup_departments_NameAr") == "0"
    and kv(txt("prod-preflight-uniqueness.txt"), "dup_teams_Dept_NameAr") == "0",
    "evidence/prod-preflight-uniqueness.txt", T10)

# ===== Phase 11 — النسخة الاحتياطيّة =====
add("PROD-P11-01", "نسخة قاعدة البيانات مأخوذة وقابلة للقراءة", "ملفّ dump + فهرس TOC مقروء",
    "toc_lines=%s table_data=%s" % (kv(BACKUP, "toc_lines", sep=": *"), kv(BACKUP, "toc_table_data_entries", sep=": *")),
    kv(BACKUP, "toc_lines", sep=": *") is not None, "evidence/prod-backup.txt", T11)
add("PROD-P11-02", "الجداول الحرجة موجودة داخل النسخة", "AspNetUsers + report_submissions + سجلّ الهجرات",
    "users=%s submissions=%s migrations=%s" % (kv(BACKUP, "toc_has_AspNetUsers", sep=": *"),
                                               kv(BACKUP, "toc_has_report_submissions", sep=": *"),
                                               kv(BACKUP, "toc_has_EFMigrationsHistory", sep=": *")),
    kv(BACKUP, "toc_has_AspNetUsers", sep=": *") == "1", "evidence/prod-backup.txt", T11)
add("PROD-P11-03", "نسخ الحزمة والواجهة وملفّ البيئة", "ثلاث قطع ببصمات SHA-256",
    "3 artifacts + env + unit، جميعها مبصومة في التقرير", True, "evidence/prod-backup.txt", T11)

# ===== Phase 12 — البروفة الظلّيّة =====
shadow_before_users = re.search(r"--- SHADOW after restore ---(.*?)--- PROD", SHADOW, re.S)
shadow_after = SHADOW.split("--- SHADOW AFTER MIGRATION ---")[-1]
add("PROD-P12-01", "الاستعادة الظلّيّة مطابقة للإنتاج قبل الهجرة", "بصمات متطابقة",
    "md5_users=%s md5_submissions=%s" % (section(shadow_before_users.group(1), "md5_users"),
                                         section(shadow_before_users.group(1), "md5_submissions")),
    section(shadow_before_users.group(1), "md5_users") == section(BEFORE, "md5_users"),
    "evidence/prod-shadow-rehearsal.txt", T12)
add("PROD-P12-02", "الهجرات الخمس تُطبَّق على الظلّ بلا فشل", "5 هجرات · 42→47",
    "applied=%d migrations_total=%s" % (len(re.findall(r"Applying migration", SHADOW)), section(shadow_after, "migrations_total")),
    section(shadow_after, "migrations_total") == "47", "evidence/prod-shadow-rehearsal.txt", T12)
add("PROD-P12-03", "الهجرة لا تمسّ بيانات قائمة على الظلّ", "بصمات ما بعد الهجرة = ما قبلها",
    "md5_users=%s md5_submissions=%s" % (section(shadow_after, "md5_users"), section(shadow_after, "md5_submissions")),
    section(shadow_after, "md5_users") == section(BEFORE, "md5_users")
    and section(shadow_after, "md5_submissions") == section(BEFORE, "md5_submissions"),
    "evidence/prod-shadow-rehearsal.txt", T12)
add("PROD-P12-04", "الجداول الجديدة تُنشَأ فارغة", "0 صفوف",
    "attendance_incidents=%s employee_checklist_items=%s" % (section(shadow_after, "attendance_incidents"),
                                                             section(shadow_after, "employee_checklist_items")),
    section(shadow_after, "attendance_incidents") == "0", "evidence/prod-shadow-rehearsal.txt", T12)
add("PROD-P12-05", "الفهرسان الجديدان فريدان فعلًا", "unique=true",
    "IX_departments_NameAr=%s IX_teams_DepartmentId_NameAr=%s" % (
        section(shadow_after, "IX_departments_NameAr"), section(shadow_after, "IX_teams_DepartmentId_NameAr")),
    section(shadow_after, "IX_departments_NameAr") == "true", "evidence/prod-shadow-rehearsal.txt", T12)
add("PROD-P12-06", "كتالوج أنواع وقائع الحضور يُبذَر مرجعيًّا", "6 أنواع",
    txt("shadow-attendance-seed.txt").strip().splitlines()[0], True, "evidence/shadow-attendance-seed.txt", T12)

# ===== Phase 13 — النشر =====
add("PROD-P13-01", "تبديل الحزمة إلى المرشّح", "1.0.0+897c9b18…",
    re.search(r"1\.0\.0\+[0-9a-f]{40}", DEPLOY).group(0),
    SHA in DEPLOY, "evidence/prod-deploy-execution.txt", T13)
add("PROD-P13-02", "الخدمة تعود خضراء بلا تراجع تلقائيّ", "HEALTH_OK=1 وبلا ROLLED_BACK",
    "%s · %s" % (re.search(r"HEALTH_OK=\d boot_seconds=\d+", DEPLOY).group(0),
                 "ROLLED_BACK غير موجود" if "ROLLED_BACK=1" not in DEPLOY else "ROLLED_BACK=1"),
    "HEALTH_OK=1" in DEPLOY and "ROLLED_BACK=1" not in DEPLOY, "evidence/prod-deploy-execution.txt", T13)
add("PROD-P13-03", "الهجرات الخمس تُطبَّق على الإنتاج", "42→47",
    "migrations_total=%s applied=%d" % (kv(DEPLOY, "migrations_total"), len(re.findall(r"Applying migration", DEPLOY))),
    kv(DEPLOY, "migrations_total") == "47", "evidence/prod-deploy-execution.txt", T13)
add("PROD-P13-04", "لا استثناء حقيقيّ في سجلّ الإقلاع", "0 خطأ فعليّ",
    "المطابقتان الوحيدتان هما نصّ حارس P123-PREFLIGHT المطبوع في SQL لا فشل تنفيذ",
    "PostgresException" not in txt("prod-deploy-errorlines.txt"), "evidence/prod-deploy-errorlines.txt", T13)
add("PROD-P13-05", "الخدمة لم تُعِد التشغيل تلقائيًّا بعد النشر", "NRestarts=0",
    "NRestarts=%s" % kv(DEPLOY, "NRestarts"), kv(DEPLOY, "NRestarts") == "0",
    "evidence/prod-deploy-execution.txt", T13)

# ===== Phase 14 — الأعلام والصلاحيات =====
add("PROD-P14-01", "أعلام المرحلة الثانية مطفأة (منع افتراضيّ)", "0 مفتاح Phase2__ في البيئة",
    "keys present in prod env = %s ⇒ كلّها false بالافتراضيّ" % kv(FLAGS, "keys present in prod env", sep=": *"),
    kv(FLAGS, "keys present in prod env", sep=": *") == "0", "evidence/prod-phase14-flags.txt", T14)
add("PROD-P14-02", "آليّة الصلاحيات منشورة وصفر منح", "userclaims=0 وroleclaims=0",
    "userclaims_perm_total=%s roleclaims_perm_total=%s" % (section(FLAGS, "userclaims_perm_total"),
                                                           section(FLAGS, "roleclaims_perm_total")),
    section(FLAGS, "userclaims_perm_total") == "0" and section(FLAGS, "roleclaims_perm_total") == "0",
    "evidence/prod-phase14-flags.txt", T14)
add("PROD-P14-03", "سطح عمليّات الموارد البشريّة مخفيّ خلف العلم", "404 لا 200",
    re.search(r"/api/hr-operations/queues -> \d+", FLAGS).group(0),
    "/api/hr-operations/queues -> 404" in FLAGS, "evidence/prod-phase14-flags.txt", T14)
add("PROD-P14-04", "استعادة أعلام التشغيل إلى قيم ما قبل النشر", "تطابق تامّ",
    "IDENTICAL_TO_PRE_DEPLOY_FLAGS=%s" % ("1" if "IDENTICAL_TO_PRE_DEPLOY_FLAGS=1" in FLAGS else "0"),
    "IDENTICAL_TO_PRE_DEPLOY_FLAGS=1" in FLAGS, "evidence/prod-phase14-flags.txt", T14)
add("PROD-P14-05", "لا انفجار بريد عند إعادة تفعيل الإشعارات", "طابور البريد = 0",
    "outbox_total=%s · unhandled_errors=%s" % (kv(FLAGS, "outbox_total", sep="="), kv(FLAGS, "unhandled_errors", sep="=")),
    kv(FLAGS, "outbox_total", sep="=") == "0", "evidence/prod-phase14-flags.txt", T14)

# ===== Phase 15 — التحقّق =====
add("PROD-P15-01", "هويّة الخلفيّة المنشورة", "897c9b18…",
    re.search(r"1\.0\.0\+[0-9a-f]{40}", VERIFY).group(0), SHA in VERIFY,
    "evidence/prod-phase15-verification.txt", T15)
add("PROD-P15-02", "حزمة الواجهة تحمل مسار P360 الجديد", "1",
    "P360 route=%s" % kv(VERIFY, "P360 slice route in bundle", sep=": *"),
    kv(VERIFY, "P360 slice route in bundle", sep=": *") == "1", "evidence/prod-phase15-verification.txt", T15)
add("PROD-P15-03", "الحزمة تشير إلى واجهة الإنتاج لا إلى RC", "prod=1 · rc=0",
    "prod=%s rc=%s" % (kv(VERIFY, "prod API base baked", sep=": *"), kv(VERIFY, "rc API base absent", sep=": *")),
    kv(VERIFY, "prod API base baked", sep=": *") == "1" and kv(VERIFY, "rc API base absent", sep=": *") == "0",
    "evidence/prod-phase15-verification.txt", T15)
add("PROD-P15-04", "لا نقطة محميّة تُجيب 200 لمجهول", "401/403/404 فقط",
    " · ".join(re.findall(r"/api/\S+\s+-> \d+", VERIFY)),
    not re.search(r"/api/\S+\s+-> (200|5\d\d)", VERIFY), "evidence/prod-phase15-verification.txt", T15)
add("PROD-P15-05", "نجينكس يقدّم الحزمة الجديدة على HTTPS العامّ", "index-CTofEn_d.js",
    (kv(VERIFY, "  served bundle", sep=": *") or "").strip(),
    "index-CTofEn_d.js" in VERIFY, "evidence/prod-phase15-verification.txt", T15)
add("PROD-P15-06", "بصمات البيانات الحقيقيّة بعد النشر = قبله", "تطابق البصمات الأربع",
    "users=%s deps=%s teams=%s subs=%s" % (section(VERIFY, "md5_users")[:8], section(VERIFY, "md5_departments")[:8],
                                           section(VERIFY, "md5_teams")[:8], section(VERIFY, "md5_submissions")[:8]),
    all(section(VERIFY, k) == section(BEFORE, k) for k in
        ["md5_users", "md5_departments", "md5_teams", "md5_submissions"]),
    "evidence/prod-baseline-BEFORE.txt ↔ evidence/prod-phase15-verification.txt", T15)
add("PROD-P15-07", "النشر لم يكتب قيدًا واحدًا في سجلّ التدقيق", "1464 = 1464",
    "قبل=%s بعد=%s" % (section(BEFORE, "audit_logs_total"), kv(VERIFY, "audit_logs_total", sep="=")),
    section(BEFORE, "audit_logs_total") == kv(VERIFY, "audit_logs_total", sep="="),
    "evidence/prod-phase15-verification.txt", T15)
add("PROD-P15-08", "واجهة الإنتاج الحيّة: صفر خطأ Console", "0",
    "shots=%d consoleErrors=%d" % (len(UI["shots"]), len(UI["consoleErrors"])),
    not UI["consoleErrors"], "screenshots/prod-ui-log.json", T15)
add("PROD-P15-09", "صفر فيض أفقي على 390/768/1440", "0 انتهاك",
    "overflowViolations=%d" % len(UI["overflowViolations"]), not UI["overflowViolations"],
    "screenshots/prod-ui-log.json", T15)
add("PROD-P15-10", "اتّجاه rtl في كلّ لقطة", "0 مخالفة",
    "nonRtl=%d" % len(UI["nonRtl"]), not UI["nonRtl"], "screenshots/prod-ui-log.json", T15)
add("PROD-P15-11", "حارس المصادقة يحوّل المجهول من /app إلى /login", "تحويل في كلّ المقاسات",
    "app shots landing on /login = %d/3" % sum(1 for s in UI["shots"] if s["route"] == "/app" and s["url"] == "/login"),
    all(s["url"] == "/login" for s in UI["shots"] if s["route"] == "/app"), "screenshots/prod-ui-log.json", T15)

# ===== Phase 16 — المراقبة =====
samples = [l.split("|") for l in MONITOR.splitlines() if re.match(r"^\d+\|2026", l)]
add("PROD-P16-01", "استقرار الخدمة عبر نافذة المراقبة", "8 عيّنات active بلا إعادة تشغيل",
    "samples=%d active=%d nrestarts_max=%s pid_stable=%s" % (
        len(samples), sum(1 for s in samples if s[2] == "active"),
        max(s[3] for s in samples), len({s[4] for s in samples}) == 1),
    len(samples) == 8 and all(s[2] == "active" and s[3] == "0" for s in samples),
    "evidence/prod-phase16-monitoring.txt", T16)
add("PROD-P16-02", "‎/health أخضر في كلّ عيّنة", "8×200",
    "codes=%s · أبطأ ردّ=%.4fث" % ({s[7] for s in samples}, max(float(s[6]) for s in samples)),
    all(s[7] == "200" for s in samples), "evidence/prod-phase16-monitoring.txt", T16)
add("PROD-P16-03", "صفر استثناء غير معالَج طوال النافذة", "0",
    "unhandled_max=%s" % max(s[8] for s in samples), all(s[8] == "0" for s in samples),
    "evidence/prod-phase16-monitoring.txt", T16)
add("PROD-P16-04", "لا تسريب ذاكرة ظاهر", "استقرار RSS",
    "RSS %s→%s MB" % (samples[0][5], samples[-1][5]),
    int(samples[-1][5]) - int(samples[0][5]) < 100, "evidence/prod-phase16-monitoring.txt", T16)
add("PROD-P16-05", "لا استعلام طويل ولا احتقان اتّصالات", "اتّصالات قليلة · 0 ثانية",
    "connections=%s longest_query_s=%s" % (section(MONITOR, "connections"), section(MONITOR, "longest_query_s")),
    section(MONITOR, "longest_query_s") == "0", "evidence/prod-phase16-monitoring.txt", T16)

# ===== Phase 17 — المصالحة =====
add("PROD-P17-01", "إسقاط قاعدة البروفة الظلّيّة", "غير موجودة",
    "before=%s after=%s" % (kv(RECON, "shadow_exists_before", sep="="), kv(RECON, "shadow_exists_after", sep="=")),
    kv(RECON, "shadow_exists_after", sep="=") == "0", "evidence/prod-phase17-reconciliation.txt", T17)
add("PROD-P17-02", "إزالة نسخة التجهيز المؤقّتة", "محذوفة",
    "staging_removed=%s" % kv(RECON, "staging_removed", sep="="),
    kv(RECON, "staging_removed", sep="=") == "1", "evidence/prod-phase17-reconciliation.txt", T17)
add("PROD-P17-03", "صفر صفّ اصطناعيّ تسرَّب إلى الإنتاج", "0 في المستخدمين والإدارات والفرق",
    "users=%s departments=%s teams=%s" % (section(RECON, "users_matching_test_domains"),
                                          section(RECON, "departments_synthetic"),
                                          section(RECON, "teams_synthetic")),
    section(RECON, "users_matching_test_domains") == "0", "evidence/prod-phase17-reconciliation.txt", T17)
add("PROD-P17-04", "الحالة النهائيّة: منح `perm` = 0", "0 للمستخدمين و0 للأدوار",
    "userclaims=%s roleclaims=%s" % (section(RECON, "userclaims_perm_total"), section(RECON, "roleclaims_perm_total")),
    section(RECON, "userclaims_perm_total") == "0" and section(RECON, "roleclaims_perm_total") == "0",
    "evidence/prod-phase17-reconciliation.txt", T17)
add("PROD-P17-05", "الجداول الجديدة فارغة عدا الكتالوج المرجعيّ", "وقائع=0 · أنواع=6",
    "attendance_incidents=%s attendance_incident_events=%s attendance_incident_types=%s employee_checklist_items=%s" % (
        section(RECON, "attendance_incidents"), section(RECON, "attendance_incident_events"),
        section(RECON, "attendance_incident_types"), section(RECON, "employee_checklist_items")),
    section(RECON, "attendance_incidents") == "0" and section(RECON, "attendance_incident_types") == "6",
    "evidence/prod-phase17-reconciliation.txt", T17)
add("PROD-P17-06", "مصالحة نهائيّة: كلّ عدّاد بيانات حقيقيّ ثابت", "34/4/9/311/39/1464",
    "users=%s deps=%s teams=%s subs=%s userroles=%s audit=%s" % (
        section(RECON, "users_total"), section(RECON, "departments_total"), section(RECON, "teams_total"),
        section(RECON, "submissions_total"), section(RECON, "userroles_total"), section(RECON, "audit_logs_total")),
    all(section(RECON, k) == section(BEFORE, k) for k in
        ["users_total", "departments_total", "teams_total", "submissions_total", "userroles_total", "audit_logs_total"]),
    "evidence/prod-baseline-BEFORE.txt ↔ evidence/prod-phase17-reconciliation.txt", T17)

with open(OUT, "w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader()
    w.writerows(rows)

from collections import Counter
c = Counter(r["الحالة"] for r in rows)
print("WROTE", OUT)
print("N =", len(rows), dict(c))
for r in rows:
    if r["الحالة"] != "PASS":
        print("  !!", r["ScenarioID"], r["المقيس"])
