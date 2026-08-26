# تقرير توافق البيانات والهجرة — `PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2`

| | |
|---|---|
| البيئة الوحيدة الممسوسة | **TEST** — `test.emarketingacademy.net` |
| RC | **لم يُمسّ** |
| الإنتاج | **لم يُمسّ** |
| مرشّح الواجهة | `706c8fe` |
| مرشّح الخلفيّة المنشور | `f8c4ad2` (شجرة `reporting-backend` متطابقة بايتًا مع `706c8fe`: `987286b7de5b35f5eedbcf9f99e98d1539985473`) |
| تاريخ التنفيذ | 26 أغسطس 2026 |

---

## 1) الخلاصة التنفيذيّة

توسعة **إضافيّة بحتة** لمحرّك القوالب: `schemaVersion: 2` يضيف مجموعة `workItems` **اختياريّة** داخل كلّ عنصر مشروع.

- **صفر هجرة بيانات (Backfill).** لم تُكتَب ولا تُقرأ عبارة `UPDATE`/`INSERT`/`DELETE` واحدة على بيانات قائمة.
- **الهجرة الوحيدة المضافة `20260826185232_AddSubmissionFieldValueJsonGinIndex` هي فهرس فقط**، بلا أيّ DML.
- **صفر تغيير في لقطة القالب** لأيّ تقرير `Submitted`/`Approved`/`Closed`.
- التقارير القديمة (v1) تُقرأ عبر **مُهايئ قراءة** يعرضها بطاقة مشروع واحدة ببند عمل واحد، **بلا كتابة أيّ مفتاح جديد** في المخزَّن.

---

## 2) الهجرة المضافة — محتواها الحرفيّ الكامل

`reporting-backend/src/Reporting.Infrastructure/Persistence/Migrations/20260826185232_AddSubmissionFieldValueJsonGinIndex.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateIndex(
        name: "ix_submission_field_values_value_json_gin",
        table: "submission_field_values",
        column: "ValueJson")
        .Annotation("Npgsql:IndexMethod", "gin")
        .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex(
        name: "ix_submission_field_values_value_json_gin",
        table: "submission_field_values");
}
```

**الحكم المقيس:** `Up` سطر واحد منطقيّ = إنشاء فهرس. `Down` = إسقاطه. **لا `Sql(...)` ولا `UpdateData` ولا `InsertData` ولا `DeleteData` ولا `AlterColumn` ولا `DropColumn`.** التراجع كامل وغير مُتلِف.

### سبب الفهرس (لا زينة)

كشف الاكتشاف المتداخل (§8) يبحث عن `ProjectId` **داخل** `ValueJson` عبر `EF.Functions.JsonContains`. بلا فهرس GIN يصير المسار `Seq Scan` على `submission_field_values` لكلّ استعلام قائمة تقارير مشروع. `jsonb_path_ops` هو المُشغِّل المناسب لأنّ الاستعلام **احتواء فقط** (`@>`)، وهو أصغر وأسرع من `jsonb_ops` العامّ.

**التحقّق الحيّ على TEST:**

```
ix_submission_field_values_value_json_gin
CREATE INDEX ix_submission_field_values_value_json_gin
  ON public.submission_field_values USING gin ("ValueJson" jsonb_path_ops)
```

عدد الهجرات المطبَّقة بعد النشر: **46** (آخرها هذه الهجرة).

---

## 3) عقد المخطَّط — v1 مقابل v2

### 3.1 القالب (`TemplateField.ConfigJson`)

**v1 (كما هو، بلا مسّ):**

```json
{
  "projectRequired": true,
  "minProjects": 1,
  "maxProjects": 5,
  "fields": [ { "key": "work_type", "label": "نوع العمل", "type": "Text", "required": true } ]
}
```

**v2 (إضافة اختياريّة بحتة):**

```json
{
  "schemaVersion": 2,
  "projectRequired": true,
  "minProjects": 1,
  "maxProjects": 0,
  "fields": [ { "key": "project_goal", "label": "هدف المشروع", "type": "Select", "…": "…" } ],
  "workItems": {
    "key": "work_items",
    "label": "بنود العمل",
    "itemLabel": "بند عمل",
    "addLabel": "+ إضافة بند عمل",
    "minItems": 1,
    "maxItems": 0,
    "uniqueBy": [],
    "fields": [ { "key": "content_type", "label": "نوع المحتوى", "type": "Select", "…": "…" } ]
  }
}
```

**قواعد التوافق المفروضة في الكود:**

| الحالة | السلوك المقيس | الاختبار |
|---|---|---|
| `schemaVersion` غائب | يُقرَأ v1 — `workItems` تبقى `undefined` | اختبار الواجهة 2 |
| `workItems` موجودة بلا `fields` | تُهمَل كليًّا (لا بطاقة خاوية) | اختبار الواجهة 3 |
| `uniqueBy: []` | **تكرار نوع العمل مسموح** (المطلوب في §2) | مصفوفة API + UAT |
| `uniqueBy` غير فارغة | القالب وحده يقرّر المنع — **لا تثبيت في الكود** | ADR §6 |
| تفرّد المشروع | **يبقى مفروضًا دائمًا** بلا استثناء | اختبارا الواجهة 9/10 + UAT C13 |

### 3.2 القيم (`SubmissionFieldValue.ValueJson`)

**v1 المخزَّن (يبقى حرفيًّا كما هو):**

```json
[ { "projectId": "…", "answers": { "work_type": "مقال" } } ]
```

**v2 المخزَّن:**

```json
[ { "projectId": "…", "answers": { … }, "workItems": [ { "answers": { … } }, { "answers": { … } } ] } ]
```

**قاعدة عدم الكتابة (مفروضة باختبار انحدار):** عنصر v1 لا يكتسب مفتاح `workItems` عند القراءة ولا عند إعادة الحفظ. الاختبار 5 يفرض حرفيًّا `expect(JSON.stringify(entries[0])).not.toContain('workItems')`.

---

## 4) مُهايئ القراءة — كيف يُعرَض القديم

| مصدر البيانات | ما يراه المستخدم | ما يُكتَب في القاعدة |
|---|---|---|
| تقرير v1 على قالب v1 | بطاقة مشروع واحدة، حقول المشروع كما كانت، **بلا قسم بنود عمل إطلاقًا** | لا شيء |
| تقرير v1 على قالب v2 | بطاقة مشروع واحدة + بند عمل واحد مشتقّ عرضًا من إجابات المشروع | لا شيء |
| تقرير v2 على قالب v2 | بطاقة مشروع + كلّ بنود عملها | لا شيء عند القراءة |

المُهايئ **عرضيّ بحت**: لا `UPDATE`، ولا ترقية صامتة، ولا تعديل لقطة قالب.

### حماية التقارير غير المسوّدة

`Submitted` / `Approved` / `Closed` **لا تُفتَح للتحرير ولا تُعاد كتابتها**. لقطة القالب المرتبطة بها ثابتة، فتُعرَض دائمًا بالبنية التي اعتُمِدت بها. لا يوجد مسار كود واحد يبدّل `TemplateVersionId` لتقرير قائم.

---

## 5) عيب مكتشَف في UAT وعلاجه — قيمة إجابة غير نصّيّة

**الاكتشاف:** أثناء Browser UAT على TEST انهارت صفحة التسليمات كاملة بـ`t.trim is not a function` (شاشة ميّتة تخالف §12).

**السبب الجذريّ المقيس:** الخادم يقبل ويخزّن في `answers` قيمة JSON عدديّة أو منطقيّة (حقل `Number` يُخزَّن رقمًا لا نصًّا — بيانات التجربة تحوي `"count": 3`). المحرّر في المقابل يعامل كلّ إجابة كنصّ ويستدعي `trim()` عليها في `SubmissionsPage.tsx` (المسار الرقميّ `validateRepeatableNumber` ومسار `risk_exists`/`risk_note`). قيمة رقميّة واحدة كانت تُسقِط الصفحة كلّها.

**العلاج (`706c8fe`) — عند القراءة وحدها:**

```ts
export function coerceAnswers(a: unknown): Record<string, string> {
  if (!a || typeof a !== 'object') return {};
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(a as Record<string, unknown>)) {
    out[k] = v === null || v === undefined ? '' : typeof v === 'string' ? v : String(v);
  }
  return out;
}
```

تُستدعى في `parseRepeatableEntries` على مستوى المشروع وعلى مستوى بند العمل.

**أثره على البيانات: صفر.** التوحيد يقع في ذاكرة المتصفّح عند التفكيك فقط؛ لا يتغيّر بايت مخزَّن ولا عقد خادم ولا لقطة قالب. القيم الغائبة تصير نصًّا فارغًا فتخضع لقاعدة المطلوبيّة كما كانت.

**اختبارا انحدار مضافان:** `5أ` (تحويل رقم/منطقيّ/غائب عند التفكيك) و`13أ` (فتح مسودّة بقيمة رقميّة على حقل رقميّ مقيَّد بلا انهيار وبلا تنبيه كاذب).

---

## 6) النسخ الاحتياطيّة وإثبات الاسترجاع

| العنصر | الموضع | الإثبات |
|---|---|---|
| قاعدة بيانات TEST | `/root/db-backups/reporting_test_uat-<TS>.dump` (`pg_dump -Fc`) | استُرجِعت إلى قاعدة مؤقّتة ثمّ أُسقِطت — الاسترجاع مُتحقَّق منه لا مفترَض |
| `publish` الخلفيّة | `/root/publish-backups/` | نسخة النشر الحيّة قبل الاستبدال |
| `dist` الواجهة | `/root/frontend-backups/reporting-test-frontend-20260826T204518Z.tgz` | 412,074 بايت · md5 `0bab00bd195b3da55c32ce38926a9746` |

**بصمة الواجهة قبل النشر:** `assets/index-DKY0pOWA.js` (md5 `77b1ab6e…`) · `index.html` md5 `4f950ba6…`
**بصمة الواجهة بعد النشر:** `assets/index-BSGCZnf1.js` (md5 `bac58809…`) · `index.html` md5 `6d1dbcb1…`
**التطابق الثلاثيّ:** بناء محلّيّ = منشور على TEST = مُقدَّم للمتصفّح في UAT — مجموع `dcc84621939c1f2f1cfabf492c02f494` (7 ملفّات).

---

## 7) عزل البيئة المُتحقَّق منه قبل النشر

```
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=reporting_test_uat;Username=reporting_test_uat_app;Password=***
EmailNotifications__Mode=DryRun
```

قاعدة `reporting_test_uat` **ليست** قاعدة الإنتاج ولا قاعدة RC. البريد في `DryRun` ⇒ **لا رسالة واحدة تُرسَل** إلى أيّ مستقبِل حقيقيّ أثناء UAT.

---

## 8) الحكم

| البند | النتيجة |
|---|---|
| `DATA_MUTATED` | **NO** |
| `BACKFILL_EXECUTED` | **NO** |
| `TEMPLATE_SNAPSHOT_CHANGED` | **NO** |
| `SUBMITTED_APPROVED_CLOSED_TOUCHED` | **NO** |
| `MIGRATION_IS_ADDITIVE_ONLY` | **YES** (فهرس فقط) |
| `MIGRATION_REVERSIBLE` | **YES** (`Down` = `DropIndex`) |
| `LEGACY_READS_UNCHANGED` | **YES** |
| `ROLLBACK_AVAILABLE` | **YES** (نسخ ثلاثيّة + إسقاط الفهرس) |
