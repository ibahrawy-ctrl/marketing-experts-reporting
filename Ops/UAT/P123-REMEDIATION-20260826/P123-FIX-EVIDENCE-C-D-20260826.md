# P123 — أدلّة الإصلاح: Phase C (DEF-P123-003) و Phase D (DEF-P123-001/002)

- **التاريخ:** 26 أغسطس 2026 · **الفرع:** `feature/p123-remediation-20260826` (worktree معزول)
- **البيئة:** محلّيّة فقط في هذه المرحلة. **لم تُمسّ RC ولا Production ولا TEST.**

## Phase C — DEF-P123-003 (P2): الموظّف يرى مسودّة الواقعة قبل إرسالها

### جذر السبب (مقيس لا مُستنتَج)
`Reporting.Application/Attendance/AttendanceAccess.cs:52` — التوقيع القديم
`CanViewIncident(FieldVisibilityContext, Guid reportedByUserId)` **لا يقبل الحالة إطلاقًا**،
ويبدأ بـ`if (ctx.IsSelf) return true;` ⇒ التفريق بين `Draft` و`Reported` مستحيل بنيويًّا.

### إعادة الإنتاج قبل الإصلاح
اختبار مؤقّت `ZzBaselineReproTests` على التوقيع القائم أثبت `CanViewIncident(ctx_self, reporter) == true`
(`Passed! Failed: 0, Passed: 1`) ثمّ حُذف بعد التقاط الدليل.

### الإصلاح
- حدّ الرؤية اشتُقّ من جدول الانتقالات لا من التخمين: `AttendanceWorkflow.cs` يحوي
  `[(Draft, Submit)] = Reported` و`[(Draft, Cancel)] = Cancelled`، و`Cancelled` **لا تُبلَغ إلّا من `Draft`**
  ⇒ مجموعة «ما قبل الإرسال» هي بالضبط `{Draft, Cancelled}`.
- `IsPreSubmission(status)` جديدة، والتوقيع صار ثلاثيًّا بالحالة.
- **البوّابة على فرع `IsSelf` وحده** عمدًا: لا مساس برؤية المشرف لمسودّات غيره (خارج نطاق العيب ⇒ لا انحدار مُقحَم).
- موضعا النداء في `AttendanceService.cs` (`LoadVisibleAsync` ~806، `LoadForWriteAsync` ~824) يمرّران `incident.Status`.
- الحجب يُترجَم في نقطة النهاية إلى `attendance.not_found` (404) لا 403 — لا تسريب للوجود.

### الدليل بعد الإصلاح
`tests/Reporting.UnitTests/AttendanceDraftPrivacyTests.cs` — **8/8 Passed**، منها:
الموضوع لا يرى `Draft` ولا `Cancelled` · يرى `Reported` · يرى **كلّ** حالة بعد الإرسال (Theory على قيم enum كاملة)
· المُبلِّغ يرى مسودّته · حامل مفتاح `Attendance.Review` يرى المسودّة · الغريب لا يكتشف الواقعة في أيّ حالة
· المشرف داخل النطاق ما زال يرى المُرسَلة (حارس انحدار).

## Phase D — DEF-P123-001 (P2) و DEF-P123-002 (P3)

### جذر السبب
- **001:** صفر تحقّق تفرّد في `DirectoryService.CreateDepartmentAsync` / `UpdateDepartmentAsync` /
  `CreateTeamAsync` / `UpdateTeamAsync`، وصفر فهرس فريد على `NameAr` في `OrgConfigurations.cs`.
- **002:** `OrgConfigurations.cs:17` يفرض فهرسًا فريدًا على `departments.Code`، ولا يوجد أيّ التقاط لـ
  `DbUpdateException`/`23505` ⇒ الاستثناء يصعد غير مُترجَم ⇒ **500 بجسم فارغ**.

### قرار العقد (مُوثَّق لا مُفترَض)
| البند | القرار | المبرّر |
|---|---|---|
| نطاق تفرّد اسم الإدارة | الشركة كلّها | لا معنى لإدارتين بالاسم نفسه في هيكل واحد |
| نطاق تفرّد اسم الفريق | **داخل إدارته فقط** | فريق «المبيعات» في إدارتين مختلفتين تسمية مشروعة |
| التطبيع | `Trim()` **فقط** | الخدمة تخزّن `Trim()` حرفيًّا؛ أيّ تطبيع أوسع (case-insensitive) يجعل الفحص التطبيقيّ يخالف قيد القاعدة ⇒ فجوة صامتة. لا يُخترَع عقد غير منصوص |
| رمز الإدارة | فريد حيث `Code IS NOT NULL` | القيد كان قائمًا أصلًا؛ الناقص هو الترجمة فقط |

### الحماية المزدوجة
1. **تحقّق تطبيقيّ مسبق** في الدوالّ الأربع ⇒ رسالة عربيّة مفهومة + رمز دلاليّ.
2. **فهرس فريد في القاعدة** ⇒ الضمانة النهائيّة ضدّ التسابق (فحص-ثمّ-كتابة لا يمنعه).
3. **مترجم `23505`** (`SaveTranslatingDirectoryConflictsAsync`) يحوّل **الفهارس الثلاثة المعروفة حصرًا** إلى
   نفس الرموز الدلاليّة، و**يُعيد رمي** أيّ انتهاك آخر عبر `ExceptionDispatchInfo` (حفظ المكدّس) — لا ابتلاع أخطاء.

الرموز: `department.name.conflict` · `department.code.conflict` · `team.name.conflict`.
لا حاجة إلى سباكة جديدة: `ApiControllerBase.ToProblem` يحوّل أيّ رمز ينتهي بـ`.conflict` إلى **409 + RFC 7807** تلقائيًّا.

### مطابقة أسماء القيود (مقيسة على الخادم والمحلّيّ — شرط صحّة الترجمة)
| ثابت في الكود | `pg_indexes` على `reporting_test_uat` | اسم القيد في رسالة 23505 |
|---|---|---|
| `IX_departments_Code` | موجود | — (قائم قبل الهجرة) |
| `IX_departments_NameAr` | يُنشأ بالهجرة | **`IX_departments_NameAr`** (مقيس حرفيًّا بـ`VERBOSITY verbose`) |
| `IX_teams_DepartmentId_NameAr` | يُنشأ بالهجرة | يطابق اسم الفهرس (PostgreSQL يعيد اسم الفهرس نفسه) |

### الهجرة `20260826073223_P123DirectoryNameUniqueness`
إضافيّة بحتة (فهرسان فريدان)، ويسبقها **حارس Preflight داخل الهجرة نفسها**: الهجرات تُطبَّق تلقائيًّا
عند الإقلاع، فلو وُجد تكرار قائم لفشل الإقلاع بخطأ Postgres غامض. الحارس يستبدله برسالة تذكر العدد
والاسم المتضارب، و**لا يحذف ولا يدمج أيّ صفّ** — معالجة البيانات قرار تشغيليّ صريح خارج الهجرة.

### مصفوفة اختبار الهجرة (نُفِّذت فعلًا — محلّيًّا)
| الحالة | القاعدة | النتيجة المقيسة |
|---|---|---|
| قاعدة فارغة | `reporting_p123_remed_iso` | طُبِّقت؛ الفهرسان موجودان في `pg_indexes` |
| نسخة مأهولة **بها تكرار** | `reporting_p123_dupguard` | **مُنِعت** برسالة `P123-PREFLIGHT: 1 duplicate department NameAr group(s) … (e.g. إدارة-مكرّرة)` |
| إثبات التراجع الذرّيّ بعد الفشل | نفسها | `migration_recorded=0` · `index_present=0` · `depts_still=2` (**لا فقدان بيانات**) |
| بعد حلّ التكرار | نفسها | `Applying … Done.` |
| التكرار (Idempotency) | نفسها | `No migrations were applied. The database is already up to date.` |
| `Down` / الرجوع | نفسها | `Reverting … Done.` · `index_after_down=0` |

### الدليل بعد الإصلاح
`tests/Reporting.IntegrationTests/DirectoryNameUniquenessTests.cs` — **9/9 Passed** على قاعدة معزولة نظيفة:
إنشاء إدارة باسم مكرّر ⇒ 409 وصفّ واحد فقط · رمز مكرّر ⇒ **409 لا 500** · تعديل إلى اسم محجوز ⇒ 409 بينما
حفظ الاسم الحاليّ ينجح (استثناء الصفّ نفسه) · فريق مكرّر داخل الإدارة ⇒ 409 · **الاسم نفسه في إدارة أخرى ⇒ 200**
· إعادة تسمية فريق على اسم شقيق ⇒ 409 · **6 طلبات متزامنة** لإدارة واحدة ⇒ نجاح واحد + الباقي 409 + **صفّ واحد**
· نفس الشيء للفرق · الفراغات الطرفيّة لا تلتفّ حول التفرّد.
كلّ تأكيد 409 يتحقّق أيضًا من **عدم تسريب** `IX_` ولا `23505` ولا `duplicate key` في الجسم.

## البناء
`dotnet build Reporting.sln` ⇒ **0 Errors**، 4 تحذيرات **سابقة الوجود** في مشروع اختبارات التكامل (`CS8604`) لا علاقة لها بهذا العمل.

## قواعد بيانات محلّيّة أنشأتُها في هذه المرحلة (للتنظيف لاحقًا)
`reporting_p123_remed_iso` · `reporting_p123_dupguard` — كلتاهما محلّيّتان على جهاز التطوير، بلا أيّ بيانات حقيقيّة.
