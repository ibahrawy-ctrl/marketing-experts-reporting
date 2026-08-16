# RC — تقرير النسخ الاحتياطيّة وجاهزيّة الاستعادة

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE` · **المرحلة:** Q · **التاريخ:** 16 أغسطس 2026
**المبدأ الحاكم:** **وجود الملفّ ليس دليلًا.** لا تُعتمد نسخة إلّا بعد **استعادة فعليّة** ومقارنة بصمة.
**الحكم:** **`RC_BACKUP_RESTORE_READINESS = PASS`**

---

## 1) مجلّد النسخ

`/root/backups/20260816-rc-deploy` — أُنشِئ نظيفًا مباشرة قبل النشر (16 أغسطس 17:45 UTC).

| الملفّ | الحجم | المحتوى |
|---|---|---|
| `reporting_rc.dump` | 570,509 B | `pg_dump -Fc` — النسخة المعتمَدة للتراجع |
| `reporting_rc.sql` | 1,286,972 B | نصّ SQL كامل (قراءة بشريّة/استعادة انتقائيّة) |
| `reporting_rc.schema.sql` | 99,748 B | مخطَّط فقط (`-s`) للمقارنة السريعة |
| `migrations-before.tsv` | 1,415 B | **30 سطرًا** — سجلّ الهجرات قبل الجسر |
| `publish-before.tgz` | 46,926,230 B | **106 مدخلات** — خلفيّة `ce166662` |
| `frontend-dist-before.tgz` | 350,657 B | **9 مدخلات** — واجهة RC الحاليّة |
| `storage-before.tgz` | 217 B | **4 مدخلات** — التخزين (فارغ فعليًّا) |
| `rc.env.FULL` | 1,361 B | **`600 root:root`** — لم يُطبع محتواه قطّ |
| `rc.env.masked` | 1,303 B | نسخة مقنَّعة للمراجعة |
| `khubara-reporting-rc.service` | 536 B | تعريف الخدمة |
| `nginx-reporting-rc.conf` + 4 نسخ احتياطيّة سابقة | — | إعداد Nginx كاملًا |
| `deployed-sha-before.txt` | 41 B | `ce166662f46598ed3593beed0105ba67059fc3bc` |
| `table-counts-before.txt` | 74 B | الأعداد المرجعيّة |
| `CHECKSUMS.sha256` | 518 B | بصمات الملفّات الستّة الكبرى |

## 2) الأعداد المرجعيّة قبل النشر

```
AspNetUsers          36
clients               8
projects             32
report_submissions   39
email_notifications 117
email_outbox          0
Migrations           30
```

## 3) فحوص السلامة (لا استعادة بعد)

| الفحص | النتيجة |
|---|---|
| `sha256sum -c CHECKSUMS.sha256` | **6/6 OK** |
| `pg_restore --list reporting_rc.dump` | **394 كائنًا · 57 `TABLE DATA`** |
| `tar tzf` للأرشيفات الثلاثة | تُقرأ بلا خطأ · 106 / 9 / 4 مدخلات |
| لا أرشيف فارغ | ✅ (أصغرها 217 B لتخزين فارغ أصلًا) |
| مساحة القرص | 54G متاح من 96G (45% مستخدَم) |

## 4) **إثبات الاستعادة الفعليّ** — الفحص الحاسم

```
createdb reporting_rc_restorecheck
pg_restore --no-owner --no-privileges -d reporting_rc_restorecheck < reporting_rc.dump
⟹ exit code = 0 · صفر خطأ
```

| المقارنة | RC الحيّة | القاعدة المستعادة | النتيجة |
|---|---|---|---|
| **بصمة المخطَّط المعياريّة** | `e137d40dcd1ad8d088fa6c4ad9a8eebb` | `e137d40dcd1ad8d088fa6c4ad9a8eebb` | **مطابقة** |
| الجداول / الأعمدة | 57 / 637 | 57 / 637 | مطابقة |
| الهجرات | 30 | 30 | مطابقة |
| `AspNetUsers` | 36 | 36 | مطابقة |
| `clients` | 8 | 8 | مطابقة |
| `projects` | 32 | 32 | مطابقة |
| `report_submissions` | 39 | 39 | مطابقة |
| `email_notifications` | 117 | 117 | مطابقة |

ثمّ **حُذِفت القاعدة المؤقّتة** (`dropdb`) وتُحقّق من زوالها: `count = 0`.
لم تُمَسّ `reporting_rc` ولا `reporting_prod` في هذا الفحص إطلاقًا.

## 5) اكتشاف جانبيّ ذو قيمة عالية

البصمة المعياريّة لـ**`reporting_prod`** = **`e137d40dcd1ad8d088fa6c4ad9a8eebb`** — **مطابقة حرفيًّا لـRC**.
⟹ ادّعاء «RC مرآة حرفيّة للإنتاج» **مُثبَت رقميًّا على مستوى المخطَّط الكامل** (أعمدة + قيود + فهارس)،
لا مستنتَجًا من تطابق عدد الهجرات وحده. وهذا يجعل نجاح الهجرة على RC **مؤشّرًا صالحًا** لسلوكها على الإنتاج.

بصمة **TEST** بعد `4fddc20` = **`3b3eb6b04fc0e6b1898468bd2cfed546`** ⟹ هذه هي **القيمة الهدف** التي يجب أن
تبلغها `reporting_rc` بعد المرحلة S، وأيّ قيمة أخرى = فشل بوّابة.

## 6) كرّاس التراجع (Restore Manifest) — أوامر حرفيّة

**سيناريو 1 — فشل الهجرة أو تلف المخطَّط**
```bash
systemctl stop khubara-reporting-rc
BK=/root/backups/20260816-rc-deploy
sudo -u postgres dropdb --if-exists reporting_rc_broken
sudo -u postgres psql -c 'ALTER DATABASE reporting_rc RENAME TO reporting_rc_broken;'
sudo -u postgres createdb -O reporting_rc_app reporting_rc
cat "$BK/reporting_rc.dump" | sudo -u postgres pg_restore --no-owner --no-privileges -d reporting_rc
sudo -u postgres psql -d reporting_rc -At -f /path/to/fingerprint.sql | md5sum   # يجب = e137d40dcd1ad8d088fa6c4ad9a8eebb
```

**سيناريو 2 — فشل التطبيق مع سلامة القاعدة**
```bash
systemctl stop khubara-reporting-rc
BK=/root/backups/20260816-rc-deploy
rm -rf /opt/reporting-rc/publish && tar xzf "$BK/publish-before.tgz" -C /opt/reporting-rc
rm -rf /opt/reporting-rc/frontend/dist && tar xzf "$BK/frontend-dist-before.tgz" -C /opt/reporting-rc/frontend
chown -R www-data:www-data /opt/reporting-rc
systemctl start khubara-reporting-rc && curl -s localhost:5092/health
```

**سيناريو 3 — تراجع الجسر وحده (قبل تطبيق الهجرات)**
```bash
sudo -u postgres psql -d reporting_rc -f /root/bridge-rollback-<timestamp>.sql   # يولّده الجسر نفسه
```

**سيناريو 4 — استعادة الإعداد**
```bash
cp /root/backups/20260816-rc-deploy/rc.env.FULL /etc/khubara-reporting-rc.env
chmod 600 /etc/khubara-reporting-rc.env && chown root:root /etc/khubara-reporting-rc.env
cp /root/backups/20260816-rc-deploy/khubara-reporting-rc.service /etc/systemd/system/ && systemctl daemon-reload
```

**زمن التراجع المقدَّر:** أقلّ من دقيقتين (الاستعادة الفعليّة أعلاه استغرقت ثوانٍ على قاعدة 14MB).

## 7) الكتلة النهائيّة

```
Backup Set Path            = /root/backups/20260816-rc-deploy
Checksum Verification      = 6/6 OK
Archive Readability        = 3/3 OK (106 / 9 / 4 entries)
Dump Object Count          = 394 (57 TABLE DATA)
Actual Restore Test        = PASS (exit 0, scratch DB, then dropped)
Restored Fingerprint Match = YES (e137d40dcd1ad8d088fa6c4ad9a8eebb)
Restored Row Counts Match  = YES (5/5 tables)
RC Fingerprint == PROD     = YES
TEST Target Fingerprint    = 3b3eb6b04fc0e6b1898468bd2cfed546
Rollback Runbook           = 4 scenarios, literal commands
Disk Headroom              = 54G free
RC Modified                = NO
Production Touched         = NO
RC Backup Restore Readiness = PASS
```
