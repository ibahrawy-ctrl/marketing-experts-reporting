# خطّة التراجع — نشر الإنتاج للمرشّح `897c9b18` (P123)

**التاريخ:** 26 أغسطس 2026 · **البيئة:** `reports.emarketingacademy.net` · **الخدمة:** `reporting-api` (systemd، `www-data`، `127.0.0.1:5090`) · **القاعدة:** `reporting_prod`

---

## 1) معرّفات النسخة الاحتياطيّة (Phase 11)

`BACKUP_ID = /var/backups/reporting/20260826-P123`

| القطعة | المسار | الحجم | SHA-256 |
|---|---|---:|---|
| قاعدة البيانات (`pg_dump -Fc`) | `reporting_prod-20260826-P123.dump` | 1,424,971 | `7cd5e8888b97560f1e971890cda457d7b5a70a8298a5ed720169c304bcec4722` |
| ثنائيّة الخلفيّة | `publish-20260826-P123.tar.gz` | 47,398,207 | `93e6469aabad368a870d95929bca22c65ae066196a0481bba55ee412cfc4fb43` |
| حزمة الواجهة | `frontend-dist-20260826-P123.tar.gz` | 394,527 | `cccbd4157ebcd2f4c41b0261641872aa2a080392bf635ff892acaed3efb53543` |
| ملفّ البيئة | `reporting-api.env.bak` (0600) | 1,553 | `afaac8b3ca9c61d09e16d7a75bc2bb7ea1f1d601eca75d9d631b8310860bd5c9` |
| وحدة systemd | `reporting-api.service.bak` | 363 | `1146b4f2e9674227a33a266f5fb35c6bdb207a2349fd8a705de770576064a1ff` |

**تحقّق TOC:** `pg_restore -l` أعطى 492 سطرًا · 79 مدخل `TABLE DATA` · `AspNetUsers` ✔ · `report_submissions` ✔ · `__EFMigrationsHistory` ✔ (3 مداخل). الفهرس مقروء ⇒ الملفّ سليم لا مبتور.

## 2) حالة ما قبل النشر (نقطة العودة)

- الخلفيّة: `1.0.0+7e063b493b50ad90ba6131e47042c7cd035fb65b` · `md5(Reporting.Api.dll) = ddf9598c0bf00f821a0aefe0c6cc1975` · mtime 2026-08-18 15:02
- الواجهة: `index-CMjXSPXr.js` · `md5 = f37fd278b073dcf391549e4c1ab57318` · mtime 2026-08-23 11:46
- الهجرات: **42**، آخرها `20260817114129_AddProjectExecutionUpdateProposals`
- الخدمة: `MainPID=1556574` · `active/running` · `NRestarts=0`
- بصمات البيانات: `md5_users=a6385875d3cfc436639864adfc3f4c0c` · `md5_submissions=08defb3b860a6d4ad97ec31f0ee1b5cc` · `md5_departments=7d80557511c8efa0ca5616a4a59e8be7` · `md5_teams=a874a3098deb7b4746d2cf6e630adb55`
- `userclaims_perm_total = 0`

## 3) تصنيف الهجرات الخمس المعلَّقة (مقيس على الظلّ لا مفترَض)

| # | معرّف الهجرة | العمليّات | التصنيف | الخطر |
|---|---|---|---|---|
| 1 | `20260824195457_AddKpiTemplateVersionBelowTargetThreshold` | `AddColumn` → `kpi_template_versions.BelowTargetThreshold` | **Additive** | لا شيء |
| 2 | `20260824230015_AddManagementNoteSensitivity` | `AddColumn` → `management_notes.Sensitivity` | **Additive** | لا شيء |
| 3 | `20260824233938_AddAttendanceIncidents` | `CreateTable ×4` (`attendance_incidents`, `attendance_incident_events`, `attendance_incident_attachments`, `attendance_incident_types`) + `CreateIndex ×9` | **Additive** | لا شيء — جداول جديدة كلّيًّا |
| 4 | `20260825111521_P2_HR010_EmployeeChecklistItems` | `CreateTable` (`employee_checklist_items`) + `CreateIndex ×2` | **Additive** | لا شيء |
| 5 | `20260826073223_P123DirectoryNameUniqueness` | `Sql` (حارس Preflight) + `CreateIndex unique ×2` (`IX_departments_NameAr`, `IX_teams_DepartmentId_NameAr`) | **Compatible-with-guard** | يفشل بأمان لو وُجد تكرار — **لا يحذف ولا يدمج صفًّا واحدًا** |

**Destructive = 0 · Data-transforming = 0.**

**Preflight على بيانات الإنتاج الحيّة (قراءة محضة):**
```
dup_departments_NameAr|0
dup_teams_Dept_NameAr|0
```
⇒ الهجرة الخامسة لن تُفعِّل حارسها.

## 4) نتيجة البروفة الظلّيّة (Phase 12)

قاعدة `reporting_prod_shadow_p123` = استعادة كاملة من نسخة اليوم، ثمّ شُغِّلت **ثنائيّة المرشّح نفسها** (`/opt/reporting-rc/publish` بختم `1.0.0+897c9b18…`) عليها ببيئة الإنتاج الحرفيّة، مع إطفاء البريد والمجدولات ومنفذ منفصل `5199`.

| المقياس | قبل الهجرة | بعد الهجرة | الحكم |
|---|---|---|---|
| `migrations_total` | 42 | **47** | +5 كما هو متوقَّع |
| `users_total` | 34 | 34 | ثابت |
| `departments_total` | 4 | 4 | ثابت |
| `teams_total` | 9 | 9 | ثابت |
| `submissions_total` | 311 | 311 | ثابت |
| `audit_logs_total` | 1464 | 1464 | ثابت |
| `userclaims_perm_total` | 0 | **0** | لا منح |
| `md5_users` | `a6385875…` | `a6385875…` | **مطابق بايتًا** |
| `md5_submissions` | `08defb3b…` | `08defb3b…` | **مطابق بايتًا** |

- زمن الإقلاع حتّى `/health` أخضر: **6.2 ثانية**.
- الجداول الخمسة الجديدة أُنشِئت فارغة (`attendance_incidents=0`, `employee_checklist_items=0`).
- الفهرسان الجديدان `unique=true` فعلًا.
- صفر أخطاء حقيقيّة في السجلّ (المطابقتان الوحيدتان لكلمة `EXCEPTION` هما نصّ حارس الـPreflight المطبوع في SQL، لا فشل).

## 5) إجراء التراجع — ثلاث درجات

### الدرجة أ — تراجع الحزمة فقط (الهجرات إضافيّة ⇒ آمنة مع الثنائيّة القديمة)
الحالة الموجِبة: خطأ وظيفيّ أو بصريّ بعد النشر بلا فساد بيانات.
```bash
systemctl stop reporting-api
rm -rf /opt/reporting/publish && mkdir -p /opt/reporting/publish
tar -xzf /var/backups/reporting/20260826-P123/publish-20260826-P123.tar.gz -C /opt/reporting
rm -rf /opt/reporting/reporting-frontend/dist
tar -xzf /var/backups/reporting/20260826-P123/frontend-dist-20260826-P123.tar.gz -C /opt/reporting/reporting-frontend
chown -R www-data:www-data /opt/reporting/publish /opt/reporting/reporting-frontend/dist
systemctl start reporting-api && curl -s http://127.0.0.1:5090/health
```
**لماذا تكفي:** الهجرات الخمس كلّها إضافيّة (أعمدة وجداول وفهارس جديدة). الثنائيّة القديمة `7e063b49` لا تعرف هذه الكيانات فتتجاهلها؛ لا يوجد عمود `NOT NULL` بلا افتراضيّ ولا تغيير في نوع عمود قائم. زمن التنفيذ المتوقَّع: أقلّ من دقيقتين.

### الدرجة ب — تراجع الحزمة + ملفّ البيئة
الحالة الموجِبة: خلل ناتج عن تغيير علم أو إعداد.
```bash
systemctl stop reporting-api
cp /var/backups/reporting/20260826-P123/reporting-api.env.bak /etc/reporting-api.env
chmod 600 /etc/reporting-api.env
# ثمّ نفّذ الدرجة أ
```

### الدرجة ج — استعادة القاعدة كاملة (الملاذ الأخير)
الحالة الموجِبة: فساد بيانات مؤكَّد أو فشل هجرة في المنتصف.
```bash
systemctl stop reporting-api
sudo -u postgres psql -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='reporting_prod';"
sudo -u postgres dropdb reporting_prod
sudo -u postgres createdb -O reporting_app reporting_prod
sudo -u postgres pg_restore -d reporting_prod /var/backups/reporting/20260826-P123/reporting_prod-20260826-P123.dump
# ثمّ الدرجة أ
```
**كلفة الدرجة ج:** تُفقَد أيّ كتابة وقعت على الإنتاج بعد `2026-08-26T19:16:15Z` (لحظة الـ`pg_dump`). لذلك تُستعمل عند فساد مؤكَّد فقط، وبعد أخذ `pg_dump` جديد للحالة الفاسدة للتحليل.

### معايير إطلاق التراجع (لا اجتهاد لحظيّ)
يُطلَق التراجع فورًا عند أيٍّ ممّا يلي:
1. `/health` غير أخضر بعد 90 ثانية من `systemctl start`.
2. `NRestarts > 0` خلال أوّل 10 دقائق.
3. فشل هجرة (رسالة `P123-PREFLIGHT` أو أيّ `PostgresException` عند الإقلاع).
4. تغيّر أيّ من بصمات `md5_users` / `md5_submissions` / `md5_departments` / `md5_teams`.
5. `userclaims_perm_total > 0` لمستخدم حقيقيّ.
6. فشل تسجيل الدخول لأيّ صفة على الواجهة المنشورة.

## 6) نافذة التنفيذ وتقليص أثر الانفجار

- نطاق التغيير **إصدار كامل لا Hotfix**: الإنتاج على `7e063b49` (18 أغسطس) والمرشّح `897c9b18` يحمل فرق 5 هجرات وعملًا وظيفيًّا واسعًا (Attendance، Employee 360، P2/P3، P360 slice، علاج `DEF-P123-RC-001`).
- `EmailNotifications__Mode=Enabled` و`Reminders__Enabled=true` على الإنتاج ⇒ إعادة التشغيل قد تُطلق بريدًا حقيقيًّا. **الإجراء:** تُطفأ قنوات البريد والمجدولات قبل إعادة التشغيل، ويُتحقَّق من الصحّة، ثمّ تُعاد تدريجيًّا (Phase 14).
- النسخ الاحتياطيّة الثلاث موجودة ومتحقَّق منها قبل أيّ كتابة.
