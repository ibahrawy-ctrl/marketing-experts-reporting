# REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_R1 — تقرير الإغلاق النهائيّ (TEST + RC)

**المرشّح:** `d25dc69` · الفرع `fix/report-template-publication-guard-r1` · سليل `origin/develop` (`2b37e39`)
**النَسَب داخل الـDLL:** `1.0.0+d25dc696556bdee50508d6129b8ce290bc36aa17`
**التاريخ:** 31 أغسطس 2026

> تقرير السبب الجذريّ ومصفوفة الأثر: `01-ROOT-CAUSE-AND-IMPACT-MATRIX.md` (المخرجان 1 و2).
> نتائج الاختبارات: `evidence/phase4/PHASE4-TEST-RESULTS.md` (المخرج 5).
> رفع حجب E0: `03-E0-UNBLOCK-CRITERIA.md` (المخرج 13).

---

## 0) بوّابة الاستلام — مُغلقة بقياس

| البند | القياس |
|---|---|
| `origin/develop` | `2b37e39` |
| دلتا `045cb0d..origin/develop` | 94 ملفًّا — **94/94 تحت `Ops/`** |
| دلتا التشغيل (`reporting-backend` + `reporting-frontend`) | **فارغة تمامًا** |
| مشاريع `Ops` داخل `Reporting.sln` | **0** |
| أساس الفرع | أُعيد تأسيسه على `2b37e39` ⟹ `HEAD` سليل `origin/develop` |
| دلتا التشغيل للمرشّح مقابل `develop` | **3 ملفّات** (تعديل `TemplateSeeder.cs` + ملفّا اختبار جديدان) |

**حالة استثنائيّة عولجت بقرار مالك المنتج:** وُجد عمل جلسة سابقة غير ملتزم على نفس التذكرة (المراحل 0–7 منفَّذة، آخر أثر 12:14 محلّيًّا). قرار المستخدم: **استئناف وتثبيت العمل القائم** لا البدء من الصفر — بعد مراجعة نقديّة كاملة وإعادة تشغيل كلّ البوابات وإعادة نشر المرشّح النهائيّ على TEST وRC.

## 1) تصحيحان أُدخلا في المراجعة النقديّة (فوق عمل الجلسة السابقة)

| # | العيب | العلاج |
|---|---|---|
| 1 | تعليق XML يتيم: وصف `ReportPublicationState` كان معلَّقًا فوق `UnpublishPredecessorsOnCreation` (كتلتا `<summary>` متراكمتان) | نُقل كلّ وصف فوق دالّته |
| 2 | `UnpublishPredecessorsOnCreation` تُعدّل `IsPublished` بلا تحديث `UpdatedAtUtc` — أثر تدقيق ناقص وخروج عن نمط المسار الرسميّ | أُضيف `previous.UpdatedAtUtc = DateTime.UtcNow` |

الثاني يمسّ فرع الإنشاء الأوّل حصرًا (غير مبلوغ على قاعدة مأهولة) ⟹ لا أثر على خمول الإقلاع.

## 2) عقد السلوك الجديد (المخرج 4)

**قاعدة جديدة فارغة**
- البذر ينشئ القوالب والإصدارات الناقصة، وينشر الإصدار الافتراضيّ **عند الإنشاء الأوّل فقط**.
- عند الإنشاء يصير المُنشَأ المنشورَ الوحيد لعائلته (`UnpublishPredecessorsOnCreation`).
- التشغيلان الثاني والثالث: **صفر صفوف متغيّرة**.

**قاعدة قائمة**
- البذر **لا يغيّر `IsPublished` لأيّ إصدار موجود** إطلاقًا.
- لا يُلغي نشر ما اختاره المستخدم أو المسار الرسميّ، ولا ينشر إصدارًا قديمًا تلقائيًّا.
- لا يُعيد كتابة Schema أو Fields أو Version metadata.
- عند تعدّد المنشورة للعائلة نفسها: **لا اختيار فائز صامت**؛ `ILogger` يُصدر `LogWarning` تشخيصيًّا (قراءة فقط)، والمصالحة تمرّ بالمسار الرسميّ أو بأداة المراجعة.
- لا يمنع الإقلاع (لا حالة تلف أو فقد بيانات تستدعي ذلك).

**قاعدة النشر ومساره**
- قرار Publish يمرّ بـ`ReportTemplateService.PublishVersionAsync` حصرًا.
- **عقد زمن التشغيل المُثبَت من الكود:** الإصدار الفعّال = **أعلى `VersionNumber` بين المنشورة** — `SubmissionService.cs:69-72` · `ReportTemplateService.cs:359` · `UnifiedReportStatusService.cs:264`.
- التقارير السابقة تبقى على `ReportTemplateVersionId` التاريخيّ. لا Rewrite ولا Migration ولا Backfill. النشر يؤثّر في الإنشاءات المستقبليّة وحدها.

**فجوة مرصودة تُرفَع كملاحظة منتج (لا حاجب):** المسار الرسميّ `PublishVersionAsync` **توسيعيّ** — ينشر الإصدار المختار **بلا إلغاء نشر سابقاته** (`ReportTemplateService.cs:702-711`)، فيبقى «الفعّال واحد» بينما «المنشورة» قد تكون أكثر من واحد. هذا هو سلوك النظام القائم قبل التذكرة وبعدها، وتعديله تغييرُ سلوكٍ خارج نطاق العيب. القياس الحاليّ: **20 عائلة متعدّدة النشر على RC · 16 على الإنتاج · 6 على TEST**. تغييره يحتاج قرار منتج مستقلًّا (انظر §7).

## 3) البوابات المحلّيّة على المرشّح النهائيّ `d25dc69` (المخرج 5)

| البوابة | الأمر | النتيجة |
|---|---|---|
| بناء Debug | `dotnet build Reporting.sln -c Debug` | `0 Warning(s)` · `0 Error(s)` |
| بناء/نشر Release | `dotnet publish src/Reporting.Api -c Release` | نجح · **48 ملفًّا** · بصمة DLLs `af849f5756892e73b0d2cc7a72d3c3c7` |
| الاختبارات الوحدويّة | `dotnet test tests/Reporting.UnitTests` | **610/610** · Failed 0 |
| اختبارات الحارس الأربعة عشر | قاعدة نظيفة `reporting_tplguard_iso` | **14/14** · Failed 0 · 658ms |
| اختبارات التكامل الكاملة | قاعدة نظيفة `reporting_tplguard_full` | **2292/2292** · Failed 0 · 8د07ث |
| نموذج/لقطة EF | `dotnet ef migrations has-pending-model-changes` | `No changes have been made to the model` ⟹ **لا هجرة** |
| فحص الأسرار | الملفّات المتغيّرة والجديدة | نظيف (مطابقتان زائفتان لكلمة `CancellationToken`) |
| بوابات الواجهة | — | **غير مطلوبة**: لا تغيير في عقد الـAPI ولا في `reporting-frontend` |

## 4) TEST — النشر وثلاثة Restarts (المخرج 7)

قاعدة TEST الحيّة = `reporting_test_uat` (من `/etc/khubara-reporting-test.env`).

نسخ احتياطيّ قبل النشر: `/root/db-backups/reporting_test_uat-tplguard-20260831T100152Z.dump` (825K) + `/opt/reporting-test/publish-backup-tplguard-20260831T100152Z`.
لقطة BEFORE: 54 صفًّا · `md5=f28c2ca49c08355636ef8bfe6c2ecd48`.

| الإقلاع | الحالة | `TOTAL` | `PUBLISHED` | `SUBMISSIONS` | الفرق مقابل BEFORE |
|---|---|---|---|---|---|
| 1 | active · `/health=200` | 54 | **41** | 24 | **0** |
| 2 | active | 54 | **41** | 24 | **0** |
| 3 | active | 54 | **41** | 24 | **0** |

الفرق محسوب على `(Id, ReportTemplateId, VersionNumber, IsPublished, PublishedAtUtc, UpdatedAtUtc)` لكلّ الصفوف ⟹ أيّ كتابة — حتّى عديمة الأثر — كانت ستظهر.

**تحقّق الأداة بعد الإقلاعات الثلاثة:** `VERIFY_ONLY` · `refusals=0` · `VERIFIED_COMPLIANT` · القوالب الأربعة `ALREADY_COMPLIANT` (المديرشن v5 · التصميم v5 · الفيديو v5 · كاتب المحتوى v6).

## 5) RC — النشر وثلاثة Restarts (المخرج 8)

نسخ احتياطيّ: `/root/db-backups/reporting_rc-tplguard-20260831T100316Z.dump` (742K) + `/opt/reporting-rc/publish-backup-tplguard-20260831T100316Z`. لقطة BEFORE: 107 صفوف.

| الإقلاع | الحالة | `TOTAL` | `PUBLISHED` | `SUBMISSIONS` | الفرق مقابل BEFORE |
|---|---|---|---|---|---|
| 1 | active | 107 | **79** | 40 | **0** |
| 2 | active | 107 | **79** | 40 | **0** |
| 3 | active · `/health=200` | 107 | **79** | 40 | **0** |

**تحقّق الأداة:** `VERIFY_ONLY` · `refusals=0` · `VERIFIED_COMPLIANT` (المديرشن v9 · التصميم v8 · الفيديو v8 · كاتب المحتوى v10).

بيانات RC كانت سليمة سلفًا ⟹ **لم تُنفَّذ مصالحة جديدة على RC** (المرحلة 7 بند 3).

## 6) أثر التقارير التاريخيّة (المخرج 9)

- `SUBMISSIONS` ثابت عبر الإقلاعات الثلاثة في البيئتين (TEST 24 · RC 40).
- بصمة التسليمات `(Id, ReportTemplateVersionId, SubmitterId, PeriodKey, Status, UpdatedAtUtc)` تُقاس في `T08_T09` وتثبت **صفر إعادة كتابة** لحالات Draft/Submitted/Closed وبقاء `ReportTemplateVersionId` نفسه.
- لا حذف إصدارات: `TOTAL` ثابت (54 · 107 · 107).
- التقارير المرتبطة بإصدارات قديمة غير منشورة تبقى مقروءة عبر لقطة إصدارها (مرجع FK) — لا يعتمد العرض على `IsPublished`.

## 7) القوالب المستهدفة للإنتاج وبصماتها (المخرج 10)

الحالة الحاليّة المقيسة على `reporting_prod` (قراءة فقط) — `TOTAL=107 · PUBLISHED=77 · MULTI_PUB_FAMILIES=16`:

| القالب | المنشور الآن (الفعّال) | `schemaVersion=2`؟ | الإصدار الصحيح | تسليمات مرتبطة بالصحيح | الحكم |
|---|---|---|---|---|---|
| تقرير المديرشن الأسبوعي | **v8** | ✗ | **v9** (أُنشئ 08-31 06:12) | 0 | **يحتاج مصالحة** |
| تقرير فريق الفيديو | **v7** | ✗ | **v8** (أُنشئ 08-31 06:12) | 0 | **يحتاج مصالحة** |
| تقرير كاتب المحتوى الأسبوعي | **v8** | ✗ | **v9** (أُنشئ 08-30 16:43) | 1 | **يحتاج مصالحة** |
| تقرير فريق التصميم | **v8** | ✓ | v8 | 1 | **سليم — لا يُمسّ** |

القوالب الثلاثة أُثبتت أهدافًا نهائيّة بمصفوفة الأثر (`01-…` §5 و§6): الإصدار الصحيح هو **الأعلى رقمًا** في عائلته، ويحمل `schemaVersion=2`، و**صفر تقارير Draft/Submitted/Closed تتأثّر بتغيير حالة نشره** (النشر يؤثّر في الإنشاءات المستقبليّة وحدها).

**مصالحة الإنتاج غير منفَّذة ومحظورة بحدود التصريح** — تنتظر موافقة صريحة.

## 8) إثبات عدم لمس الإنتاج (المخرج 12)

| الدليل | القيمة |
|---|---|
| آخر إقلاع لـ`reporting-api` | `2026-08-31 06:39:04 UTC` — **قبل بدء الجلسة** (≈09:27 UTC) ⟹ لا Restart مقصود |
| آخر تعديل لـ`/opt/reporting/publish/Reporting.Api.dll` | `2026-08-31 04:47:20 UTC` — لم يُمسّ |
| `report_template_versions` | `TOTAL=107` · `PUBLISHED=77` — **دون تغيير** |
| `max(UpdatedAtUtc)` على جدول الإصدارات | `2026-08-31 06:39:09` = لحظة إقلاع R5 ⟹ **لا كتابة بعده** |
| `max(CreatedAtUtc)` | `2026-08-31 06:12:37` ⟹ لا إصدارات جديدة |
| الهجرات | لا هجرة في المرشّح ⟹ لا تغيير بنيويّ ممكن |

**ملاحظة شفّافة:** `report_submissions` على الإنتاج تغيّر 328 ← **329** أثناء الجلسة. هذا **نشاط مستخدم حيّ**، لا كتابة منّي: كلّ عملياتي على الإنتاج كانت `SELECT` فقط، وجدول الإصدارات لم يتغيّر بأيّ مقياس.

## 9) خطّة نشر وتراجع إنتاجيّة (المخرج 11)

**التسلسل المعتمد:** Hotfix الحارس ← إعادة نشر القوالب الصحيحة ← مراقبة ← E0 وتفعيل Phase 2.

**قبل النشر**
1. Backup ثلاثيّ: `pg_dump -Fc reporting_prod` + نسخ `/opt/reporting/publish` + نسخ `dist`.
2. لقطة BEFORE: `report_template_versions` كاملة إلى CSV + `md5`.
3. تسجيل `PUBLISHED=77` و`TOTAL=107` و`SUBMISSIONS` اللحظيّ.

**النشر**
4. `rsync` من نسخة معزولة مبنيّة بـ`-p:SourceRevisionId=<sha>` ⟹ إثبات النَسَب داخل الـDLL.
5. `chown -R www-data:www-data` ثمّ `systemctl restart reporting-api`.
6. تحقّق: `/health=200` · النَسَب · **`PUBLISHED` يبقى 77 بالضبط** · فرق CSV = 0.

**المصالحة (بعد تثبيت الحارس فقط)**
7. `--verify-only` ثمّ Dry-run على القوالب الثلاثة المسمّاة ⟹ مراجعة قبل/بعد.
8. `--apply` — يمرّ بـ`PublishVersionAsync` حصرًا، ويرفض بلا كتابة عند أيّ اختلاف بصمة.
9. `PUBLISHED` المتوقّع بعدها: **77 + 3 = 80**.
10. ثلاثة Restarts ⟹ إثبات عدم التراجع.

**التراجع**
- تراجع الكود: `rsync` عكسيّ من `publish-backup-*` + Restart. الحارس القديم سيعيد الضرر تلقائيًّا ⟹ **التراجع بعد المصالحة يستلزم استرجاع القاعدة أيضًا**.
- تراجع البيانات: الأداة **لا تحذف شيئًا**؛ التراجع بإلغاء نشر الإصدار المُصالَح عبر المسار الرسميّ، أو استرجاع `pg_dump` عند الضرورة القصوى.

## 10) الحكم

```
REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_RC_PASS
STARTUP_PUBLICATION_MUTATION_ELIMINATED
THREE_AFFECTED_TEMPLATES_READY_FOR_PRODUCTION_RECONCILIATION
AWAITING_PRODUCTION_HOTFIX_APPROVAL
E0_REMAINS_BLOCKED
```

## 11) ما لم يُنفَّذ (بحدود التصريح)

نشر الإنتاج · مصالحة الإنتاج · أيّ كتابة إنتاجيّة · Restart للإنتاج · تفعيل Phase 2 · تعديل `perm` · حذف إصدارات · إعادة ربط تقارير · Merge إلى `develop` · بدء R6.

## 12) بقايا مؤقّتة على الخادم (تُنظَّف عند إغلاق التذكرة — بقرار المستخدم)

`tplguard_before` · `tplguard_repro` · `/tmp/tplguard` · نسخ `publish-backup-tplguard-*` · `/root/db-backups/*-tplguard-*.dump`.
