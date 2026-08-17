# بيان الأثر — مرشّح الإصدار `rc-p360-wf-r2-20260817`

**التذكرة:** `PROJECT360-OPERATIONAL-WORKFLOW-CLOSURE-R2`
**التاريخ:** ١٧ أغسطس ٢٠٢٦
**الحالة:** مرشّح مُعَدّ وغير منشور. **لم تُمسّ بيئة RC ولا بيئة الإنتاج.**

---

## 1) هويّة المرشّح

| البند | القيمة |
|---|---|
| الوسم | `rc-p360-wf-r2-20260817` (وسم موصوف — annotated) |
| الالتزام | `d66360f665605d62b546a6a1004f7f690449bc02` |
| الفرع | `origin/develop` (الوسم على رأسه بالضبط) |
| الالتزام الأساس قبل التذكرة | `95cc146` هو آخر التزام مسّ `reporting-backend` |
| حالة الوسم | **مُنشأ محلّيًّا ولم يُدفَع** — دفعه يحتاج تصريحًا صريحًا مستقلًّا |

### الالتزامات التي يضمّها المرشّح فوق ما سبق

```
d66360f  fix(projects-ui): stop the edit form from reporting an existing assignment as none
c43c70e  fix(governance): let a risk say which project it belongs to
477a682  fix(project360): let the executor reach the screen the server already opened for him
defcfcd  fix(project360-ui): make the weighted progress mode reachable at all
31feed4  fix(projects-ui): let the person assigning project roles actually assign them
95cc146  test(projects): expect the answer that refuses to confirm a project exists
b20b5a8  test(project360): prove the bridge separates claiming execution from deciding it
177b477  fix(project360): stop the dashboard from scoring a project the stored column refuses to score
e979588  feat(project360): connect what the team executed to what the client was promised
23533fa  feat(governance): make a risk on a project actually reach the project
```

**حقيقة تخصّ الخادم:** كلّ ما بعد `95cc146` تغييرات واجهة خالصة.
البرهان: `git diff 95cc146..d66360f -- reporting-backend` = **فارغ**.
⟹ شجرة الخادم في `d66360f` مطابقة لشجرة الخادم في `95cc146` حرفيًّا.

---

## 2) الهجرات

| البند | القيمة |
|---|---|
| ملفّات الهجرة في المرشّح | **39** |
| الرأس | `20260817114129_AddProjectExecutionUpdateProposals` |
| الهجرتان المضافتان في هذه التذكرة | `20260817101108_AddProjectProgressAndHealthStates` · `20260817114129_AddProjectExecutionUpdateProposals` |
| صفوف سجلّ الهجرات على TEST | **40** = 39 هجرة مطبَّقة + صفّ `20260622180127_LeaveBalanceGuardSnapshot` الموروث من جسر توحيد النَسَب |
| فرق «مطبَّق بلا ملفّ» | صفّ واحد فقط هو صفّ الجسر أعلاه |
| فرق «ملفّ بلا تطبيق» | **صفر** |

كلتا الهجرتين **إضافيّتان بحتًا** ولهما `Down` قابل للتنفيذ.

---

## 3) أثر الواجهة (Frontend)

| البند | القيمة |
|---|---|
| ملفّ الحزمة | `assets/index-CMcdvlZM.js` |
| ملفّ الأنماط | `assets/index-BejvRoEu.css` |
| عدد ملفّات `dist` | 7 |
| **البصمة الكلّيّة** | `14b2ee3694df6b971eda37a77672f462614e8b73ef36e52b8fbfb45b0204f627` |
| المنشور على TEST | **البصمة نفسها حرفيًّا** — قُرئت من `/opt/reporting-test/frontend/dist` |

### إعادة الإنتاج مُثبَتة

أُعيد بناء الواجهة من `d66360f` في نسخة عمل معزولة فأعطت **نفس اسم الملفّ ونفس البصمة الكلّيّة**:

```
VITE_API_BASE_URL=https://test.emarketingacademy.net/api npx vite build
```

**قيد حاكم يجب ألّا يُنسى:** بصمة الحزمة **تابعة لقيمة `VITE_API_BASE_URL`**.
بناءٌ بقيمة `/api` أعطى حزمة مختلفة (`index-UGeKbXPt.js`، بصمة `a55a8fba…`)، وبناءٌ بلا القيمة يسقط
إلى `http://localhost:5090/api` فيُنتِج واجهة معطوبة على أيّ بيئة.
⟹ **حزمة RC وحزمة الإنتاج ستختلفان بالبصمة عن حزمة TEST بحكم التصميم**، ولا يصحّ مقارنتها بها.
البوّابة الصحيحة لكلّ بيئة: التحقّق من قاعدة الـAPI المخبوزة داخل الحزمة قبل النشر، ثمّ دخان متصفّح حقيقيّ بعده.

---

## 4) أثر الخادم (Backend)

نُشِرت حزمة `Release` من `d66360f` إلى `/tmp/p360wfr2-rc/backend-publish`:

| التجميعة | SHA-256 (بناء macOS من `d66360f`) |
|---|---|
| `Reporting.Api.dll` | `bbf587d67a0a55a331358174a57ae1d249baa8d4434dd9ca4897f85f0af23d81` |
| `Reporting.Application.dll` | `4ae8b5c5839e4fc3fcd70c5fe8ee40cf1ff394764fd7afd00a22f328e098abf8` |
| `Reporting.Domain.dll` | `168d180b5bb435e63a4cf7901843e52cee9c808a22a6dd0bf6ba5546f1a4c1c1` |
| `Reporting.Infrastructure.dll` | `64045734f0df7547beb41fcd32c5394d09a4f55a061329effc2f9a4d379ba3d5` |

التجميعة تحمل هويّة مصدرها: `AssemblyInformationalVersion = 1.0.0+d66360f665605d62b546a6a1004f7f690449bc02`.

### لماذا لا تُقارَن هذه البصمات ببصمات TEST

بصمات `Reporting.*.dll` على TEST مختلفة (`5052953e…` وغيرها)، **وهذا متوقَّع لا مُريب**:
بناء .NET غير مُعاد الإنتاج بايتًا عبر منصّتين مختلفتين (macOS محلّيًّا مقابل Linux على الخادم)،
فيختلف `MVID` ومسارات التصحيح المضمَّنة. **مقارنة البصمات هنا ليست برهان هويّة ولا نقضها.**

**برهان الهويّة المعتمد بدلًا منها — على مستوى المصدر:**
شجرة `reporting-backend` في `d66360f` مطابقة لشجرتها في `95cc146` (فرق فارغ)،
وهي الشجرة التي بُنِيت ونُشِرت على TEST ونُفِّذت عليها دورة UAT كاملة.

---

## 5) البوّابات المجتازة قبل تجميد المرشّح

| البوّابة | النتيجة |
|---|---|
| `tsc -b` (الواجهة) | نظيف — بلا خطأ |
| اختبارات وحدة الواجهة (vitest) | **577 / 577** في 50 ملفًّا |
| اختبارات الخادم على قاعدة نظيفة معزولة | مُجتازة (عدا عيبَي الأساس المعروفَين `BASELINE-DEFECT-01/02` وهما خارج نطاق هذه التذكرة) |
| تصادم معرّفات الهجرات | صفر |
| مزامنة نموذج EF (`Model Sync`) | نظيفة |
| دخان متصفّح حقيقيّ على TEST | صفر خطأ تطبيقيّ في وحدة التحكّم |

**قاعدة مُلزِمة طُبِّقت:** بوّابة الواجهة هي `tsc -b && vite build`، لا `vite build` وحده.

---

## 6) التحقّق الميدانيّ على TEST (لا على الورق)

| البند | القيمة |
|---|---|
| البيئة | `https://test.emarketingacademy.net` |
| الحسابات المستعملة | سبعة أدوار مستقلّة + حساب موظّف من خارج الفريق |
| مشروع الاختبار | `3b65b90c-a1f0-4ab5-959d-3eca39053ca1` |
| عميل الاختبار | `173794e1-855c-4680-af59-8e03a97bbb1c` |
| دورة جسر التنفيذ | كاملة: ادّعاء ← قبول ← إعادة قبول (بلا أثر مزدوج) ← ادّعاء ← رفض معلَّل |
| منع التعداد | 404 موحّد على خمسة مسارات لحسابَين خارج النطاق |
| الإفصاحات الخمسة للتقدّم | مقيسة نصًّا، ومنها تحذير سقوط الأوزان مُختبَر حيًّا ثمّ أُعيدت الأوزان |
| اللقطات | 15 لقطة في `Docs/Guides/Marketing-Experts-Client360-Project360-Guide-Assets-R2/` |

---

## 7) مخرَجات التوثيق المرافقة

| الملفّ | SHA-256 |
|---|---|
| `…Operating-and-UAT-Guide-AR-R2.docx` | `a072f9819b5bce7af8539fe1af38992a33c1f76ec7c45bb6cc9128e38cb45335` |
| `…Operating-and-UAT-Guide-AR-R2.pdf` | `8c082ec0dcffd7e7e19f55f993cb3e64b6de5ded74c05556bccb58b4ffdc79d7` |
| `…UAT-Workbook-AR-R2.docx` | `597314fa97cc65ca620916de1f20ec9ef16f033c759fa069c3deaccc66c07fdc` |

الدليل: 155 صفحة · 60 شكلًا (45 من R1 + 15 جديدة) · 115 حالة UAT في 12 حزمة.
مسارها `Docs/Guides/` وهي **غير مُتتبَّعة في المستودع** لأنّ `Docs/*` مستبعَد في `.gitignore:45` — شأن مخرَجات R1 نفسه.

---

## 8) إثبات عدم المساس بـRC والإنتاج

قراءة فقط، بلا أيّ كتابة:

| البيئة | آخر تعديل للخادم | آخر تعديل للواجهة | صفوف سجلّ الهجرات | الرأس |
|---|---|---|---|---|
| **RC** (`reporting_rc`) | 2026-08-16 17:49 | 2026-08-16 17:54 | 40 | `20260811142239_AddProject360Foundation` |
| **الإنتاج** (`reporting_prod`) | 2026-08-07 08:52 | 2026-08-12 20:03 | 30 | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |

كلا التاريخين **يسبق يوم هذه التذكرة (١٧ أغسطس)**، والرأسان لم يتحرّكا.
`RC_WRITE_OR_DEPLOY` و`PRODUCTION_WRITE_OR_DEPLOY` بقيا **غير مُصرَّح بهما ولم يُنفَّذا**.

---

## 9) ما يحتاجه هذا المرشّح قبل أيّ نشر

1. **تصريح صريح جديد** لكلّ عمليّة نشر على حدة (RC ثمّ الإنتاج) — التصريح السابق لا يمتدّ.
2. **دفع الوسم** `rc-p360-wf-r2-20260817` إلى `origin` — يحتاج تصريحًا مستقلًّا؛ الوسم اليوم محلّيّ.
3. **اعتماد مالك المنتج** لنتائج UAT الوظيفيّة — الاختبار التقنيّ لا يغني عنه.
4. **إغلاق `PROD-READINESS-01`** قبل الإنتاج: الإنتاج بلا `FileStorage__DocumentsRootPath`، فيسقط جذر المستندات
   داخل مجلّد `publish` المستبدَل في كلّ نشر = فقدان صامت لمستندات العملاء. حاجب **إعداد** لا شيفرة.
5. **بناء واجهة مستقلّ لكلّ بيئة** بقيمة `VITE_API_BASE_URL` الخاصّة بها، مع فحص القاعدة المخبوزة داخل الحزمة
   قبل النشر ودخان متصفّح حقيقيّ بعده.
