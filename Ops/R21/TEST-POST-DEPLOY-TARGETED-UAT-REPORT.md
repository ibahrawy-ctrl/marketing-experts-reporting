# تقرير UAT مختصر بعد النشر — PROJECT360-R2.1 (TEST فقط)

**التاريخ:** 2026-08-18 · **النطاق:** UAT وظيفيّ مختصر بعد النشر على **TEST وحدها**.
**المرشَّح:** `TARGET_SHA = 7e063b493b50ad90ba6131e47042c7cd035fb65b` · **العنوان:** `https://test.emarketingacademy.net/`.
**لم يُمسّ RC ولا الإنتاج · لا نشر · لا هجرة · لا تغيير شيفرة · لا git.** الأسرار لم تُطبَع (عدا انكشاف عابر عولِج بالتدوير — §8).

الخدمة الحيّة: `khubara-reporting-test` · `127.0.0.1:5091` · قاعدة `reporting_test_uat`. جميع نداءات `/api` و`/hubs` ذهبت **فعلًا** إلى هذه الخدمة.

---

## 1) عناصر UAT الموسومة (موجودة مسبقًا على TEST)

| العنصر | المعرّف | القيمة قبل |
|---|---|---|
| المشروع | `9e731196…` **P360-R21-UAT-PROJECT** | Active · تقدّم 40.00 · Weighted · صحّة Delayed |
| العميل | `f2dd43c4…` **P360-R21-UAT-CLIENT** | — |
| مالك المشروع | `231565b2` (emp2@uat.local) | — |
| قائد الفريق (المشروع) | `de210ca6` (team.leader@uat.local) | — |
| مدير العميل | `f18df329` (account.manager@uat.local) | — |
| فريق المالك | `626b14aa` «فريق UAT أ» | يضمّ emp1 · emp2 · lead |
| المخرَج A | `a013906f` | 50.00 · InProgress |
| المخرَج B | `68418dd4` | 25.00 · NotStarted |
| الهدف/المؤشّر | `82d09772` / `98e7bf07` (P360-R21-UAT-KPI) | — |
| تيار العمل | `a8ef75dd` P360-R21-UAT-WORKSTREAM | Active |

**حسابات الأدوار المستقلّة (11 حساب `@uat.local` بكلمة سرّ مشتركة عبر واجهة الإدارة الرسميّة — لم تُطبَع):**
موظّف داخل الفريق = `emp1` (عضو فريق المالك) · قائد الفريق = `team.leader` (TeamLeaderId للمشروع) · مدير العميل = `account.manager` · الإدارة = `manager` (+`ceo`/`gm` للتدقيق) · موظّف خارج الفريق = `employee` (`aa016f5e`، فريق آخر `2e0cac57`) · المالك = `emp2`.

**لقطة الأعداد قبل (قاعدة كاملة):** `project_execution_update_proposals=3` · `project_kpi_readings=12` · `project_workstreams=8` · `workstream_deliverables=2` · `audit_logs=534`.

---

## 2) السيناريو المختصر (19 خطوة) — تنفيذ فعليّ عبر الـAPI الحيّ

| # | الخطوة | التنفيذ | الحالة | النتيجة |
|---|---|---|---|---|
| 1 | موظّف داخل الفريق يفتح المشروع | `GET projects/{P}` + `/overview` كـ`emp1` | 200/200 | **PASS** |
| 2 | يرفع مطالبة تقدّم موسومة | `POST execution-proposals` (B، 40، InProgress) → `ba1fa30d` | 200 · Pending | **PASS** |
| 3 | المطالبة Pending ولا تغيّر المخرَج/المشروع | B=25 (بلا تغيير) · تقدّم المشروع=40 | — | **PASS** |
| 4 | قائد الفريق يرى المشروع والمطالبة | `GET project` 200 + القائمة تحوي `ba1fa30d` | 200 | **PASS** |
| 5 | الرفض بلا سبب ممنوع (خادمًا) | `PATCH review {accept:false}` بلا سبب | **400** (محجوب · بلا أثر) | **PASS** |
| 6 | الرفض بسبب ينجح ولا يغيّر التقدّم | `PATCH review {accept:false, reviewNote}` | 200 · Rejected · B=25 · مشروع=40 | **PASS** |
| 7 | مطالبة ثانية تُقبل وتغيّر المخرَج مرّة | `POST` (B،60) → `1287482d` + `PATCH accept` | 200 · B: 25→**60** · مشروع 40→54 | **PASS** |
| 8 | إعادة القبول لا تُضاعف الأثر | `PATCH accept` ثانيةً | 200 · B **يبقى 60** | **PASS** |
| 9 | قائد الفريق يحدّث مخرَجًا تشغيليًّا مباشرةً | `PATCH contract-deliverables/A/progress` (70) | 200 · A: 50→**70** | **PASS** |
| 10 | قائد الفريق يسجّل قراءة KPI | `POST …/kpis/98e7bf07/readings` (7) → `ce60811f` | 200 | **PASS** |
| 11 | قائد الفريق يدير تيار عمل | `PATCH …/workstreams/{WS}/deactivate` ثمّ `/activate` | 200/200 | **PASS** |
| 12 | قائد الفريق لا يحذف/يؤرشف المشروع | `DELETE project` · `POST project/archive` | **403 / 403** | **PASS** |
| 13 | خارج النطاق ⟹ 404 | `employee` على المشروع · `team.leader` على مشروع أجنبيّ `40075b7b` | **404 / 404** | **PASS** |
| 14 | القيمة الجديدة تظهر في Project 360 | `GET overview`: `project.progressPercent=66` + المخرجات A70/B60 | 200 | **PASS** |
| 15 | إعادة حساب تقدّم/صحّة المشروع | التقدّم 40→**66** · صحّة Delayed | — | **PASS** |
| 16 | الأثر نفسه في Client 360 | `GET clients/{C}` 200 + `projects?clientId=C` يُظهر المشروع بتقدّم **66** | 200 | **PASS** |
| 17 | مدير العميل يرى التحديث + الفاعل + الوقت | `GET execution-proposals` كـ`account.manager` | 200 · `reviewedByFullName=قائد فريق UAT` · `reviewedAtUtc` · 25→60 | **PASS** |
| 18 | الإدارة تراه | `manager`: `overview` 200 (تقدّم 66) · `ceo`/`gm`: `audit-logs` 200 | 200 | **PASS** |
| 19 | السجلّ يُظهر السابق/الجديد + المصدر + المراجِع | مقترح `1287482d`: prev **25** → new **60** · مصدر جسر التنفيذ · مراجِع قائد فريق UAT + Audit (CEO/GM) | 200 | **PASS** |

**الإجمالي: 19/19 PASS · 0 FAIL.**

**ملاحظات فنّيّة مثبَتة (لا عيوب):**
- الرفض بلا سبب يُحجَب برمز **400** (لا 422) — الإنفاذ سليم والأثر صفر.
- **تحديث المخرَج المباشر يُقيَّد تلقائيًّا كمقترح تنفيذ «مقبول»** في السجلّ (جسر التنفيذ) ⟹ خطوة 9 أنشأت `987401dc`. سلوك مقصود («لا حذف من السجلّ»).
- `audit-logs` سياستها `ExecutiveOnly` = Admin/CEO/GM ⟹ `manager` يحصل 403 عليها لكنه يرى التحديث عبر `overview`. لا تعارض مع «رؤية الإدارة».

---

## 3) مصفوفة الحكم (13 شرطًا)

| # | الشرط | الحالة | حالة الـAPI | الإثبات |
|---|---|---|---|---|
| 1 | TEAM_LEADER_SEES_ASSIGNED_PROJECT_DETAILS | **PASS** | 200 | خطوة 4 · متصفّح: عنوان «P360-R21-UAT-PROJECT» في `05-project360.png` |
| 2 | TEAM_LEADER_CAN_REVIEW_EXECUTION_CLAIM | **PASS** | 200 | خطوة 7 (B 25→60 مرّة) + خطوة 8 (تعادل) · `r21-uat-results.json` |
| 3 | TEAM_LEADER_CAN_REJECT_WITH_REQUIRED_REASON | **PASS** | 400 ثمّ 200 | خطوتا 5+6 |
| 4 | TEAM_LEADER_CAN_UPDATE_OPERATIONAL_DELIVERABLE | **PASS** | 200 | خطوة 9 (A 50→70) |
| 5 | TEAM_LEADER_CAN_RECORD_KPI_READING | **PASS** | 200 | خطوة 10 (`ce60811f`) |
| 6 | TEAM_LEADER_CAN_MANAGE_WORKSTREAM | **PASS** | 200/200 | خطوة 11 (deactivate→activate) |
| 7 | TEAM_LEADER_CANNOT_DELETE_PROJECT | **PASS** | 403 | خطوة 12 (Policy `ProjectStructuralManage`) |
| 8 | TEAM_LEADER_CANNOT_ARCHIVE_PROJECT | **PASS** | 403 | خطوة 12 |
| 9 | TEAM_LEADER_OUT_OF_SCOPE_RETURNS_404 | **PASS** | 404 | خطوة 13 (قائد الفريق على `40075b7b` + موظّف خارج الفريق على المشروع) |
| 10 | TEAM_LEADER_ACTION_VISIBLE_IN_PROJECT360 | **PASS** | 200 | خطوة 14 (`overview` تقدّم 66 · A70/B60) · متصفّح `05-project360.png` |
| 11 | TEAM_LEADER_ACTION_VISIBLE_IN_CLIENT360 | **PASS** | 200 | خطوة 16 (المشروع بتقدّم 66 تحت العميل) · متصفّح `06-client360.png` |
| 12 | TEAM_LEADER_ACTION_VISIBLE_TO_ACCOUNT_MANAGER | **PASS** | 200 | خطوة 17 (مراجِع + وقت + 25→60) |
| 13 | TEAM_LEADER_ACTION_VISIBLE_TO_MANAGEMENT | **PASS** | 200 | خطوة 18 (`manager` overview + `ceo`/`gm` audit: فاعل=قائد فريق UAT، `progress_updated`) |

---

## 4) بوّابة المتصفّح والشبكة

الموقع كلّه خلف `auth_basic` (كلمة السرّ غير متاحة — تجزئة فقط). استُعمل **الأسلوب المعزول المُثبَت**: تُقدَّم **بايتات الواجهة المنشورة نفسها** (`dist` بصمة `12a5d309f2b7a88ba42c8f6931719e7a433d25e63a69810335729ea91fad47d5` — **مطابقة بايتًا** لـ`/opt/reporting-test/frontend/dist`) على **نفس الأصل** `https://test.emarketingacademy.net` عبر اعتراض Playwright للساكن فقط، بينما `/api/**` و`/hubs/**` تذهب **فعلًا** إلى الخادم الحقيقيّ (`auth_basic off` عليها). الدخول كـ**قائد الفريق** (`team.leader@uat.local`) بكلمة سرّ من stdin (لم تُطبَع).

**الطبقة الوحيدة غير المُمارَسة:** حاجب `auth_basic` الجذريّ في Nginx (طبقة تحقّق شبكيّة أمام الأصل). نفس الأصل، نفس الـAPI، نفس القاعدة، نفس بايتات الواجهة.

| المسار | الحالة | العنوان | أخطاء جديدة |
|---|---|---|---|
| `/login` | 200 | تسجيل الدخول | 0 |
| `/app` | 200 | لوحة قائد الفريق | 0 |
| `/app/projects` | 200 | المشاريع | 0 |
| `/app/projects/9e731196…/360` | 200 | P360-R21-UAT-PROJECT | 0 |
| `/app/clients/f2dd43c4…` | 200 | P360-R21-UAT-CLIENT | 0 |

```
CONSOLE_ERRORS          = 0
PAGE_ERRORS             = 0
FAILED_NETWORK_REQUESTS = 0
CORS_ERRORS             = 0
MIXED_CONTENT           = 0
REQUESTS_TO_LOCALHOST   = 0
REQUESTS_TO_RC          = 0
REQUESTS_TO_PRODUCTION  = 0
SIGNALR                 = PASS   (/hubs/notifications/negotiate ×5 · WebSocket مفتوح ×5 · 0 خطأ)
```
اللقطات: `/tmp/r21-uat-browser-out/{01-login,02-after-login,03-app,04-projects,05-project360,06-client360}.png` · الملخّص `summary.json`.

---

## 5) التنظيف ومقارنة الأعداد

- **أُرجِعت القيم المرجعيّة:** المخرَج A → 50/InProgress · المخرَج B → 25/NotStarted · تقدّم المشروع → **40.0** · صحّة Delayed · تيار العمل → **Active** (كلّها = ما قبل UAT).
- **سجلّات مُبقاة عمدًا (append-only بحكم التصميم — «لا حذف من السجلّ»، لا يوجد مسار حذف لها):**

| الجدول | قبل | بعد | الفرق (كلّه من أفعال UAT، موثَّق) |
|---|---|---|---|
| `project_execution_update_proposals` | 3 | **8** | +5: ادّعاءان (`ba1fa30d` مرفوض · `1287482d` مقبول) + تحديث مباشر (`987401dc`) + إرجاعان (`a59eb04a`,`b7ba4989`) |
| `project_kpi_readings` | 12 | **13** | +1: القراءة الموسومة `ce60811f` (تُبقى — سجلّ) |
| `project_workstreams` | 8 | 8 | 0 |
| `audit_logs` | 534 | **544** | +10: أثر تدقيق أفعال UAT (يُبقى عمدًا) |

- **البيانات المرجعيّة السابقة لم تُحذف ولا تُمسّ** (المقترحات الثلاثة الأصليّة `135d96f1`/`19647992`/`ff20bb88` سليمة).

---

## 6) الصحّة والسجلّ بعد الانتهاء

```
TEST_HEALTH            = 200
SERVICE                = active · PID 1264134 · NRestarts 0 (بلا إعادة تشغيل)
LOG_ERRORS (منذ 17:30) = 0 استثناء · 0 unhandled · 0 (28P01)
```
(مطابقات «fail» في السجلّ هي اسم العمود `AccessFailedCount` في استعلام الهويّة، لا أخطاء.)

---

## 7) المحظورات (كلّها محترمة)

```
PRODUCT_CODE_CHANGE = NO   TEST_DEPLOYMENT = NO   TEST_MIGRATION = NO
RC_TOUCHED          = NO   PRODUCTION_TOUCHED = NO
GIT_CHANGE          = NO   COMMIT_PUSH = NO
```
لم يظهر أيّ عيب منتج (حجب/فقدان بيانات/مسار حرج) ⟹ لا حاجة لأيّ تراجع؛ نشر TEST باقٍ كما هو.

---

## 8) انكشاف عابر لكلمة سرّ اختبار — عُولِج بالتدوير

أثناء تمرير كلمة سرّ حسابات `@uat.local` المشتركة إلى متصفّح الاختبار، فشل تجميع صدفة `zsh` (`{ …; }`) فطُبِعت القيمة مرّة واحدة في مخرجات الجلسة. **النطاق: كلمة سرّ حسابات UAT التجريبيّة على TEST وحدها** (لا علاقة لها بقاعدة/بنية/إنتاج/RC). **المعالجة الفوريّة:** توليد قيمة جديدة وتطبيقها عبر واجهة الإدارة الرسميّة على الحسابات الاثني عشر المعنيّة (`11 → 200` + `emp2 → 200`)، ثمّ التحقّق: **الدخول بالقيمة المنكشفة = 401 (مُبطَلة) · بالقيمة الجديدة = OK**. القيمتان لم تُطبَعا في هذا التقرير.

---

## 9) الخلاصة

```
TARGET_SHA                   = 7e063b493b50ad90ba6131e47042c7cd035fb65b
TARGETED_UAT_TOTAL           = 19
TARGETED_UAT_PASSED          = 19
TARGETED_UAT_FAILED          = 0
TEAM_LEADER_CAPABILITY_GATE  = PASS
PROPAGATION_GATE             = PASS
SECURITY_SCOPE_GATE          = PASS
BROWSER_GATE                 = PASS
CLEANUP                      = PASS (القيم المرجعيّة مُرجَعة · سجلّات append-only مُبقاة وموثَّقة)
TEST_HEALTH                  = 200
TEST_READY_FOR_RC_PROMOTION  = YES
RC_TOUCHED                   = NO
PRODUCTION_TOUCHED           = NO
NEXT_REQUIRED_ACTION         = تصريح منفصل لنشر المرشح على RC
```
