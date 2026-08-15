# SUBMITTED-REPORTS-MISSING-EXPECTED-OVERDUE-R1 — PRODUCTION CONTROLLED DEPLOYMENT AND ACCEPTANCE

> النطاق: **الإنتاج فقط**. RC لم يُمَسّ. **بلا هجرة، بلا كتابة بيانات، بلا git tag.** ترقية artifacts المعتمَدة في RC حرفيًّا (لا إعادة بناء).

## هوية المرشّح (LIVE على الإنتاج)
- Candidate HEAD: `24575f6be17f5cf722f1b329d2c9cc4a75c2c297`
- Legal parent: `50113efbcc88630064e0d2bbb827af39deec3204`
- RC Acceptance = COMPLETE AND ACCEPTED (المرجع: `SUBMITTED-REPORTS-MISSING-EXPECTED-OVERDUE-R1-RC-DEPLOYMENT-ACCEPTANCE-REPORT.md`)
- Deploy TS: `20260723-110006` — Deployed 2026-07-23 11:02:44 UTC

## بيئة الإنتاج
- Service `reporting-api.service` / bind `127.0.0.1:5090` / DB `reporting_prod` (user `reporting_app`)
- Backend `/opt/reporting/publish` — Frontend `/opt/reporting/reporting-frontend/dist`
- EnvironmentFile `/etc/reporting-api.env` — Domain `reports.emarketingacademy.net`
- بوابات محفوظة كما كانت: `Email__Enabled=false`, `Reminders__Enabled=true`, `Scheduler__Enabled=true`؛ env mtime `2026-07-17 12:29:37` **بلا تغيير**.

## المرحلة 1 — تحقّق ما قبل النشر (قراءة فقط) — PASS
- الهوية السابقة الحيّة SourceLink `50113ef` (كل DLL)؛ DB=`reporting_prod`؛ health 200/200؛ migration head `20260716015239_KpiEvaluationPartialUniqueIndex` count=29؛ لا نسب غير متوقّع.

## المرحلة 2 — النسخ الاحتياطية الإلزامية — PASS
- DB: `/root/db-backups/reporting_prod-premissingr1-20260723-110006.dump` (pg_dump -Fc، `pg_restore --list` exit=0، sha256 `c0cc478e…`، 900350 bytes)
- Backend: `/opt/reporting/publish-backup-missingr1-20260723-110006` (مطابق للهوية السابقة 50113ef، Development.json غائب)
- Frontend: `/opt/reporting/reporting-frontend/dist-backup-missingr1-20260723-110006` (`index-Bj57LtNl.js`)
- Config: `/root/config-backup-missingr1-20260723-110006` (env، systemd unit، nginx، appsettings.json) + PRE/POST manifests (بلا أسرار)

## المرحلة 3 — إثبات تطابق الـartifacts (24575f6) — PASS
- الحزمة المحلية `publish-rc/` = المُدرَّجة على الخادم = الهوية الحيّة بعد التبديل، sha256 مطابق بايتيًّا:
  - `Reporting.Api.dll` `1510845e6415ab3a6ca57762671751e38b3482340ec7b64017b160f5831b4f8c` (338432)
  - `Reporting.Application.dll` `9483a8dce52fd85b3a88d6520ec5acdf5502a4d8ae4749fe21800acbd8b92ae3` (1401856)
  - `Reporting.Domain.dll` `acf9447abfdb9e45a4366913e95a0f41813845ca7391fa0e75d824bd9b0c9bfb` (88064)
  - `Reporting.Infrastructure.dll` `b5f746dd996751058b05341d8b1e7c17820cad026a8b0161ddfef17db828c8d8` (3574272)
- appsettings.json المُدرَّج = appsettings.json الإنتاج بايتيًّا (لا تغيير).

## المرحلة 4 — نشر الـbackend أولًا — PASS
- rsync `--delete --exclude appsettings.Development.json` (0 حذف إضافي؛ فقط LatoFont/runtimes وكلاهما موجود) + chown www-data + restart.
- كل DLL حيّ = 24575f6 (SourceLink مؤكّد، 50113ef غائب)؛ Development.json غائب.
- سجلّ الإقلاع: **«No migrations were applied. The database is already up to date»** + Hosting environment=Production + Now listening 127.0.0.1:5090.
- health 200/200؛ NRestarts=0؛ head `20260716015239` count=29 (بلا تغيير).

## المرحلة 5 — التبديل الذرّي للواجهة — PASS
- المُدرَّج = التذكرة بايتيًّا؛ التبديل بـ rename (dist القديم محفوظ `dist-old-swap-…`).
- index.html يشير إلى `index-D6wU43rH.js`؛ الهاش المُقدَّم عبر HTTPS مطابق للتذكرة؛ safety: localhost:5090=0، 127.0.0.1=0، domain=0، sourcemaps=0.
- homepage 200؛ bundle 200؛ anonymous `/api/submissions/overview`=401 و`/api/dashboard/me`=401.

## المرحلة 6 — اختبار دخاني مُصادَق قراءة فقط (حساب موجود break-glass admin، بلا إعادة تعيين، التوكن لم يُطبع) — PASS
- **A** period behavior: TotalCount(187)==Summary.Total(187)؛ rowKind/status سلاسل نصّية.
- **B** Overdue: كل صفّ IsOverdue=true؛ القائم فقط Draft/Returned وإلا ExpectedMissing؛ overdueCount(64)==existingOverdue(2)+missingOverdue(62)؛ TotalCount==Total.
- **C** NeedsAction: SubmissionId غير null، ExistingSubmission فقط، status ∈ {Draft,Returned,Escalated} (7)؛ لا ExpectedMissing.
- **D** MineApproval: currentApproverId==المستخدم المصادَق؛ لا ExpectedMissing؛ summary.waitingMyApprovalCount==عدد الصفوف (0 للأدمن — ليس معتمِدًا حاليًّا لأي تسليم).
- **E** Missing (71 صفًّا) عرض فقط: SubmissionId=null، HasSubmission=false، IsExpectedSubmission=true، status=NotSubmitted؛ لا تدخل NeedsAction/MineApproval؛ لا تدخل Closed إطلاقًا.
- **F** الفلاتر: status/period محترمة؛ filtered TotalCount==Summary.Total؛ Returned/Closed صحيحة.

## المرحلة 7 — سلامة البيانات — PASS
- report_submissions=120، users=35، templates=41، audit_logs=820.
- توزيع الحالة: Draft=3، Returned=4، Submitted=60، ApprovedByDirectManager=7، Closed=46.
- إعادة فتح العرض 6 مرّات (baseline/Overdue/NeedsAction/MineApproval/Draft/period) ⇒ **submissions_delta=0، audit_delta=0** (فتح الصفحة يُنشئ صفر صفوف — ExpectedMissing مشتقّ آنيًّا لا يُخزَّن).

## المرحلة 8 — نافذة المراقبة — PASS
- الخدمة active، NRestarts=0، ActiveEnter واحد وقت النشر؛ 0 استثناءات/500 في النافذة؛ health 200×3؛ head ثابت؛ env mtime بلا تغيير.

## المرحلة 9 — ما بعد النشر
- POST manifest بلا أسرار على الخادم: `/root/config-backup-missingr1-20260723-110006/POST-DEPLOY-MANIFEST-missingr1-20260723-110006.md`
- **لم يُنشأ أي git tag** (يتطلّب تفويضًا منفصلًا). staging المؤقّت أُزيل؛ النسخ الاحتياطية محفوظة.

## Rollback (عند الحاجة لاحقًا)
- Backend: استعادة `/opt/reporting/publish-backup-missingr1-20260723-110006` + chown www-data + restart.
- Frontend: استعادة `dist-backup-missingr1-20260723-110006` (أو `dist-old-swap-…`) + chown.
- DB (إن لزم): من `reporting_prod-premissingr1-20260723-110006.dump`. **لا هجرة لعكسها** (لم تُطبَّق أي هجرة).

## الحكم
**PRODUCTION DEPLOYMENT COMPLETE — 24575F6 VERIFIED LIVE, FILTERED COUNTERS, MISSING OVERDUE, NEEDS ACTION AND WAITING-MY-APPROVAL ACCEPTED — DATA INTEGRITY PRESERVED**
