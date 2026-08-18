# تقرير إغلاق — `PROJECT360-R2-UAT-EVIDENCE-AND-GUIDE-CLOSURE-R2.1`

التاريخ: ١٨ أغسطس ٢٠٢٦ · لقطة الأساس في `BASELINE.md` · مراجعة الأسطح في `CODE-REALITY-REVIEW.md`

## 1) البوّابات — أرقام مقيسة ورموز خروج

| البوّابة | الأمر | النتيجة | رمز الخروج | السجلّ |
|---|---|---|---|---|
| أنواع الواجهة | `npx tsc -b` | 0 سطر مخرَج | `0` | `/tmp/r21-tsc.log` (فارغ) |
| اختبار جسر التنفيذ | `npx vitest run --pool=forks --no-file-parallelism …/ProjectExecutionBridgeTab.test.tsx` | `Test Files 1 passed (1)` · `Tests 16 passed (16)` | `0` | `/tmp/r21-vitest-bridge.log` |
| الواجهة كاملة | `npx vitest run` | `Test Files 50 passed (50)` · `Tests 588 passed (588)` · 81.08s | `0` | `/tmp/r21-vitest-all.log` |
| الوحدوي | `dotnet test tests/Reporting.UnitTests -c Release` | `Failed: 0, Passed: 359, Total: 359` | `0` | `/tmp/r21-unit.log` |
| التكامل | `dotnet test tests/Reporting.IntegrationTests -c Release --no-build` | `Failed: 1, Passed: 2010, Total: 2011` · 14م48ث | `1` | `/tmp/r21-integration-3.log` |
| الفشل الوحيد على قاعدة نظيفة | `--filter ProjectRepeatableGridTests` على `reporting_r21_clean` | `Failed: 0, Passed: 16, Total: 16` | `0` | `/tmp/r21-clean-grid.log` |

**الفشل الوحيد ليس انحدارًا.** `SeoRollup_Aggregation_Unchanged_ReadsTopLevelNumbers` يتوقّع `10` ويقرأ `50.00`:
التجميع يجمع عبر المُرسِلين، وثلاث جولات متتالية على القاعدة المعزولة نفسها راكمت خمسة تسليمات (٥ × ١٠ = ٥٠).
البرهان على أنّه أثر قياس لا أثر شيفرة قائم على شيئَين معًا: `git diff f9bca63..HEAD -- reporting-backend`
يساوي **صفر سطر**، والصنف نفسه على قاعدة أُنشِئت نظيفة يمرّ **16/16**.

## 2) ما أُغلِق في هذا الإصدار

| المعرّف | الوصف | الإغلاق | الدليل |
|---|---|---|---|
| `GAP-R21-07` | سرد «تحديثات التنفيذ» يروي انتقالات لم تقع: المقبول «من ٥٠٪ إلى ٥٠٪» لأنّ المقارنة مع قيمة المخرَج **بعد** التطبيق، والمرفوض يروي انتقالًا لم يُطبَّق أصلًا | المقبول يُروى من لقطته المخزَّنة `previousProgressPercent`، والمرفوض لا يُروى كانتقال بل «نسبة مُدَّعاة — لم تُطبَّق على المخرَج» | إعادة القياس في المرحلة `q` · `shots-stage-q.json` · الشكلان `02-r21-tl-execution-history-fixed.png` و`04-r21-mgmt-execution-history-fixed.png` |
| `GAP-R21-02` | جدول مشاريع Client 360 بلا عمودَي التقدّم والصحّة | مُغلَق سلفًا في `fc20284`؛ ما نقص هنا هو **الدليل**: سكربت الالتقاط كان يصوّر تبويب «الملف التعريفي» لا «المشاريع» | `21-r21-final-verification.mjs` صار ينقر تبويب «المشاريع» ⟸ رؤوس الجدول المقروءة: `التقدّم` و`الصحّة`، والصفّ `٤٠٪ متوسّط موزون` · `متأخّر ٣٥٫٦٪` |

## 3) نطاق قائد الفريق — الحكم من ردّ الخادم لا من الشاشة

`TL_STRUCTURAL_BUTTONS = []` (لا «تعديل المشروع» ولا «أرشفة المشروع» ولا «حذف المشروع» على شاشته).
وغياب الزرّ **لا يُحتَجّ به وحده**؛ كلّ منع مقيس بـ`apiProbe` من داخل جلسة المتصفّح نفسها:

| المسبار | المتوقَّع | المقيس |
|---|---|---|
| `POST /projects/{id}/archive` | 403 | **403** |
| `DELETE /projects/{id}` | 403 | **403** |
| `POST /projects/{id}/contract-deliverables` (حمولة صالحة بنيويًّا) | 403 | **403** · `auth.forbidden` · «لا تملك صلاحية إدارة المخرَجات التعاقديّة لهذا المشروع.» |
| `GET /projects/{معرّف خارج النطاق}` | 404 | **404** · `project.not_found` (منع التعداد لا رفض صريح) |

الحمولة في المسبار الثالث صالحة عمدًا: إغفال `deliverableTypeCode` يُنتج 400 قبل بلوغ الحارس، فيبدو المنع محقَّقًا وهو لم يُختبر أصلًا.

## 4) انتشار الأثر — مقيس على ستّة أسطح

سلسلة السرد بعد الإصلاح كما قرأها المتصفّح: `من ٠٪ إلى ٢٥٪` · `من ٠٪ إلى ٥٠٪` · `نسبة مُدَّعاة ٣٥٪ — لم تُطبَّق على المخرَج`.
والتحقّق الحسابيّ للمتوسّط الموزون: `٠٫٦ × ٥٠ + ٠٫٤ × ٢٥ = ٤٠٪` — وهو **نفس** الرقم الظاهر في Project 360 وفي جدول مشاريع Client 360 وفي العين الإداريّة، لا احتسابًا ثانيًا في المتصفّح.

## 5) حالة البيئات — قراءة فقط بعد الانتهاء

| البيئة | الخدمة | الهجرات | حزمة الواجهة | الحكم |
|---|---|---|---|---|
| الإنتاج `reports.emarketingacademy.net` | `reporting-api` نشِطة منذ **٧ أغسطس 08:57:45 UTC** | **30** · الرأس `20260724224053_AddReportApproverAndKpiReviewerOverrides` | `index-Bok_mmjt.js` · `dist` mtime **١٢ أغسطس 20:03** | `PRODUCTION_TOUCHED = NO` |
| RC `rc-report.emarketingacademy.net` | `khubara-reporting-rc` نشِطة منذ **١٦ أغسطس 18:35:15 UTC** | **40** · الرأس `20260811142239_AddProject360Foundation` | `index-ccSnFxKJ.js` · `dist` mtime **١٦ أغسطس 17:54** | `RC_TOUCHED = NO` |
| TEST `test.emarketingacademy.net` | `khubara-reporting-test` · PID `1235735` · `NRestarts 0` · `/health` **200** | **40** صفًّا (39 هجرة + صفّ جسر النَسَب) · الرأس `20260817114129_AddProjectExecutionUpdateProposals` | `index-D7JHWCts.js` · `dist` mtime **١٨ أغسطس 08:28** | بيئة العمل المُصرَّح بها |

**لا هجرة جديدة في هذه التذكرة على أيّ بيئة**: رأس TEST و`RC` والإنتاج كلّها كما في لقطة الأساس حرفيًّا.

بصمات TEST المنشورة:

- `Reporting.Api.dll` = `394167279596d1457db359d33fb94842740f22c3f7f1933b64cb8ea449499ca4` · `1.0.0+f9bca63e5b2f2564a06a729d6059b4ed3002e94b` · بُني ١٧ أغسطس 22:36 UTC.
- `assets/index-D7JHWCts.js` = `ec269b481cfccf788a31bbc629142543b5d4e4bc02f43564b914e22155813edb`؛ بصمة `dist` المجمّعة = `12a5d309f2b7a88ba42c8f6931719e7a433d25e63a69810335729ea91fad47d5`.
- الخلفيّة المنشورة مبنيّة من `f9bca63`، والالتزام التالي `17f7044` **واجهة خالصة**: `git diff f9bca63..HEAD -- reporting-backend` = صفر سطر. فالخلفيّة المخدومة مطابقة مصدرًا لرأس الفرع.

## 6) المخرَجات الوثائقيّة

مقاسة ومُوقَّعة بالبصمات في `Docs/Guides/Marketing-Experts-Client360-Project360-Guide-Generation-Report-R2.1.md`:
الدليل **240 صفحة** · **121 شكلًا** · **141 حالة UAT في 13 حزمة** · 0 صفحة فارغة · 0 صورة مبتورة.
`Docs/*` مستبعَد من التتبّع (`.gitignore:45`)، فالمخرَجات على القرص لا في المستودع.

## 7) التنظيف

- أُسقِطت قواعد الاختبار المعزولة الخمس التي أنشأتها هذه التذكرة: `reporting_r21_iso` (487MB) · `reporting_r21_main` (111MB) · `reporting_r21_pfe` · `reporting_r21_cal` · `reporting_r21_clean` (12MB لكلّ منها) — المتبقّي بالنمط `reporting_r21%` = **0**.
- **بيانات UAT الموسومة على TEST مُبقاة عمدًا** (`P360-R21-UAT-CLIENT` · `P360-R21-UAT-PROJECT`): هي المرجع الذي تصفه ١٢١ لقطة في الدليل، وحذفها يُبطل إمكان إعادة التحقّق من كلّ شكل. الإبقاء قرار مُعلَن لا سهو، ومحلّه بيئة الاختبار وحدها.

## 8) ما لم يُنفَّذ — `NOT EXECUTED`

- **نشر RC**: `NOT EXECUTED` — خارج التصريح.
- **نشر الإنتاج**: `NOT EXECUTED` — محجوب بثلاثة: تصريح صريح جديد، واعتماد مالك المنتج، وإغلاق `PROD-READINESS-01`.
- **وسم أو دفع وسم**: `NOT EXECUTED` — `TAG_PUSH = NO`.
- **تحريك `origin/main`**: `NOT EXECUTED` — `origin/main_CHANGED = NO`.
- **دفع قسريّ**: `NOT EXECUTED` — `FORCE_PUSH = NO`.
- **اعتماد مالك المنتج لنتائج UAT الوظيفيّة**: `NOT EXECUTED` — قرار عمل لا يُنجزه تنفيذ تقنيّ.
- **`BASELINE-DEFECT-01/02`**: لم يُمسّا — لهما تذكرة عزل مستقلّة في الطابور.
