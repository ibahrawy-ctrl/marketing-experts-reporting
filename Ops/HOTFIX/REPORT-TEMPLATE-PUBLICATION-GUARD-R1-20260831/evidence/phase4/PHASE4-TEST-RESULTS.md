# المرحلة 4 — الاختبارات الإلزاميّة ونتائجها المقيسة

التذكرة: `REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_R1` · الفرع `fix/report-template-publication-guard-r1` · الشجرة `/Users/ibrahimelbahrawi/hotfix-tpl-guard-r1`

## 1) الاختبارات الإلزاميّة الأربعة عشر

ملفّ الاختبارات: `reporting-backend/tests/Reporting.IntegrationTests/TemplateSeederPublicationGuardTests.cs`
المصنع المعزول: `TemplateSeederPublicationGuardIsolatedFactory.cs` — قاعدة `reporting_tplguard_iso` (متغيّر `TEST_DB_CONNECTION_TPLGUARD`)، منفصلة عن `reporting_test` المشتركة الملوَّثة كي يكون «قاعدة فارغة تُبذَر مرّة» حتميًّا.

**النتيجة: `Passed! — Failed: 0, Passed: 14, Skipped: 0, Total: 14`**

| # | المطلب | الاختبار | الحالة |
|---|--------|----------|--------|
| 1 | قاعدة فارغة تُبذَر مرّة | `T01_EmptyDatabase_IsSeededOnce_WithExactlyOnePublishedVersionPerNewFamily` | PASS |
| 2 | الإقلاع الثاني لا يغيّر شيئًا | `T02_T03_SecondAndThirdSeedRuns_ChangeNoRow` | PASS |
| 3 | الإقلاع الثالث لا يغيّر شيئًا | `T02_T03_...` (نفسه) | PASS |
| 4 | إصدار أحدث منشور لا يُلغى نشره | `T04_NewerPublishedVersion_IsNeverUnpublishedByTheSeeder` | PASS |
| 5 | إصدار أقدم غير منشور لا يصير منشورًا | `T05_OlderUnpublishedVersion_IsNeverAutoPublishedByTheSeeder` | PASS |
| 6 | إصداران منشوران ⟹ لا اختيار فائز صامت | `T06_TwoPublishedVersions_SeederDoesNotSilentlyPickAWinner` | PASS |
| 7 | المسار الرسميّ يجعل المختار وحيدًا | `T07_OfficialPublishPath_MakesChosenVersionTheEffectiveOne` | PASS |
| 8 | التقارير التاريخيّة تبقى على نفس `VersionId` | `T08_T09_HistoricalSubmissions_KeepTheirVersionAndAreNeverRewritten` | PASS |
| 9 | Draft/Submitted/Closed لا يُعاد كتابتها | `T08_T09_...` (نفسه) | PASS |
| 10 | `schemaVersion=2` يبقى منشورًا بعد 3 إقلاعات | `T10_T11_AffectedTemplate_KeepsItsEffectiveVersion_AcrossThreeRestarts` | PASS |
| 11 | القوالب الأربعة المتضرّرة باختبارات انحدار بالاسم | `T10_T11_...` — `[Theory]` بالعناوين الأربعة | PASS |
| 12 | R22A/R22B لا تنحدر | `T12_R22A_R22B_WorkItemsStructure_SurvivesThreeRestarts_WhenPresent` | PASS |
| 13 | R5 وKPI غير متأثّرة | `T13_KpiTemplatesAndVersions_AreUnaffectedByRepeatedSeeding` | PASS |
| 14 | لا كتابة عند Startup على قاعدة مستقرّة | `T14_StableDatabase_SeederPerformsNoWrite_AcrossTemplatesAndSubmissions` | PASS |

**آليّة كشف الكتابة:** بصمة `(Id\|ReportTemplateId\|VersionNumber\|IsPublished\|PublishedAtUtc\|PublishedById\|UpdatedAtUtc)` لكلّ الإصدارات. وجود `UpdatedAtUtc` يجعل أيّ كتابة — حتّى الكتابة عديمة الأثر — مكشوفة، لا حالة النشر وحدها.

## 2) المجموعات الكاملة

| المجموعة | الأمر | النتيجة |
|---|---|---|
| بناء Debug | `dotnet build … -c Debug` | `0 Error(s)` · `Build succeeded` |
| بناء/نشر Release | `dotnet publish src/Reporting.Api -c Release -o /tmp/tplguard-publish` | نجح · 48 ملفًّا · بصمة DLLs `54c0328f4262a2e90775…` |
| الاختبارات الوحدويّة | `dotnet test tests/Reporting.UnitTests` | **610/610** · Failed 0 |
| اختبارات التكامل الكاملة | `dotnet test tests/Reporting.IntegrationTests` على قاعدة نظيفة `reporting_tplguard_full` | **2292/2292** · Failed 0 · 8د10ث |
| نموذج/لقطة EF | `dotnet ef migrations has-pending-model-changes` | `No changes have been made to the model since the last migration` ⟹ **لا هجرة مطلوبة** |
| فحص الأسرار | `grep` على الملفّات المتغيّرة والجديدة | نظيف (المطابقات الوحيدة كانت ثنائيّات `bin/obj` وحُذفت) |

> ملاحظة: البند 0ب من طابور الذاكرة («8 اختبارات تكامل تفشل على قاعدة نظيفة في مجال الحالة الموحّدة») **لم يَعُد قائمًا** في هذا القياس: 2292/2292 خضراء على قاعدة نظيفة.

## 3) تصحيح اكتُشِف بالاختبارات نفسها (T05)

الصياغة الأولى للإصلاح أزالت إلغاء نشر السابقات من **فرعَي الترقية معًا**: فرع «الترقية مطبَّقة سلفًا» (وهو موضع العطب الحقيقيّ) و**فرع الإنشاء**. النتيجة المقيسة على قاعدة فارغة: أربعة إصدارات منشورة لكلّ عائلة مُرقّاة بدل واحد — مخالفة صريحة لقاعدة النشر في العقد («الإصدار المنشور حديثًا يصير المنشور الوحيد لعائلته»). كشف ذلك `T05` بفشل افتراضه المرجعيّ.

**العلاج:** `UnpublishPredecessorsOnCreation(template, created)` تُستدعى في فروع الإنشاء الثلاثة **حصرًا**. الفرق الجوهريّ عن العطب الأصليّ:

- فرع الإنشاء لا يُبلَغ أصلًا إلّا حين تكون الترقية ناقصة؛ وبعد إنشائها مرّة واحدة يخرج البذر مبكّرًا بلا أيّ كتابة في كلّ إقلاع لاحق ⟹ **idempotent**.
- العطب الأصليّ كان يفرض حالة النشر في **كلّ إقلاع** على عائلات مُرقّاة سلفًا، فيختار فائزًا بترتيب `Include` (تصاعد GUID عشوائيّ) لا بعقد زمن التشغيل.

**القياس بعد العلاج على قاعدة فارغة (`reporting_tplguard_iso`):** لا توجد **أيّ** عائلة عدد منشوراتها ≠ 1 · 34 منشورًا / 46 إصدارًا.

## 4) إعادة التحقّق من عدم انحدار المرحلة 2ب بالمرشّح المُحدَّث

أُعيد بناء Release ورُفع إلى `/tmp/tplguard/app-new`، وأُعيدت قاعدة التكرار من نسخة الإنتاج الاحتياطيّة.

| القياس | القيمة |
|---|---|
| بعد `reset.sh` | `NOT_OWNED=0` · `TOTAL=107` · `PUBLISHED=81` · `MIGRATIONS=47` |
| الإقلاع 1 | `HEALTH=200` · `UPDATE_report_template_versions=0` · `UPDATE_report_templates=0` · `INSERT=0` · `PUBLISHED=81` · `DIAGNOSTICS=24` |
| الإقلاع 2 | مطابق تمامًا — `UPDATE=0` · `PUBLISHED=81` |
| الإقلاع 3 | مطابق تمامًا — `UPDATE=0` · `PUBLISHED=81` |
| الفرق الكامل مقابل المرجع `tplguard_before` على `(Id, IsPublished, PublishedAtUtc)` | `DIFF_LINES=0` على `ROWS=107` |
| الفائز الفعليّ · عدد المنشورة · أرضيّة الانطباق | المديرشن `v9`/2/`08-23 16:48:14` · التصميم `v8`/2/`08-23 16:48:14` · الفيديو `v8`/2/`08-23 16:48:14` · كاتب المحتوى `v9`/2/`08-23 16:48:14` |
| التسليمات | 328 = 328 المرجعيّة |

⟹ إضافة `UnpublishPredecessorsOnCreation` **لم تُحدث أيّ انحدار** على قاعدة مأهولة مُرقّاة سلفًا، لأنّ فرع الإنشاء لا يُبلَغ فيها إطلاقًا.

## 5) الإنتاج

لم تُجرَ أيّ كتابة على الإنتاج في هذه المرحلة: كلّ العمل على `reporting_tplguard_iso` و`reporting_tplguard_full` محلّيًّا، و`tplguard_before`/`tplguard_repro` على الخادم — وكلّها قواعد معزولة مؤقّتة.
