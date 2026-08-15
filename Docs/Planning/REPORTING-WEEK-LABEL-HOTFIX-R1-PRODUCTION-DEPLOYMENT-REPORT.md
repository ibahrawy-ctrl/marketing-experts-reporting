# REPORTING-WEEK-LABEL-HOTFIX-R1 — PRODUCTION DEPLOYMENT REPORT

- **النطاق:** Frontend فقط — الحزمة المعتمدة الوحيدة `index-Bpd--Clz.js`. بلا أي تغيير Backend، بلا restart، بلا DB/Migration/Seeder، بلا Email/Reminder/Scheduler، بلا كود جديد.
- **وقت النشر:** 2026-07-19 ~11:08 UTC (rsync atomic إلى Production dist).
- **الخادم:** `root@187.127.72.232` (srv1747233) — خدمة `reporting-api.service`، دومين `https://reports.emarketingacademy.net`.

## ملاحظة صريحة
تُنفَّذ هذه العملية بقرار مالك النظام بالنشر المباشر إلى Production **مع قبول مخاطرة تجاوز authenticated RC UAT** (لم تُتَح حسابات RC حقيقية). الـ UAT التجاري المصادَق ما زال مطلوبًا بعد النشر.

## المرحلة 1 — حالة Production قبل النشر (قراءة فقط)
| # | البند | القيمة |
|---|------|--------|
| 1 | health | داخلي 200 + HTTPS 200 |
| 2 | Backend | active (`reporting-api.service`) |
| 3 | ActiveEnterTimestamp | `Sat 2026-07-18 15:10:40 UTC` |
| 4 | ASPNETCORE_ENVIRONMENT | `Production` |
| 5 | الحزمة السابقة | `index-C0M_G0Dp.js` |
| 6 | hash الحزمة السابقة | `782190018a6210f165db39c5f0a9e66e1e73d1750bb426a436ef684bdaa95752` |
| 7 | آخر migration | `20260716015239_KpiEvaluationPartialUniqueIndex` (إجمالي 29) |
| 8 | البوابات | Email__Enabled=false، EmailNotifications__Mode=DryRun، BackgroundJobs__Enabled=false، Scheduler__Enabled=true، Reminders__Enabled=true (كما هي) |
| 9 | القرص | 72G متاح (26% مستخدم) |
| 10 | مسار dist | `/opt/reporting/reporting-frontend/dist` |

آخر migration مطابق تمامًا لحالة RC. Scheduler/Reminders=true فارق إعداد Backend وقت التشغيل (لم يُمَسّ في نشر Frontend-only). **لا فارق جوهري موجب لـ STOP.**

## المرحلة 2 — النسخة الاحتياطية
- المسار: `/opt/reporting/reporting-frontend/dist-backup-reporting-week-label-hotfix-r1-20260719-111206`
- الملكية: `www-data:www-data`. الحزمة القديمة `index-C0M_G0Dp.js` محفوظة.
- hashات النسخة: index.html `4ed317af…9b8a79` ؛ bundle `782190018a…95752` (مطابقة للأصل).
- لم تُحذف أي نسخة احتياطية سابقة (20 نسخة تاريخية سليمة).

## المرحلة 3 — التحقق من الـ Artifact (اجتاز STOP gate)
- bundle sha256 محلي = `21263c5a8ce9c37a3a174a4e1d8c773b20da02858209796c434f309981d6f632` (مطابق للمعتمد + RC byte-for-byte).
- index.html محلي (`3b73ef92…ecf1b2`) يشير إلى `index-Bpd--Clz.js`.
- قاعدة API = `` `/api` `` نسبية، لا دومين مضمّن، لا `reports.emarketingacademy` داخل الحزمة.
- علامات موجودة: my-cycles / my-days / cycleLabel / reporting-calendar / incoming_messages / avg_response_minutes / converted_opportunities / cases_grid / clients/ / execution / governance / «جهات الاتصال» / «القنوات» / «دورة التقارير من السبت».
- CSS sha256 = `25895648…42bae`. تسريبات 127.0.0.1/5092/5090/reporting_test = 0 ؛ localhost حميد = 2.

## المرحلة 4 — النشر (Frontend فقط)
- `rsync -az --delete` من dist المحلي المعتمد → Production dist + `chown -R www-data:www-data`.
- بلا restart للـ Backend، بلا restart لـ PostgreSQL، بلا migrations.
- index.html بعد النشر يشير إلى `index-Bpd--Clz.js` ؛ الحزمة القديمة `index-C0M_G0Dp.js` أُزيلت.

## المرحلة 5 — التحقق الفوري بعد النشر
| # | البند | النتيجة |
|---|------|--------|
| 1 | HTTPS index | 200 |
| 2 | health | 200 |
| 3 | index.html cache | `no-cache, no-store, must-revalidate` |
| 4 | الحزمة الجديدة | 200 |
| 5 | hash المُقدَّم عبر HTTPS | `21263c5a…d6f632` (مطابق المحلي + RC) |
| 6 | ActiveEnterTimestamp | `Sat 2026-07-18 15:10:40 UTC` (**لم يتغيّر ⇒ Backend لم يُعد تشغيله**) |
| 7 | migrations | 29 / `20260716015239` (**لم تتغيّر**) |
| 8 | البوابات | لم تتغيّر (Email=false، DryRun، Scheduler/Reminders=true، BackgroundJobs=false) |
| 9 | أخطاء جديدة | 0 استثناءات/5xx في السجل |
| 10 | إقلاع الواجهة | سليم (الحزمة القديمة 404، الجديدة تُقدَّم) |

## المرحلة 6 — اختبار الدخان (غير متلِف)
- صفحة الدخول/SPA 200 ؛ CSS 200 ؛ كل الأصول (favicon/icons/logo×2) 200.
- `/api/reporting-calendar/my-cycles` و`/my-days` بدون مصادقة = **401** (موصولة، لا 404/500).
- login ببيانات خاطئة = 401 (Backend سليم).
- الحزمة المُقدَّمة عبر HTTPS تحوي كل علامات الاتحاد (تقويم + إنتاج + Client360 + Execution + Governance) وصفر تسريبات.
- **لم يُتَح حساب Production للقراءة فقط** ⇒ لم يُنفَّذ فحص منتقي الدورة المصادَق (متروك لـ UAT التجاري)؛ لم تُنشأ/تُعدَّل أي بيانات.

## المرحلة 7 — Rollback
- لم يتحقق أي شرط فشل ⇒ **لم يُنفَّذ Rollback.**
- أمر الرجوع عند الحاجة:
```
ssh -i ~/.ssh/academy_vps_ed25519 root@187.127.72.232 \
 'rsync -a --delete /opt/reporting/reporting-frontend/dist-backup-reporting-week-label-hotfix-r1-20260719-111206/ /opt/reporting/reporting-frontend/dist/ && chown -R www-data:www-data /opt/reporting/reporting-frontend/dist'
```

## الحزم
- القديمة: `index-C0M_G0Dp.js` sha `782190018a…95752`
- الجديدة: `index-Bpd--Clz.js` sha `21263c5a…d6f632`
- hashات مطابقة: محلي = RC = Production (HTTPS) = `21263c5a…d6f632`.

## الحكم النهائي
**PRODUCTION FRONTEND DEPLOYMENT PASSED — AUTHENTICATED BUSINESS UAT STILL REQUIRED**
