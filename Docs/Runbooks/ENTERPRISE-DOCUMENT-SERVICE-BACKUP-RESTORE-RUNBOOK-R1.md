# ENTERPRISE DOCUMENT SERVICE — BACKUP & RESTORE RUNBOOK — R1

**النطاق:** خدمة المستندات المؤسّسيّة (CPW-R1B2) — الجداول `client_documents` و`client_document_versions` و`client_external_links`، وشجرة التخزين المحلّيّة تحت `FileStorage__DocumentsRootPath`.

**سبب الوجود (C-03):** النسخ الاحتياطيّ القائم للنظام يغطّي **قاعدة البيانات فقط**. ملفّات المستندات تعيش على القرص خارج القاعدة وخارج `wwwroot`، فلا يشملها `pg_dump` إطلاقًا. لذلك أيّ نسخة احتياطيّة لا تجمع **القاعدة + شجرة الملفّات + بيان التخزين (Manifest)** في **نفس النافذة الزمنيّة** تُعدّ نسخة ناقصة، واستعادتها تنتج نظامًا غير متّسق.

> **تحذير تنفيذيّ:** هذا المستند إجرائيّ فقط. لا يُنفَّذ أيّ أمر هنا على بيئة حيّة (TEST/RC/Production) إلّا بتصريح نشر مستقلّ. توثيق الإجراء **لا يعني** الإذن بتشغيله.

---

## 0. الثوابت والمعرّفات

| البند | القيمة |
|---|---|
| قاعدة البيانات | `reporting_prod` (أو نظيرتها في البيئة المستهدفة) |
| جذر التخزين | قيمة `FileStorage__DocumentsRootPath` من `/etc/reporting-api.env` |
| بنية المفتاح | `{resourceKind}/{resourceId:D}/{documentId:D}/{versionId:D}{ext}` |
| نوع المورد في R1B2 | `client` حصرًا (`ResourceType`) |
| معرّف المورد | `client_documents."ClientId"` (`ResourceId`) |
| الخدمة | `reporting-api` (systemd) |
| مالك الملفّات | `www-data:www-data` — الملفّات `640`، المجلّدات `750` |

استخراج الجذر بلا تحميل الملفّ كاملًا (قيم عربيّة تكسر الـshell):

```bash
STORAGE_ROOT=$(grep '^FileStorage__DocumentsRootPath=' /etc/reporting-api.env | cut -d= -f2-)
echo "$STORAGE_ROOT"
```

---

## 1. النسخ الاحتياطيّ (Backup)

### 1.1 المبدأ الحاكم — نافذة واحدة متّسقة

الترتيب الإلزاميّ: **(1) تجميد الكتابة اختياريًّا ⟵ (2) `pg_dump` ⟵ (3) بيان التخزين ⟵ (4) أرشفة شجرة الملفّات ⟵ (5) بصمات + هجرات + إعداد.**

سبب الترتيب: `pg_dump` أوّلًا يضمن ألّا يوجد **صفّ في النسخة بلا ملفّ** إذا رُفع ملفّ جديد أثناء النافذة؛ الملفّ الزائد بلا صفّ (يتيم) عَرَض قابل للكشف والتنظيف، أمّا الصفّ بلا ملفّ فهو فقد بيانات. القبول بالانحياز إلى «ملفّ زائد» مقصود.

للنسخ اليقينيّة تمامًا (قبل ترحيل/تراجُع كبير) أوقِف الخدمة أثناء النافذة:

```bash
systemctl stop reporting-api      # اختياريّ — للنسخة اليقينيّة فقط
```

### 1.2 متغيّرات النافذة

```bash
TS=$(date -u +%Y%m%d-%H%M%S)
OUT=/root/db-backups/docsvc-$TS
mkdir -p "$OUT"
```

### 1.3 قاعدة البيانات

```bash
su postgres -c "pg_dump -Fc reporting_prod" > "$OUT/reporting_prod-$TS.dump"
```

### 1.4 بيان التخزين (Storage Manifest) — إلزاميّ

الحقول الدنيا المطلوبة لكلّ نسخة ملفّ:

| الحقل | المصدر |
|---|---|
| `DocumentVersionId` | `client_document_versions."Id"` |
| `DocumentId` | `client_document_versions."ClientDocumentId"` |
| `ResourceType` | ثابت `client` في R1B2 |
| `ResourceId` | `client_documents."ClientId"` |
| `StorageKeyHash` | `sha256(StorageKey)` — **لا تُكتب القيمة الخام** |
| `SizeBytes` | `client_document_versions."SizeBytes"` |
| `Sha256` | `client_document_versions."Sha256"` |
| `UploadedAtUtc` | `client_document_versions."CreatedAtUtc"` |

**لماذا تجزئة المفتاح لا قيمته الخام:** `StorageKey` لا يظهر في أيّ استجابة API ولا في التدقيق (ثابت أمنيّ في R1B2). كتابته خامًا في ملفّ بيان يُنسَخ وينتقل بين الأجهزة تنقض هذا الثابت. التجزئة كافية تمامًا للمطابقة لأنّ المفتاح **مشتقّ حتميًّا** من المعرّفات: يُعاد بناؤه محلّيًّا وقت الاستعادة من `ResourceType/ResourceId/DocumentId/DocumentVersionId + الامتداد` ثمّ تُجزَّأ النتيجة وتُقارَن.

```bash
su postgres -c "psql -d reporting_prod -At -F',' -c \"
SELECT v.\\\"Id\\\",
       v.\\\"ClientDocumentId\\\",
       'client',
       d.\\\"ClientId\\\",
       encode(digest(v.\\\"StorageKey\\\",'sha256'),'hex'),
       v.\\\"SizeBytes\\\",
       v.\\\"Sha256\\\",
       to_char(v.\\\"CreatedAtUtc\\\" AT TIME ZONE 'UTC','YYYY-MM-DD\\\"T\\\"HH24:MI:SSZ')
FROM client_document_versions v
JOIN client_documents d ON d.\\\"Id\\\" = v.\\\"ClientDocumentId\\\"
ORDER BY v.\\\"Id\\\";\"" > "$OUT/storage-manifest-$TS.csv"
```

> إن لم تكن إضافة `pgcrypto` مثبَّتة فدالّة `digest` غير متاحة. **لا تُثبَّت إضافة على الإنتاج من أجل نسخة احتياطيّة.** البديل: أخرِج `StorageKey` إلى أنبوب واحسب التجزئة خارج القاعدة (`sha256sum` لكلّ سطر) بحيث لا يُكتَب المفتاح الخام على القرص إطلاقًا.

سطر رأس البيان (يُكتب قبل التصدير):

```
DocumentVersionId,DocumentId,ResourceType,ResourceId,StorageKeyHash,SizeBytes,Sha256,UploadedAtUtc
```

### 1.5 أرشفة شجرة التخزين

```bash
tar -czf "$OUT/documents-tree-$TS.tar.gz" -C "$(dirname "$STORAGE_ROOT")" "$(basename "$STORAGE_ROOT")"
```

`-C` + الاسم النسبيّ ضروريّان كي لا يحمل الأرشيف مسارات مطلقة تُفشِل الاستعادة إلى جذر مختلف.

### 1.6 البصمات وسجلّ الهجرات وإعداد التشغيل

```bash
cd "$OUT" && sha256sum ./* > SHA256SUMS

su postgres -c "psql -d reporting_prod -At -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";'" > "$OUT/migrations-$TS.txt"

md5sum /opt/reporting/publish/appsettings.json > "$OUT/runtime-hashes-$TS.txt"
stat -c '%Y %n' /etc/reporting-api.env >> "$OUT/runtime-hashes-$TS.txt"
grep -E '^FileStorage__' /etc/reporting-api.env | sed 's/=.*/=<redacted>/' >> "$OUT/runtime-hashes-$TS.txt"
```

**لا تُنسَخ `/etc/reporting-api.env` نفسه إلى مجلّد النسخ** — يحوي أسرارًا. تُسجَّل مفاتيحه بلا قيم + طابعه الزمنيّ فقط.

```bash
systemctl start reporting-api     # إن كانت أوقِفت في 1.1
```

### 1.7 معيار قبول النسخة

النسخة صالحة فقط إذا: وُجدت الملفّات الخمسة (dump، manifest، tar، SHA256SUMS، migrations)، و`sha256sum -c SHA256SUMS` نظيف، وعدد أسطر البيان = `SELECT count(*) FROM client_document_versions`.

---

## 2. الاستعادة (Restore) — الترتيب الإلزاميّ

الترتيب غير قابل لإعادة الترتيب: كلّ خطوة تعتمد على سابقتها.

### 2.1 استعادة قاعدة البيانات

```bash
systemctl stop reporting-api
su postgres -c "pg_restore -d reporting_target --clean --if-exists /path/reporting_prod-<TS>.dump"
```

تحقّق فورًا أنّ رأس الهجرات يطابق `migrations-<TS>.txt`؛ أيّ فرق يعني أنّ الشيفرة المنشورة والقاعدة ليستا من نفس النافذة ⟹ **أوقِف الاستعادة**.

### 2.2 استعادة شجرة التخزين

```bash
mkdir -p "$STORAGE_ROOT"
tar -xzf /path/documents-tree-<TS>.tar.gz -C "$(dirname "$STORAGE_ROOT")"
```

### 2.3 الصلاحيّات والملكيّة

```bash
chown -R www-data:www-data "$STORAGE_ROOT"
find "$STORAGE_ROOT" -type d -exec chmod 750 {} \;
find "$STORAGE_ROOT" -type f -exec chmod 640 {} \;
```

جذر التخزين **يجب** أن يبقى خارج `wwwroot` وخارج أيّ `location` في nginx. تحقّق:

```bash
grep -rn "$STORAGE_ROOT" /etc/nginx/ || echo "OK — لا تقديم مباشر"
```

### 2.4 مطابقة البيان (Manifest Reconciliation)

لكلّ سطر في البيان: أعِد بناء المفتاح محلّيًّا ⟵ جزّئه ⟵ قارنه بـ`StorageKeyHash` ⟵ ثمّ تحقّق من وجود الملفّ وحجمه وبصمته.

```
المفتاح = client/{ResourceId}/{DocumentId}/{DocumentVersionId}{الامتداد}
```

الامتداد يُشتقّ من `OriginalFileName` في القاعدة المستعادة (المفتاح لا يحمل الاسم الأصليّ إطلاقًا — معرّفات فقط).

### 2.5 كشف الملفّات المفقودة (صفّ بلا ملفّ)

**التعريف:** يوجد صفّ في `client_document_versions` ولا يوجد ملفّ على القرص عند مفتاحه.

**الأثر:** الميتاداتا تظهر في الواجهة، والتنزيل يفشل بـ`FileNotFoundException` من `LocalFileStorage.OpenReadAsync`.

**الإجراء:** لا تُحذف الصفوف. سجّل القائمة، وحاول جلب الملفّات من نسخة أقدم بمطابقة `Sha256` (الملفّ ذاته قد يكون موجودًا تحت مفتاح نسخة أخرى إن كان مكرَّرًا). إن تعذّر، صعّد الأمر كفقد بيانات مؤكَّد ووثّقه — **لا تخفِ الصفّ ولا تصطنع ملفًّا بديلًا**.

### 2.6 كشف الملفّات اليتيمة (ملفّ بلا صفّ)

**التعريف:** ملفّ على القرص لا يقابله صفّ في القاعدة.

**السبب الطبيعيّ:** رفعٌ وقع بين لحظة `pg_dump` ولحظة `tar` (الانحياز المقصود في 1.1)، أو حذف نهائيّ للصفّ.

**الإجراء:** **لا يُحذف تلقائيًّا.** ينقل إلى `"$STORAGE_ROOT/../quarantine-<TS>/"` ويُراجَع يدويًّا. الحذف التلقائيّ خطر لأنّ يتيمًا اليوم قد يكون ملفًّا شرعيًّا لصفّ سيُستعاد من نسخة أحدث.

الملفّات `*.tmp` استثناء: بقايا كتابة ذرّيّة فاشلة، آمنة الحذف بعد التأكّد من قِدَم طابعها الزمنيّ.

### 2.7 التحقّق من البصمات

لكلّ ملفّ مستعاد: `sha256sum` الفعليّ = `Sha256` في القاعدة **و** في البيان. أيّ عدم تطابق = تلف صامت ⟹ الملفّ لا يُقدَّم للتنزيل ويُعلَّم للمراجعة.

---

## 3. التراجُع (Rollback)

سيناريو: استعادة/ترحيل أنتج حالة غير مقبولة.

1. `systemctl stop reporting-api`.
2. استعادة الـdump السابق للنافذة.
3. استعادة `documents-tree` **من نفس النافذة** — لا تخلط قاعدة نافذة مع ملفّات نافذة أخرى إطلاقًا.
4. عكس هجرة R1B2 عند اللزوم: `Down` = `DropForeignKey` + `DropTable ×3` (`client_external_links`, `client_documents`, `client_document_versions`). **هذا يمحو كلّ ميتاداتا المستندات** بينما تبقى الملفّات على القرص كأيتام. لا يُنفَّذ إلّا بقرار صريح مع نسخة احتياطيّة كاملة سابقة.
5. إعادة الصلاحيّات (2.3) ثمّ `systemctl start reporting-api`.
6. تحقّق: `/health` = 200، ورأس الهجرات = المتوقَّع، وعيّنة تنزيل تعمل.

---

## 4. التحقّق من الاستعادة (Restore Verification)

| الفحص | المعيار |
|---|---|
| صحّة الخدمة | `/health` = 200 داخليًّا وعبر الوكيل |
| رأس الهجرات | مطابق لـ`migrations-<TS>.txt` |
| عدد الصفوف | `client_documents` / `client_document_versions` / `client_external_links` = المتوقَّع |
| مفقود | 0 صفّ بلا ملفّ |
| يتيم | 0 خارج الحجر الصحّيّ |
| البصمات | 0 عدم تطابق |
| الصلاحيّات | `www-data` / 750 / 640 |
| عدم التقديم المباشر | لا `location` في nginx يشير إلى جذر التخزين |
| مسار حيّ | تنزيل نسخة واحدة عبر الـendpoint المصادَق ⟹ 200 وبصمة مطابقة |
| تسريب | `StorageKey` غير ظاهر في أيّ استجابة أو سجلّ تدقيق |

---

## 5. التشغيل الجافّ (Dry-Run)

**إلزاميّ قبل أوّل استعادة حقيقيّة، وبعد أيّ تغيير في بنية التخزين.**

1. قاعدة جديدة معزولة `reporting_restore_drill_<TS>` — الاسم لا يطابق أيّ بيئة قائمة.
2. جذر تخزين مؤقّت `/var/tmp/docsvc-drill-<TS>`.
3. نفّذ 2.1 ⟵ 2.7 كاملة على العزل.
4. شغّل §4 كلّه واحسب: المفقود، اليتيم، عدم تطابق البصمات، الزمن الكلّيّ.
5. احذف قاعدة التمرين وجذره بعد تسجيل النتائج.

**قواعد التمرين:** لا يلمس الإنتاج ولا قاعدته ولا جذر تخزينه؛ لا يُعاد تشغيل `reporting-api` الحيّة؛ لا تُطبَّق هجرة على بيئة حيّة.

---

## 6. القيود المعروفة (Known Limitations)

1. **لا فحص برمجيّات خبيثة (C-01):** المحرّك `None` والحالة `NotScanned`. الملفّات المستعادة **لم تُفحص قطّ** — لا يُدَّعى أنّها نظيفة.
2. **لا نسخ تزايديّ:** كلّ نسخة كاملة. مع نموّ الشجرة يطول زمن `tar` وتكبر النافذة.
3. **لا تزامن تلقائيّ بين القاعدة والشجرة:** الاتّساق مضمون بالترتيب اليدويّ في §1 فقط، ولا يوجد قفل عالميّ يمنع الرفع أثناء النافذة ما لم تُوقَف الخدمة.
4. **لا مراقبة دوريّة للأيتام/المفقود:** الكشف يقع وقت الاستعادة أو التمرين فقط؛ لا مهمّة خلفيّة تفحص التماسك في التشغيل العاديّ.
5. **`pgcrypto` قد لا تكون متاحة** ⟹ يُحسب تجزئة المفتاح خارج القاعدة (انظر 1.4).
