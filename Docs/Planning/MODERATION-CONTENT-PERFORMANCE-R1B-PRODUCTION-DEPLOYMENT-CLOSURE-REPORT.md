# MODERATION-CONTENT-PERFORMANCE-R1B — تقرير النشر على الإنتاج والإغلاق

- **التاريخ:** 2026-07-20
- **البيئة:** الإنتاج — `reports.emarketingacademy.net`، خدمة `reporting-api`، ربط داخلي `127.0.0.1:5090`، قاعدة `reporting_prod`، `ASPNETCORE_ENVIRONMENT=Production`.
- **الالتزام (Commit):** `1b8dc7f80df805c71339aba4b77a4d93f7754e39` (الأب `265fa7a3c8c20d9db447d6978c44a2db5f13483a`).
- **شجرة العزل:** `/tmp/release-mod-r1b-prod-20260720-170338` (detached HEAD على `1b8dc7f`، git status نظيف).
- **المسار المعتمَد:** نشر V6 عبر أداة النشر المعزولة (Publisher، خارج الحل) + إعادة توجيه مسودّة W30 المؤهَّلة فقط + نشر الواجهة فقط من نفس الالتزام. **بلا نشر/إعادة تشغيل خدمة الـ API، بلا Migration، بلا Seeder، بلا SQL لتغيير بيانات الأعمال.**

---

## 1) خطّ الأساس المعزول (PROD-1)
- diff الالتزام مقابل الأب = **7 ملفات فقط** لا غير: `ModerationV6PublisherTests.cs` (A)، `Reporting.IntegrationTests.csproj` (M)، `ModerationV6Publisher.csproj` (A)، `Program.cs` (A)، `Publisher.cs` (A)، `ProjectRepeatableGrid.test.tsx` (M)، `SubmissionsPage.tsx` (M).
- لا تغييرات خارج النطاق (لا Backend API، لا Schema، لا Migration، لا قوالب أخرى).

## 2) فحص ما قبل النشر — قراءة فقط (PROD-2)
- الخدمة `reporting-api` نشطة، NRestarts=0.
- قالب المديرشن `db8c764d-6f10-4997-8c07-512334dad30b` كان يحمل **5 إصدارات (v1–v5)**، **V6 لم يكن موجودًا** ⇒ الأداة ستُنشئه.
- مسودّة W30 (`00c0f706-ca8b-468c-817a-c139268c9dc0`) = المرشّح الوحيد المؤهَّل (Draft، قيم=0، غير فارغ=0، مرفقات=0، مراجع=0، بلا تعارض فريد).
- W28/W29 حالتهما **Closed** ⇒ ليستا مرشّحتين (محميتان).
- عدّاد الهجرات = **29**، الرأس `20260716015239_KpiEvaluationPartialUniqueIndex`.

## 3) النسخ الاحتياطية قبل الكتابة (PROD-3)
- طابع النشر: `/root/modr1b-prod-ts.txt` = **20260720-140649**.
- قاعدة الإنتاج: `/root/db-backups/reporting_prod-premodr1b-20260720-140649.dump` (**820946 bytes**، 344 مدخلًا، مقروء).
- واجهة الإنتاج: `/root/db-backups/prod-frontend-dist-premodr1b-20260720-140649.tar.gz` (**337903 bytes**، 9 ملفات، مقروء).

## 4) معاينة الأداة (Dry-Run) (PROD-4)
- الحصيلة = **Planned** (معاينة، بلا كتابة، Rollback).
- V5 = `1638f686-65f7-460b-9754-ba50804eda16` (v5)، 15 حقلًا فرعيًّا؛ 6 مفاتيح مقترحة للإضافة.
- مسودّة W30 = مؤهَّلة، لم يُكتب شيء (عدّاد الهجرات 29→29 ثابت).

## 5) التطبيق مرة واحدة + التحقّق + المعاينة الثانية (PROD-5)
- **`--apply` نُفِّذ مرّة واحدة فقط** — الحصيلة = **Applied (Commit)**.
- **V6 المنشور = `90d78144-e689-45af-a9bc-a9e66a24f714`، VersionNumber = 6، Published = true.**
- 6 مفاتيح مضافة **فقط**، جميعها `required=false`: `content_highlights, audience_insight, lessons_learned, decisions_required, risk_exists, risk_note`.
- W30 أُعيد توجيهها إلى V6 (Repointed=نعم)؛ 0 محظورة.
- عدّاد الهجرات 29→29 (ثابت — الأداة لا تشغّل Migration).

### التحقّق بعد التطبيق (قراءة فقط)
| البند | النتيجة |
|---|---|
| إصدارات قالب المديرشن | **6 (v1..v6)** |
| V5 (`1638f686`) | منشور، **15** حقلًا فرعيًّا، canonical hash **`c99b26f9b35dc92c650a6dc1068c322e`** (ثابت) |
| V6 (`90d78144`) | منشور، **21** حقلًا فرعيًّا، hash `e065e7726e068f1ed4fc41288a0b4ed0` |
| المفاتيح المضافة (V6−V5) | **6 فقط** (المذكورة أعلاه)، كلها `required=false` |
| المفاتيح المحذوفة (V5−V6) | **0** — الـ15 القديمة محفوظة |
| W28 (`2a7a831c`) | **Closed، V5، IsDeleted=false** — بلا تغيير |
| W29 (`1632d2e1`) | **Closed، V5، IsDeleted=false** — بلا تغيير |
| W30 (`00c0f706`) | **Draft، V6** (أُعيد توجيهها) — نفس المعرّف/المُرسِل/الفترة |
| تسليمات مكرّرة (submitter, periodKey) | **0** |
| قيم يتيمة (submission_field_values بلا أب) | **0** |
| قوالب أخرى حصلت على إصدار عند/بعد إنشاء V6 | **0** — قالب المديرشن فقط |

- **المعاينة الثانية (Dry-Run)** = **AlreadyApplied (idempotent)**: لا إجراء، لا مسودّات مرشّحة، الهجرات 29→29 ثابتة.

> ملاحظة: معرّف V6 المطبَّق (`90d78144`) يختلف عن المعرّف الذي اقترحته معاينة سابقة — سلوك متوقّع لأن الـGUID يُولَّد لكل تشغيل عند لحظة الالتزام.

## 6) بناء الواجهة من شجرة العزل (PROD-6)
- التثبيت عبر `npm ci` (حسب lockfile).
- **Vitest كامل: 224/224 خضراء** (24 ملفًّا)؛ ومجموعة `ProjectRepeatableGrid.test.tsx` = **51/51**.
- Typecheck نظيف؛ `npm run build` ناجح (تحذير signalr `/*#__PURE__*/` الحميد فقط).
- **فحص الأصول:** localhost = وحيد داخل كود مكتبات طرف ثالث (`http://localhost` كاحتياطي حين `location.origin===null`، لا يُفعَّل في المتصفّح عبر HTTPS) — **ليس قاعدة الـAPI**. 127.0.0.1 = **0**، `rc-report.emarketingacademy.net` = **0**، قاعدة الـAPI الإنتاجية `reports.emarketingacademy.net/api` = **موجودة**.
- الحزم الجديدة: `index-DiC09SC_.js` + `index-PCeF1KCH.css`.

## 7) نشر الواجهة فقط (PROD-7) — قابل للرجوع
- الحزم قبل النشر: `index-COB6nGUW.js` + `index-DJljDyE4.css` (نسخة احتياطية محفوظة من PROD-3).
- rsync لمجلد `dist` الجديد إلى `/opt/reporting/reporting-frontend/dist` + `chown www-data`.
- بعد النشر: `index.html` يشير إلى الحزم الجديدة؛ JS/CSS الجديدة = **200**؛ القديمة = **404**؛ health داخلي+عام = **200**؛ رابط SPA عميق = **200**.
- **NRestarts = 0** (لا إعادة تشغيل)، الخدمة active/running.
- **md5 لـ `Reporting.Api.dll` = `ea40b5a969a35edbd5655a1a59d5e660` (بلا تغيير)** — لم يُنشر أي Backend.

## 8) اختبار القبول على الإنتاج (PROD-8) — قراءة فقط، بلا بيانات اختبار دائمة
- W28/W29 (V5، Closed): تُصيَّر عبر الـAPI بنجاح (200)، مرتبطتان بـV5 ⇒ لا تظهر فيهما حقول V6 الستّة الجديدة (ضمان بنيوي عبر ربط الإصدار).
- W30 (V6، Draft): تُصيَّر (200)، نفس المعرّف/المُرسِل/الفترة، بلا فقدان بيانات (كانت مسودّة فارغة مؤهَّلة وبقيت فارغة).
- **لم يُكتب أي شيء في مسودّة موظّف حقيقي، ولم تتغيّر حالة أي تقرير.** السلوك الوظيفي الكامل (تصيير 21 حقلًا، إعادة ترتيب `content_highlights`، مفتاح المخاطر بلا فقدان `risk_note`) مغطًّى مسبقًا في اختبار قبول RC.

## 9) التحقّق النهائي من البيانات (PROD-9)
| البند | النتيجة |
|---|---|
| عدّاد الهجرات / الرأس | **29 / `20260716015239`** (بلا تغيير) |
| إصدارات قالب المديرشن | **6** |
| V5 count/hash | 15 / `c99b26f9…` (ثابت) |
| V6 count/hash | 21 / `e065e772…` |
| التقارير القديمة | W28/W29 على V5، W30 على V6 — كلها على نُسخها الصحيحة |
| تكرار/يتيم | 0 / 0 |
| قوالب أخرى تغيّرت | 0 |
| NRestarts / حالة الخدمة | 0 / active (لا إعادة تشغيل Backend) |
| md5 لـ Reporting.Api.dll | `ea40b5a9…` (بلا تغيير) |
| تغيير env | **لا شيء** (لم يُلمَس `/etc/reporting-api.env`؛ ولا إعادة تشغيل ⇒ لا إعادة تحميل إعدادات) |
| Email__Enabled | `false` (بلا تغيير) — `email_outbox` صفر صف معلّق |
| الواجهة المُقدَّمة | الحزمة الجديدة `index-DiC09SC_.js` |
| health عام | 200 |

## 10) الحدود التي لم تُمَسّ
لا نشر/إعادة تشغيل خدمة الـ API، لا Migration، لا Seeder، لا تعديل V5، لا حذف/إنشاء أي Submission يدويًّا، لا تغيير لحالة أي تقرير Submitted/Returned/Approved/Closed، لا تعديل env/nginx/الخدمة، لا Email/Reminders/Scheduler تغيير، لم يُشغَّل Account Manager، ولم تُنشأ بيانات اختبار دائمة على الإنتاج. لم تُطبَع أي أسرار.

## 11) إجراء الرجوع (Rollback)
- **الواجهة:** استعادة `/root/db-backups/prod-frontend-dist-premodr1b-20260720-140649.tar.gz` إلى `/opt/reporting/reporting-frontend/dist` + `chown www-data` (بلا إعادة تشغيل — الواجهة ثابتة).
- **إصدار V6 (عند اللزوم فقط):** إلغاء نشر/حذف الإصدار v6 `90d78144` وإعادة توجيه W30 إلى V5، أو استعادة نسخة القاعدة `/root/db-backups/reporting_prod-premodr1b-20260720-140649.dump`.
- لا Migration لعكسها (لم تُطبَّق أي هجرة).

---

## الحكم النهائي

**MODERATION R1B DEPLOYED TO PRODUCTION — CONTENT ANALYSIS OPERATIONAL CLOSURE COMPLETE**
