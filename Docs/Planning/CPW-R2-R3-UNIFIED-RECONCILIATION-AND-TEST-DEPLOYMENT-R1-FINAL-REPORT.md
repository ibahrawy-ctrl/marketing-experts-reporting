# CPW-R2 + CPW-R3 — المصالحة الموحّدة والنشر على TEST — R1 — التقرير النهائي

**التذكرة:** `CPW-R2-R3-UNIFIED-RECONCILIATION-AND-TEST-DEPLOYMENT-R1`
**التاريخ:** 16 أغسطس 2026
**النطاق:** توحيد نَسَب الهجرات (CPW-R2 + CPW-R3) على `develop`، ثمّ النشر على بيئة TEST/UAT حصرًا.
**البيئات المحظورة في هذه التذكرة:** RC، Production — **لا مساس بهما إطلاقًا**.

---

## 1) بوّابة المصالحة المعتمدة (Reconciliation Gate)

### 1.1 الخلفيّة (Backend)

| البند | القيمة | الحالة |
|---|---|---|
| Backend Unit | 115 / 115 | PASS |
| Backend Integration — الإجمالي | 1511 | — |
| Backend Integration — ناجح | 1509 | — |
| Backend Integration — فاشل | 2 | عيبا أساس معروفان |
| Backend Integration — متخطّى | 0 | — |
| مدّة التنفيذ | 6 د 6 ث | — |
| Known order-dependent baseline defects | 2 | مسجَّلة مسبقًا |
| **Unified Candidate Regression** | **0** | **PASS** |
| Unresolved | 0 | PASS |

**الفاشلان — مطابقة بالاسم لا بالعدد فقط:**

| # | الاختبار | العيب المسجَّل | رسالة الفشل |
|---|---|---|---|
| 1 | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` | `BASELINE-DEFECT-01` | `Assert.Equal() Failure — Expected: OK · Actual: NotFound` (سطر 309) |
| 2 | `EmployeeProfileScopeTests.Profile_Summary_Reflects_Submitted_Kpi` | `BASELINE-DEFECT-02` | `Assert.NotNull() Failure — Nullable<decimal> has no value` (سطر 152) |

كلاهما `order-dependent`: ينجح منفردًا ويفشل ضمن المجموعة الكاملة، وكلاهما مثبت على الأساس قبل المرشَّح. ⟹ **صفر انحدار جديد**.

**دليل التشغيل:** `/tmp/cpw-uni-regression.log` — شجرة التنفيذ المعزولة `/private/tmp/cpw-uni-reg`.

### 1.2 الواجهة (Frontend)

| البند | القيمة | الحالة |
|---|---|---|
| Frontend (Vitest) | 296 / 296 | PASS |
| Project 360 | 44 / 44 | PASS |
| Client Documents | 25 / 25 | PASS |

### 1.3 E2E — التصنيف النهائيّ

الجولة الأولى أظهرت فشلًا واحدًا في `e2e/landing.spec.ts:3` («landing page shows system title and login CTA»).

**السبب الجذريّ (مثبت):** خيار Playwright `reuseExistingServer` أعاد استخدام خادم Python قديم من `/tmp/l02uat/uat_serve.py` يستمع على المنفذ `4173`؛ فاختبرت الجولة نسخة `dist` **لا تتبع المرشَّح ولا `origin/develop`**.

**إعادة التشغيل على منفذ نظيف:** `E2E = 1/1 PASS`.

| التصنيف | القيمة |
|---|---|
| E2E Candidate Regression | 0 |
| E2E Baseline Defect | 0 |
| E2E Environment Contamination | 1 incident — **resolved** |

**قرار صريح:** لا يُسجَّل `BASELINE-DEFECT-03`، ولا يُضاف هذا البند إلى سجلّ عيوب الأساس؛ لأنّه تلوّث بيئة تشغيل لا عيب في الكود.

### 1.4 السلامة الهيكليّة (Schema / Model / Catalog)

| البند | القيمة | الحالة |
|---|---|---|
| Model Sync | CLEAN | PASS |
| Unified migrations | 35 | PASS |
| Temporary Probe | REMOVED (لا وجود له في `reporting-backend/src` ولا `reporting-frontend/src`) | PASS |
| Catalog | 38 | PASS |
| Catalog duplicates | 0 | PASS |

**إثبات Model Sync** — تطابق تامّ لبصمة المخطّط بين ثلاث قواعد مستقلّة:

```
MD5 (schema_dev.txt)      = 0cc81ddc4e10b5e7905fd196e11090e5
MD5 (schema_fresh.txt)    = 0cc81ddc4e10b5e7905fd196e11090e5
MD5 (schema_testlike.txt) = 0cc81ddc4e10b5e7905fd196e11090e5
```

---

## 2) نَسَب الهجرات الموحّد (Unified Migration Lineage)

| النَسَب | عدد الهجرات | الرأس (Head) |
|---|---|---|
| `origin/develop` (CPW-R3) | 33 | `20260811142239_AddProject360Foundation` |
| TEST/UAT الحاليّ (CPW-R2, `3344f78`) | 34 | `20260809165617_ClientDocumentVisibility` |
| **Unified (CPW-R2 + CPW-R3)** | **35** | `20260811142239_AddProject360Foundation` |

**الهجرتان الخاصّتان بـCPW-R2 وغير الموجودتين على `origin/develop`:**

1. `20260807033602_ClientDocumentsAndExternalLinks`
2. `20260809165617_ClientDocumentVisibility`

**الدلتا المتوقّعة على TEST عند النشر الموحّد:** هجرة **واحدة** فقط —
`20260811142239_AddProject360Foundation` — أي **34 → 35** بلا إعادة تطبيق هجرتَي CPW-R2.

---

## 3) حادثة `reporting_dev` — إفصاح شفّاف

### 3.1 الوصف

استخدمت **الجولة الأولى** من أوامر الهجرة **مفتاح سلسلة اتصال غير صحيح**، فحُلَّت السلسلة إلى قاعدة **التطوير المحلّيّة** بدل القاعدة المعزولة المقصودة، وطُبِّقت عليها **هجرتان إضافيّتان**.

### 3.2 الوقائع

| البند | القيمة |
|---|---|
| اسم القاعدة المتأثّرة | `reporting_dev` |
| المضيف | `127.0.0.1:5432` (محلّيّ — جهاز التطوير) |
| تصنيف البيئة | **Local Development** — ليست TEST ولا RC ولا Production |
| عدد الهجرات قبل | **33** |
| عدد الهجرات بعد | **35** |
| الهجرتان المطبَّقتان | `20260807033602_ClientDocumentsAndExternalLinks` · `20260809165617_ClientDocumentVisibility` |
| طبيعة الأثر | **Additive بحت** — إنشاء جداول جديدة فقط (`client_documents`, `client_document_versions`, `client_document_allowed_roles`, `client_document_allowed_users`, `client_external_links`) بلا `DROP` ولا تعديل مدمِّر على جداول قائمة |
| المساس ببيئة محميّة | **لا شيء** — صفر مساس بـTEST/RC/Production |

### 3.3 إثبات بقاء البيانات (Row Counts بعد الحادثة)

| الجدول | عدد الصفوف |
|---|---|
| `AspNetUsers` | 44 |
| `clients` | 12 |
| `projects` | 17 |
| `report_submissions` | 50 |
| `report_templates` | 45 |
| `client_documents` | 1 |
| `client_document_versions` | 1 |
| `project_objectives` | 0 |
| `project_kpis` | 0 |

جميع الجداول السابقة للحادثة احتفظت بمحتواها كاملًا؛ والجداول الجديدة أُنشئت فارغة (الصفّ الواحد في `client_documents` من تجربة محلّيّة لاحقة).

### 3.4 قرار عدم التراجع (No Downgrade / No Rollback)

**لن يُنفَّذ Downgrade ولا Rollback عشوائيّ**، للأسباب التالية مجتمعةً:

1. القاعدة **محلّيّة** وليست TEST/RC/Production.
2. الأثر **Additive** ولا يحذف ولا يعدّل أيّ بنية قائمة.
3. **بقاء البيانات مثبت** بالأعداد أعلاه.
4. الهجرتان أصبحتا **جزءًا من الـUnified Lineage المعتمدة (35)** — فالتراجع عنهما يخالف النَسَب المستهدف نفسه.

النتيجة النهائيّة: `reporting_dev` الآن **مطابقة تمامًا للنَسَب الموحّد (35 هجرة، الرأس `20260811142239_AddProject360Foundation`)** — أي أنّ الحادثة انتهت إلى حالة صحيحة لا إلى انحراف.

### 3.5 الإجراء الوقائيّ الجديد (إلزاميّ من الآن)

**قبل أيّ أمر Migration** (`dotnet ef database update` أو أيّ كتابة على قاعدة)، يجب **إثبات صريح مكتوب** للبنود الأربعة:

```
Environment Identity
Resolved Database Name
Resolved Host
Expected Migration Head
```

ولا يُنفَّذ الأمر قبل مطابقة الأربعة للهدف المقصود.

**مفتاح الاتصال المعتمد والمثبت هو:**

```
REPORTING_DB_CONNECTION
```

يُستخدم هذا المفتاح حصرًا؛ ولا يُعتمد على مفتاح افتراضيّ ضمنيّ ولا على سلسلة اتصال مضمّنة في `appsettings`.

---

## 4) حالة الفروع قبل المصالحة

| المرجع | الـSHA | الملاحظة |
|---|---|---|
| `develop` (محلّيّ) قبل | `c157829` | متأخّر — أب دمج CPW-R3 |
| `origin/develop` | `78c8a2d` | CPW-R3 مدموج (33 هجرة) |
| `origin/feature/cpw-r1b2-document-service-20260807` | `3344f78` | CPW-R2 غير مدموج (34 هجرة) |

**تحليل تداخل شجرة العمل مع `origin/develop`:** 149 ملفًّا مختلفًا (125 إضافة + 24 تعديل)، ودمج CPW-R3 مسّ 82 ملفًّا. **التداخل الفعليّ = ملفّ واحد فقط**: `DependencyInjection.cs` — وتبيّن أنّ نسخة شجرة العمل **مجموعة عليا (superset)** تحتفظ بكامل تسجيلات Project 360 وتضيف `ReportReminderSchedulerService`، مع إعادة ترتيب ستّ تسجيلات لا أثر دلاليّ لها في حاوية الـDI. ⟹ **لا فقد لأيّ عمل من CPW-R3**.

---

## 5) نظافة الشجرة قبل الالتزام

| الفحص | النتيجة |
|---|---|
| Temporary Probe في `reporting-backend/src` | لا وجود |
| Temporary Probe في `reporting-frontend/src` | لا وجود (المطابقة الوحيدة مكوّن اختبار مشروع داخل `auth.test.tsx`) |
| ملفّات `*.orig` / `*.rej` / `*.bak` / `*.dump` / `*.sql.gz` | لا وجود |
| ملفّات غير متتبَّعة خارج `Docs/` و`Ops/` و`src/` و`tests/` و`tools/` | لا وجود |
| أسرار بين الملفّات غير المتتبَّعة (`.env`, `*.pem`, `*.key`, `appsettings.*`) | لا وجود — القوالب placeholders فقط |

---

*(تُستكمل الأقسام 6–12: الالتزام الذرّيّ، الدمج والدفع، النشر على TEST، Smoke، حزمة UAT.)*
