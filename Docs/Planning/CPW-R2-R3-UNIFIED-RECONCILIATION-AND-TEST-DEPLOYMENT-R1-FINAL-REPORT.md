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

## 6) الالتزام الذرّيّ والدمج والدفع

### 6.1 قرار حدود المرشَّح (حاسم)

شجرة الانحدار المُصادَق عليها `/private/tmp/cpw-uni-reg` **لا تحتوي** على: `ReportReminderSchedulerService.cs` · `ReportWorkingDaysPolicy.cs` · `ActionResultToast.tsx` وسائر العمل المحلّيّ غير الملتزَم.

⟹ **المرشَّح الموحّد = `origin/develop` (`78c8a2d`) + CPW-R2 (`3344f78`) فقط، لا شيء غيره.**

لو التُزم العمل المحلّيّ الإضافيّ لَنُشِر على TEST كودٌ **لم تغطِّه بوّابة المصالحة إطلاقًا** — وهو ما يُبطل معنى البوّابة. لذلك حُفِظ ذلك العمل كاملًا ولم يُدمَج:

| البند | القيمة |
|---|---|
| فرع الحفظ | `wip/local-uncommitted-20260816` |
| الـSHA | `89000a8` |
| المحتوى | 33 ملفًّا · `+2777 / -274` |
| الحالة | **محفوظ بالكامل — مستبعَد من `develop` ومن TEST** |

### 6.2 الالتزامات الذرّيّة

| # | الـSHA | الرسالة | النطاق |
|---|---|---|---|
| 1 | `a373afe` | `docs(cpw-r2-r3): record unified reconciliation gate, lineage, and ops runbooks` | 117 ملفًّا — `Docs/` · `Ops/` · `CLAUDE.md` (توثيق بحت، صفر كود) |
| 2 | `f355164` | `merge(cpw-r2-r3): unify CPW-R2 client documents with CPW-R3 Project 360 lineage` | دمج النَسَب الموحّد — 35 هجرة |

**تجهيز مُسمَّى (Named Staging):** أُضيفت الملفّات بالمسار الصريح لا بـ`git add -A` العشوائيّ، وفُصل التوثيق عن الدمج في التزامَين مستقلَّين ليبقى كلّ التزام قابلًا للمراجعة والعكس منفردًا.

### 6.3 الدمج في شجرة عمل معزولة

الشجرة الرئيسيّة كانت متّسخة، وثلاثة ملفّات متداخلة (`DependencyInjection.cs` · `format.ts` · `types/api.ts`). لذلك نُفِّذ الدمج في شجرة عمل مرتبطة معزولة `/tmp/uni-merge` (`git worktree add --detach`) بدل المساس بالشجرة الرئيسيّة.

**تعارضان — وحُلّا اتّحادًا (Union) لا اختيارًا:**

| # | الملفّ | طبيعة التعارض | الحلّ |
|---|---|---|---|
| 1 | `AppDbContext.cs` | `both-added` لمجموعات `DbSet` | اتّحاد: 6 مجموعات Project 360 (CPW-R3) + 5 مجموعات مستندات العملاء (CPW-R2) |
| 2 | `App.tsx` | `both-added` لثوابت الأدوار | اتّحاد: `PROJECT_360_ROLES` **و** `CLIENT_360_ROLES` معًا |

**إثبات مطابقة الأثر (Artifact Fidelity):** `diff -rq` بين `/tmp/uni-merge` و`/private/tmp/cpw-uni-reg` ⟹ شجرتا المصدر **متطابقتان بايتًا ببايت**؛ الفروق الوحيدة: مجلّد `App_Data` المتجاهَل في git، وملفّا اختبار يختلفان حصرًا في اسم قاعدة الاختبار المعزولة (`reporting_uni_reg` مقابل `reporting_test`).

### 6.4 إثبات عدم فقد أيّ عمل

| الفحص | الأمر | النتيجة |
|---|---|---|
| قبل تحريك مؤشّر الفرع | `git merge-base --is-ancestor c157829 origin/develop` | PASS |
| قبل الدفع | `git merge-base --is-ancestor origin/develop f355164` | PASS |

⟹ الدفع **Fast-forward خالص**، بلا إعادة كتابة تاريخ وبلا فقد التزام واحد.

### 6.5 الدفع وإثبات تطابق الرؤوس

```
78c8a2d..f355164  HEAD -> develop
```

| المرجع | الـSHA |
|---|---|
| `HEAD` المحلّيّ | `f3551648a929a1178b9eb3789861d6a0106ff7f1` |
| `origin/develop` | `f3551648a929a1178b9eb3789861d6a0106ff7f1` |
| الفرع | `develop` |
| `git status --porcelain` | **فارغ — الشجرة نظيفة** |
| `merge-base --is-ancestor origin/develop HEAD` | **YES** |

---

## 7) النسخ الاحتياطيّة قبل النشر

| البند | القيمة |
|---|---|
| المسار | `/root/db-backups/cpw-r2-r3-unified-20260815-212607/` |
| التحقّق من صحّة الـdump | `pg_restore --list` ⟹ **430 كائنًا · 72 `TABLE DATA`** |
| أرشيفات التطبيق والواجهة | `tgz` — سلامة مثبتة بـ`tar -tzf` بلا أخطاء |
| الحداثة | مأخوذة قبل النشر مباشرةً (لا نسخة قديمة) |
| جاهزيّة التراجع | **مثبتة** — لم يُحتَج إليها |

**الإجراء الوقائيّ الجديد مطبَّق فعليًّا قبل أمر الهجرة:**

```
Environment Identity   = TEST/UAT (khubara-reporting-test)
Resolved Database Name = reporting_test_uat
Resolved Host          = 127.0.0.1:5432 (الخادم البعيد 187.127.72.232)
Expected Migration Head= 20260811142239_AddProject360Foundation
```

الأربعة طُوبقت على الهدف المقصود **قبل** أيّ كتابة.

---

## 8) النشر الموحّد على TEST

| البند | القيمة |
|---|---|
| الخدمة | `khubara-reporting-test` — **active** |
| ثنائيّ الـAPI | `/opt/reporting-test/publish/Reporting.Api.dll` — `2026-08-15 21:26:56 UTC` |
| بناء الواجهة | `/opt/reporting-test/frontend/dist/index.html` — `2026-08-15 21:27:09 UTC` |
| حزمة الواجهة | `assets/index-Cnoz3M8P.js` — تحتوي مسارات Project 360 (مطابقة مثبتة) |
| `GET /health` (داخليّ 5091) | **200** |
| `GET https://test.emarketingacademy.net/health` | **200** |
| `GET https://test.emarketingacademy.net/` | **401** — بوّابة `auth_basic` المقصودة على TEST (`.htpasswd-rc-test`)، **ضابط لا عيب** |
| nginx | active |
| أخطاء `journalctl -p err` بعد النشر | **0** |

### 8.1 الهجرة — 34 → 35 بلا إعادة تطبيق

| الفحص | النتيجة |
|---|---|
| الهجرات المعلّقة قبل التطبيق (بـ`comm` بين المطبَّق والأثر) | **واحدة فقط**: `20260811142239_AddProject360Foundation` |
| هجرات زائدة على TEST غير موجودة في الأثر | **صفر** — مجموعة عليا مثاليّة |
| عدد الهجرات بعد | **35** |
| الرأس بعد | `20260811142239_AddProject360Foundation` |
| صفوف مكرّرة في `__EFMigrationsHistory` | **0** ⟹ **هجرتا CPW-R2 لم تُعاد** |

---

## 9) Catalog Bootstrap — إثبات الخمول (Idempotency)

نُفِّذ **مرّتين متتاليتين**، والنتيجة متطابقة حرفيًّا:

| التشغيل | `contract_deliverable` | `strategy_field` | `strategy_section` | الإجمالي | التكرارات |
|---|---|---|---|---|---|
| الأوّل | 18 | 14 | 6 | **38** | **0** |
| الثاني | 18 | 14 | 6 | **38** | **0** |

⟹ التشغيل الثاني **لم يُنشئ صفًّا واحدًا** — الخمول مثبت لا مفترض.

---

## 10) بقاء بيانات CPW-R2 — إثبات مادّيّ

| البند | قبل النشر | بعد النشر | الحالة |
|---|---|---|---|
| `client_documents` | 1 | **1** | PASS |
| `client_document_versions` | 1 | **1** | PASS |
| `clients` | 4 | **4** | PASS |
| `projects` | 5 | **5** | PASS |
| `AspNetUsers` | 17 | **17** | PASS |
| `report_submissions` | 13 | **13** | PASS |

**إثبات بقاء الملفّ الفعليّ على القرص — لا الصفّ فقط:**

```
md5(storage file) قبل النشر = a704a76335615805401bee5c7cb9c4b5
md5(storage file) بعد النشر = a704a76335615805401bee5c7cb9c4b5
```

⟹ المستند ونسخته وملفّه المخزَّن **نجت جميعها سليمة**، و`GET .../documents/{docId}/download` يُرجِع **200**.

---

## 11) بوّابة الدخان الموحّدة (Smoke Gate)

**السكربت:** `/root/uni-smoke.sh` — قراءة فقط، بلا طباعة أيّ سرّ.

**النتيجة النهائيّة: `PASS=34 · FAIL=0 · SMOKE_GATE=PASS`**

| المجموعة | التغطية | النتيجة |
|---|---|---|
| CPW-R2 · مستندات العملاء | القائمة · الروابط · تفصيل المستند · استهلاك التخزين · التنزيل · ظهور المستند في القائمة | 6/6 PASS |
| CPW-R3 · Project 360 | `overview` · `strategy` · `strategy/schema` · `objectives` · `kpis` · `contract-deliverables` · `risks` · `decisions` · `notes` | 9/9 PASS |
| الكتالوج | `GET /api/execution-taxonomy` | 1/1 PASS |
| مكافحة التعداد والنطاق | 8 فحوص | 8/8 PASS |
| صمت البريد | العلامات + الصندوق الصادر + المُرسَل | 2/2 PASS |
| بقاء البيانات | 7 جداول | 7/7 PASS |

### 11.1 تصحيح مهمّ — الجولة الثانية

أظهرت الجولة الثانية **5 إخفاقات، وكلّها أخطاء في افتراضاتي عن المسارات لا عيوبًا في النشر**:

| الافتراض الخاطئ | المسار الصحيح المثبت من الكود |
|---|---|
| `/api/clients/{id}/external-links` | `/api/clients/{id}/links` |
| `/api/client-documents/{id}` | `/api/clients/{clientId}/documents/{documentId}` |
| `GET /api/projects/{id}/health` | **غير موجود** — الموجود `POST .../health/recompute` فقط |
| `/api/projects/{id}/governance` | `/risks` · `/decisions` · `/notes` |
| `strategy` يجب أن يعيد 200 | **204 مشروع** = لا يوجد صفّ استراتيجيّة بعد |

⟹ صُحِّح السكربت وأُعيد التشغيل: **34/34**. **صفر عيب نشر.**

---

## 12) بوّابة الأدوار والنطاق ومكافحة التعداد

**السكربت:** `/root/uni-role-scope.sh` — دخول فعليّ بـ**9 أدوار حقيقيّة** من حسابات UAT، بلا طباعة أيّ كلمة مرور.

**النتيجة: `PASS=125 · FAIL=0 · ROLE_GATE=PASS`**

| الدور | `clients/{id}/documents` | `links` | `documents/{d}` | Project 360 (4 مسارات) | مكافحة التعداد (5) | `POST` مجهول |
|---|---|---|---|---|---|---|
| Viewer | 404 | 404 | 404 | 404 ×4 | 404 ×5 | 400 |
| Employee | 404 | 404 | 404 | 404 ×4 | 404 ×5 | 400 |
| TeamLeader | 404 | 404 | 404 | 404 ×4 | 404 ×5 | 400 |
| Manager (Ops) | 404 | 404 | 404 | 404 ×4 | 404 ×5 | 400 |
| HR | **200** | 404 | 404 | 404 ×4 | 404 ×5 | 400 |
| FinanceManager | **200** | 404 | 404 | 404 ×4 | 404 ×5 | 400 |
| AccountPortfolioReader | 404 | 404 | 404 | 404 ×4 | 404 ×5 | — |
| GeneralManager | **200** | **200** | **200** | **200 ×4** | 404 ×5 | 400 |
| CEO | **200** | **200** | **200** | **200 ×4** | 404 ×5 | 400 |

### 12.1 قراءة النتيجة

1. **مكافحة التعداد صلبة عبر كلّ الأدوار بلا استثناء:** المورد المجهول يُرجِع **404 دائمًا، ولا يُرجِع 403 ولا 500 لأيّ دور**. ⟹ لا تسريب لوجود المورد.
2. **HR وFinanceManager يريان القائمة (200) لكن لا يريان المستند نفسه (404):** هذا **بالضبط** السلوك المقصود — صلاحيّة الوصول إلى نقطة القائمة مع تصفية على مستوى المورد (`ClientDocumentAllowedRoles` / `ClientDocumentAllowedUsers`)؛ فالقائمة تعود مُنطَقةً والمستند غير المصرَّح به يُرجِع **404 لا 403** طبقًا لقاعدة المعماريّة المعتمدة.
3. **AccountPortfolioReader يُرجِع 404 على هذا العميل/المشروع تحديدًا:** لأنّهما خارج محفظته — نطاق صحيح لا عطل.
4. **`POST` على مورد مجهول = 400** قبل أيّ كتابة ⟹ لا كتابة صامتة.

### 12.2 ملاحظتان تشغيليّتان (لا تحجبان)

| # | الملاحظة | الأثر | التوصية |
|---|---|---|---|
| 1 | كلمة مرور `account.manager@uat.local` مُدوَّرة في CPW-R2؛ المفتاح الصالح `UAT_PW_AM_R2` في `/root/uat-prep-runtime/cpwr2-am.env` لا `UAT_PW_AM` | إخفاق دخول واحد في الجولة الأولى — **ليس عيبًا في النظام** | توحيد المفتاح في ملفّ أسرار واحد |
| 2 | **[أمنيّ — يستحقّ الإصلاح]** قيم `/root/uat-prep-runtime/uat-role-accounts.env` **غير مقتبسة** وتحوي رموز صدفة (`&`, `$`, `%`, `)`) ⟹ أيّ `source` للملفّ **يُسرّب قيمة سرّيّة إلى `stderr`** ضمن رسالة خطأ الصدفة | تسريب محتمل لكلمة مرور UAT في سجلّات أيّ سكربت يستعمل `source` | **اقتباس كلّ القيم** (`KEY='value'`)؛ وقد تُجُنِّب الأمر هنا بقراءة آمنة عبر `grep`/`cut` بلا `source` ولا `eval` |

---

## 13) صمت البريد والتذكيرات — إثبات نهائيّ

| البند | القيمة | الحالة |
|---|---|---|
| `Email__Enabled` | `false` | PASS |
| `EmailNotifications__Mode` | `DryRun` | PASS |
| `Reminders__Enabled` | `false` | PASS |
| `email_outbox` قبل | 0 | — |
| `email_outbox` بعد كلّ الفحوص | **0** | PASS |
| رسائل مُرسَلة خلال آخر ساعتين | **0** | PASS |

⟹ **صفر بريد، وصفر تذكير أُطلق** طوال النشر وبوّابتَي الدخان والأدوار.

---

## 14) حزمة UAT الموحّدة

### 14.1 الوصول

| البند | القيمة |
|---|---|
| الواجهة | `https://test.emarketingacademy.net/` |
| بوّابة nginx | `auth_basic` — «Khubara Reporting — RC Test» (`/etc/nginx/.htpasswd-rc-test`) |
| الصحّة (بلا بوّابة) | `https://test.emarketingacademy.net/health` ⟹ 200 |
| API الداخليّ | `127.0.0.1:5091` |
| القاعدة | `reporting_test_uat` — 35 هجرة |

### 14.2 الحسابات (كلمات المرور في ملفّات الأسرار على الخادم — لا تُطبع)

`ceo@uat.local` · `gm@uat.local` · `ops.manager@uat.local` · `account.manager@uat.local` (المفتاح `UAT_PW_AM_R2`) · `team.leader@uat.local` · `employee@uat.local` · `hr.manager@uat.local` · `finance.manager@uat.local` · `finance.employee@uat.local` · `sales.employee@uat.local` · `viewer@uat.local`

### 14.3 نطاق الاختبار المطلوب من المستخدمين

**CPW-R2 — مستندات وأصول العميل:**
1. رفع مستند جديد ثمّ إصدار نسخة ثانية منه، والتحقّق من ظهور النسختين في السجلّ.
2. ضبط صلاحيّة المستند بالدور وبالمستخدم، ثمّ التحقّق من **اختفائه تمامًا (404)** لغير المصرَّح له — لا رسالة «ممنوع».
3. الروابط الخارجيّة: إضافة/تعديل/حذف.
4. استهلاك التخزين لكلّ عميل.

**CPW-R3 — Project 360:**
1. نظرة عامّة على المشروع (`overview`) وتماسك أرقامها.
2. الاستراتيجيّة ومخطّطها (`strategy` / `strategy/schema`) — ملاحظة: **204 قبل إنشاء أوّل استراتيجيّة أمر طبيعيّ**.
3. الأهداف والمؤشّرات وقراءاتها.
4. مخرجات العقد (`contract-deliverables`) مقابل الكتالوج (38 عنصرًا).
5. الحوكمة: المخاطر · القرارات · الملاحظات.
6. إعادة احتساب صحّة المشروع (`POST .../health/recompute`).

**تكامليًّا:** التنقّل بين العميل ومشاريعه في مسار واحد، والتأكّد من عدم تعارض الشاشتَين.

### 14.4 ما هو **خارج** نطاق هذه الحزمة صراحةً

- كلّ ما في `wip/local-uncommitted-20260816` (مجدول التذكيرات، سياسة أيّام العمل، `ActionResultToast`) — **غير منشور ولا يُختبَر**.
- البريد الحيّ — القناة في `DryRun` والتذكيرات مطفأة عمدًا.
- RC والإنتاج — **لم يُمسَّا إطلاقًا في هذه التذكرة**.

---

## 15) العيوب المفتوحة المرحَّلة

| المعرّف | الاختبار | الطبيعة | القرار |
|---|---|---|---|
| `BASELINE-DEFECT-01` | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` | `order-dependent` — ينجح منفردًا ويفشل ضمن المجموعة | مرحَّل — لا يُصلَح ضمن هذه التذكرة |
| `BASELINE-DEFECT-02` | `EmployeeProfileScopeTests.Profile_Summary_Reflects_Submitted_Kpi` | `order-dependent` — نفس النمط | مرحَّل — لا يُصلَح ضمن هذه التذكرة |

**ولا يُسجَّل `BASELINE-DEFECT-03`** — حادثة E2E صُنِّفت تلوّث بيئة تشغيل وحُلَّت (القسم 1.3).

---

## 16) الحكم النهائيّ

```
CPW-R2 + CPW-R3 Unified on develop = YES
Unified TEST Deployment            = SUCCESS
CPW-R2 Data Preserved              = YES
CPW-R2 Regression                  = 0
CPW-R3 Regression                  = 0
TEST Smoke                         = PASS
Ready for Unified UAT              = GO
Ready for RC                       = NO-GO
Ready for Production               = NO-GO
```

**سبب `NO-GO` لـRC وللإنتاج:** حوكمة لا فنّيّة — نطاق هذه التذكرة محصور في TEST/UAT حصرًا، وأيّ تقدّم إلى RC أو الإنتاج يستلزم **تصريحًا صريحًا جديدًا من المالك** بعد اجتياز UAT الموحّد.

**لم يُنفَّذ أيّ Rollback** — لأنّ بوّابة الدخان وبوّابة الأدوار اجتازتا بلا إخفاق واحد.

