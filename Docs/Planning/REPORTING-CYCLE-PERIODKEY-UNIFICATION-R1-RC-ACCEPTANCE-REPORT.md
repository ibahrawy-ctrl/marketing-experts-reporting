# تقرير قبول RC — توحيد PeriodKey لدورة التقارير + إصلاح أيّام العمل اليوميّة + فصل بوّابة السبت (R1)

> **الحالة:** RC مقبول بالكامل — **بانتظار تفويض إنتاج صريح**. لا نشر إنتاج في هذا التقرير.
> **اللغة:** عربيّة كاملة؛ المعرّفات التقنيّة/المسارات/الأوامر/الـ hashes/سلاسل القرار بصيغتها الأصليّة.

---

## 1) هوية المرشّح والأساس

| البند | القيمة |
|---|---|
| المرشّح النهائيّ (Candidate) | `e66f1c86e8e976b05c421fdbaf234d157666060d` |
| الأب (Parent baseline) | `447fe3e1de70021e0eda9a7bfb8e44dc9d8773b6` |
| أساس الـ RC قبل المرشّح | `a3249cdb` |
| طبيعة التغيير | **Backend code-only** في 8 ملفات خدمة. **بلا** migration/snapshot/csproj/dependency/Frontend-prod |
| الأدلّة المحلّيّة | Unit 283/283، Frontend 271/271، لا تغيير Migration/Snapshot/Frontend |

**جوهر المرشّح:** توحيد PeriodKey (يقبل مفاتيح غير قانونيّة `6-7-2026`, `2026-07-9` ويحلّها لليوم المنطقيّ مع حفظ المفتاح الخام) + إصلاح أيّام العمل اليوميّة (الأحد→الخميس) + فصل بوّابة السبت (تسليم السبت مرئيّ تاريخيًّا لكنه مستبعَد من العدّ). السياسات المركزيّة في `ReportingCalendarPolicy`: `IsDailyExpectedBusinessDay`, `IsDailyHoliday` (جمعة+سبت), `IsDailySubmissionBlockedDay` (جمعة فقط), `DailyExpectedDates`.

## 2) طوبولوجيا RC (مقبولة رسميًّا)

| الخدمة | المنفذ | البيئة | القاعدة | الحالة |
|---|---|---|---|---|
| `reporting-api.service` (الإنتاج) | 5090 | Production | reporting_prod | يعمل — **لم يُمَسّ** |
| `khubara-reporting-rc.service` (RC) | 5092 | ReleaseCandidate | reporting_rc | يعمل — بيئة القبول |
| `khubara-reporting-test.service` | 5091 | Testing | — | موجود |
| `academy-api` | 5000 | — | — | مشروع منفصل |

- EnvironmentFile للـ RC: `/etc/khubara-reporting-rc.env`؛ ExecStart: `/opt/reporting-rc/publish/Reporting.Api.dll`.
- المصادقة على RC: سكّ توكن HS256 خادميًّا (قراءة `Jwt__Key/Issuer/Audience` من env بلا طباعة، claims قصيرة `sub/nameid/email/role` مطابقةً لما يصدره `TokenService` بعد الـ outbound-mapping). الأسرار لم تُطبَع إطلاقًا.

## 3) المراحل 0–5 (مكتملة بأدلّة سابقة)
توفّر RC → صحّة قراءة-فقط → Baseline RC → Backups → بناء Artifact → نشر RC Backend-only. كلها مغلقة بأدلّة قبل جلسة القبول هذه.

## 4) المرحلة 6 — قبول W28 الحيّ (ريم جاب الله)

**الهدف** (الموظّفة `9102630f-bbd8-4121-b8e5-34cb6ebce170`، دورة `2026-W28`): متوقّع=5، مُسلَّم=4، ناقص=1، ناقص-متأخّر=1، النسبة=80%. أيّام متوقّعة: 07-05(أحد)..07-09(خميس)؛ مستبعَد: 07-04(سبت)، 07-10(جمعة).

**الحقيقة الأرضيّة من القاعدة (المفاتيح الخام محفوظة):**
| المفتاح الخام | اليوم المنطقيّ | الحالة |
|---|---|---|
| `2026-07-04` | 07-04 (سبت) | Submitted — مرئيّ، **غير محسوب** |
| `6-7-2026` | 07-06 | Submitted — محسوب |
| `2026-07-07` | 07-07 | Submitted — محسوب |
| `2026-07-08` | 07-08 | Submitted — محسوب |
| `2026-07-09` | 07-09 | Draft |
| `2026-07-9` | 07-09 | Submitted — محسوب |
| (لا سجلّ) | 07-05 | **الناقص الوحيد** |

**النتيجة:** متوقّع=5، مُسلَّم=4 (06/07/08/09)، ناقص=1 (07-05)، السبت 07-04 مرئيّ مستبعَد، لا التزام يوم الجمعة 07-10، **لا Expected=7**. ✔

## 5) المرحلة 7 — المفاتيح التاريخيّة
`6-7-2026`→07-06، `2026-07-9`→07-09 (بالإضافة إلى `2026-07-06`, `2026-07-09` القانونيّة): تُحلّ لليوم المنطقيّ الصحيح، المفتاح الخام باقٍ في القاعدة والـ DTO، الفعليّ يظهر، **لا ExpectedMissing مكرّر، لا MissingOverdue كاذب، لا حذف/دمج، السجلّات المتعدّدة لنفس اليوم تبقى مرئيّة، لا صفّ Expected ثالث**. ✔ (07-09 له سجلّان Draft+Submitted، كلاهما مرئيّ.)

## 6) المرحلة 8 — مصفوفة اتّساق الأسطح الثمانية (ريم / W28)

| # | السطح | Endpoint / المصدر | النتيجة | متّسق |
|---|---|---|---|:--:|
| 1 | Submitted Reports Overview | `/api/submissions/overview` (نافذة افتراضيّة، Daily) | 4 فعلي + 1 ExpectedMissing(07-05)؛ 07-04 مرئيّ بلا Missing؛ صفّا 07-09؛ لا 07-10؛ لا Expected=7 | ✔ |
| 2 | ReportDueService | `/api/reports/due/overview` + `/due/overdue` | عنصر متأخّر واحد فقط، dueDate `2026-07-05`، «تسليم 1 تقرير يومي متأخّر» | ✔ |
| 3 | Reporting/Compliance | `/api/reports/submission-compliance` + `/compliance-summary` | «سلّم 4 من 5 يوم (متأخر)»، lateSubmitted=true | ✔ |
| 4 | ReportCalendarService | `/api/report-calendar/sales-daily-compliance` + `/missing-reports` | expectedDays=5، submittedDays=4، missingDays=1، needsReview=true؛ غائبة عن missing-reports الأسبوعيّ | ✔ |
| 5 | ReportReminderService | فحص `ReportReminderService.DailyExpectedDates` (سطر 514–523) | أحد→خميس، يستثني جمعة/سبت، يحدّ عند `today` ⇒ 5 متوقّع، ريم 1 ناقص ⇒ ≤1 تذكير؛ لا 7/سبت/جمعة | ✔ |
| 6 | Dashboard breakdowns | `/api/dashboard/pending-reports` + `/employee-profile` | لا pending كاذب لـ W28 (فقط دورة W30 الحاليّة الشرعيّة)؛ لا Expected=7 | ✔ |
| 7 | ReportingCalendarCycleService | UnifiedCycleStatus عبر pending-reports | enum `OverdueNotSubmitted` صحيح للدورة الحاليّة؛ لا W28 وهميّة | ✔ |
| 8 | ReportingAggregationService | `/api/reporting/aggregation/b2c/new-old` | مُفتَّح على `2026-W28`، submissionsConsidered=2 (معتمَد فقط)، Ignored=0، لا صفوف وهميّة | ✔ |

**الأرقام موحّدة عبر الأسطح:** Expected=5, Submitted=4, Missing=1, MissingOverdue=1, rate=80%. لا Expected=7، لا احتساب سبت/جمعة، لا تذكير كاذب، لا pending كاذب.

## 7) المرحلة 9 — قبول الـ overview الموحّد (`/api/submissions/overview`)

| المعيار | القياس الحيّ | النتيجة |
|---|---|---|
| الافتراضي = All | default items=167 = All items=167 | ✔ |
| Daily/Weekly/All (اتحاد) | Daily=79 + Weekly=88 = All=167 | ✔ |
| Summary.total == totalCount | كل cadence: 79==79, 88==88, 167==167 | ✔ |
| الصفحة لا تغيّر Summary | p1.total=167, p2.total=167 (بينما items/صفحة=10) | ✔ |
| ExpectedMissing عرض-فقط SubmissionId=null | 132 EM، **0** بمعرّف غير فارغ | ✔ |
| Overdue يدمج يومي+أسبوعي | All.overdue=129 = Daily(63)+Weekly(66) | ✔ |
| معادلة overdue | overdueCount = existingOverdue + missingOverdue في كل cadence | ✔ |

## 8) Regression (المتطلّب الجديد)

**(أ) رؤية قائد الفريق ومرؤوسيه** — قائد فريق B2C خالد مجدي (`8284241a-be8c-42f9-92cf-e6442ea8db61`، TeamLeader):
- نطاقه = فريق B2C فقط (5 مُسلِّمين: ريم، زينب، مروة، عائشة، خالد نفسه).
- يرى ذاته ✔، يرى مرؤوسته ريم ✔، **لا يرى خارج نطاقه** (محمد إبراهيم/سوشيال بود 1) ✔.
- صفوف ريم في نطاقه صالحة (07-05 ExpectedMissing، 07-09 Draft بمعرّف حقيقي).

**(ب) «تقاريري» مقابل «تقويم التقارير» لنفس المستخدم/الفترة (ريم/W28):**
- «تقاريري» (منظور ريم): أيام ذات تسليم = {07-04(سبت), 07-06, 07-07, 07-08, 07-09} = 5؛ 07-05 = ExpectedMissing؛ 07-10 غائب.
- «التقويم»: expectedDays=5، submittedDays=4، missingDays=1.
- **الفارق = يوم واحد بالضبط = تسليم السبت 07-04**: التقويم يَعُدّ أيّام العمل فقط (استبعاد السبت)؛ «تقاريري» يُظهِر السبت كسجلّ تاريخيّ مرئيّ مستبعَد من العدّ. **اتّساق بالتصميم** يُثبت فصل بوّابة السبت عبر السطحين (لا فقدان بيانات، لا احتساب مزدوج، لا تضخيم المتوقّع). ✔

## 9) المرحلة 10 — سلامة البيانات + المراقبة

- 6 صفوف خام لريم في نافذة W28، **كل المفاتيح الخام محفوظة بلا تغيير** (`2026-07-9` و`6-7-2026` بصيغتهما غير القانونيّة): لا دمج، لا حذف، لا كتابة تطبيع، لا تكرار (count=1 لكل مفتاح).
- **total report_submissions = 35** — ثابت؛ القراءات لم تُنشئ/تحذف أيّ صفّ.
- 07-09 له سجلّان (Draft + Submitted) — كلاهما محفوظ ومرئيّ.
- **لا أخطاء** في journal الـ RC آخر 30 دقيقة؛ `/health` داخليّ = 200.
- **التنظيف:** أُزيلت كل سكربتات الجلسة (`/root/rc-*.mjs`)، التوكنات (`rc-token.txt`, `rc-khaled.txt`, `rc-rim.txt`)، و`w28-compliance.json`. أُبقيت طوابع نشر RC السابقة (`rc-*-deploy-ts.txt`).

## 10) الحكم النهائيّ على RC

**مقبول بالكامل.** كل الأسطح الثمانية متّسقة، الـ overview الموحّد يحقّق كل المعايير، الـ Regression بشقّيه ناجح، سلامة البيانات محفوظة، لا أخطاء تشغيل. المرشّح `e66f1c86` جاهز لعرض تفويض الإنتاج. **لم يُنشَر إنتاج. لن يُنشَر إلا بتفويض صريح.**

---

# مُلحَق: موجّه نشر الإنتاج (Backend-only) — للاعتماد الصريح لاحقًا

> **لا تُنفَّذ أيّ خطوة إنتاج قبل تفويض المستخدم الصريح.** هذا الموجّه جاهز للنسخ كمهمّة منفصلة.

**العنوان:** نشر إنتاج Backend-only — توحيد PeriodKey + إصلاح أيّام العمل اليوميّة + فصل بوّابة السبت (المرشّح `e66f1c86`).

**الشروط الإلزاميّة:**
1. **المرشّح:** `e66f1c86e8e976b05c421fdbaf234d157666060d` حصرًا (نفس المقبول على RC). التحقّق من hash الـ Artifact قبل النشر.
2. **Baseline الإنتاج:** توثيق آخر migration على `reporting_prod` قبل النشر، وتأكيد أنها لم تتغيّر بعده (**No migrations applied** متوقّع).
3. **Backups قبل النشر (إلزاميّة):** DB dump لـ `reporting_prod` + نسخة `/opt/reporting/publish` (backend). لا حاجة لنسخة frontend (بلا تغيير واجهة).
4. **Backend-only:** `dotnet publish -c Release` من نسخة معزولة للمرشّح + `rsync -az --delete --exclude appsettings.Development.json` → `/opt/reporting/publish` + `chown -R www-data:www-data` + `systemctl restart reporting-api`.
5. **No Migration / No Frontend:** التغيير code-only؛ سجلّ الإقلاع يجب أن يُظهِر «No migrations were applied»؛ لا مساس بـ dist.
6. **Smoke tests (قراءة-فقط، بلا طباعة أسرار):** `/health` داخليّ+عام=200؛ environment=Production؛ آخر migration بلا تغيير؛ عيّنة أسطح W28 (submission-compliance، sales-daily-compliance، submissions/overview) تُظهر Expected=5/Submitted=4/Missing=1 لريم.
7. **تحقّق W28:** تكرار قبول المرحلة 6 على الإنتاج (ريم، 2026-W28): 5/4/1، السبت مرئيّ مستبعَد، لا Expected=7، لا تذكير/pending كاذب.
8. **سلامة البيانات:** عدّ `report_submissions` قبل/بعد ثابت؛ المفاتيح الخام غير القانونيّة (إن وُجدت في الإنتاج) محفوظة بلا تطبيع.
9. **Rollback:** استعادة `publish-backup` + `systemctl restart` (لا migration لعكسها). أو استعادة DB dump عند الضرورة القصوى (غير متوقّع لأن التغيير code-only).
10. **المراقبة:** journal الـ `reporting-api` بلا أخطاء بعد النشر؛ متابعة نافذة قصيرة.
11. **التنظيف:** إزالة كل سكربتات/توكنات الجلسة من الخادم بعد الانتهاء.
12. **STOP:** عرض تقرير نشر إنتاج كامل بالأدلّة قبل إغلاق المهمّة.

**Backups المتوقّعة (نموذج تسمية):** `reporting_prod-preperiodkey-<TS>.dump`، `publish-backup-periodkey-<TS>`.
