# CPW-R3 — تقرير الحظر الموحَّد لنَسَب بيئة TEST وحزمة مصالحة CPW-R2 (R1)

**التصنيف حسب §10:** **C (خطر انحدار كود مقابل CPW-R2) + D (تعارض نَسَب هجرات)** ⟹ **توقّف إلزاميّ قبل أيّ نشر على TEST.**
**التاريخ:** 15 أغسطس 2026 · **الطبيعة:** تدقيق **قراءة-فقط** بالكامل — صفر كتابة على TEST، صفر نسخ احتياطيّ، صفر نشر، صفر هجرة.
**لا يمنع هذا التقرير الدمج على `develop`** — فقد تمّ الدمج بنجاح بعد اجتياز بوّابته المستقلّة (`origin/develop` = `78c8a2d`).

---

## 1) الخلاصة التنفيذيّة في ثلاث جمل

بيئة TEST تعمل حاليًّا على فرع **CPW-R2** (`feature/cpw-r1b2-document-service-20260807` عند `3344f78`) وهو **ليس سلفًا لـ`origin/develop`**، ويحمل **34 هجرة** تشمل هجرتَي مستندات العملاء غير الموجودتَين في مرشَّح CPW-R3 (33 هجرة).
مرشَّح CPW-R3 و فرع CPW-R2 **شقيقان** ينحدران من الأساس نفسه `c157829`، ولا يعرف أيّ منهما ملفّات الآخر؛ لذلك نشر CPW-R3 على TEST كما هو **سيحذف كامل ميزة مستندات العملاء من الكود** (المتحكّم والخدمة ومقيّم الوصول والواجهة) ويترك سبعة جداول `client_document*` معزولة بلا تعيين في النموذج.
المصالحة ممكنة تقنيًّا وصغيرة الحجم (تعارضان نصّيّان فقط)، لكنّها **تستلزم إعادة توليد `AppDbContextModelSnapshot` ودمج CPW-R2 إلى `develop`** — وكلاهما خارج التصريح الحاليّ ويحتاج قرار مالك صريحًا.

---

## 2) الانحراف الدقيق (Exact divergence)

| البُعد | بيئة TEST الحاليّة | مرشَّح CPW-R3 (`78c8a2d`) |
| --- | --- | --- |
| الالتزام المنشور | `3344f7800f223a97b2fd4429d92d8c3449f3cfd9` (مثبَت عبر SourceLink داخل `Reporting.Api.dll` و`Reporting.Infrastructure.dll`) | `78c8a2d6996e614b979f0e4ff479b7233fe43946` |
| الفرع | `feature/cpw-r1b2-document-service-20260807` (CPW-R2) | `feature/cpw-r3-project-360-candidate-r1` |
| سلف لـ`origin/develop`؟ | **لا** | نعم (هو نفسه رأس `develop` الآن) |
| الأساس المشترك | `c157829` | `c157829` |
| المسافة عن الأساس | 13 التزامًا | 12 التزامًا |
| عدد الهجرات في الكود | 34 | 33 |
| عدد الهجرات المطبَّقة على القاعدة | **34** (`reporting_test_uat`) | — |
| رأس الهجرات | `20260809165617_ClientDocumentVisibility` | `20260811142239_AddProject360Foundation` |
| جداول `public` | 72 | — |
| حجم القاعدة | 12 MB | — |

**الهجرتان الموجودتان على TEST والغائبتان عن المرشَّح:**
1. `20260807033602_ClientDocumentsAndExternalLinks` (CPW-R1B2)
2. `20260809165617_ClientDocumentVisibility` (CPW-R2)

**الهجرة الموجودة في المرشَّح والغائبة عن TEST:** `20260811142239_AddProject360Foundation` (واحدة فقط، **إضافيّة بحتة**).

---

## 3) دليل طبقة التطبيق (قراءة-فقط، من الخادم مباشرة)

| الفحص | النتيجة | التفسير |
| --- | --- | --- |
| `GET /health` على `127.0.0.1:5091` | `HTTP 200` · `{"status":"ok","service":"reporting-api"}` | الخدمة سليمة |
| `GET /api/clients/{guid}/documents` | **401** | مسار CPW-R2 **موجود وحيّ** (401 = يتطلّب مصادقة) |
| `GET /api/clients/{guid}/external-links` | 404 | المسار بصيغة مختلفة داخل متحكّم المستندات (لا يُستدَلّ منه على غياب الميزة) |
| `GET /api/projects/{guid}/overview` | **404** | Project 360 **غير موجود إطلاقًا** على TEST |
| `GET /api/projects/{guid}/strategy/schema` | **404** | Project 360 غير موجود |
| رموز `ClientDocumentsController` في `Reporting.Api.dll` | **12 تطابقًا** | الميزة مبنيّة داخل الثنائيّة المنشورة |
| رموز `ClientDocumentService` في `Reporting.Infrastructure.dll` | **18 تطابقًا** | الخدمة مبنيّة داخل الثنائيّة المنشورة |
| رموز `ProjectOverviewController` في `Reporting.Api.dll` | **0** | تأكيد غياب Project 360 |
| حزمة الواجهة | `index-QJsadsGt.js` (1,439,143 بايت، 10 أغسطس 10:16) تحوي `client-documents` و«المستندات» | واجهة مستندات العملاء **منشورة وفعّالة** |
| صفّ فعليّ في `client_documents` | **1** | يوجد بيان مستخدَم فعلًا في البيئة |

---

## 4) ميزات الكود التي ستنحدر لو نُشر المرشَّح كما هو

| الملفّ الموجود على TEST (CPW-R2) | حالته في مرشَّح CPW-R3 | الأثر عند النشر |
| --- | --- | --- |
| `Reporting.Api/Controllers/ClientDocumentsController.cs` | **غير موجود** | كلّ مسارات المستندات ⟵ 404 |
| `Reporting.Application/Documents/IClientDocumentService.cs` | غير موجود | — |
| `Reporting.Application/Documents/IDocumentAccessEvaluator.cs` | غير موجود | **تعطّل صلاحيّات المستندات بالكامل** |
| `Reporting.Infrastructure/Services/ClientDocumentService.cs` | غير موجود | — |
| `Reporting.Infrastructure/Services/DocumentAccessEvaluator.cs` | غير موجود | — |
| `Domain/Entities/Clients/ClientDocument*.cs` (4 كيانات) | غير موجودة | 7 جداول تصبح **معزولة بلا تعيين** في `AppDbContext` |
| `reporting-frontend/src/lib/useClientDocuments.ts` + شاشات المستندات | غير موجودة | اختفاء واجهة المستندات من الحزمة |
| هجرتا CPW-R2 (`20260807033602`, `20260809165617`) | غير موجودتَين في الكود | تبقيان مطبَّقتَين في `__EFMigrationsHistory` بلا مقابل في الكود ⟹ **انحراف نموذج/قاعدة دائم** |

**البيانات لن تُحذف** (الهجرات إضافيّة والجداول تبقى)، لكنّ الميزة **ستصبح غير قابلة للوصول** وواجهة المستخدم ستفقدها — وهذا بحدّ ذاته انحدار محظور صراحةً في §2 و§10.

---

## 5) الفروق التي تخصّ الهجرات فقط (TEST migration-only differences)

- الترتيب الزمنيّ **سليم بلا انعكاس**: `20260807033602` < `20260809165617` < `20260811142239`.
  ⟹ لو نُشرت حزمة مُصالَحة، ستكون `20260811142239` هي **الهجرة المعلَّقة الوحيدة** وستُطبَّق فوق هجرتَي CPW-R2 بلا أيّ تعديل يدويّ على `__EFMigrationsHistory`، والنتيجة **35 هجرة**.
- لا حاجة إلى هجرة #34 جديدة، ولا إلى `DROP`، ولا إلى إعادة إنشاء القاعدة.
- الخطر الوحيد المتبقّي على مستوى المخطَّط هو **`AppDbContextModelSnapshot`**: لقطة المرشَّح لا تعرف جداول المستندات، ولقطة CPW-R2 لا تعرف جداول Project 360. الدمج النصّيّ الآليّ لهذا الملفّ **غير موثوق** والصحيح هو **إعادة توليده** بعد الدمج.

---

## 6) جدوى المصالحة تقنيًّا (قِيس فعليًّا بـ`git merge-tree`، بلا كتابة)

- ملفّات لمسها **الفرعان** معًا: **5** فقط
  `DependencyInjection.cs` · `AppDbContext.cs` · `AppDbContextModelSnapshot.cs` · `App.tsx` · `ProjectDetailPage.tsx`
- **دُمج آليًّا بنجاح:** `DependencyInjection.cs` · `AppDbContextModelSnapshot.cs` · `ProjectDetailPage.tsx`
- **تعارض محتوى حقيقيّ:** ملفّان فقط
  | الملفّ | أسطر مضافة من CPW-R2 | أسطر مضافة من CPW-R3 | الطبيعة |
  | --- | --- | --- | --- |
  | `Persistence/AppDbContext.cs` | 10 | 10 | تسجيل `DbSet` — **إضافيّ بحت، لا تعارض دلاليّ** |
  | `reporting-frontend/src/App.tsx` | 7 | 8 | تسجيل مسارات — **إضافيّ بحت، لا تعارض دلاليّ** |
- لا حذف ولا إعادة تسمية في أيّ من الفرعَين لملفّات الآخر.

⟹ **التعارض تقنيّ لا دلاليّ**، وحجمه ضئيل (≈35 سطرًا). العقبة ليست الدمج بل **الحوكمة**.

---

## 7) لماذا لا يجوز المتابعة تلقائيًّا رغم صغر الحجم

ثلاثة موانع صريحة في التوجيه الحاكم، كلٌّ منها كافٍ وحده:

1. **§10 — تصنيف C/D يوجب التوقّف قبل النشر.** ثبت C (انحدار كود مقابل CPW-R2) و D (نَسَب هجرات متفرّع).
2. **«الحاجة إلى تغيير مخطَّط أو هجرة جديدة» من الموانع الإلزاميّة.** إعادة توليد `AppDbContextModelSnapshot` عمل على طبقة النموذج يلزمه إقرار مالك.
3. **دمج CPW-R2 إلى `develop` لم يُصرَّح به قطّ.** التوجيه يمنع صراحةً «الدمج التلقائيّ لـCPW-R2»، وفرع CPW-R2 لم يُدمَج تاريخيًّا بقرار مالك.

---

## 8) الخيارات المعروضة على المالك

| # | الخيار | ما يحدث | المخاطر | نطاق العمل |
| --- | --- | --- | --- | --- |
| **أ** | **حزمة مصالحة CPW-R2 + CPW-R3 على `develop` ثمّ نشر موحَّد على TEST** *(المُوصى به)* | دمج فرع CPW-R2 إلى `develop` فوق `78c8a2d`، حلّ التعارضَين الإضافيَّين، إعادة توليد اللقطة، انحدار كامل، ثمّ نشر واحد يجمع الميزتَين | منخفضة–متوسّطة: التعارض تقنيّ فقط؛ الخطر الحقيقيّ في اللقطة ويُغطّى باختبار Model Sync | ~5 ملفّات + إعادة توليد لقطة + انحدار كامل |
| ب | تجميد TEST على CPW-R2 وإنشاء بيئة ثانية لـCPW-R3 | صفر مساس بـCPW-R2؛ UAT لـProject 360 في بيئة منفصلة | تكلفة بنية تحتيّة + تشتّت بيانات UAT + نَسَبان متوازيان | إعداد بيئة كاملة |
| ج | نشر CPW-R3 على TEST كما هو | Project 360 يعمل | **مرفوض** — يحذف ميزة منشورة ويخالف §2/§10 صراحةً | — |
| د | التراجع بـTEST إلى نَسَب `develop` ثمّ نشر CPW-R3 | نَسَب موحَّد | **مرفوض** — يعني فقدان CPW-R2 من البيئة وإبطال UAT ماليّ/مستنديّ سابق | — |

---

## 9) حزمة المصالحة المُوصى بها (تفصيليًّا — للتنفيذ عند التصريح فقط)

**الفرع/الالتزامات المطلوبة:** `feature/cpw-r1b2-document-service-20260807` عند `3344f78`، وهو 13 التزامًا فوق `c157829`:

```
7d160af feat(documents): add enterprise document domain and migration (CPW-R1B2)
179a900 feat(documents): add secure storage and content validation (CPW-R1B2)
c799d79 feat(documents): add document application contracts (CPW-R1B2)
f224014 feat(documents): implement client document and link services (CPW-R1B2)
d6c9c22 feat(documents): expose client document APIs and upload limits (CPW-R1B2)
3db462d feat(clients): add documents and links to client 360 (CPW-R1B2)
1121e57 test(documents): add document regression coverage and backup runbook (CPW-R1B2)
0da5153 feat(documents): add document visibility domain and migration (CPW-R2)
d445836 feat(documents): add document visibility policies and contracts (CPW-R2)
e28affb feat(documents): enforce server-side document visibility (CPW-R2)
212162d feat(documents): expose document visibility controls (CPW-R2)
97434c2 feat(clients): align client manager access and document visibility ux (CPW-R2)
3344f78 test(documents): add visibility and client manager regression coverage (CPW-R2)
```

**الخطوات المقترحة (كلّها في شجرة معزولة، وكلّها تحتاج تصريحًا صريحًا):**
1. شجرة تكامل جديدة من `origin/develop` (= `78c8a2d`).
2. `git merge 3344f78` (بلا rebase وبلا force).
3. حلّ التعارضَين بالضمّ الإضافيّ (لا `ours`/`theirs`): كلا مجموعتَي `DbSet` وكلا مجموعتَي المسارات.
4. **إعادة توليد `AppDbContextModelSnapshot`** بأداة EF لا يدويًّا، ثمّ إثبات `has-pending-model-changes` = «No changes» — بلا إنشاء هجرة جديدة.
5. انحدار كامل على قاعدة معزولة نظيفة؛ المعيار: **صفر انحدار مرشَّح** وبقاء عيبَي الأساس المعروفَين فقط.
6. إثبات عدم انحدار CPW-R2 باختباراته الخاصّة: `ClientDocumentsTests` · `ClientDocumentVisibilityTests`.
7. Backup ثلاثيّ لـTEST ثمّ نشر موحَّد؛ الهجرة المعلَّقة الوحيدة `20260811142239` ⟹ 35 هجرة.
8. Catalog Bootstrap مرّتَين لإثبات الـIdempotency (المتوقّع: +38 قيمة لـProject 360 فوق 170 القائمة = 208).

---

## 10) ضوابط السلامة على TEST — مثبَتة الآن (تُفيد أيّ نشر مستقبليّ)

| الضابط | القيمة المقروءة | الحكم |
| --- | --- | --- |
| هويّة البيئة | `ASPNETCORE_ENVIRONMENT=Staging` | ✅ ليست إنتاجًا |
| سلسلة الاتّصال | `Database=reporting_test_uat` | ✅ TEST حصرًا، لا اتّصال بـRC/الإنتاج |
| البريد (القناة الجديدة) | `EmailNotifications__Mode=DryRun` | ✅ لا إرسال حقيقيّ |
| البريد (العلم القديم) | `Email__Enabled=false` | ✅ مكبح ثانٍ |
| المذكّرات/الجدولة | `Reminders__Enabled=false` | ✅ لا مهامّ خلفيّة |
| المساحة | `/dev/sda1` 96G مستخدَم 41G متاح **56G** | ✅ تكفي لنسخ احتياطيّ ثلاثيّ (القاعدة 12MB، النشر 108MB، الواجهة 1.5MB) |
| نسخ سابقة | `/root/db-backups/reporting_test_uat-precpwr2def01-20260810-163737.dump` موجودة | ✅ المسار والنمط معروفان |

**لم يُنفَّذ أيّ نسخ احتياطيّ في هذه الجولة** لأنّ §11 يربطه بقرار النشر، والقرار = توقّف.

---

## 11) الحكم

```
TEST Lineage:        BLOCKED  (تصنيف C + D)
سبب الحظر:           TEST على فرع CPW-R2 غير المدموج؛ المرشَّح لا يعرف مستندات العملاء
النشر على TEST:      NOT STARTED  (توقّف قبل النسخ الاحتياطيّ وقبل أيّ كتابة)
انحدار CPW-R2:       0  (لم يُلمَس شيء — تدقيق قراءة-فقط بالكامل)
الدمج على develop:   تمّ بنجاح ومستقلّ عن هذا الحظر
الإجراء المطلوب:     قرار مالك بين الخيارَين (أ) و(ب)
```
