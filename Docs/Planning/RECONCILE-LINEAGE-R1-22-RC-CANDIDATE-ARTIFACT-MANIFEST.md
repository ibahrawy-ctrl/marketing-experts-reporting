# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 22: بيان مرشَّح RC وكرّاس النشر والتراجع

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** N — RC Candidate Freeze
**التاريخ:** 16 أغسطس 2026
**الحالة:** **مجمَّد وجاهز — غير منشور.**

> **إقرار صريح ومكرّر:** لم تُنفَّذ في هذه التذكرة **أيّ** عمليّة كتابة أو هجرة أو إعادة
> تشغيل أو نشر على **RC** أو **الإنتاج**. القراءات على `reporting_rc` و`reporting_prod`
> كانت قراءات محضة (`SELECT` على `__EFMigrationsHistory`) لإثبات ثباتهما.
> **تنفيذ هذا الكرّاس يحتاج تصريحًا صريحًا جديدًا من مالك النظام.**

---

## 1) هويّة المرشَّح

| البند | القيمة |
|---|---|
| الالتزام | **`4fddc20ad23757636c54f3a5baa94fec08a84c61`** |
| الفرع | `origin/develop` |
| الوسم المحلّيّ | `rc-lineage-unified-20260816` — **محلّيّ فقط، غير مدفوع** |
| سلف من `develop` السابق (`10c26f7`) | **نعم** — 43 التزامًا بينهما |
| سلف من الإنتاج الحيّ (`ce166662`) | **نعم** — 55 التزامًا غير دمج بينهما |
| قاعدة التفرّع الأصليّة | `6fd2253` |
| `origin/main` | `508509ad…` — لم يتحرّك |

**معنى ذلك:** هذا أوّل التزام في تاريخ المستودع يجمع النَسَبَين. أيّ نشر منه على
الإنتاج **لا يفقد أيّ التزام إنتاجيّ**، وهي المشكلة الحاجبة التي فُتِحت لأجلها التذكرة.

### 1.1 إثبات النَسَب (يُعاد تشغيله للتحقّق)

```bash
git merge-base --is-ancestor 10c26f7  4fddc20 && echo "descendant of develop"
git merge-base --is-ancestor ce166662 4fddc20 && echo "descendant of production"
```

---

## 2) الهجرات

| البند | القيمة |
|---|---|
| عدد الهجرات في المرشَّح | **38** (35 من `develop` + 3 من نَسَب الإنتاج) |
| الرأس | `20260811142239_AddProject360Foundation` |
| تكرار معرّفات الهجرات | 0 |
| تكرار أسماء الهجرات | 0 |
| `dotnet ef migrations has-pending-model-changes` | `No changes have been made to the model since the last migration.` |
| بصمة مخطَّط القاعدة الجديدة | `3dc2638fe72aadbdaa5450a9aa70c2c2` |

### 2.1 الهجرات الثلاث القادمة من نَسَب الإنتاج

| المعرّف | الأثر | الأمان |
|---|---|---|
| `20260715162851_AddBypassTeamLeaderApproval` | عمود `bool` على `AspNetUsers` | إضافيّ بحت |
| `20260716015239_KpiEvaluationPartialUniqueIndex` | `DropIndex` ثمّ `CreateIndex … WHERE "IsDeleted" = false` | المرشَّح **أقلّ تقييدًا** من الفهرس القائم ⟹ لا احتمال خرق |
| `20260724224053_AddReportApproverAndKpiReviewerOverrides` | عمودان `Guid?` + مفتاحان أجنبيّان على `AspNetUsers` | إضافيّ بحت |

### 2.2 وضع RC والإنتاج الحاليّ (قراءة محضة — 16 أغسطس)

| القاعدة | الهجرات | الرأس | آخر إقلاع للخدمة |
|---|---|---|---|
| `reporting_prod` | **30** | `20260724224053_AddReportApproverAndKpiReviewerOverrides` | 7 أغسطس 08:57 UTC |
| `reporting_rc` | **30** | نفس الرأس | 7 أغسطس 07:07 UTC |
| `reporting_test_uat` | **38** | `20260811142239_AddProject360Foundation` | 16 أغسطس 16:34 UTC |

⟹ الترقية المطلوبة على RC/الإنتاج هي **30 → 38 (ثماني هجرات)**، **بعد الجسر حتمًا**.

---

## 3) جسر سجلّ الهجرات — إلزاميّ قبل أيّ إقلاع على RC أو الإنتاج

نَسَب الإنتاج طبّق هجرتَين بمعرّفَين زمنيَّين مختلفَين عن المعتمدَين مع **محتوى SQL
متطابق حرفيًّا**:

| المعتمد (في المرشَّح) | التاريخيّ (على الإنتاج/RC) | الجدول |
|---|---|---|
| `20260622140138_KpiTemplateAssignmentsPhaseT1` | `20260622144900_KpiTemplateAssignmentsPhaseT1` | `kpi_template_assignments` |
| `20260626124527_AddReportViewGrants` | `20260626135944_AddReportViewGrants` | `report_view_grants` |

**بلا الجسر:** الإقلاع يفشل بـ`42P07 relation "kpi_template_assignments" already exists`
وتبقى القاعدة **نصف-مهاجَرة (31 هجرة / 61 جدولًا)** — أُثبِت عمليًّا على نسخة معزولة.

**الأداة:** `Ops/MigrationHistoryBridge/bridge.sh` — تُدرِج **صفَّين اثنين فقط** في
`__EFMigrationsHistory` داخل معاملة واحدة، بلا أيّ `CREATE`/`ALTER`/`DROP` وبلا مساس
بأيّ صفّ بيانات، وتولّد سكربت تراجع قبل التنفيذ. تسعة فحوص قبليّة كلّها حاجبة، والتاسع
منها يقارن **بصمة المخطَّط الكاملة** بالمرجع `e137d40dcd1ad8d088fa6c4ad9a8eebb`
(`expected-fingerprint-prod-rc.txt` · 905 أسطر).

**التحقّق السلبيّ المُنفَّذ على TEST (16 أغسطس):** `RESULT = REFUSED · exit 3` عند الفحص 6
لأنّ TEST ليست من نَسَب الإنتاج ⟹ **الأداة لا يمكن تطبيقها على البيئة الخطأ**.

---

## 4) آثار البناء المرشَّحة (المقيسة على TEST من نفس الالتزام)

| الأثر | القيمة |
|---|---|
| `publish/` | 109 ميغابايت · 86 ملفًّا |
| `Reporting.Api.dll` | `6a4b6022cb73735877f971a07219ab69fbd615ba41c3ae9d4f32cefe8fd7f085` |
| `Reporting.Application.dll` | `73dd90ffd15e3c26e32f03c777aa76163084d93ee25bd51854ed6402bc860f00` |
| `Reporting.Infrastructure.dll` | `285927516bc582492a306f594fe0748756cf54f299a7fce580e2721c57b28de4` |
| `Reporting.Domain.dll` | `c6fe07cb53b88855ccaf5982088d63ab6b97e103202703853e9f77a868e8377b` |
| `frontend/dist/` | 1.6 ميغابايت · 7 ملفّات |
| بصمة `dist` التجميعيّة | `f836bb9797b3457112cceeadfdfcd40954b765ae77faa0c42cb7931655c32150` |

> بناء RC/الإنتاج يجب أن يُعاد من نفس الالتزام في نسخة معزولة؛ هذه البصمات مرجع
> للمقارنة لا أثر يُنسَخ مباشرةً بين البيئات.

---

## 5) توقّعات التهيئة على RC والإنتاج

| المفتاح | القيمة المتوقَّعة | السبب |
|---|---|---|
| `EmailNotifications__Mode` | `Disabled` أو `DryRun` عند أوّل نشر | المصدر الموثوق الوحيد للقناة الجديدة |
| `Email__Enabled` | لا يتحكّم بالقناة الجديدة | علم قديم — لا يُعتمد عليه |
| `Reminders__Enabled` | `false` عند أوّل نشر | مكبح ثانٍ مستقلّ |
| `ReportReminderScheduler__Enabled` | **غير مضبوط** (الافتراضيّ `false` في الكود) | خدمة مجدولة قادمة من نَسَب الإنتاج |
| `ConnectionStrings__Default` | قاعدة البيئة الصحيحة | **مزلق موثَّق:** تمرير الاتّصال عبر متغيّر بيئة لـ`dotnet ef` قد يُتجاهَل ويُستعمل `reporting_dev` الافتراضيّ؛ استعمل علم `--connection` صراحةً |
| Swagger | معطّل في الإنتاج | قاعدة معماريّة |

---

## 6) الفحوص القبليّة الحاجبة قبل أيّ نشر على RC أو الإنتاج

```sql
-- 1) عدد الهجرات ورأسها (يجب أن يكون 30 والرأس 20260724224053_…)
SELECT count(*), max("MigrationId") FROM "__EFMigrationsHistory";

-- 2) وجود الصفَّين التاريخيَّين (شرط عمل الجسر — يجب أن يكون 2)
SELECT count(*) FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN ('20260622144900_KpiTemplateAssignmentsPhaseT1',
                        '20260626135944_AddReportViewGrants');

-- 3) غياب الصفَّين المعتمدَين (يجب أن يكون 0)
SELECT count(*) FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN ('20260622140138_KpiTemplateAssignmentsPhaseT1',
                        '20260626124527_AddReportViewGrants');

-- 4) وجود الجدولَين فعلًا (يجب أن يكون 2)
SELECT count(*) FROM information_schema.tables WHERE table_schema='public'
  AND table_name IN ('kpi_template_assignments','report_view_grants');

-- 5) لا معاملات معلّقة
SELECT count(*) FROM pg_stat_activity
WHERE datname = current_database() AND state = 'idle in transaction';

-- 6) الفهرس الفريد الحاليّ على kpi_evaluations (للمقارنة بعد الترقية)
SELECT indexdef FROM pg_indexes
WHERE schemaname='public' AND tablename='kpi_evaluations' AND indexdef ILIKE '%UNIQUE%';

-- 7) خطّ الأساس العدديّ (يُحفَظ للمقارنة بعد النشر)
SELECT 'users',count(*) FROM "AspNetUsers"
UNION ALL SELECT 'submissions',count(*) FROM report_submissions
UNION ALL SELECT 'templates',count(*)   FROM report_templates
UNION ALL SELECT 'clients',count(*)     FROM clients
UNION ALL SELECT 'projects',count(*)    FROM projects
UNION ALL SELECT 'tables',count(*) FROM information_schema.tables WHERE table_schema='public';
```

---

## 7) قائمة النسخ الاحتياطيّة (ثلاثيّة — كلّها حاجبة)

| # | العنصر | الأمر |
|---|---|---|
| 1 | قاعدة البيانات (ثنائيّة) | `sudo -u postgres pg_dump -Fc -d <db> > $BK/<db>.dump` |
| 2 | قاعدة البيانات (نصّيّة) | `sudo -u postgres pg_dump -d <db> > $BK/<db>.sql` |
| 3 | سجلّ الهجرات | `psql -tA -c 'select "MigrationId" from "__EFMigrationsHistory" order by 1' > $BK/migrations-before.txt` |
| 4 | ثنائيّات الخلفيّة | `tar czf $BK/publish-before.tgz -C <root> publish` |
| 5 | حزمة الواجهة | `tar czf $BK/frontend-dist-before.tgz -C <root>/frontend dist` |
| 6 | مستندات العملاء | `tar czf $BK/documents-before.tgz -C <docroot> .` |
| 7 | بصمة التخزين | `find <docroot> -type f \| sort \| xargs md5sum \| md5sum > $BK/storage-md5-before.txt` |
| 8 | التهيئة (مقنَّعة) و وحدة الخدمة | نسخ بلا أسرار |
| 9 | البصمات | `sha256sum $BK/* > $BK/SHA256SUMS.txt` |

> **مزلق موثَّق:** `sudo -u postgres pg_dump -f /root/…` يفشل بـ`Permission denied` لأنّ
> المستخدم `postgres` لا يكتب في `/root`. الحلّ: حذف `-f` وإعادة التوجيه من صدفة الجذر.

---

## 8) تسلسل النشر المقترح (لا يُنفَّذ بلا تصريح)

```bash
# 0) نسخ احتياطيّة كاملة (القسم 7) + فحوص قبليّة (القسم 6) — كلّها خضراء
# 1) بناء من الالتزام في نسخة معزولة، ورفع إلى مجلّد مرحليّ (لا استبدال بعد)
# 2) إيقاف الخدمة
systemctl stop <service>
# 3) الجسر — جافًّا أوّلًا، ثمّ تنفيذًا
cd Ops/MigrationHistoryBridge
./bridge.sh --db <db> --env <rc|production> --expected-commit 4fddc20
./bridge.sh --db <db> --env <rc|production> --expected-commit 4fddc20 --apply [--allow-production]
#    المتوقَّع: APPLIED (2 alias rows) · الجداول بلا تغيير · البصمة بلا تغيير
# 4) استبدال ذرّيّ مع إبقاء القديم في مكانه للتراجع اللحظيّ
mv publish publish-backup-<ts> && mv publish-staging publish
mv frontend/dist frontend/dist-backup-<ts> && mv frontend/dist-staging frontend/dist
chown -R www-data:www-data publish frontend/dist
# 5) الإقلاع — الهجرات الثمانية تُطبَّق تلقائيًّا
systemctl start <service>
# 6) التحقّق الفوريّ
curl -s http://127.0.0.1:<port>/health
journalctl -u <service> --since "-10 min" -p 3 --no-pager
psql -tA -c 'select count(*), max("MigrationId") from "__EFMigrationsHistory";'   # 40, رأس Project360
```

> **ملاحظة عدديّة:** بعد الجسر يصير عدد الصفوف 32، وبعد الترقية 32 + 8 = **40 صفًّا**
> في `__EFMigrationsHistory` على RC/الإنتاج (منها صفّان تاريخيّان يبقيان شاهدَي تاريخ)،
> مقابل **38** على قاعدة نظيفة. **هذا فرق متوقَّع ومقصود ولا يعني تباينًا في المخطَّط**؛
> المقياس الحاسم هو **بصمة المخطَّط** لا عدد الصفوف.

---

## 9) نقاط قرار التراجع والأوامر

| العَرَض | القرار | الأمر |
|---|---|---|
| الجسر يُرجِع `REFUSED` عند أيّ فحص | **توقّف قبل أيّ تغيير** — الأداة لم تكتب شيئًا | لا شيء |
| الجسر طُبِّق ثمّ تقرّر التراجع قبل الإقلاع | تراجع الجسر | `psql -d <db> -v ON_ERROR_STOP=1 -f /tmp/bridge-rollback-<db>-<ts>.sql` |
| الخدمة لا تُقلِع / `42P07` في السجلّ | تراجع كامل | إيقاف · استعادة `publish-backup-<ts>` و`dist-backup-<ts>` بـ`mv` · `pg_restore` من `.dump` · إقلاع |
| `/health` ≠ 200 بعد 60 ثانية | تراجع كامل | كما أعلاه |
| فحوص دخان أقلّ من 100% | تحليل أوّلًا؛ تراجع إن كان الفشل في مسار بيانات | كما أعلاه |
| اختلاف عدّاد بيانات عن خطّ الأساس | **تراجع فوريّ بلا تحليل** | كما أعلاه |
| بصمة ملفّات التخزين تغيّرت | **تراجع فوريّ** + تحقيق | كما أعلاه |

**التراجع اللحظيّ للكود** لا يحتاج فكّ أرشيف لأنّ النسخة السابقة تبقى مجلّدًا كاملًا
باسم `*-backup-<ts>` بجوار المجلّد الحيّ — وهو ما طُبِّق فعليًّا على TEST.

---

## 10) قوائم التحقّق بعد النشر

### 10.1 المخطَّط — كائنات يجب أن تكون موجودة

```
AspNetUsers.BypassTeamLeaderApproval
AspNetUsers.ReportApproverOverrideUserId
AspNetUsers.KpiReviewerOverrideUserId
IX_kpi_evaluations_KpiTemplateVersionId_SubjectUserId_PeriodKey  WHERE ("IsDeleted" = false)
جداول: positions · position_permissions · position_scopes
جداول: kpi_template_assignments · report_view_grants
جداول Project 360 و client_documents و client_document_versions
عدد الجداول = 78 · عدد الأعمدة = 928 · بصمة المخطَّط = 3dc2638fe72aadbdaa5450a9aa70c2c2
```

### 10.2 ميزات الإنتاج الحيّة — يجب ألّا تنحدر

| الميزة | الفحص |
|---|---|
| المناصب المرنة | `GET /api/positions` = 200 لـAdmin · 403 لغيره · 404 للمعرّف الوهميّ |
| منح الرؤية | `GET /api/report-view-grants` = 200 لـAdmin · `effective/me` = 200 للجميع |
| ورشة الحوكمة | `GET /api/risks` · `/api/escalations` · `/api/decisions` = 200 لـCEO |
| تجاوز اعتماد قائد الفريق | العمود موجود وسلوكه مغطّى باختبارات الانحدار الـ11 |
| تجاوزا المعتمِد ومراجع KPI | العمودان موجودان + مفتاحاهما الأجنبيّان |
| فهرس تقييم KPI الجزئيّ | المرشّح `IsDeleted = false` موجود في `indexdef` |
| مسارا الواجهة | `/app/positions` و`/app/governance-workspace` داخل `dist` وفي جدول `App.tsx` |

### 10.3 ميزات `develop` المعتمَدة — يجب ألّا تنحدر

| المجموعة | الفحص |
|---|---|
| CPW-R2 مستندات العملاء | القائمة · الروابط · التفاصيل · التحميل · استهلاك التخزين · نموذج الرؤية بأنواعه السبعة |
| CPW-R3 Project 360 | `overview` · `strategy` · `strategy/schema` · `objectives` · `kpis` · `contract-deliverables` · `risks` · `decisions` · `notes` |
| كتالوج التنفيذ | 38 قيمة في النطاقات الثلاثة (6 / 14 / 18) · 0 تكرار · التمهيد idempotent |
| التقويم الواعي بالدور | `my-cycles` · `my-days` · `missing-reports` |
| لوحة المعلومات والإشعارات | `dashboard/me` · `dashboard/pending-reports` · `notifications` |

### 10.4 الأمن

| الفحص | المتوقَّع |
|---|---|
| مصفوفة الأدوار (11 هويّة × 12 فحصًا) | مطابقة خليّة بخليّة للمصفوفة في التقرير 21 §2.1 |
| منع التعداد | 404 لا 403 من **كلّ** دور بما فيه CEO |
| بلا رمز / رمز فاسد | 401 |
| صندوق البريد الصادر | بلا تغيير |

---

## 11) العيوب الأساسيّة المعروفة والمقبولة

| المعرّف | الاختبار | الطبيعة |
|---|---|---|
| `BASELINE-DEFECT-01` | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` | معتمد على الترتيب: ينجح منفردًا ويفشل ضمن المجموعة الكاملة · **مشترك مع الأبوَين** ⟹ ليس انحدار مرشَّح |
| `BASELINE-DEFECT-02` | `EmployeeProfileScopeTests.Profile_Summary_Reflects_Submitted_Kpi` | نفس الصنف |

كلاهما يحتاج **تذكرة عزل اختبارات مستقلّة** ولا يُصلَح ضمن هذه التذكرة.

---

## 12) كتلة جاهزيّة RC

```
RC Candidate Commit                 = 4fddc20ad23757636c54f3a5baa94fec08a84c61
Local Tag                           = rc-lineage-unified-20260816  (local only, not pushed)
Descendant of develop + production  = YES / YES
Migration Count / Head              = 38 / 20260811142239_AddProject360Foundation
Migration Bridge Required for RC    = YES (2 alias rows, verified on isolated copies)
Unified Candidate Regression        = 0
Unresolved                          = 0
TEST Smoke / Role Gate / Lineage UAT= PASS / PASS / PASS  (213 checks, 0 failures)
Unexpected Data Loss                = 0
Migration Collision                 = 0
Bootstrap Duplicates                = 0
Security Scope Expansion            = 0
Email / Scheduler Leakage           = 0
Credential Incident                 = CLOSED
Known Baseline Defects              = 2 (order-dependent, shared with both parents)
RC Deployed                         = NO
Production Deployed                 = NO
Ready for RC Deployment             = YES — pending explicit owner authorization
```
