# بوابات الشجرة النهائيّة — المرشّح `9c7b82a`

**SHA المقيس:** `9c7b82aba2dc55b6da39188183deb44da3f69c79`
**التركيب:** `ab2e0cf` (مرشّح R5 المدموج) + `4b8902ee` (خطّ الأساس المنشور — دمج طوبولوجيّ) + `812ae903` (أحدث `origin/develop`).
**السجلّات الخام:** `logs/` بجوار هذا الملفّ.

## 0) إثبات أنّ الدمجَين لم يغيّرا شيئًا تشغيليًّا

```
git diff --stat ab2e0cf HEAD -- reporting-backend reporting-frontend   ⟹  فارغ
git diff --name-only ab2e0cf HEAD | grep -cv '^Ops/'                   ⟹  0
```

⟹ شجرة التشغيل مطابقة **بايتًا ببايت** للشجرة التي سبق أن اجتازت كلّ البوابات، وكلّ التغيير توثيقيّ تحت `Ops/`.
ورغم ذلك أُعيد القياس كاملًا على SHA النهائيّ نفسه كما يوجب التكليف.

## 1) البناء

| البوابة | النتيجة |
|---|---|
| `dotnet build Reporting.sln -c Debug` | **0 Error(s)** · 5 Warning(s) · `DEBUG_EXIT=0` |
| `dotnet build Reporting.sln -c Release` | **0 Error(s)** · 5 Warning(s) · `RELEASE_EXIT=0` |
| `npx tsc --noEmit` | **نظيف** · `TSC_EXIT=0` · مخرجات صفرية |
| `vite build` (بـ`VITE_API_BASE_URL=https://test.emarketingacademy.net/api`) | **نجح** · `FEBUILD_EXIT=0` |

## 2) الاختبارات

| المجموعة | النتيجة |
|---|---|
| الوحدويّة (`Reporting.UnitTests`) | **610 / 610** · `UNIT_EXIT=0` |
| الواجهة (`vitest run` بالمُبلِّغ الافتراضيّ) | **826 / 826** في **71** ملفًّا · `FE_EXIT=0` |
| التكامليّة — الجولة 1 | 2277 / 2278 · فشل واحد: `DirectoryNameUniquenessTests.LeadingOrTrailingWhitespace_DoesNotBypassUniqueness` |
| التكامليّة — عزل الفشل | **9 / 9** لكامل `DirectoryNameUniquenessTests` منفردًا ⟹ لا عيب في الكود |
| التكامليّة — الجولة 2 (كاملة، نفس القواعد) | **2278 / 2278** · `INT2_EXIT=0` · 14 د 55 ث |

**تحليل الفشل العابر:** الاختبار توقّع رمز خطأ مُعالَجًا فورد الخام `23505` (انتهاك تفرّد في PostgreSQL)،
أي أنّ الاستثناء سبق طبقة التحويل بسبب تسابق في الجولة المتوازية. الدليل الحاسم على أنّه ليس انحدارًا:
(أ) نجح 9/9 منفردًا، (ب) نجح ضمن الجولة الكاملة الثانية، (ج) شجرة التشغيل **مطابقة بايتًا ببايت**
للشجرة التي سجّلت 2278/2278 سابقًا (§0) ⟹ يستحيل أن يكون تغييرُ كودٍ سببَه لعدم وجود تغيير كود أصلًا.

**عزل قواعد البيانات:** ستّ قواعد **جديدة** (`reporting_fin9c7b82a_{main,cal,pfe,kpi,dec,p2}`) مُهجَّرة **تسلسليًّا**
قبل الجولة المتوازية (5 مراحل تمهيد)، وكلّ واحدة أظهرت **84 جدولًا**، وطُبِّقت عبارة
`Classification='Supplementary'` على `main` (**UPDATE 5**) كما توجب قواعد البيئة.

## 3) الهجرات

```
عدد ملفّات الهجرة في الشجرة النهائيّة = 47   (95 ملفّ .cs = 47×2 + ModelSnapshot)
خطّ الأساس المنشور 4b8902ee             = 45
هجرات موجودة في 4b8902ee وغائبة عن النهائيّ = 0
هجرات مُعدَّلة أو محذوفة                    = 0  (المعدَّل الوحيد AppDbContextModelSnapshot.cs وهو مولَّد لا هجرة)
```

**الهجرتان المضافتان — فحص `Up()` و`Down()`:**

| الهجرة | `Up()` | `Down()` |
|---|---|---|
| `20260826185232_AddSubmissionFieldValueJsonGinIndex` | `CreateIndex` واحد فقط (gin · `jsonb_path_ops`) على `submission_field_values.ValueJson` | `DropIndex` مقابل |
| `20260829214324_R5_DecOneCadenceEffectivityAndEmploymentWindow` | 4 × `AddColumn` **`nullable: true`** (`EffectiveFrom`/`EffectiveTo` على `kpi_template_assignments` · `HireDate`/`ExitDate` على `AspNetUsers`) + `CreateIndex` واحد | `DropIndex` + 4 × `DropColumn` |

⟹ **إضافيّة بحتة**: صفر `DROP TABLE` · صفر `NOT NULL` بلا افتراضيّ · صفر تعديل بيانات · صفر `UPDATE`/`DELETE`.
`Down()` عكسيّة كاملة ومتّسقة في الحالتين.

**مطابقة مع الإنتاج (قراءة فقط):** `reporting_prod` يحمل **47** صفًّا في `__EFMigrationsHistory`.
الفارق عن الكود مفسَّر بالكامل: صفّان تاريخيّان من تصادم النَسَب الموثَّق سابقًا
(`20260622144900_KpiTemplateAssignmentsPhaseT1` · `20260626135944_AddReportViewGrants` — أداة `Ops/MigrationHistoryBridge`)
وهما **غائبان عن `4b8902ee` نفسه بنفس الصورة**، أي ليسا انحدارًا أحدثه المرشّح.
الهجرتان اللتان ستُطبَّقان عند النشر = **`AddSubmissionFieldValueJsonGinIndex` و`R5_DecOneCadence…` فقط**.

## 4) فحص الأسرار والملفّات المولَّدة

| الفحص | النتيجة |
|---|---|
| أنماط الأسرار في كامل فرق `origin/develop...HEAD` (JWT · Bearer · سلسلة اتصال بكلمة مرور · مفاتيح خاصّة) | **0** |
| `me_access`/`me_refresh` | ورود **توثيقيّ واحد** داخل نصّ تقرير UAT كمصطلح، لا قيمة رمز |
| ملفّات `bin/` · `obj/` · `node_modules/` · `dist/` داخل الفرق (194 ملفًّا) | **0** |
| ملفّات `.env` · `secrets` · `.pem` · `.key` | **0** |
| نفس الأنماط في كامل شجرة `HEAD` | **0** |

## 5) الانحرافات التوثيقيّة المصحَّحة (DEV-02 · DEV-03)

- **`DEV-02` — صُحِّح:** أُضيفت في `R5-CLOSURE-REPORT.md` (سطر بناء الواجهة) تصحيحةٌ صريحة تبيّن أنّ
  `index-BEPw02yj.js`/`6de99bfccf274bd2` بصمة **بائتة** من جولة أسبق، وأنّ الحزمة المنشورة والمُختبَرة فعلًا هي
  `index-DPf3EPx4.js` (`md5 = 6d7bfb75b27d989a97a21f353fcddee3`، متطابقة محلّيًّا وعلى الخادم).
  **القياسات التاريخيّة تُركت كما سُجِّلت والتصحيح مضاف لا بديل**، امتثالًا لشرط عدم تعديل نتائج القياس الأصليّة.
- **`DEV-03` — مُغلَق قياسًا مسبقًا:** التوصيف الدقيق مسجَّل في `MERGED-CANDIDATE-INTEGRATION-REPORT.md`
  (§2 السطران 42 و50، و§11 البند `DEV-03`): `36a6a5b` **يعدّل كودًا إنتاجيًّا** (`ReportTemplateService.cs`، 9 أسطر)
  خلافًا لما ورد في نصّ التكليف، وتماسّه مع أسطح R1–R5 قيس فكان **صفر ملفّ**. لا نصّ غير دقيق متبقٍّ.
