# جاهزيّة إصدار الإنتاج R22B — تقرير الحسم (Go / No-Go)

- **المرشَّح:** `c5e0202d0a528a1a45856790716e449b812f0184` (`c5e0202`).
- **الإنتاج الحاليّ:** `d25dc69` (31 أغسطس 2026).
- **RC المنشور والمُختبَر عليه:** `RC_DEPLOYED_SOURCE_SHA = c5e0202` على `https://rc-report.emarketingacademy.net` (أصل حقيقيّ، بلا نفق).
- **التاريخ:** 6 سبتمبر 2026. **شجرة العمل:** نسخة معزولة `/private/tmp/r22b-release-20260906` (لم تُمسّ شجرة المستودع الرئيسيّة).
- **التقرير التفصيليّ للبوّابة التشغيليّة والبصريّة:** `RC-OPERATIONAL-AND-VISUAL-GATE.md` (§9 · §10 · §11 · §12).

## 1) النَسَب والنطاق — `d25dc69 → c5e0202`
| القياس | القيمة |
|---|---|
| ملفّات مُعدَّلة خارج `Ops/` | **36** = 6 خلفيّة إنتاجيّة + 8 اختبارات خلفيّة + 11 واجهة إنتاجيّة + 7 اختبارات واجهة + 3 أدوات/E2E + 1 Runbook |
| ملفّات أدلّة تحت `Ops/` | 103 (توثيق فقط، لا أثر تشغيليّ) |
| إجماليّ الفرق | `35 files changed, 3033 insertions(+), 37 deletions(-)` في مسارَي الخادم والواجهة |
| ملفّات الخادم الإنتاجيّة | `ClientModels.cs` · `EmailModels.cs` · `TemplateSeeder.cs` · `Reporting.Infrastructure.csproj` · `ProjectService.cs` · `SubmissionService.cs` |
| ملفّات الواجهة الإنتاجيّة | `NotificationsBell.tsx` · `ProjectReportsTab.tsx` · `api.ts` · `format.ts` · `main.tsx` · `AdminArchivePage.tsx` · `ClientDetailPage.tsx` · `Project360Page.tsx` · `ProjectDetailPage.tsx` · `SubmissionsPage.tsx` · `types/api.ts` |

## 2) الهجرات — **لا ترحيل على الإنتاج في هذا الإصدار**
| المصدر | العدد |
|---|---|
| ملفّات الهجرة في `c5e0202` | **47** |
| مطبَّقة على RC | **49** |
| مطبَّقة على الإنتاج | **49** |

- `rel-rc-migs.txt` و`rel-prod-migs.txt` **متطابقتان تمامًا** (`diff` = صفر فروق).
- الفارق `49 − 47` = هجرتان مطبَّقتان تاريخيًّا وغير موجودتين في شجرة المصدر الحاليّة: `20260622144900_KpiTemplateAssignmentsPhaseT1` و`20260626135944_AddReportViewGrants`. وجودهما في `__EFMigrationsHistory` **لا يولّد عمليّة ولا تراجعًا** عند `MigrateAsync()`؛ الإقلاع يطبّق المعلّق فقط.
- **`PENDING_MIGRATIONS = 0` ⟹ نشر بلا أيّ كتابة مخطّط على قاعدة الإنتاج.**

## 3) بوّابات الخادم — كلّها خضراء
| البوّابة | النتيجة | الدليل |
|---|---|---|
| اختبارات الوحدة | **618/618 · فشل 0** · `EXIT=0` | `evidence/rel-be-unit.txt` |
| اختبارات التكامل | **2311/2311 · فشل 0** · `EXIT=0` (8د 50ث، قاعدة معزولة) | `evidence/rel-be-int.txt` |
| البوّابات المسمّاة (عقد الـIdempotency ومتعدّد الأسطر) | **10/10** | `evidence/rel-be-named-gates.txt` |

## 4) بوّابات الواجهة — أُعيد قياسها في شجرة نظيفة معزولة

شجرة القياس: `git worktree add --detach /private/tmp/rel-fe-clean c5e0202` + `npm ci` مستقلّ (266 حزمة، `vitest 4.1.8` نفسها). بلا `tsc`/`build`/Playwright متوازٍ، وبلا خادم معاينة.

| البوّابة | النتيجة | الدليل |
|---|---|---|
| `vitest run` — التشغيل 1 | **75/75 ملفًّا · 857/857 · أخطاء غير ملتقَطة 0 · `VITEST_EXIT=0`** | `evidence/frontend-clean-gate/clean-fe-run1.txt` |
| `vitest run` — التشغيل 2 | **75/75 · 857/857 · 0 · `VITEST_EXIT=0`** | `evidence/frontend-clean-gate/clean-fe-run2.txt` |
| `vitest run` — التشغيل 3 (تحت `bash -c` لالتقاط رمز خروج موثوق) | **75/75 · 857/857 · 0 · `VITEST_EXIT=0`** | `evidence/frontend-clean-gate/clean-fe-run3.txt` |
| `tsc -b` | **`TSC_EXIT=0`** | `evidence/frontend-clean-gate/clean-tsc.txt` · `evidence/rel-tsc.txt` |
| `vite build` | `EXIT=0` — `dist/assets/index-o0jQqvkU.js` + `index-ENWa4a-J.css` | `evidence/frontend-clean-gate/clean-build.txt` |
| حارس الحزمة المبنيّة `verify-multiline-bundle.mjs` | **7/7 PASS · `BUNDLE_MULTILINE_GATE=PASS` · `BUNDLE_GATE_EXIT=0`** | `evidence/frontend-clean-gate/clean-bundle-gate.txt` |

```
FRONTEND_GATE   = PASS
TEST_FILES      = 75/75      TESTS = 857/857
UNHANDLED_ERRORS = 0         UNHANDLED_REJECTIONS = 0
VITEST_EXIT     = 0   (ثلاث مرّات متتالية)
PREVIOUS_EXIT_1 = NON_REPRODUCED_TEST_ENVIRONMENT_CONTAMINATION
```

**فرق البيئة الموثَّق (سبب `EXIT=1` السابق):** التشغيل الفاشل `rel-fe-tests2.txt` جرى في `/private/tmp/r22b-release-20260906/reporting-frontend`، وفيه `node_modules` **وصلة رمزيّة (symlink)** إلى شجرة المستودع الرئيسيّة الحيّة `/Users/…/Mrketing Experts syestem/reporting-frontend/node_modules` المشتركة مع جلسات عمل أخرى. الشجرة النظيفة تملك شجرة تبعيّات خاصّة بها من `npm ci`. مع تطابق إصدار `vitest` وعدد الحزم، الفارق الوحيد هو مصدر `node_modules` ⟹ الاستثناء غير الملتقَط في `KpiPage.tsx:589` **لم يُعَد إنتاجه إطلاقًا** في ثلاثة تشغيلات نظيفة متتالية.

**التشغيل الفاشل `evidence/rel-fe-tests2.txt` محفوظ كما هو بلا تعديل ولا حذف، ومصنَّف `NON_REPRODUCED_TEST_ENVIRONMENT_CONTAMINATION`. لم يُمنَح أيّ `waiver` يدويّ لـ`EXIT=1`؛ البوّابة اجتازت بقياس نظيف مكرَّر.**

## 5) البوّابة التشغيليّة والبصريّة على RC — 44/44
| البوّابة | النتيجة |
|---|---|
| دورة القرار الكاملة بتعليقات متعدّدة الأسطر | **19/19 PASS** (`ui/rc-ui-decision-cycle.json`) |
| أسطح الواجهة (Chromium) | **12/12 PASS** |
| أسطح الواجهة (WebKit) | **12/12 PASS** — `WEBKIT_GATE = PASS` لا `NOT_RUN` |
| `VIS-01..VIS-05` (Chromium) | **10/10 PASS** (مكتب 1440 + جوّال 390) |
| `VIS-01..VIS-05` (WebKit) | **10/10 PASS** (مكتب 1440 + جوّال 390) |
| الانسياح الأفقيّ · أخطاء الكونسول · أخطاء الشبكة غير المتوقّعة | **0 · 0 · 0** على المحرّكين |
| بوّابة التقارير عبر واجهة البرمجة (§12) | 57 فحصًا مغلقة (`rc-journey-api2.json` + `rc-journey-api2-delta.json`) |

خطأ `negotiate 401` المتكرّر في الجولات الأولى **مِشْبَك اختبار لا عيب منتَج**: `httpCredentials` في WebKit يستبدل `Bearer` بـ`Basic` على `/hubs/`، و`auth_basic` موجود على RC وحده وغير موجود في الإنتاج. بعد تصحيح المِشْبَك: `CONSOLE_ERRORS = 0`. التفصيل والقياس المضبوط في §5 من تقرير البوّابة.

## 5-ب) تنظيف بيانات UAT على RC — عبر الواجهات الرسميّة حصرًا

الأداة: `evidence/rc-cleanup/rc-cleanup-apply.py` (تشغيل جافّ أوّلًا ثمّ `--apply`). الجرد والتحقّق: `rc-cleanup-inventory.sql` **قراءة فقط**، شُغِّل قبل التنفيذ وبعده.

| الكيان المؤقّت | البصمة | السطح الرسميّ المستعمَل | الحالة بعد التنفيذ |
|---|---|---|---|
| 5 حسابات UAT | `r22brel-*@rc-uat.local` | `POST /api/directory/users/{id}/reset-password` (إبطال توكنات المستخدم وحده) ثمّ `PUT /api/directory/users/{id}` بـ`IsActive=false` | `IsActive = f` للخمسة · الصفوف باقية |
| 6 تسليمات | مملوكة للحسابات الخمسة | `POST /api/submissions/{id}/admin-delete` بسبب إلزاميّ + تدقيق | `IsDeleted = t` للستّة · **لا حذف صلب** |
| 3 مشاريع | `R22BREL%` | `POST /api/projects/{id}/archive` | `Status = Closed` |
| 1 عميل | `R22BREL%` | `POST /api/clients/{id}/archive` | `Status = Closed` |
| فريق + إدارة | `R22BREL%` | `PUT /api/directory/teams|departments/{id}` بـ`IsActive=false` | `IsActive = f` |

```
RC_CLEANUP_STEPS            = 23      RC_CLEANUP_FAILED        = 0
RC_UAT_TEMP_ACCOUNTS_ACTIVE = 0       RC_UAT_ACTIVE_REFRESH_TOKENS = 0  (كانت 76)
RC_CLEANUP_MISMATCH         = 0       HARD_DELETE = 0        RAW_SQL_WRITE = 0
RC_REAL_ACCOUNTS_UNCHANGED  = YES     real_users_md5 = f70223ae6763b68204da370428dd0310 (قبل = بعد)
RC_REAL_DATA_UNCHANGED      = YES     real_submissions=40 · real_projects=36 · real_clients=9 (بلا تغيّر)
TOTAL_USERS                 = 58 (بلا تغيّر) · ACTIVE_USERS 40 → 35 = المؤقّتة الخمسة حصرًا
SEO_TEMPLATE_V7_PRESERVED   = YES     v7 IsPublished=t · row_md5 = 1c888017b0115ccc09ddb7013fb69bc3 (قبل = بعد)
```

المحظورات لم تُلمَس إطلاقًا: لا كتابة SQL، ولا حذف صلب، ولا تصفير كلمة مرور لأيّ حساب حقيقيّ (حساب الأدمن الأداتيّ قائم مسبقًا ولم تُغيَّر كلمته)، ولا حذف fixture غير مملوك، ولا إبطال توكنات جماعيّ (`ResetUserPasswordAsync` يبطل توكنات المستخدم المستهدف وحده — `DirectoryService.cs`).

## 6) تحفّظات مسجَّلة (لا تحجب، ولا تُطوى)
1. **قناة البريد لم تُشغَّل زمن التشغيل على RC** — متعدّد الأسطر في البريد مُثبَت عند المُصيِّر (`EmailHtmlMultilineTests`) لا عند الإرسال الحيّ: `EMAIL = PASS_AT_RENDERER / NOT_RUN_RUNTIME (CHANNEL_DISABLED)`.
2. **خيارات `work_status` تُعرَض بالإنجليزيّة** (`Draft/Revision/Approved/Published`) مطابِقةً لتهيئة النسخة المحكومة v7 المعتمَدة في إغلاق R22B. ليست ارتدادًا يُحدثه هذا الإصدار؛ قرار المالك.
3. ~~بيانات UAT على RC لم تُنظَّف بعد~~ — **أُغلِق**: نُفِّذ التنظيف بتصريح المالك عبر الواجهات الرسميّة حصرًا (§5-ب)، بلا حذف صلب وبلا مساس ببيانات حقيقيّة.

## 7) الحسم
```
LINEAGE_VERIFIED            = YES (d25dc69 → c5e0202)
ORIGIN_DEVELOP              = c5e0202d0a528a1a45856790716e449b812f0184  (لم يتحرّك)
ORIGIN_MAIN                 = 508509ad8474b321c80cbdd48eb84ecb54bee212  (لم يتحرّك)
APP_SOURCE_DELTA 5b0febf..c5e0202 (خارج Ops) = 0 ملفّ  ⟹ المرشَّح أدلّة فقط فوق مصدر التطبيق
UNEXPLAINED_SOURCE_DELTA    = 0
PENDING_MIGRATIONS          = 0
BACKEND_GATES               = 618 + 2311 + 10  ALL PASS
FRONTEND_GATE               = PASS  (75/75 · 857/857 · unhandled 0 · VITEST_EXIT=0 ×3)
PREVIOUS_EXIT_1             = NON_REPRODUCED_TEST_ENVIRONMENT_CONTAMINATION
BUNDLE_MULTILINE_GATE       = PASS (7/7)
RC_OPERATIONAL_VISUAL_GATE  = PASS (44/44 · chromium + webkit · desktop + mobile390)
RC_CLEANUP                  = PASS (23 خطوة · فشل 0 · حذف صلب 0 · بيانات حقيقيّة بلا تغيّر)
BLOCKING_DEFECTS            = 0
RECOMMENDATION              = GO
DEPLOY_AUTHORIZATION        = GRANTED (تصريح المالك النهائيّ — 6 سبتمبر 2026)
```

**تصحيح حكم سابق (لا يُطوى):** كُتب في نسخة أولى من هذا التقرير أنّ التوصية `GO` استنادًا إلى أنّ `KpiPage.tsx` خارج فرق الإصدار. **ذلك وحده لا يكفي لتجاوز خروج العمليّة بـ`EXIT=1`.** فصار الحكم الوسيط `CONDITIONAL_GO` مع `PRODUCTION_DEPLOYMENT = HELD_PENDING_FRONTEND_PROCESS_EXIT_ZERO`، ثمّ **رُفع الحجز فقط بعد** إثبات `VITEST_EXIT = 0` في ثلاثة قياسات نظيفة متتالية بشجرة تبعيّات مستقلّة (§4). **لا `waiver` يدويّ مُنِح لـ`EXIT=1`.** التشغيل الفاشل `rel-fe-tests2.txt` **محفوظ كما هو ومصنَّف، لم يُحذف ولم يُعدَّل.**

**تسلسل النشر المعتمَد:** نسخ احتياطيّ ثلاثيّ (واجهة + خادم + قاعدة `reporting_prod`) والتحقّق منه ⟹ بوّابة ما قبل النشر ⟹ نشر يدويّ من نسخة معزولة وفق `deployment-runbook-rules.md` بتوقّع **صفر هجرة** ⟹ تحقّق تشغيليّ وبصريّ على الأصل الحقيقيّ `https://reports.emarketingacademy.net` ⟹ مراقبة ⟹ التقرير النهائيّ `PRODUCTION-DEPLOYMENT-AND-VERIFICATION.md`.
