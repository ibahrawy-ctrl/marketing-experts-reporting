# تقرير أمنيّ — تدوير بيانات اعتماد قاعدة RC

**التاريخ:** 2026-08-18 · **النطاق:** تدوير كلمة سرّ دور PostgreSQL **`reporting_rc_app`** فقط.
**السبب:** ظهور القيمة السابقة في مخرجات جلسة عمل ⟹ تُعامَل كبيانات اعتماد مكشوفة.
**لا يحوي هذا التقرير أيّ قيمة سرّيّة — لا القديمة ولا الجديدة.**

---

## 1) الحالة قبل التدوير

| المتغيّر | القيمة |
|---|---|
| `RC_HEALTH_BEFORE` | `200` |
| `RC_PID_BEFORE` | `1142569` |
| `RC_RESTART_COUNT_BEFORE` | `0` (نشط منذ `2026-08-16 18:35:15 UTC`) |
| `RC_MIGRATION_COUNT_BEFORE` | `40` |
| `ENV_FILE_MODE_BEFORE` | `600 root:root` (1,428 بايت) |
| `DB_ROLE_NAME` | `reporting_rc_app` |
| `DB_NAME` | `reporting_rc` |
| بصمة الخلفيّة قبل | `be9c0fec20b9a1134c7c4b396369127255fd71e3efee167099c199c38e2ffc4b` (`1.0.0+4fddc20…`) |
| بصمة الواجهة قبل | `de3166e684e62397e2d6e27d972023b5366d41e102dffaf68e83f239c80bbb29` |

**سياسة تسجيل PostgreSQL المتحقَّق منها قبل التنفيذ:** `log_statement=none` · `log_min_duration_statement=-1` · `logging_collector=off` ⟹ عبارة `ALTER ROLE` **لا تُسجَّل**، وأُضيف `SET log_statement='none'` في الجلسة نفسها احتياطًا.

---

## 2) التنفيذ (بلا طباعة أيّ سرّ)

| الخطوة | التنفيذ | الإثبات |
|---|---|---|
| توليد السرّ | `openssl rand -base64 96` مُرشَّحًا إلى أبجديّ-رقميّ، في ملفّ مؤقّت داخل دليل `700` | `NEW_SECRET_LENGTH = 44` حرفًا (≥32) · `NEW_SECRET_CHARSET_OK = 1` (`^[A-Za-z0-9]{44}$` — يمنع كسر سلسلة الاتّصال بـ`;` أو `'`) |
| تجهيز ملفّ البيئة | نسخة جديدة بـ`sed` في المؤقّت (`600`) بلا عرض | `ENV_LINES` 34 ⟵ 34 · **سطر واحد فقط تغيّر** (`CHANGED_LINES=2` = قديم+جديد) |
| التحقّق البنيويّ بلا عرض | فحوص عدّ لا طباعة | `Database=reporting_rc;` = 1 · `Username=reporting_rc_app;` = 1 · `ASPNETCORE_URLS=http://127.0.0.1:5092` = 1 · `ASPNETCORE_ENVIRONMENT=ReleaseCandidate` = 1 · طول حقل `Password=` = 44 · تطابقه مع المُولَّد = 1 · إشارات إلى قواعد الإنتاج/TEST = **0** |
| تغيير سرّ الدور | `ALTER ROLE reporting_rc_app WITH PASSWORD …` عبر **stdin** (`psql -f -`) ⟹ لا يظهر في `ps` ولا في سجلّ القاعدة | `ALTER_ROLE_EXIT = 0` |
| استبدال ملفّ البيئة ذرّيًّا | `install -m 600 -o root -g root … && mv -f` (نفس نظام الملفّات ⟹ استبدال ذرّيّ) | `ENV_SWAP_EXIT = 0` · `ENV_FILE_MODE_AFTER = 600 root:root` (1,442 بايت) · لا ملفّات وسيطة متبقّية في `/etc` |
| إعادة التشغيل | **مرّة واحدة** `systemctl restart khubara-reporting-rc` | `RESTART_EXIT = 0` |

**عزل التغيير على مستوى القاعدة:** بصمات مُتحقِّقات كلمات السرّ (`md5(rolpassword)` — تجزئة فوق تجزئة، لا تكشف شيئًا) قبل/بعد:

| الدور | قبل | بعد | الحالة |
|---|---|---|---|
| `reporting_rc_app` | `79a6984b5071` | `f216a241ea0b` | **مُدوَّر (المستهدَف)** |
| `reporting_rc_owner` | `d41d8cd98f00` | `d41d8cd98f00` | بلا تغيير |
| `reporting_app` (الإنتاج) | `8a330a31d6b0` | `8a330a31d6b0` | بلا تغيير |
| `reporting_test_app` | `edab297f4967` | `edab297f4967` | بلا تغيير |
| `reporting_test_uat_app` | `061d401bd147` | `061d401bd147` | بلا تغيير |
| `LMS_EMA_user` | `c68c2d2e2bf1` | `c68c2d2e2bf1` | بلا تغيير |

⟹ `OTHER_DB_ROLE_CHANGED = NO` مُثبَت رقميًّا.

---

## 3) التحقّق بعد التدوير

```
RC_HEALTH_AFTER              = 200
RC_DATABASE_CONNECTION       = PASS   (اتّصال حيّ: reporting_rc | reporting_rc_app)
RC_DATABASE_NAME             = reporting_rc
RC_SERVICE_ACTIVE            = YES    (PID 1261073 · NRestarts 0 · إعادة تشغيل واحدة مقصودة)
RC_MIGRATION_COUNT_UNCHANGED = 40
RC_PRODUCT_SHA_UNCHANGED     = 4fddc20
RC_FRONTEND_HASH_UNCHANGED   = YES    (de3166e6…)
RC_BACKEND_HASH_UNCHANGED    = YES    (be9c0fec…)
OLD_PASSWORD_REJECTED        = YES    (محاولة اتّصال بالسرّ القديم ⟹ فشل مصادقة · رمز خروج 2)
NEW_PASSWORD_ACCEPTED        = YES    (اتّصال ناجح ⟹ reporting_rc/40)
SECRETS_PRINTED              = NO
```

بنية القاعدة وبياناتها بعد التدوير: **78 جدولًا · 928 عمودًا · 36 مستخدمًا** — مطابقة لما قبله (التدوير لا يمسّ البيانات).

**فحص السجلّات بعد إعادة التشغيل:** `Now listening on: http://127.0.0.1:5092` + `Application started` + `Hosting environment: ReleaseCandidate` · **0** خطأ مصادقة (`28P01`) · **0** استثناء أو سطر `fail:` · **0** إدخال جديد في `rc-api.err.log` · **0** ملفّ سجلّ يحوي السرّ الجديد (بحث حرفيّ في `/var/log` كاملًا).
الواجهة العامّة `https://rc-report.emarketingacademy.net/` = `401` (سلوك `auth_basic` الطبيعيّ، بلا تغيير في Nginx).

---

## 4) النسخ الاحتياطيّة القديمة

`/opt/backups/rc-preflight-20260818T145419Z-r21`:
- **لم تُحذف ولم يُنقَص منها شيء.**
- الدليل `700` · `khubara-reporting-rc.env` و`htpasswd-reporting-rc` و`SHA256SUMS` بـ`600`.
- أُضيف `CREDENTIAL-NOTICE.md` (`600`) يُسجّل أنّ ملفّ البيئة داخلها يحوي **بيانات اعتماد منتهية الصلاحيّة**.
- أُضيف تحذير صريح في `ROLLBACK-STEPS.md` قبل خطوة استعادة الإعدادات: **استعادة ملفّ البيئة القديم تستوجب إعادة حقن السرّ الحاليّ في حقل `Password=` قبل تشغيل الخدمة**، وإلّا فشل الإقلاع بـ`28P01`؛ و**لا يُعاد استخدام السرّ القديم إطلاقًا**.
- أُعيد توليد `SHA256SUMS` ليشمل الملفّين الجديدين ⟹ `sha256sum -c` = **11/11 OK** (خروج 0).

---

## 5) التنظيف والنطاق

- كلّ الملفّات المؤقّتة (6 ملفّات: السرّ، ملفّ البيئة الجديد، سكربت `ALTER`، مخرجات الاختبارات) **مُحيت بـ`shred -u`** ودليلها المؤقّت أُزيل · لا بقايا في `/root` ولا في `/etc`.
- الحزمة المرحليّة `/opt/reporting-rc/staging-r21-7e063b4-20260818` سليمة (9 مدخلات) ولم تُفعَّل.
- **لم يُنشر منتج، ولم تُطبَّق هجرة، ولم تُغيَّر الواجهة أو الخلفيّة أو Nginx.**
- خدمتا TEST والإنتاج نشطتان ولم تُمسّا (`khubara-reporting-test` و`reporting-api` نشطتان · `mtime` ملفّي بيئتهما `2026-08-07` و`2026-07-26` كما كانا).
- **لا تغيير في git** (لا التزام ولا دفع ولا وسم).

---

## 6) الخلاصة

```
RC_CREDENTIAL_ROTATION = PASS
RC_HEALTH              = 200
OLD_CREDENTIAL_REVOKED = YES
NEW_CREDENTIAL_ACTIVE  = YES
MIGRATIONS_CHANGED     = NO
PRODUCT_DEPLOYED       = NO
TEST_TOUCHED           = NO
PRODUCTION_TOUCHED     = NO
NEXT_REQUIRED_ACTION   = نشر 7e063b4 على TEST أولًا بتصريح منفصل
```
