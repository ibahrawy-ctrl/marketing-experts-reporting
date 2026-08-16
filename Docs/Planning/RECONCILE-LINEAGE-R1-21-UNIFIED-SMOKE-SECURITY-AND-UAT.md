# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 21: الدخان والأمن وUAT الموحّدة على TEST

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** M — Unified Smoke · Role/Scope Gate · Functional UAT · Credential Incident
**التاريخ:** 16 أغسطس 2026
**الحكم:** **البوّابات الثلاث خضراء** · 213 فحصًا · 0 فشل · توسّع صلاحيّات = 0
**RC والإنتاج: لم يُمَسّا إطلاقًا.**

> يجمع هذا التقرير موضوعات التقارير 24–27 المخطَّطة (بوّابة الدخان والأمن، UAT
> الوظيفيّة، UAT البصريّة، إغلاق حادثة الاعتمادات).

| البوّابة | النتيجة |
|---|---|
| بوّابة الدخان الموحّدة | **43 / 43 · `SMOKE_GATE = PASS`** |
| بوّابة الأدوار والنطاق ومنع التعداد | **143 / 143 · `ROLE_GATE = PASS`** |
| UAT ميزات نَسَب الإنتاج | **27 / 27 · `LINEAGE_UAT = PASS`** |
| **المجموع** | **213 / 213 · 0 فشل** |

---

## 1) بوّابة الدخان — وثلاثة عيوب أداة كُشِفت وأُصلِحت

التشغيل الأوّل لـ`/root/uni-smoke.sh` أعطى `PASS=26 FAIL=8`. **لا واحد من الثمانية كان
عيب منتج**؛ كلّها عيوب في أداة القياس نفسها، وكلّها من صنف واحد: **مقارنة بثوابت
مجمَّدة أو بمعرّفات غير مترابطة**.

### 1.1 العيب الأوّل — عميل ومستند غير مترابطَين

```sql
CLIENT_ID=$(psql -Atc 'select "ClientId" from client_documents limit 1;')
DOC_ID=$(psql   -Atc 'select "Id"       from client_documents limit 1;')
```

جملتان مستقلّتان بلا `ORDER BY` وبلا ربط: المستند قد يخصّ عميلًا آخر. **مُوِّه العيب
تمامًا حين كان في القاعدة مستند واحد فقط**؛ ومع 16 مستندًا وعميلَين صار الربط الخاطئ
يُنتِج 404 مشروعًا يُقرأ خطأً على أنّه انحدار. الإصلاح: انتقاء الحقلَين من **الصفّ نفسه**.

### 1.2 العيب الثاني — مقارنة عدد الواجهة بالعدّ الخام للقاعدة

الواجهة أعادت 11 مستندًا والقاعدة تحوي 15 لنفس العميل. التحليل:

| الحالة | العدد |
|---|---|
| المجموع | 15 |
| محذوف منطقيًّا (`IsDeleted`) | 4 |
| مؤرشَف | 0 |
| **حيّ** | **11** |

الواجهة **مصيبة**: تُرجِع الحيّ فقط. والمستند الذي انتقته الأداة (`ORDER BY "Id" LIMIT 1`)
كان أحد الأربعة المحذوفة، فعودة 404 له هي **السلوك الصحيح** بموجب قاعدة «الوصول
المرفوض يُرجِع 404 لا 403». حُوِّل هذا من «فشل» إلى **فحص موجب مستقلّ**.

### 1.3 العيب الثالث — «حفظ البيانات» بثوابت من 15 أغسطس

القسم السابع كان يقارن بأعداد مكتوبة يدويًّا (`clients=4` · `projects=5` · `migrations=35`).
هذا ليس قياسًا لحفظ البيانات بل قياسًا لثبات نصّ السكربت. استُبدِل بمقارنة بخطّ الأساس
المستخرَج من **نسخة `pg_dump` لما قبل النشر** (التقرير 20 §5)، مع استثناء واحد معلَن:
`migrations = baseline + 3`.

### 1.4 النتيجة بعد الإصلاح — `/root/uni-smoke3.sh`

```
===== SMOKE SUMMARY =====
PASS=43 FAIL=0
SMOKE_GATE=PASS
```

الأقسام: المصادقة · مستندات CPW-R2 (7 فحوص) · Project 360 (9) · الكتالوج (3) ·
منع التعداد (8) · صمت البريد (4) · حفظ البيانات مقابل النسخة (11).

---

## 2) بوّابة الأدوار والنطاق ومنع التعداد

`/root/uat-role-gate.py` — يقرأ الأسرار من مخزن JSON بصلاحيّة `600` داخل مجلّد `700`،
**بلا `source` وبلا `eval` وبلا `argv`**، ويباعد نداءات المصادقة 2.2 ثانية احترامًا
لحدّ المعدّل 30/60ث.

**النتيجة: `PASS=143 FAIL=0 · ROLE_GATE=PASS`** على 11 هويّة × 12 فحصًا.

### 2.1 المصفوفة الناتجة — مطابقة تامّة للمصفوفة المجمّدة

| الدور | R2 documents | R2 links | R2 doc detail | R3 overview | R3 objectives | R3 kpis | R3 decisions | AE ×5 |
|---|---|---|---|---|---|---|---|---|
| VIEWER | 404 | 404 | 404 | 404 | 404 | 404 | 404 | 404 |
| EMP | 404 | 404 | 404 | 404 | 404 | 404 | 404 | 404 |
| SALES | 404 | 404 | 404 | 404 | 404 | 404 | 404 | 404 |
| TL | 404 | 404 | 404 | 404 | 404 | 404 | 404 | 404 |
| OPS_MGR | **200** | **200** | **200** | 404 | 404 | 404 | 404 | 404 |
| HR | **200** | 404 | 404 | 404 | 404 | 404 | 404 | 404 |
| FIN_EMP | 404 | 404 | 404 | 404 | 404 | 404 | 404 | 404 |
| FIN_MGR | **200** | 404 | 404 | 404 | 404 | 404 | 404 | 404 |
| AM | **200** | **200** | **200** | 404 | 404 | 404 | 404 | 404 |
| GM | **200** | **200** | **200** | **200** | **200** | **200** | **200** | 404 |
| CEO | **200** | **200** | **200** | **200** | **200** | **200** | **200** | 404 |

مقارنة خليّة بخليّة بمصفوفة `CPW-UNIFIED-UAT-R1-05-ROLE-SCOPE-MATRIX.md` §1:
**132 خليّة متطابقة · 0 اختلاف** ⟹ **`Security Scope Expansion = 0`**.

### 2.2 اختلاف ظاهريّ حُسِم — ولماذا لا يجوز الاكتفاء بالبوّابة

التشغيل الأوّل أظهر `AM · R2 doc detail = 404` مقابل `200` في المصفوفة المجمّدة.
**البوّابة نفسها لا تحسم هذا** لأنّ توقّعها لفحوص النطاق هو `{200, 403, 404}` أي مقبول
دائمًا؛ الفحوص الحاجبة الحقيقيّة فيها هي فحوص منع التعداد فقط (`{404}`). لذلك بُني
استقصاء مستقلّ يربط الحالة بخصائص المستند لا بتشغيل واحد:

| المستند | `VisibilityType` | OPS_MGR | AM | HR | FIN_MGR | CEO |
|---|---|---|---|---|---|---|
| `348d9cbe…` | `ManagementOnly` | 200 | **404** | 404 | 404 | 200 |
| `5fc5b8b9…` | `CustomUsers` | 404 | 404 | 404 | 404 | 404 |
| `61fce43f…` | `FinanceOnly` | 404 | 404 | 404 | **200** | 404 |
| `896c3db7…` | `ProjectTeam` | 200 | **200** | 404 | 404 | 200 |
| `93223630…` | `HRManagementOnly` | 404 | 404 | **200** | 404 | 200 |
| `a4d102fd…` | `CustomUsers` | 404 | **200** | 404 | 404 | 404 |
| `c28e2e3e…` | `ClientScoped` | 200 | **200** | 404 | 404 | 200 |
| `ca36f895…` | `ClientScoped` | 200 | **200** | 404 | 404 | 200 |
| `d018f978…` | `CustomRoles` | 404 | 404 | **200** | **200** | 404 |
| `d647b1d7…` | `ClientScoped` | 200 | **200** | 404 | 404 | 200 |
| `e1a88037…` | `ManagementAndFinance` | 200 | 404 | 404 | **200** | 200 |

**السبب مثبَت بالبيانات:** المستند الذي مرّرته الأداة في التشغيل الأوّل كان
`ManagementOnly`، وهو محجوب عن `AM` **بالتصميم**؛ والمصفوفة المجمّدة قيست على مستند
`ClientScoped`. إعادة التشغيل على مستند `ClientScoped` أعطت المصفوفة المطابقة تمامًا.

يُلاحَظ أنّ **حتّى `CEO` يُحجَب** عن `FinanceOnly` و`CustomUsers` و`CustomRoles`: نموذج
الرؤية يُطبَّق على مستوى المستند المفرد لا على مستوى الدور، ولا يوجد تجاوز عامّ لأيّ دور.

---

## 3) UAT الوظيفيّة لميزات نَسَب الإنتاج المستعادة

`/root/uni-lineage-uat.py` — **27 / 27 · `LINEAGE_UAT = PASS`**. هذا هو الفحص الذي
يخصّ هذه التذكرة تحديدًا: هل عادت الميزات الحيّة على الإنتاج إلى العمل فعلًا؟

### 3.1 المناصب المرنة — الميزة التي كادت تُفقَد من طرفَيها

| الفحص | النتيجة |
|---|---|
| `GET /api/positions` (Admin) | **200** |
| `GET /api/positions/permission-options` (Admin) | **200** |
| `GET /api/positions` (CEO) | **403** — محجوب بالتصميم (`PositionManagement` = Admin فقط، عمدًا لا CEO/GM) |
| `GET /api/positions` (TeamLeader) | **403** |
| `GET /api/positions` (بلا رمز) | **401** |
| `GET /api/positions/{ghost}` | **404** لا 403 |
| جداول القاعدة | `positions` · `position_permissions` · `position_scopes` |

هذه الميزة كانت مفقودة من **الخلفيّة** (`ScopeResolver.ResolvePositionScopeAsync` و
`positions.manage` — التقرير 06 §4.1) ومن **الواجهة** (تسجيل المسار `/app/positions` —
التقرير 19 §2.2) في آنٍ واحد. عودتها الآن مثبَتة على الطرفَين معًا.

### 3.2 منح الرؤية · ورشة الحوكمة · التذكيرات

| الفحص | النتيجة |
|---|---|
| `GET /api/report-view-grants` (Admin) | 200 |
| `GET /api/report-view-grants` (CEO) | **403** — أدمن فقط |
| `GET /api/report-view-grants/effective/me` (موظّف) | 200 |
| `GET /api/risks` · `/api/escalations` · `/api/decisions` (CEO) | 200 · 200 · 200 |
| `GET /api/risks/{ghost}` | **404** لا 403 |
| `GET /api/decisions` (موظّف) | **403** — محكوم بالنطاق |
| `POST /api/report-reminders/dry-run/generate` (Admin) | **200** — جافّ فقط |
| نفس المسار (موظّف) | **403** |

### 3.3 ميزات `develop` المعتمَدة — بلا انحدار

`GET /api/execution-taxonomy` · `/api/dashboard/me` · `/api/dashboard/pending-reports` ·
`/api/notifications` · `/api/reporting-calendar/my-cycles` · `/api/reporting-calendar/my-days` ·
`/api/report-calendar/missing-reports` — **كلّها 200**.

> **عيب أداة رابع كُشِف هنا:** التشغيل الأوّل استعمل مسارَين مخترعَين
> (`/api/dashboard/summary` و`/api/reporting-calendar/me`) فأعطى 404. المسارات الحقيقيّة
> استُخرِجت من سمات `[HttpGet]` في المتحكّمات، فصار الفحص يقيس المنتج لا التخمين.

---

## 4) صمت البريد والمجدول

| الفحص | النتيجة |
|---|---|
| `EmailNotifications__Mode` | `DryRun` |
| `Email__Enabled` | `false` |
| `Reminders__Enabled` | `false` |
| `ReportReminderScheduler__Enabled` | **غائب عن البيئة** ⟹ الافتراضيّ `false` في الكود |
| `email_outbox` قبل/بعد | **0 → 0** |
| `email_notifications` | 0 |
| رسائل أُرسِلت في آخر ساعة | 0 |
| أسطر سجلّ تخصّ المجدول بعد الإقلاع | **0** |

**`Email/Scheduler Leakage = 0`.** المكابح ثلاثة مستقلّة، وخدمة المجدول القادمة من
نَسَب الإنتاج (`ReportReminderSchedulerService`) لم تُسجّل سطرًا واحدًا.

---

## 5) حادثة الاعتمادات — لا تزال مغلقة

الحادثة أُغلِقت في تذكرة `CPW-UNIFIED-UAT-R1` (التقرير 01). المطلوب هنا هو التحقّق من
**عدم انتكاسها** بعد النشر، لا إعادة تنفيذها:

| الفحص | النتيجة |
|---|---|
| ملفّات `.env` بأسرار نصّيّة متبقّية (`/root` · `/tmp` · `/opt`) | **0** |
| بيانات الحادثة | 3 ملفّات manifest ببصمات SHA-256 ومفاتيح بلا قيم |
| `/root/uat-secrets` | `700 root:root` |
| `/root/uat-secrets/uat-accounts.json` | `600 root:root` |
| الأدوات الحسّاسة | `700` (`uat-role-gate.py` · `with_uat_secrets.py` · `uni-lineage-uat.py`) |
| حسابات UAT | 15 |
| **جلسات نشطة أُنشِئت قبل التدوير** | **0** |
| أقدم جلسة نشطة | `2026-08-15 22:06:01Z` — بعد التدوير |

**`Credential Incident = CLOSED (verified, not re-opened).`**

---

## 6) UAT البصريّة — الأدلّة الممكنة والقيد المعلَن

واجهة TEST خلف `auth_basic` (401 متوقَّع ومقصود؛ `/health` مستثنى)، فلا تصوير آليّ من
هذه الجلسة. الأدلّة البديلة **موضوعيّة وقابلة لإعادة التحقّق**:

| الدليل | النتيجة |
|---|---|
| `curl https://test.emarketingacademy.net/` | **401** — الحاجب الأمنيّ قائم كما هو مصمَّم |
| `/app/governance-workspace` داخل حزمة `dist` المنشورة | **موجود** |
| `/app/positions` داخل حزمة `dist` المنشورة | **موجود** |
| `/app/report-view-grants` · `/app/governance` | موجودان |
| حارس `routeRegistry.test.ts` | يقارن كلّ `to:` في `navConfig.ts` بجدول `App.tsx` — أخضر ضمن 550/550 |
| روابط تنقّل بلا مسار | **0** |

هذا يثبت أنّ المسارَين اللذين استُعيدا في التقرير 19 وصلا فعلًا إلى الحزمة المنشورة على
TEST، لا إلى شجرة المصدر وحدها.

---

## 7) كتلة بوّابة المرحلة M

```
TEST Smoke Gate                 = PASS   43/43
TEST Role/Scope Gate            = PASS   143/143  (11 identities × 12 checks)
Role Matrix vs Frozen Baseline  = IDENTICAL (132/132 cells)
Security Scope Expansion        = 0
Anti-Enumeration (404 not 403)  = PASS   (all roles incl. CEO)
Production Lineage UAT          = PASS   27/27
Develop-approved Feature Regr.  = 0
CPW-R2 Regression               = 0
CPW-R3 Regression               = 0
Email / Scheduler Leakage       = 0
Credential Incident             = CLOSED (0 pre-rotation sessions active)
Restored Routes in Deployed dist= 2/2
Tool Defects Found and Fixed    = 4  (all in measurement scripts, 0 product defects)
Product Defects Found           = 0
RC Touched                      = NO
Production Touched              = NO
```

## 8) الأدوات المُصلَحة أو المُضافة على TEST

| الأداة | الحالة |
|---|---|
| `/root/uni-smoke3.sh` | جديدة — تصحّح العيوب الثلاثة في `uni-smoke.sh` (المُبقاة كدليل) |
| `/root/uni-lineage-uat.py` | جديدة — UAT ميزات نَسَب الإنتاج |
| `/root/am-doc-probe.py` | جديدة — استقصاء الرؤية لكلّ مستند × دور |
| `/root/dump-counts.sh` | جديدة — استخراج خطّ الأساس العدديّ من نسخة `pg_dump` |
| `/root/backups/20260816-recon-l/baseline-counts.env` | خطّ الأساس المستخرَج |
| `/root/uat-role-matrix-recon-20260816.json` | لقطة المصفوفة لهذه الجولة |
