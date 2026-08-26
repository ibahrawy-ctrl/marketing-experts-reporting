# مذكّرة الإغلاق — دورة `DEF-P123-RC-001` → الإنتاج (26 أغسطس 2026)

**الحكم النهائيّ:** `P123_PRODUCTION_DEPLOYMENT_PASS`

---

## 1) المعرّفات

| المعرّف | القيمة |
|---|---|
| `HOTFIX_SHA` | `59f483ebd86211a793bd96a5b2a602fda123d36f` |
| `DEVELOP_MERGE_SHA` | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| `RC_CANDIDATE_SHA` | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| `PRODUCTION_CANDIDATE_SHA` | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| الإصدار السابق على الإنتاج | `7e063b493b50ad90ba6131e47042c7cd035fb65b` (18 أغسطس) |
| `origin/develop` | `897c9b18…` (المرشّح هو رأس develop) |
| الوسوم | **صفر** — لم يُنشأ وسم ولم يُدفَع |
| `BACKUP_ID` | `/var/backups/reporting/20260826-P123` |

**RC والإنتاج يشغّلان الـSHA نفسها** (`1.0.0+897c9b18…`)، والخلفيّة رُقّيت **بايتًا ببايت** من نفس حزمة النشر التي تحقّقت على RC (`Reporting.Api.dll` md5 `46e0a3a169c9ef2285aef3b8e9d7fe13`). الواجهة **أُعيد بناؤها حصرًا** من الـSHA نفسها لأنّ `VITE_API_BASE_URL` مخبوز داخل الحزمة، وحزمة RC تشير إلى نطاق RC.

## 2) حزمة المخرجات الستّ عشرة

| # | البند | المسار |
|---|---|---|
| 1 | Hotfix implementation report | `../P123-RC001-HOTFIX-20260826/P123-RC001-HOTFIX-IMPLEMENTATION-REPORT-20260826.md` *(مرجع تاريخيّ — لم يُعدَّل)* |
| 2 | Hotfix test report | `../P123-RC001-HOTFIX-20260826/P123-RC001-HOTFIX-TEST-REPORT-20260826.md` *(مرجع تاريخيّ)* |
| 3 | RC redeployment report | `../P123-RC-REVALIDATION-20260826/P123-RC-HOTFIX-VALIDATION-REPORT-20260826.md` *(مرجع تاريخيّ)* |
| 4 | RC scenario matrix | `../P123-RC-REVALIDATION-20260826/P123-RC-REVALIDATION-SCENARIO-MATRIX-20260826.csv` *(99 سيناريو)* |
| 5 | Updated defect register | `P123-PROD-DEFECT-REGISTER-20260826.csv` |
| 6 | Production preflight report | `P123-PROD-ROLLBACK-PLAN-20260826.md` §2–3 + `evidence/prod-baseline-BEFORE.txt` + `evidence/prod-preflight-uniqueness.txt` |
| 7 | Shadow migration report | `P123-PROD-ROLLBACK-PLAN-20260826.md` §4 + `evidence/prod-shadow-rehearsal.txt` |
| 8 | Backup/restore proof | `evidence/prod-backup.txt` (SHA-256 + تحقّق TOC بـ`pg_restore -l`) |
| 9 | Production deployment report | `P123-PRODUCTION-DEPLOYMENT-REPORT-20260826.md` |
| 10 | Production smoke matrix | `P123-PROD-DEPLOYMENT-SCENARIO-MATRIX-20260826.csv` (46 صفًّا · 46 PASS) |
| 11 | Production reconciliation report | التقرير §7 + `evidence/prod-phase17-reconciliation.txt` |
| 12 | Rollback readiness report | `P123-PROD-ROLLBACK-PLAN-20260826.md` (ثلاث درجات + ستّة معايير إطلاق) |
| 13 | Evidence index | `P123-PROD-EVIDENCE-INDEX-20260826.csv` (31 مدخلًا ببصمات md5) |
| 14 | Closure note | هذا الملفّ |
| 15 | تقرير Word عربيّ RTL | `P123-PRODUCTION-DEPLOYMENT-REPORT-AR.docx` |
| 16 | PDF مطابق ومراجَع بصريًّا | `P123-PRODUCTION-DEPLOYMENT-REPORT-AR.pdf` (7 صفحات) |

**لم يُعدَّل أيّ تقرير سابق ولا أيّ حكم سابق.** الحزم السابقة مرتبطة كمراجع تاريخيّة فقط.

## 3) بوّابة الحكم النهائيّ — بندًا بندًا

| الشرط | المقيس | الحكم |
|---|---|---|
| `DEF-P123-RC-001` مغلق جذريًّا | الشرط انتقل إلى تعبير `Expression` واحد قابل للترجمة إلى SQL داخل `IQueryable` قبل `Count`/`Skip`/`Take`/الإسقاط · 10/10 على المِشدّ الموجَّه | ✔ |
| القائمة والتفاصيل والعدّادات بقواعد رؤية متّسقة | `Attendance_List_And_Detail_UseEquivalentVisibilityRules` أخضر ضمن 14/14 | ✔ |
| RC الجديد صفر FAIL وصفر BLOCKED أمنيّ | 99 سيناريو: 97 PASS · 2 SUPERSEDED · **0 FAIL** · **0 BLOCKED** | ✔ |
| صفر عيب P0/P1/P2 مفتوح | 0 | ✔ |
| كلّ الاختبارات خضراء | وحدوي 556/556 · تكامل 2188/2188 (جولتان متطابقتان) · حضور/أمن 14/14 · TypeScript بلا خطأ · Vitest 735/735 في 62 ملفًّا · بناء Release `0 Error(s)` | ✔ |
| RC والإنتاج على نفس الـSHA | `897c9b18…` على الاثنين | ✔ |
| Shadow migration والمصالحة خضراوان | 42→47 هجرة في 6.2 ث · كلّ بصمات البيانات ثابتة قبل/بعد | ✔ |
| صحّة الإنتاج والسجلّات والواجهة خضراء | `/health` 200 في 8/8 عيّنات · `warning_or_worse_lines=0` · `CONSOLE_ERRORS=0` في 6 لقطات | ✔ |
| صفر تسريب أو تجاوز نطاق | فحوص التسريب الاصطناعيّ = 0 | ✔ |
| صفر فرق بيانات غير مفسَّر | الفرق الوحيد: 6 صفوف `attendance_incident_types` (كتالوج مرجعيّ يُبذَر idempotent عند الإقلاع) | ✔ |
| البريد والإشعارات بلا أثر غير مقصود | `email_outbox = 0` في كلّ مرحلة · القنوات أُطفئت قبل إعادة التشغيل وأُعيدت على مرحلتين مرصودتين | ✔ |
| الصلاحيات deny-by-default بلا منح حقيقيّ | `AspNetUserClaims(perm) = 0` و`AspNetRoleClaims(perm) = 0` قبل وأثناء وبعد | ✔ |
| Rollback مثبت وجاهز | ثلاث درجات موثَّقة · حارس تراجع تلقائيّ مضمَّن في سكربت النشر (لم يُفعَّل) · القِطع السابقة محفوظة على الخادم | ✔ |

## 4) هل حدث تراجع؟

**لا.** `/health` صار أخضر خلال **10 ثوانٍ**، والحارس التلقائيّ (سقف 90 ثانية) لم يُفعَّل، ولم تتحقّق أيّ من معايير الإطلاق الستّة.

## 5) ما نُظِّف وما بقي عمدًا

**نُظِّف:**
- قاعدة الظلّ `reporting_prod_shadow_p123` على الخادم (أُسقِطت بعد استخراج الأدلّة — تأكيد: لا قاعدة باسم `%shadow%` أو `%p123%` باقية على الإنتاج).
- تسع قواعد اختبار محلّيّة أنشِئت لهذه الدورة على جهاز التطوير: `reporting_mc897_{main,kpi,cal,p2,pfe}` · `reporting_hf_full1` · `reporting_rc001_{pre,post}` · `reporting_merge_897c9b1`.
- نفق SSH المحلّيّ `-L 15092` الذي فُتِح لهذه الجولة (PID 40105).
- سكربت التحقّق البصريّ المؤقّت `reporting-frontend/prod-ui-check.mjs` (أُزيل بعد التقاط اللقطات).

**بقي عمدًا:**
- على الخادم: `/opt/reporting/publish-pre-p123-20260826` · `/opt/reporting/reporting-frontend/dist-pre-p123-20260826` · `/etc/reporting-api.env.pre-p123` — نقطة عودة فوريّة للدرجة (أ) بلا فكّ أرشيف.
- النسخة الاحتياطيّة الكاملة `/var/backups/reporting/20260826-P123` (قاعدة + ثنائيّة + واجهة + بيئة + وحدة systemd).
- سكربتا البروفة على الخادم `/tmp/shadow-run.py` و`/tmp/fix-shadow-owner.py` (لا يحويان أيّ سرّ — يقرآن البيئة وقت التشغيل).
- شجرات عمل git المحلّيّة (منها `p123-rc001-merge-20260826` على المرشّح) وأنفاق SSH سابقة لهذه الجولة (PIDs 38646 و91056) — لم تُنشأ هنا فلم تُمسّ.
- تعديلات المستخدم غير المودَعة في شجرة العمل الرئيسة (` M CLAUDE.md` + ملفّان في `Ops/R21/` + `Ops/UAT/`) — **لم تُودَع ولم تُلمَس**، لأنّ الإيداع خارج نطاق التصريح.

## 6) القرار التشغيليّ المؤجَّل

**ملكيّة صلاحيات `perm` — بانتظار قرار مالك المنتج.**
آليّة الصلاحيات منشورة وفعّالة على الإنتاج (`RequireClaim(AppPermissions.ClaimType, …)`)، وعدد المنح الحقيقيّة **صفر** عمدًا. أعلام `Phase2` الأربعة (`Employee360Enabled`, `AttendanceEnabled`, `HrOperationsEnabled`, `EmployeeChecklistEnabled`) غائبة عن بيئة الإنتاج ⇒ `false`.
إشعال أيّ منها يكشف سطحًا **لا يستطيع أحد تشغيله** لأنّ تشغيله يستلزم منح مطالبة `perm` صريحة لمستخدم حقيقيّ — وهو ما يمنعه التصريح التنفيذيّ لهذه الجولة. لذلك يلزم قرار صريح من مالك المنتج يحدّد: مَن يملك `Attendance.Review` و`HrOperations.View/Export`، وبأيّ إجراء اعتماد، وقبل أيّ إشعال للأعلام.

---

`P123_PRODUCTION_DEPLOYMENT_PASS`
