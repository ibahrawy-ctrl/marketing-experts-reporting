# RC — النسخ الاحتياطيّ الثلاثيّ وخطّة التراجع (R22A)

**وقت الأخذ:** `2026-08-30T15:37:39Z` · **قبل أيّ عمليّة كتابة على RC.**
**مجلّد النسخ:** `/opt/reporting-rc/backups/r22a-20260830T153739Z`

## 1) النسخ الثلاث — مقيسة ومُبصمَة

| # | النسخة | المسار | الحجم (بايت) | SHA-256 |
|---|---|---|---|---|
| 1 | الخلفيّة | `…/backend-publish.tar.gz` | 47,716,391 | `9e306b85c1661533595eedcce5ec77c027ca887033bcb10a5dd07109f2108eaa` |
| 2 | الواجهة | `…/frontend-dist.tar.gz` | 409,208 | `78549bb3f47f49b426caa8668cac6a2f5c6f13e336fe54ec3b25fec1418b9f31` |
| 3 | قاعدة RC كاملة | `…/reporting_rc.dump` | 526,976 | `b8d7ab81958e9712a64f97fce46f0cb751696da14b70219c8e6fc333b03a683a` |

نسخة القاعدة بصيغة `custom` (`pg_dump -Fc`) وصلاحيّتها `600` لمالكها `postgres`.

## 2) التحقّق الفعليّ من نسخة القاعدة

```
pg_restore -l /opt/reporting-rc/backups/r22a-20260830T153739Z/reporting_rc.dump   → EXIT=0
RESTORE_LIST_ENTRIES = 506
TABLE_DATA_ENTRIES   = 84      ← يطابق 84 جدولًا مقيسة في مخطّط RC تمامًا
INDEX_ENTRIES        = 199
```

`RC_BACKUP_GATE = PASS` · `RC_RESTORE_LIST_GATE = PASS`

## 3) خطّة التراجع — مرتّبة بالأقلّ أثرًا أوّلًا

1. **إعادة الخلفيّة:**
   `systemctl stop khubara-reporting-rc` ثمّ
   `rm -rf /opt/reporting-rc/publish && tar -C /opt/reporting-rc -xzf …/backend-publish.tar.gz`
   ثمّ `chown -R www-data:www-data /opt/reporting-rc/publish`.
2. **إعادة الواجهة:**
   `rm -rf /opt/reporting-rc/frontend/dist && tar -C /opt/reporting-rc/frontend -xzf …/frontend-dist.tar.gz`.
3. **إعادة تشغيل خدمة RC وحدها:** `systemctl restart khubara-reporting-rc` —
   **لا تُمسّ** `reporting-api` (الإنتاج) ولا `khubara-reporting-test`.
4. **التحقّق بالبصمات:** بصمة كلّ `*.dll` في `/opt/reporting-rc/publish` يجب أن تعود إلى
   `4cf78ca4d2cf1f48b5f48be0fb54492e22f8255f7c26f89fbf31b6d419c1c487`،
   و`InformationalVersion` إلى `1.0.0+897c9b187ab4216213b4f453ec65948cd06dff27`،
   و`/health` = 200.
5. **تراجع القالب — بالواجهة الرسميّة لا بـSQL:** يُعاد نشر الإصدار **v8**
   (`597e6895-304d-4370-b210-61062cd12f5e`) عبر واجهة إدارة القوالب.
   **لا يُحذف ولا يُعدَّل أيّ إصدار سابق**، والإصدار الجديد يبقى قائمًا غير منشور.
6. **إعادة القاعدة — الملاذ الأخير فقط:** لا تُنفَّذ إلّا عند **فساد بيانات مُثبَت**.
   `pg_restore` إلى `reporting_rc` حصرًا. ممنوع `DROP DATABASE`.

## 4) ضبط الآثار الخارجيّة على RC (مقيس من ملفّ البيئة)

| مفتاح | قيمة RC | الأثر |
|---|---|---|
| `EmailNotifications__Mode` | `DryRun` | لا بريد حقيقيّ يُرسَل ⟹ `RC_ACTUAL_EMAILS_SENT = 0` بنيويًّا |
| `Email__Enabled` | `false` | مكبح ثانٍ مستقلّ |
| `BackgroundJobs__Enabled` | `false` | لا مجدولات تعمل على RC |
| `Integrations__Enabled` | `false` | لا تكاملات خارجيّة |

`RC_EXTERNAL_EFFECTS_CONTROLLED = YES` · `RC_ROLLBACK_READY = YES`
