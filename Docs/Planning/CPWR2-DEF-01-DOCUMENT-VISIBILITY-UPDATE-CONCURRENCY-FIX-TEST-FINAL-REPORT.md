# CPWR2-DEF-01 — إصلاح تزامن تحديث رؤية المستند + تدوير كلمات مرور UAT + انحدار TEST حصرًا

**التاريخ:** 10 أغسطس 2026
**البيئة الوحيدة الممسوسة:** TEST — `khubara-reporting-test` / المنفذ 5091 / قاعدة `reporting_test_uat` / `Environment=Staging`
**القرار النهائيّ:** `TEST: PASS — CPWR2-DEF-01 FIXED / NO MIGRATION / PRODUCTION: NO-GO`

---

## 1. السبب الجذريّ (§1 — أُثبِت تجريبيًّا لا افتراضًا)

`PUT /api/clients/{clientId}/documents/{documentId}` كان يُرجِع **HTTP 500** كلّما تطلّب التغيير **إدراج** صفوف جديدة في `client_document_allowed_roles` أو `client_document_allowed_users`.

السلسلة السببيّة المُثبَتة بفحص `ChangeTracker` الحيّ:

1. `BaseEntity` يضبط `Id = Guid.NewGuid()` في المُنشئ ⇒ المفتاح الأساسيّ للكيان الابن **مضبوط** قبل التعقّب (`IsKeySet = true`).
2. تهيئة EF للمفتاح الأساسيّ للجدولين الابنين هي `ValueGenerated = OnAdd`.
3. الكود القديم كان يضيف الصفوف عبر **ملاحة الأصل** (`document.AllowedRoles.Add(...)`) بدل `DbSet` الصريح ⇒ يمرّ عبر `NavigationFixer` ⟶ `EntityGraphAttacher.AttachGraph(targetState: Added, storeGenTargetState: Modified)`.
4. لأنّ المفتاح مضبوط ومولَّد `OnAdd`، يختار EF الحالة **`Modified` لا `Added`**.
5. ⇒ يُصدَر `UPDATE` على مفتاح **غير موجود** في الجدول ⇒ **صفر صفوف متأثّرة** ⇒ `DbUpdateConcurrencyException` ⇒ 500.

**تصحيح مهمّ:** السبب ليس تعارض تزامن حقيقيًّا ولا قيدًا فريدًا ولا مشكلة سكيمة — بل حسم حالة الكيان في مُتعقِّب التغييرات. لذلك **لا هجرة مطلوبة**.

---

## 2. الإصلاح (§2 + §3 — الأضيق، داخل `ApplyVisibilityAsync` حصرًا)

ملفّ واحد: `reporting-backend/src/Reporting.Infrastructure/Services/ClientDocumentService.cs` — الدالّة `ApplyVisibilityAsync` فقط.

- **إدراج صريح عبر `DbSet`** (`_db.ClientDocumentAllowedRoles.Add(...)` / `_db.ClientDocumentAllowedUsers.Add(...)`) ⇒ يحسم الحالة `Added` قطعًا ويُصدِر `INSERT`.
- **مزامنة تفاضليّة** بدل `RemoveRange` + `Clear()`: تُحذف الصفوف الزائدة فقط وتُضاف الناقصة فقط ⇒ لا حذف/إعادة إدراج لصفوف لم تتغيّر ⇒ لا اصطدام بالفهرسين الفريدين `IX_client_document_allowed_roles_DocumentId_RoleName` و`IX_client_document_allowed_users_DocumentId_UserId`.
- **حارس ازدواج الملاحة**: `if (!document.AllowedRoles.Contains(added))` — لأنّ EF يُصلِح الملاحة تلقائيًّا عند `DbSet.Add` حين يكون الأصل متعقَّبًا، والإضافة اليدويّة بعدها كانت تُنتج عنصرًا مكرَّرًا في الـDTO (اكتُشف بستّة فشلات اختبار وأُصلح).
- **الذرّيّة (§3)**: كلّ العمليّات ضمن `SaveChangesAsync` واحد ⇒ `CustomUsers [A] → CustomUsers [B,C]` يُنتج حذف A وإدراج B وC معًا أو لا شيء؛ عند الفشل تبقى الحالة القديمة سليمة.

**Migration: NO.** `dotnet ef migrations has-pending-model-changes` ⟶ **No changes**.

### الملفّات المتغيّرة (سطح الباتش)

```
 M reporting-backend/src/Reporting.Infrastructure/Services/ClientDocumentService.cs        |  35 ++-
 M reporting-backend/tests/Reporting.IntegrationTests/ClientDocumentVisibilityTests.cs     | 317 +++++-
 2 files changed, 343 insertions(+), 9 deletions(-)
```

**صفر ملفّ واجهة. صفر هجرة. صفر تغيير عقد/سياسة/دور.**

---

## 3. الاختبارات (§4 + §5)

| البند | النتيجة |
|---|---|
| `dotnet build` (Release) | **0 أخطاء** (تحذير واحد قائم مسبقًا) |
| `Reporting.UnitTests` | **69 / 69** |
| `ClientDocumentVisibilityTests` (16 اختبارًا جديدًا `Def01_1..Def01_16` + 21 قائمًا) | **37 / 37** |
| `ClientDocumentsTests` + انحدار Client 360 | **120 / 120** |
| `has-pending-model-changes` | **No changes** |
| الواجهة: `tsc` | **0** |
| الواجهة: `ClientDetailPage.test.tsx` | **23 / 23** (1.61 ث) |

الستّة عشر اختبارًا تغطّي: `ClientScoped→CustomRoles`، `ClientScoped→CustomUsers`، التوسيع، التقليص، الاستبدال الكامل، العودة إلى `ClientScoped`، التكرار (idempotency)، الذرّيّة عند الفشل، الرفض `visibility_roles_required` / `visibility_users_required` / `visibility_role_invalid` / `visibility_user_invalid`، وعدم التسرّب إلى مستخدم خارجيّ.

**لم يُصلَح `BASELINE-DEFECT-01/02`** (خارج النطاق، كما تنصّ التذكرة).

---

## 4. أثر الواجهة (§6)

- `git status` يُثبِت **صفر ملفّ واجهة معدَّل** ⇒ الإصلاح خلفيّ بحت ⇒ **لا إعادة نشر للواجهة**.
- دخان نموذج تعديل المستند القائم بحمولة الواجهة الحرفيّة (`title, categoryCode, confidentialityCode, lifecycleStatus, description, tags, visibilityType, allowedRoles, allowedUserIds`) من `ClientDetailPage.tsx`:

```
UI-FORM CustomRoles  save=200 reload.vis=CustomRoles  reload.roles=["FinanceManager","TeamLeader"]
UI-FORM CustomUsers  save=200 reload.vis=CustomUsers  reload.users=["f18df329-…"]
UI-FORM ClientScoped save=200 reload.vis=ClientScoped roles=[] users=[]
UI_FORM_ROUNDTRIP=PASS
```

**عيب واجهة مستقلّ: لا يوجد.**

---

## 5. تدوير كلمات مرور UAT (§7 — TEST حصرًا)

عبر الـAPI الرسميّ `POST /api/directory/users/{id}/reset-password` — **صفر SQL على `PasswordHash`**، كلمات عشوائيّة قويّة (24 محرفًا) **لم تُطبَع إطلاقًا**.

```
ADMIN_LOGIN=OK   DIRECTORY_USERS=17
UAT_PW_CEO      USER_FOUND=YES OLD_BEFORE=200 RESET=200 OLD_AFTER=401 NEW_AFTER=200 VERDICT=PASS
UAT_PW_GM       USER_FOUND=YES OLD_BEFORE=200 RESET=200 OLD_AFTER=401 NEW_AFTER=200 VERDICT=PASS
UAT_PW_OPS_MGR  USER_FOUND=YES OLD_BEFORE=200 RESET=200 OLD_AFTER=401 NEW_AFTER=200 VERDICT=PASS
ROTATION=PASS
```

- التخزين: `/root/uat-prep-runtime/uat-role-accounts.env` (محدَّث في مكانه) + `/root/uat-prep-runtime/uat-role-accounts.def01-rotated-20260810T163657Z.env` — كلاهما `-rw------- root root` (**600**)، خارج Git.
- النسخة الاحتياطيّة المؤقّتة `.pre-def01-*.bak` **حُذِفت** بعد التحقّق لأنّها كانت تكرّر ثمانية أسرار حيّة.
- **صفر تدوير على Production أو RC.**

---

## 6. النشر على TEST (§8 + §9)

| البند | القيمة |
|---|---|
| النطاق | **Backend فقط** (لا واجهة، لا هجرة) |
| نَسَب النسخة المنشورة | `1.0.0+3344f7800f223a97b2fd4429d92d8c3449f3cfd9+cpwr2-def01` على **الأربع** DLLs |
| نسخة الكود الاحتياطيّة | `/opt/reporting-test/publish-backup-cpwr2def01-20260810-163737` (86 ملفًّا) |
| نسخة القاعدة | `/root/db-backups/reporting_test_uat-precpwr2def01-20260810-163737.dump` — 532,049 بايت، sha256 `3fdab6a08f5e8f0b4045209e84da53211143075fcc0b702bc51d05defafdd539` |
| نسخة الإعداد | `/root/db-backups/khubara-reporting-test.env.cpwr2def01-20260810-163737` (md5 `4742656a61a42d9384189f00e83bdc26`، بلا تغيير بعد النشر) |
| مخزون التخزين | `/root/db-backups/storage-manifest-cpwr2def01-20260810-163737.txt` |
| إعادة التشغيل | **واحدة فقط** لخدمة `khubara-reporting-test` — 16:38:25 ⟶ 16:38:34 UTC، MainPID 799258 ⟶ **812859**، `NRestarts=0` |
| الهجرات | «No migrations were applied» — العدد **34**، الرأس `20260809165617_ClientDocumentVisibility` (بلا تغيير) |
| الصحّة | `HEALTH=200` |

**صفر مساس بـ Production أو RC.**

---

## 7. انحدار TEST الحيّ (§10)

**`TOTAL=37 PASS=37 FAIL=0 → VERDICT=PASS`**

الحالات الحاسمة (كانت 500 وصارت 200):

| المسبار | قبل | بعد |
|---|---|---|
| P3 — `PUT` نفس المستند إلى `CustomUsers` | 500 | **200** |
| P5 — `PUT` إلى `CustomRoles` | 500 | **200** |
| P5 — ثمّ العودة إلى `ClientScoped` | — | **200**، `roles=[]` |
| P6 — `PUT` مستند `ClientScoped` ⟶ `CustomRoles` (إضافة فقط) | 500 | **200** |

- **الذرّيّة**: استبدال `CustomUsers [AM] → [FIN]` ⇒ العدد النهائيّ **1** (AM أُزيل، FIN أُدرج) في عمليّة واحدة.
- **الفرض الخادميّ**: `GET` المستند `CustomUsers` بحساب مدير العميل ⇒ **404 `client_document.not_found`** (مضادّ التعداد، لا 403). التنزيلات 404/200 حسب السياسة. فلترة القوائم مطابقة.
- **مصفوفة العقود**: `ManagementAndFinance` (المالية ترى / مدير العميل يُمنَع)، `TechnicalProposal` (مدير العميل يرى)، `MarketingPlan` (مدير العميل يرى) — كلّها مطابقة.
- **الرفض الصحيح**: `visibility_roles_required` / `visibility_users_required` / `visibility_role_invalid` ⇒ **400** والمستند يبقى `ClientScoped` (لا كتابة جزئيّة).
- الحسابات المدوَّرة CEO/GM/Ops سجّلت الدخول **200** ⇒ تأكيد ثانٍ لـ§7.

---

## 8. تنظيف بيانات QA (§11)

القصّ محصور بمفاتيح `CPWR2-DEF01-QA` حصرًا، داخل معاملة واحدة بحارسين.

**الحارس (1) قبل الحذف:** المستهدَف = **6 بالضبط** و**لا يحوي** المستند الحقيقيّ `7a31b96d-…`.
**الحارس (2) بعد الحذف:** صفر بقايا QA، والمستند الحقيقيّ ونسخته الوحيدة **قائمان**.

| العدّاد | قبل | بعد |
|---|---|---|
| `client_documents` (إجماليّ) | 7 | **1** |
| منها QA | 6 | **0** |
| منها حقيقيّ | 1 | **1** |
| `client_document_versions` | 7 | **1** |
| `client_document_allowed_roles` | 0 | **0** |
| `client_document_allowed_users` | 1 | **0** |

- **ملاحظة تقنيّة**: المحاولة الأولى فشلت على `FK_client_documents_client_document_versions_CurrentVersionId` (مرجع دائريّ)؛ المعاملة **تراجعت كاملةً** (تُحقِّق: 6 مستندات و7 نسخ سليمة بعدها)، ثمّ أُضيفت خطوة `UPDATE … SET "CurrentVersionId" = NULL` للمستهدَفات فقط ونجحت.
- **التخزين**: حُذِفت مجلّدات المستندات الستّة بمعرّفاتها الحرفيّة فقط. الملفّات: **7 ⟶ 1**.
- **المستند الحقيقيّ الوحيد «العقد» لم يُمَسّ** (`ClientScoped`، `UpdatedAtUtc` ما زال `NULL` ⇒ لم يُكتَب عليه إطلاقًا).
- **تسوية التخزين**: `db_rows=1 disk_files=1` ⟶ **`RECONCILIATION=PASS`**، صفر يتيم وصفر مفقود.
- سكربتات QA المؤقّتة أُزيلت من الخادم (ملفّات الأسرار الدائمة `600` بقيت كما هي).

---

## 9. عزل Production و RC (Zero Delta)

| البيئة | الهجرات | الرأس | `client_documents` | MainPID | NRestarts | الحالة |
|---|---|---|---|---|---|---|
| **Production** `reporting-api` / `reporting_prod` | **30** | `20260724224053_AddReportApproverAndKpiReviewerOverrides` | **غير موجود** | 654185 | **0** | active |
| **RC** `khubara-reporting-rc` / `reporting_rc` | **30** | `20260724224053_AddReportApproverAndKpiReviewerOverrides` | **غير موجود** | 647747 | **0** | active |
| **TEST** `khubara-reporting-test` / `reporting_test_uat` | **34** | `20260809165617_ClientDocumentVisibility` | موجود | 812859 | **0** | active |

**لم تُنشر أيّ بايتة على Production أو RC، ولم تُعَد تشغيل أيّ خدمة منهما، ولم تُنفَّذ أيّ هجرة عليهما.**

## 10. أمان البريد على TEST

مكبحان مستقلّان قائمان في `/etc/khubara-reporting-test.env` (`Email__Enabled=false` **و** `EmailNotifications__Mode=DryRun` — تطابقان 2/2)، والعدّادات:
`email_outbox = 0` / `email_notifications = 0` / `notifications = 0` — **بلا تغيير قبل وبعد**.

---

## 11. قائمة GO / NO-GO

| البند | القرار |
|---|---|
| CPWR2-DEF-01 مُصلَح | **GO** |
| تحديث `CustomRoles` | **GO** |
| تحديث `CustomUsers` | **GO** |
| الذرّيّة (Atomicity) | **GO** |
| الصلاحيّات الخادميّة (فرض خادميّ + 404 مضادّ التعداد) | **GO** |
| تدوير كلمات مرور UAT | **GO** |
| اتّساق التخزين | **GO** |
| صحّة بيئة TEST | **GO** |
| عزل Production / RC | **GO** |
| جاهزيّة UAT للمالك | **GO** |
| **الجاهزيّة للإنتاج** | **NO-GO** |

---

## 12. التوقّف المُلزَم

ممنوع بلا تصريح جديد: النشر على Production، النشر على RC، بدء Project Workspace، بدء CRM، بدء وحدة المالية، تنفيذ `EF Down`، أيّ Backfill، إصلاح عيوب خطّ الأساس، أيّ تنظيف عالميّ، أيّ تغيير في سلسلة النَسَب (lineage).

**تمّ التوقّف بعد هذا التقرير.**
