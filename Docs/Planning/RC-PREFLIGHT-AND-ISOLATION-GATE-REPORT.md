# RC — تقرير ما قبل النشر وبوّابة العزل

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE` · **المرحلة:** P · **التاريخ:** 16 أغسطس 2026
**النطاق:** قراءة فقط. **لم تُنفَّذ أيّ كتابة** على RC ولا على الإنتاج في هذه المرحلة.
**الحكم:** **`RC_ISOLATION_GATE = PASS`** — يجوز المضيّ إلى النسخ الاحتياطيّة.

---

## 1) هويّة بيئة RC (مقروءة، لا مفترضة)

| البند | القيمة |
|---|---|
| الخدمة | `khubara-reporting-rc.service` — نشطة، آخر إقلاع **7 أغسطس 2026 07:07:39 UTC** |
| المستخدم | `www-data` |
| `ASPNETCORE_ENVIRONMENT` | **`ReleaseCandidate`** |
| المنفذ | `http://127.0.0.1:5092` |
| ملفّ البيئة | `/etc/khubara-reporting-rc.env` — صلاحيّة **`600 root:root`** |
| مجلّد الخلفيّة | `/opt/reporting-rc/publish` — **107 ميغابايت · 86 ملفًّا** |
| مجلّد الواجهة | `/opt/reporting-rc/frontend/dist` (mtime 12 أغسطس) |
| مجلّد التخزين | `/opt/reporting-rc/storage` — **0 ملفّ · 16 كيلوبايت** |
| النطاق | `rc-report.emarketingacademy.net` خلف `auth_basic "Khubara Reports RC — Not Production"` |
| `robots.txt` | `Disallow: /` — لا فهرسة |
| الصحّة | `GET /health` ⟹ **200** · `{"status":"ok","service":"reporting-api"}` |

## 2) الحالة المنشورة حاليًّا على RC

| البند | القيمة |
|---|---|
| **SHA المنشور** (من SourceLink داخل `Reporting.Api.dll`) | **`ce166662f46598ed3593beed0105ba67059fc3bc`** |
| `sha256(Reporting.Api.dll)` | `5f25a0a5113ac2130a9362bb484f1b1538a85eac9c010af7460035243af198fe` |
| mtime للثنائيّ | `2026-08-07 07:06:27 UTC` |
| بصمة `dist` التجميعيّة الحاليّة | `779c4c2071b53f6d1e7f51a6c6a086f6dd0b7bc33511c461ce96966790a78d89` |
| عدد الهجرات | **30** |
| رأس الهجرات | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| الجداول / الأعمدة | **57 / 637** |
| **بصمة المخطَّط قبل النشر** — الصيغة المعياريّة `Ops/MigrationHistoryBridge/fingerprint.sql` (أعمدة + قيود + فهارس، باستثناء `__EFMigrationsHistory`) | **`e137d40dcd1ad8d088fa6c4ad9a8eebb`** |
| البصمة نفسها على **الإنتاج** `reporting_prod` | **`e137d40dcd1ad8d088fa6c4ad9a8eebb`** ⟹ **مطابقة تامّة** — RC مرآة حرفيّة للإنتاج على مستوى المخطَّط |
| بصمة **TEST** بعد `4fddc20` (الهدف الذي يجب أن تبلغه RC بعد الهجرة) | **`3b3eb6b04fc0e6b1898468bd2cfed546`** |

> **تصحيح صيغة القياس:** القيمة `60746c4e5886a9acd2767dc3a6ee7d53` التي وردت في مسوّدة سابقة من هذا التقرير
> كانت ناتجة عن صيغة `md5` مبسَّطة (أعمدة فقط) لا عن الأداة الملتزَمة. **الصيغة الوحيدة المعتمَدة**
> في هذه التذكرة هي `Ops/MigrationHistoryBridge/fingerprint.sql`، ولا تُقارَن بصمتان إلّا إذا اشتُقّتا منها.

**ملاحظة نَسَب:** الواجهة (12 أغسطس) أحدث من الخلفيّة (7 أغسطس) على RC. لا أثر لذلك على هذا الإصدار لأنّ كلا الطرفَين سيُستبدلان بمصنوعات `4fddc20`، لكنّه يُسجَّل لأنّ «RC مرآة حرفيّة للإنتاج» صحيحة على مستوى **الخلفيّة والقاعدة** لا على مستوى حزمة الواجهة.

## 3) جدولا التصادم موجودان فعلًا

```
kpi_template_assignments   موجود
report_view_grants         موجود
client_documents           غير موجود  (متوقَّع: ميزة CPW-R2 لم تصل RC بعد)
```

⟹ شرط الجسر متحقّق: الجدولان اللذان سيصطدم معرّفا هجرتهما **قائمان بالفعل**.

## 4) الأعداد المرجعيّة قبل النشر (قراءة)

| الجدول | العدد |
|---|---|
| `AspNetUsers` | 36 |
| `clients` | 8 |
| `projects` | 32 |
| `report_submissions` | 39 |
| `leave_requests` | 1 |
| `kpi_template_assignments` | 0 |
| `report_view_grants` | 0 |
| `email_notifications` | 117 |
| **`email_outbox`** | **0** ← خطّ الأساس لكشف أيّ تسريب بريد |

## 5) **بوّابة العزل الصارمة** — بند بند

| # | الشرط | الدليل | النتيجة |
|---|---|---|---|
| 1 | RC لا تتصل بقاعدة الإنتاج | سلسلة اتصال RC = `Database=reporting_rc;Username=reporting_rc_app` | **PASS** |
| 2 | لا مشاركة اسم قاعدة | `reporting_rc` ≠ `reporting_prod` ≠ `reporting_test_uat` | **PASS** |
| 3 | **استحالة الوصول فعليًّا لا حُكمًا** | `pg_hba.conf:118-121` قاعدة **`reject`** صريحة لـ`reporting_rc_app → reporting_prod` (local · 127.0.0.1 · ::1) | **PASS** |
| 4 | إثبات تجريبيّ للرفض | محاولة اتصال بكلمة سرّ وهميّة ⟹ `FATAL: pg_hba.conf rejects connection` **قبل** طبقة المصادقة؛ وضابط مقابل على `reporting_rc` ⟹ `password authentication failed` (أي وصل إلى المصادقة) | **PASS** |
| 5 | صفر صلاحيّة كائنات في الإنتاج | `table_privileges` لـ`reporting_rc_app` داخل `reporting_prod` = **0 جدول**؛ `SELECT/INSERT/UPDATE/DELETE` على `AspNetUsers` = **false ×4** | **PASS** |
| 6 | لا وراثة أدوار | `pg_auth_members` لأدوار `reporting%` = **صفر عضويّة** | **PASS** |
| 7 | لا مشاركة مجلّد نشر | `/opt/reporting-rc/publish` ≠ `/opt/reporting/publish` | **PASS** |
| 8 | لا مشاركة حزمة واجهة | `/opt/reporting-rc/frontend/dist` مستقلّ | **PASS** |
| 9 | لا مشاركة تخزين | RC: `/opt/reporting-rc/storage/…` · الإنتاج: `/var/lib/reporting/…` | **PASS** |
| 10 | لا بريد حقيقيّ | `Email__Enabled=false` · `Email__Provider=none` · `Email__SmtpHost` فارغ · **`EmailNotifications__Mode=DryRun`** | **PASS** |
| 11 | لا مجدولات خارجيّة | `Reminders__Enabled=false` · `Scheduler__Enabled=false` · `BackgroundJobs__Enabled=false` · `ReportReminderScheduler__Enabled=false` | **PASS** |
| 12 | لا تكاملات خارجيّة | `Integrations__Enabled=false` · `Notifications__Realtime__Enabled=false` | **PASS** |
| 13 | لا اعتمادات إنتاج داخل RC | ملفّ بيئة RC لا يحمل أيّ مفتاح إنتاجيّ؛ `Jwt`/`Cookie`/`Cors`/`App__BaseUrl` كلّها نطاق `rc-report` | **PASS** |
| 14 | صندوق الصادر نظيف | `email_outbox = 0` · صفر سطر SMTP في سجلّ الخدمة منذ 7 أغسطس | **PASS** |

**المكبحان المستقلّان مؤكَّدان:** `Email__Enabled=false` **و**`EmailNotifications__Mode=DryRun` — والمصدر الموثوق للقناة الجديدة هو الثاني.

## 6) ملاحظتان مسجَّلتان (لا تمنعان النشر)

### 6.1 `FileStorage__DocumentsRootPath` غير مضبوط على RC
`LocalFileStorage.cs:29-33` يعود إلى الافتراضيّ `ContentRoot/App_Data/documents`
⟹ على RC = `/opt/reporting-rc/publish/App_Data/documents` — **داخل مجلّد يُستبدَل عند كلّ نشر**.
- **العزل غير منتهَك** (المسار خاصّ بـRC ولا يمسّ الإنتاج) ⟹ ليس سبب `NO-GO`.
- **الخطر:** ضياع مستندات UAT عند أيّ إعادة نشر، وصعوبة النسخ الاحتياطيّ.
- **الإجراء:** ضبط `FileStorage__DocumentsRootPath=/opt/reporting-rc/storage/documents` ضمن المرحلة T قبل إعادة التشغيل (تعديل إعداد **RC** — مصرَّح به).

### 6.2 قاعدة `reject` تغطّي `reporting_rc_app` وحده
مصفوفة الوصول الفهرسيّة:

| الدور | `reporting_prod` | `reporting_rc` | `reporting_test_uat` |
|---|---|---|---|
| `reporting_app` | true | false | false |
| `reporting_rc_app` | true (فهرسيًّا) — **مرفوض بـ`pg_hba`** | true | false |
| `reporting_test_uat_app` | true — **غير مشمول بقاعدة رفض** | false | true |
| `reporting_test_app` | true — **غير مشمول بقاعدة رفض** | false | false |

السبب: `datacl` لـ`reporting_prod` = `=Tc/reporting_app` أي **PUBLIC يملك `CONNECT`** (السلوك الافتراضيّ لـPostgreSQL)، والرفض الصريح أُضيف لـRC فقط (`RC-ISOLATION-BEGIN — 20260710`).

**حجم المخاطرة مُقاس لا مُقدَّر:** صلاحيّات `PUBLIC` داخل `reporting_prod` هي **62 على `information_schema` و127 على `pg_catalog` فقط، و**صفر** على مخطَّط `public`** ⟹ اتصال ناجح لا يمنح أيّ بيانات تطبيق.
**التوصية:** تذكرة تقوية مستقلّة (`REVOKE CONNECT ON DATABASE reporting_prod FROM PUBLIC` + توسيع قواعد الرفض). **لم يُنفَّذ** — تعديل إعداد الإنتاج غير مصرَّح به في هذه المهمّة.

## 7) سعة وبيئة التشغيل

| البند | القيمة |
|---|---|
| القرص | `/dev/sda1` 96G · مستخدَم 43G · **متاح 54G (45%)** |
| PostgreSQL | **16.14** (Ubuntu 24.04) |
| .NET | **8.0.129** |
| Nginx | نشط · `nginx -t` ناجح |
| أحجام القواعد | `reporting_prod` 18MB · `reporting_rc` 14MB · `reporting_test_uat` 14MB · `reporting_test_rc` 12MB |

المتاح (54G) يفوق مجموع كلّ النسخ المطلوبة بفارق ثلاثة أوامر قدر ⟹ **سعة كافية**.

## 8) تأكيد

**الإنتاج لم يُمَسّ.** كلّ استعلام على `reporting_prod` في هذه المرحلة كان `SELECT` على فهارس النظام أو عدّ صفوف، ولم يُنفَّذ أيّ `INSERT/UPDATE/DELETE/DDL` ولا إعادة تشغيل ولا تعديل إعداد.

```
RC Environment Isolation = PASS
RC Deployed SHA          = ce166662
RC Migrations / Head     = 30 / 20260724224053_AddReportApproverAndKpiReviewerOverrides
RC Schema Fingerprint    = e137d40dcd1ad8d088fa6c4ad9a8eebb  (= Production, canonical formula)
TEST Target Fingerprint  = 3b3eb6b04fc0e6b1898468bd2cfed546
RC Email Mode            = DryRun (+ Email__Enabled=false)
RC Schedulers            = ALL DISABLED
Outbox Baseline          = 0
Production Touched       = NO
```
