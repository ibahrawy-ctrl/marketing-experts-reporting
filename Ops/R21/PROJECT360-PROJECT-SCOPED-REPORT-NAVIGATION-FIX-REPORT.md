# PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1 — التقرير النهائيّ

**التاريخ:** 26 أغسطس 2026 · **البيئة المستهدَفة:** TEST حصرًا · **الإنتاج وRC لم يُنشَر عليهما**
**المرشّح:** `63b7d42f2d0cc54899b22cc919045389c23b2ec7` · **القاعدة:** `d937788` (خطّ TEST القائم، باختيار مالك المنتج)

---

## 1) خلاصة تنفيذيّة

بُلِّغ عن ثلاث مشكلات في صفحة مشروع على الإنتاج. القياس فصلها إلى **عطل واحد حقيقيّ + خطر تصميميّ حقيقيّ + شكوى بلا عطل تقنيّ**:

| البلاغ | ما ثبت بالقياس | القرار |
|---|---|---|
| زرّ «مساحة عمل المشروع (360)» لا يفتح | **لا عطل تقنيّ**: سجلّات nginx الإنتاجيّة تُظهر `GET /api/projects/…/overview` = **200 / 1597 بايت** مع `Referer` = صفحة `/360`. المشروع المعنيّ ببساطة بلا أهداف ولا KPI ولا مُخرجات ⟹ مساحة عمل شبه فارغة قُرئت كـ«لم يفتح» | لا إصلاح وظيفيّ. أُغلقت **صفحتان بيضاوان صامتتان** (`return null`) وقائيًّا لأنّهما تُنتجان بالضبط هذا الالتباس |
| زرّ «فتح» بجانب تقرير الموظّف لا يفتح | **عطل حقيقيّ**: `LinkedReportsCard` كان يوجّه دائمًا إلى `/app/submissions?open={id}` — أي إلى **قسم التقارير العامّ**، فيُفقَد `projectId` | أُصلح: الوجهة صارت مقيَّدة بالمشروع |
| الخطر الأكبر: عرض التقرير الكامل من داخل مشروع | **مؤكَّد وخطير**: التقرير الأسبوعيّ الواحد يحمل عمل عدّة مشروعات، وفتحه عامًّا من داخل مشروع أ كان يعرض عمل مشروع ب | أُغلق **خادميًّا** بنقطة نهاية مقيَّدة بالمشروع |

**النتيجة:** التصفية خادميّة بالكامل، لا تصفية تجميليّة في الواجهة، صفر تسريب عبر المشروعات في 56 قياسًا مستقلًّا، و**صفر مسّ** للإنتاج وRC و`origin/main`.

---

## 2) السبب الجذريّ لكلّ عطل

### 2.1 `ROOT_CAUSE_PROJECT360 = NO_REPRODUCIBLE_TECHNICAL_FAULT`
المسار `/app/projects/:projectId/360` مسجَّل صحيحًا (`App.tsx:183`) و`Project360Page` يُصيَّر فعلًا. أُثبت ذلك بمسبار DOM مستقلّ على TEST: `H1 = اسم المشروع` (`Project360Page.tsx:131`)، حاوية `Tabs ariaLabel="أقسام مساحة عمل المشروع"` حاضرة (`:194`)، ورابط 360 الخاصّ بصفحة التفاصيل **غائب** ⟹ لسنا على صفحة التفاصيل. وسجلّ الإنتاج يثبت أنّ النداء نجح وقت البلاغ.

**العيب الكامن الذي أُصلح رغم ذلك:** خروجان مبكران بـ`return null` كانا يُنتجان صفحة بيضاء صامتة لا تُميَّز عن «الزرّ لم يعمل». استُبدلا بحالة خطأ صريحة (`QueryError`) بعنوان ووصف وزرّ إعادة محاولة.

### 2.2 `ROOT_CAUSE_PROJECT_REPORT = ROUTING_TO_GENERAL_REPORTS_LOSING_PROJECT_SCOPE`
`LinkedReportsCard` (في `ClientDetailPage.tsx`) كان يبني الوجهة ثابتةً:
```
to={`/app/submissions?open=${r.submissionId}`}
```
وهي صفحة التقارير العامّة. أثرُه المزدوج: (أ) تجربة تنقّل تبدو «لا تفتح» لأنّ المستخدم يُقذف خارج سياق المشروع، (ب) — والأخطر — عرض **كامل** التقرير الأسبوعيّ بما فيه عمل مشروعات أخرى.

---

## 3) تدقيق نموذج البيانات (قبل أيّ سطر شيفرة)

السؤال الحاسم الذي فرضته التذكرة: **هل يدعم النموذج استخراج شريحة مشروع فعليًّا، أم أنّ الربط على مستوى التقرير ككلّ فقط؟**

| السؤال | الجواب المقيس |
|---|---|
| كيف يرتبط التقرير بمشروع؟ | `ReportSubmissions.ProjectId` — ربط **اختياريّ** على مستوى التقرير ككلّ |
| هل التقرير كلّه يخصّ مشروعًا واحدًا؟ | **لا**. التقرير الأسبوعيّ الواحد يحمل عمل عدّة مشروعات |
| هل هناك ربط على مستوى **الجزء**؟ | **نعم** — `FieldType.ProjectRepeatableSection` (=22) هو **نوع الحقل الوحيد** الذي يحمل في `ValueJson` مصفوفة `[{ projectId, answers }]` |
| هل يمكن استخراج الشريحة بلا تخمين؟ | **نعم**، من `projectId` المصرَّح داخل كلّ عنصر |

⟹ **`DATA_MODEL_SUPPORTS_PROJECT_SLICE = YES`** — ولذلك **لم تُوقَف** التذكرة عند «Design Hard Stop».

**ما استُبعد عمدًا من الشريحة:** كلّ حقل ليس من نوع `ProjectRepeatableSection` (نصّ حرّ، ملخّص عامّ، مرفقات غير مربوطة). لا رابط موثوق لها بمشروع، وإخراجها = تسريب. **لم يُخمَّن انتماء فقرة من نصّها، ولم يُستعمل اسم عميل أو مشروع كعلاقة موثوقة في بحث نصّيّ.**

**`MIGRATION_REQUIRED = NO` · `BACKFILL_REQUIRED = NO`** — الربط قائم بنيويًّا في البيانات الموجودة، فلا مبرّر لهجرة.

---

## 4) العقد الجديد (خادميّ) ونموذج الصلاحيات

### المسار
```
GET /api/projects/{projectId}/reports/{reportId}
```
`ProjectsController` → `ProjectService.GetReportSliceAsync(id, submissionId, ct)`.

### تسلسل الفحص الخادميّ (بالترتيب)
1. غير مصادَق ⟵ `auth.unauthenticated`.
2. `IClientProjectAccess.ResolveAsync` ثمّ `vis.CanViewProject(id)` — **نفس** خدمة رؤية المشروعات القائمة، **لم يُنشأ مصدر حقيقة جديد** ولم يُكرَّر منطق صلاحيات.
3. المشروع غير موجود ⟵ رفض.
4. التسليم غير موجود ⟵ رفض.
5. تُستخرج عناصر `ProjectRepeatableSection` التي `projectId`ها = المشروع المطلوب **فقط**.
6. **لا عناصر ولا ربط مباشر** (`fields.Count == 0 && sub.ProjectId != id`) ⟵ رفض «غير مرتبط».

### عقد الرفض الموحّد (منع التعداد)
الحالات الثلاث — *خارج النطاق* و*غير موجود* و*غير مرتبط* — تُعيد **حرفيًّا** نفس الشيء:
`title = Not Found` · `detail = المشروع غير موجود.` · `type/code = project.not_found` ⟵ HTTP **404**.
ثابت واحد في الشيفرة (`ProjectNotFoundMessage` / `ProjectNotFoundCode`) كي لا تنحرف صياغة إحداها بتعديل لاحق فيعود التمييز من حيث لا يُقصَد.

### الفشل المغلق
`JsonException` عند تحليل `ValueJson` تُبتلَع عمدًا وتُعيد قائمة فارغة: JSON تالف = «لا أعرف لمن هذا العنصر» ⟹ **لا يخرج شيء** (fail-closed) لا العكس.

### ما لم يُمسّ
- **`ScopeResolver` والهيكل التنظيميّ:** لم يُعدَّلا إطلاقًا.
- **صلاحيات التقارير العامّة:** لم تُوسَّع — أُثبت بالقياس (§6، الحالة 14).
- **صلاحيات المسار الجديد:** نفس بوّابة صفحة المشروع (`PROJECT_360_ROLES`) لا أوسع.
- **زرّ يقود إلى التقرير الكامل من داخل 360:** لم يُضَف (يتطلّب قرار صلاحيات منفصلًا).

---

## 5) الواجهة — المسارات قبل/بعد

| العنصر | قبل | بعد |
|---|---|---|
| «فتح» داخل صفحة مشروع | `/app/submissions?open={id}` (عامّ) | `/app/projects/{projectId}/reports/{reportId}` |
| «فتح» في سياق العميل (لا مشروع) | `/app/submissions?open={id}` | **بلا تغيير** — `projectId` اختياريّ، فلا انحدار خارج نطاق التذكرة |
| مساحة عمل 360 عند غياب `projectId` | `return null` (صفحة بيضاء صامتة) | `QueryError` «رابط غير مكتمل» + إرشاد |
| مساحة عمل 360 عند بيانات غير صالحة | `return null` | `QueryError` «تعذّر عرض مساحة عمل المشروع» + إعادة محاولة |

**صفحة الشريحة `ProjectReportSlicePage`** تعرض حصرًا: العنوان «مساهمة تقرير {اسم المقدّم} في مشروع {اسم المشروع} / {الفترة}»، بيانات وصفيّة ضروريّة فقط (المقدّم، الدوريّة، الفترة، الحالة)، عناصر المشروع المطلوب فقط، ورابط **«← رجوع إلى صفحة المشروع»**. الـRTL والنمط البصريّ القائم بلا تغيير.

**`FRONTEND_ONLY_FILTERING = NO`** — الواجهة لا تستقبل التقرير الكامل أصلًا؛ الاستجابة نفسها لا تحتوي بيانات المشروع الآخر.

---

## 6) نتائج البناء والاختبارات (أرقام حقيقيّة مقيسة، لا بائتة)

| البند | النتيجة | الفشل |
|---|---|---|
| `dotnet build -c Release` | نجح | **0 خطأ** · 4 تحذيرات سابقة للتذكرة |
| اختبارات الوحدة (خلفيّة) | **556 / 556** | 0 |
| اختبارات التكامل (قاعدة نظيفة `p360rc_180332`) | **2183 / 2183** · 11د29ث | 0 |
| منها اختبارات الشريحة الجديدة `ProjectScopedReportSliceTests` | **9 / 9** | 0 |
| `tsc --noEmit` | نظيف | 0 |
| `vitest run` | **748 / 748** عبر 64 ملفًّا | 0 |
| منها `ProjectScopedReportNav.test.tsx` | **5 / 5** | 0 |
| منها `ProjectReportSlicePage.test.tsx` | **8 / 8** | 0 |
| `vite build` | نجح | 0 |

**مصفوفة الأمان الخادميّة على TEST الحيّ — 16 / 16 PASS** (`Ops/UAT/P360-NAVFIX-20260826/evidence/api-matrix.json`، تُقرأ من **نصّ الاستجابة الخام** لا من كائن مُفكَّك):

| # | الحالة | الدليل |
|---|---|---|
| 1–5 | شريحة أ لكلّ من الإدارة/مدير الحساب/مالك المشروع/قائد الفريق/الموظّف | `200` · `776001` حاضرة · `889002` غائبة · معرّف ب غائب · الملخّص العامّ غائب |
| 6–10 | شريحة ب لنفس الأدوار | `200` · `889002` حاضرة · `776001` غائبة · معرّف أ غائب · الملخّص العامّ غائب |
| 11–12 | خارج النطاق على أ وعلى ب | `404` بلا أيّ بصمة |
| 13 | العبث بمعرّف مشروع وهميّ / تقرير وهميّ | `404/404` وبصمة رفض **واحدة** ⟹ لا تعداد |
| 14 | خارج النطاق على `/api/submissions/{id}` | `403` ⟹ **لم تُوسَّع صلاحيات التقارير العامّة** |
| 15 | مقدّم التقرير على مساره العامّ | `200` والبصمتان معًا ⟹ **لا تضييق غير مقصود** |
| 16 | `GET /api/projects/{أ}/reports` | `200` والتسليم مُدرَج |

---

## 7) UAT المصوَّر على TEST — 40 / 40 PASS

**الوثيقة الكاملة بالمنهج والقيود:** `Ops/UAT/P360-NAVFIX-20260826/UAT-EVIDENCE-20260826.md`
**اللقطات:** `Ops/UAT/P360-NAVFIX-20260826/screenshots/` (23 ملفًّا) · **النتائج الخام:** `…/evidence/uat-results.json`

**سيناريو الحوكمة المنفَّذ:** تقرير UAT واحد يحمل بيانات مشروعَين — أ ببصمة `776001`، ب ببصمة `889002`، وملخّص عامّ خارج أيّ مشروع.

| السيناريو | الإدارة | مدير الحساب | مالك المشروع | قائد الفريق | موظّف داخل | موظّف خارج |
|---|---|---|---|---|---|---|
| تسجيل الدخول | PASS | PASS | PASS | PASS | PASS | PASS |
| 360 ⟵ نفس المشروع | PASS | PASS | PASS | PASS | PASS | — |
| «فتح» ⟵ شريحة أ وحدها | PASS | PASS | PASS | PASS | PASS | — |
| الرجوع ⟵ صفحة نفس المشروع | PASS | PASS | PASS | PASS | PASS | — |
| نفس التقرير من ب ⟵ شريحة ب وحدها | PASS | PASS | PASS | PASS | PASS | — |
| رفض نهائيّ واضح | — | — | — | — | — | PASS |
| العبث بالمعرّفات بلا تسريب | — | — | — | — | — | PASS |
| SignalR | PASS | PASS | PASS | PASS | PASS | PASS |
| نظافة Console/شبكة/أصول | PASS | PASS | PASS | PASS | PASS | PASS |

**قياسات النظافة:** أخطاء Console غير متوقَّعة = **0** لكلّ الأدوار الستّة · طلبات شبكة فاشلة = **0** · SignalR: تفاوض **200** ومقابس 3–4 بلا خطأ مقبس · الأصول المرصودة ثلاثة فقط: منصّة الاختبار المحلّيّة، `test.emarketingacademy.net`، خطوط Google ⟹ **صفر إنتاج · صفر RC · صفر `localhost:5090`**.

**رقمان غير صفريّين صُنِّفا بدل إخفائهما:** `console_404_مقصود = 2` للموظّف خارج النطاق (رسالة Chrome العامّة الناتجة عن سياسة منع التعداد نفسها — غيابها كان سيعني فشل الأمان) · `aborted_by_nav = 1` (إلغاء استعلام من العميل عند التنقّل، لا فشل خادم).

**تدقيق اللقطات:** 23 ملفًّا · **0 مجموعة بصمات مكرّرة** · 23/23 تُظهر حالة نهائيّة لا دوّامة تحميل · 23/23 تحمل شريطًا يذكر الدور والحساب والمسار الحيّ · **0 لقطة تحوي رمزًا أو كلمة مرور أو سرًّا**.

---

## 8) بوّابة TEST والنَسَب

| البند | القيمة |
|---|---|
| `BASE_SHA` | `d937788` (خطّ TEST القائم) |
| `FIX_SHA` | `63b7d42f2d0cc54899b22cc919045389c23b2ec7` |
| نوع الدفع | **تقديم سريع** `8479d37..63b7d42` — بلا `force`، بلا دمج، بلا وسم |
| دلتا `d937788..origin/develop` قبل الدفع | 8 التزامات · 19 ملفًّا · **صفر تغيير في `src/`** ⟹ النشر لا يغيّر سلوك TEST إلّا بتغييرات هذه التذكرة |
| نسخة احتياطيّة ثلاثيّة قبل النشر | طابع `20260826-151801` |
| مطابقة البصمة محلّيًّا ⟷ TEST | `Reporting.Api.dll` md5 `06d09ba2e39f9468c509be8f97927df2` — **مطابق** |
| النسخة المضمَّنة على TEST | `1.0.0+63b7d42f2d0cc54899b22cc919045389c23b2ec7` |
| `dist` المنشور | `index.html` md5 `2fdc9c5b6078444937799ca1de3670f5` · حزمة `index-Dyz6sh2e.js` · **7/7 ملفّات مطابقة بـsha256** |
| الهجرات بعد الإقلاع | **45 = 45** · `No migrations were applied` ⟹ **لا هجرة جديدة** |
| `GET /health` | **200** |
| RC والإنتاج بعد النشر | `MainPID` وزمن التفعيل والبصمات **متطابقة بايتيًّا** مع خطّ الأساس |

---

## 9) التنظيف بعد UAT

نُفِّذ في معاملة واحدة على `reporting_test_uat`:

- **حُذف:** التسليم المحوريّ وقيمه (2 صفّ)، القالب `P360NAV-` وإسناده ونسخه وحقوله، المشروعان أ وب، العميل، الفريق، الإدارة ⟹ كلّها **0** بعد التنفيذ.
- **`audit_logs`: 730 قبل ⟵ 730 بعد** ⟹ `append-only` محفوظ، لم يُحذف سجلّ واحد.
- **الحسابات المؤقّتة الستّة:** لم تُحذف بل **عُطِّلت** (`IsActive=false` · `LockoutEnd=9999-12-31` · `SecurityStamp` مُدوَّر). النشِطة: 6 ⟵ **0**.
- **رموز التحديث:** 40 سارية ⟵ **0** (أُبطلت بـ`RevokedAtUtc` لا بالحذف — أثر قابل للتدقيق).
- **المرجعيّات:** تجزئة كلمة مرور `uat.p123.admin@uat123.test` أُعيدت **بايتًا ببايت** من لقطتها مع `SecurityStamp`/`ConcurrencyStamp`/`LockoutEnd=NULL`/`AccessFailedCount=0`. التحقّق الخادميّ: `hash_restored=t` · `stamp_restored=t` · `lockout_null=t`.

**إثباتات سلوكيّة بعد التنظيف:** دخول بكلمة المرور المؤقّتة = **401** (⟹ الاستعادة حقيقيّة) · دخول حساب UAT = **403** (⟹ التعطيل حقيقيّ) · `/health` = **200** · خدمة TEST `active` بنفس `MainPID=1589037` وزمن تفعيل 15:18:56 UTC (لا إعادة تشغيل) · **أخطاء/استثناءات في سجلّ TEST = 0**.

**الأسرار:** حُذفت محلّيًّا `.pw` و`.hash` و`login.json` (كانت تحوي رمز وصول ورمز تحديث)، وحُذف من الخادم `temp-pwd` ولقطة الاعتماد بـ`shred -u` والمجلّد صار فارغًا. نفق SSH والخادم الساكن مُفكَّكان (0 مستمع على `4420`، 0 نفق على `15091`).

**`SECRET_SCAN`:** مسح الشيفرة المتغيّرة (`password|secret|api[_-]?key|bearer|BEGIN (RSA|OPENSSH|PRIVATE)|Pwd=`) = **صفر مطابقة**. مسح مجلّد الأدلّة المحفوظ: 3 مطابقات، كلّها **أسماء متغيّرات** (`{"password": PW}` · `["accessToken"]` · `input[type="password"]`) ولا قيمة سرّيّة مخزَّنة واحدة.

---

## 10) الثغرات المفتوحة والملاحظات — مذكورة بلا تجميل

### 10.1 ثغرة مفتوحة: قائمة «التقارير المرتبطة» ما زالت على الربط الكلّيّ
`GetReportsAsync` يُدرِج التقارير بشرط `s.ProjectId == id`. أثرُه: تقرير أسبوعيّ `ProjectId`ه = أ لكنّه يحمل عملًا للمشروع ب **لن يظهر** في قائمة تقارير المشروع ب — رغم أنّ نقطة الشريحة تخدمه صحيحًا عند التنقّل المباشر (ولذلك استُعمل التنقّل المباشر في سيناريو ب في UAT). توسيع القائمة قرار نطاق منفصل لم تُصرِّح به هذه التذكرة ولم يُنفَّذ. **لا أثر أمنيًّا** (الاتّجاه تضييق لا توسيع)، والأثر وظيفيّ: اكتشاف أضعف.

### 10.2 قيد منهجيّ في منصّة UAT
`auth_basic` على nginx وكلمة مروره تجزئة غير قابلة للاسترجاع (وتغيير `htpasswd` ممنوع) ⟹ صفحات SPA لا تُحمَّل من الأصل بمتصفّح آليّ. المنهج البديل: بايتات `dist` المنشورة على TEST **نفسها** (7/7 `sha256` مطابقة) على خادم ساكن محلّيّ، وكلّ `/api/**` و`/hubs/**` مُحوَّلة عبر نفق إلى **خدمة TEST الحيّة**. النتيجة: الشيفرة والبيانات والصلاحيات كلّها من TEST، لكن **شريط العنوان يعرض `127.0.0.1:4420`** لا اسم النطاق. يُذكَر كما هو بدل ادّعاء ما لم يحدث.

### 10.3 تغيّر خارجيّ على RC أثناء النافذة (ليس من هذه التذكرة)
خطّ أساس RC الموثَّق قبل نشري وبعده: `MainPID=1579500` · 13:34:46 UTC · بصمة `7f77bfbf…`. القياس بعد UAT: `MainPID=1592241` · **16:45:43 UTC** · النسخة المضمَّنة `1.0.0+897c9b18…` = `merge: DEF-P123-RC-001` — **تذكرة أخرى**. السجلّ يُظهر إيقافًا/تشغيلًا نظيفًا لا انهيارًا. **لم أكتب ولا مرّة على `/opt/reporting-rc` ولا `reporting_rc`**؛ كتاباتي انحصرت في `/opt/reporting-test` و`reporting_test_uat` و`/root/p360-navfix`. النسب سليم رغم ذلك: `63b7d42` **سلف مباشر** لـ`origin/develop` الحاليّ (`merge-base --is-ancestor` = 0) ⟹ لا `force` ولا نقض لإصلاحي.

### 10.4 تباين توثيقيّ سابق (سُجّل ولم يُعتمَد عليه)
`P123-TEST-DEPLOY-EVIDENCE-I-20260826.md` يسجّل TEST عند `4c452e8…`/md5 `9240e16a…`، بينما القياس المباشر قبل نشري أظهر `1.0.0+d937788…`/md5 `81eed683…`/`mtime` 2026-08-26 10:30:10 UTC. سُجّل بأمانة، ولم يُصحَّح ذلك التقرير (خارج نطاق هذه التذكرة) ولم يُبنَ عليه شيء.

---

## 11) بيان الحقول النهائيّ

> **ملاحظة تنسيق مقصودة:** كتلة الحقول أدناه **بالمحارف اللاتينيّة حصرًا**. السبب مقيس لا تجميليّ: خلط العربيّة بالبصمات السداسيّة على السطر الواحد داخل كتلة أحاديّة المسافة يجعل مُصيِّر PDF يعيد ترتيب المقاطع ثنائيّة الاتّجاه فيكسر البصمة بصريًّا (شوهد فعلًا: `8479d37..63b7d42` و`897c9b18` انقسمتا في التوليد الأوّل). الشروح العربيّة نُقلت إلى الجدول التالي للكتلة.

```
TICKET = PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1

ROOT_CAUSE_PROJECT360        = NO_REPRODUCIBLE_TECHNICAL_FAULT
ROOT_CAUSE_PROJECT_REPORT    = ROUTING_TO_GENERAL_REPORTS_LOSING_PROJECT_SCOPE
DATA_MODEL_SUPPORTS_PROJECT_SLICE = YES
BACKEND_PROJECT_SCOPE_ENFORCED    = YES
BACKEND_SLICE_ENDPOINT       = GET /api/projects/{projectId}/reports/{reportId}
FRONTEND_ONLY_FILTERING      = NO
CROSS_PROJECT_DATA_LEAK      = NONE_DETECTED
MIGRATION_REQUIRED           = NO
BACKFILL_REQUIRED            = NO

BASE_SHA            = d937788
FIX_SHA             = 63b7d42f2d0cc54899b22cc919045389c23b2ec7
ORIGIN_DEVELOP_AT_PUSH = 63b7d42f2d0cc54899b22cc919045389c23b2ec7
PUSH_TYPE           = fast-forward 8479d37..63b7d42 (no-force, no-merge, no-tag)
ORIGIN_DEVELOP_NOW  = 897c9b187ab4216213b4f453ec65948cd06dff27
FIX_IS_ANCESTOR_OF_ORIGIN_DEVELOP = YES
TEST_DEPLOYED_SHA   = 63b7d42f2d0cc54899b22cc919045389c23b2ec7
TEST_DLL_MD5        = 06d09ba2e39f9468c509be8f97927df2
TEST_INDEX_HTML_MD5 = 2fdc9c5b6078444937799ca1de3670f5
TEST_DIST_SHA256_MATCH = 7/7

BUILD_RESULT                = SUCCESS (0 errors, 4 pre-existing warnings)
BACKEND_UNIT_TESTS          = 556/556 PASS
BACKEND_INTEGRATION_TESTS   = 2183/2183 PASS (11m29s, clean DB p360rc_180332)
NEW_SLICE_TESTS             = 9/9 PASS
FRONTEND_TYPECHECK          = CLEAN
FRONTEND_TESTS              = 748/748 PASS (64 files; incl. 5/5 nav + 8/8 slice page)
FRONTEND_BUILD              = SUCCESS
MIGRATIONS_ON_TEST          = 45 = 45 (No migrations were applied)
TEST_HEALTH                 = 200

ROUTE_360_OPENS_SAME_PROJECT         = PASS (5/5 in-scope roles)
ROUTE_REPORT_IS_PROJECT_SCOPED       = PASS (5/5)
BACK_NAVIGATION_RETURNS_TO_PROJECT   = PASS (5/5)
SECURITY_OUT_OF_SCOPE_404            = PASS
SECURITY_ID_TAMPERING_NO_LEAK        = PASS (single rejection fingerprint)
SECURITY_GENERAL_REPORT_PERMS_UNCHANGED = PASS (403 preserved)
SECURITY_OWNER_NOT_OVER_RESTRICTED   = PASS
SCOPE_RESOLVER_MODIFIED              = NO
ORG_HIERARCHY_MODIFIED               = NO

BROWSER_CONSOLE_ERRORS   = 0 unexpected (+2 intentional-404, classified not waived)
FAILED_NETWORK_REQUESTS  = 0 (+1 ERR_ABORTED cancelled by navigation)
SIGNALR                  = negotiate 200, 3-4 sockets, 0 socket errors, all 6 roles
OBSERVED_ORIGINS         = 127.0.0.1:4420 + test.emarketingacademy.net + fonts.googleapis.com
PRODUCTION_ORIGIN_HITS   = 0
RC_ORIGIN_HITS           = 0
LOCALHOST_5090_HITS      = 0

UAT_ROLES               = 6/6
UAT_CASES               = 40/40 PASS
API_SECURITY_MATRIX     = 16/16 PASS
UAT_SCREENSHOTS         = 23
DUPLICATE_SCREENSHOTS   = 0
SPINNER_ONLY_SCREENSHOTS = 0
SECRETS_IN_SCREENSHOTS  = 0

UAT_CLEANUP             = DONE (all P360NAV- entities = 0)
AUDIT_LOGS_BEFORE_AFTER = 730 -> 730 (append-only preserved)
TEMP_ACCOUNTS_DISABLED  = 6/6 (active 6 -> 0; login now 403)
REFRESH_TOKENS_REVOKED  = 40 -> 0 live
REFERENCE_VALUES_RESTORED = YES (temp password now rejected with 401)
POST_CLEANUP_HEALTH     = 200
TEST_SERVICE_ERRORS     = 0
TEST_MAINPID_UNCHANGED  = 1589037
SECRET_SCAN             = 0 real matches (3 hits = variable names only)

DOCX_VISUAL_QA = PASS
PDF_VISUAL_QA  = PASS

PRODUCTION_TOUCHED   = NO (MainPID 1556574, activated 06:22:54 UTC, baseline-identical)
RC_TOUCHED           = NO (by this ticket)
ORIGIN_MAIN_TOUCHED  = NO
PRODUCTION_DEPLOYMENT_PERFORMED = NO
RC_DEPLOYMENT_PERFORMED         = NO

OPEN_GAP = GetReportsAsync still filters by s.ProjectId == id
TEST_READY_FOR_OWNER_REVIEW = YES
NEXT_REQUIRED_ACTION = PRODUCT_OWNER_SIGN_OFF review of TEST UAT evidence
```

### شرح الحقول التي اختُصرت في الكتلة

| الحقل | الشرح |
|---|---|
| `ROOT_CAUSE_PROJECT360` | مشروع بلا محتوى 360 (لا عطل تقنيّ) + صفحتان بيضاوان صامتتان أُغلقتا وقائيًّا |
| `ROOT_CAUSE_PROJECT_REPORT` | `LinkedReportsCard` كان يوجّه إلى `/app/submissions?open={id}` فيُفقَد نطاق المشروع |
| `DATA_MODEL_SUPPORTS_PROJECT_SLICE` | `FieldType.ProjectRepeatableSection` يحمل `projectId` بنيويًّا داخل `ValueJson` |
| `CROSS_PROJECT_DATA_LEAK` | صفر تسريب في 16/16 قياسًا خادميًّا و40/40 قياسًا بالمتصفّح |
| `ORIGIN_DEVELOP_NOW` | تقدّم بتذكرة أخرى (`DEF-P123-RC-001`) ومرشّحي سلفٌ مباشر له — لا `force` ولا نقض |
| `SECURITY_ID_TAMPERING_NO_LEAK` | بصمة الرفض الواحدة: `Not Found` · `المشروع غير موجود.` · `project.not_found` |
| `SECURITY_GENERAL_REPORT_PERMS_UNCHANGED` | خارج النطاق ما زال يُرفَض بـ403 على `/api/submissions/{id}` |
| `SECURITY_OWNER_NOT_OVER_RESTRICTED` | مقدّم التقرير ما زال يرى تقريره كاملًا على مساره العامّ |
| `BROWSER_CONSOLE_ERRORS` | الرقمان غير الصفريّين مصنَّفان في §7 لا معفوّان |
| `RC_TOUCHED` | لم يُمسّ بفعل هذه التذكرة؛ تغيّره اللاحق موثَّق في §10.3 |
| `OPEN_GAP` | تقرير يحمل عمل مشروع ب لا يظهر في قائمة تقارير ب (لا أثر أمنيّ · اكتشاف أضعف · خارج نطاق التصريح) |
| `NEXT_REQUIRED_ACTION` | مراجعة مالك المنتج لأدلّة UAT على TEST. **لا نشر RC ولا إنتاج بلا تصريح صريح جديد.** |

### تدقيق الجودة البصريّة للمستندَين

جرى فعليًّا بتصيير صفحات من الـPDF وقراءتها بصريًّا (لا بافتراض نجاح التحويل):

| البند | النتيجة |
|---|---|
| عدد الصفحات | 26 |
| الخطوط المضمَّنة | 6 (`ArialMT` · `Arial-BoldMT` · `ArialUnicodeMS` · `Menlo-Regular` · `AppleSymbols` · `Symbol`) |
| تشكيل الحروف العربيّة ووصلها | سليم · **صفر مربّع بديل (tofu)** |
| اتّجاه النصّ والفقرات | RTL صحيح |
| اتّجاه الجداول | RTL صحيح (عمود العنوان يمين) |
| الجداول | مكتملة بلا خلايا مقطوعة |
| اللقطات الـ23 في الملحق | مُدرَجة كاملة وبعناوينها |
| كتلة الحقول | **أُصلحت**: كانت البصمات تنكسر بتشوّه ثنائيّ الاتّجاه في التوليد الأوّل، فصارت لاتينيّة حصرًا مع جدول شرح عربيّ منفصل |
| أسرار مرئيّة في المستند | **0** |

---

## 12) فهرس الأدلّة

| الملفّ | المحتوى |
|---|---|
| `Ops/UAT/P360-NAVFIX-20260826/TEST-DEPLOY-EVIDENCE-20260826.md` | بوّابة النَسَب · جدول الانحدار · خطّ أساس البيئات الثلاث · النسخ الاحتياطيّة · مطابقة البصمات · قياس ما بعد النشر |
| `Ops/UAT/P360-NAVFIX-20260826/UAT-EVIDENCE-20260826.md` | منهج القياس · بيانات وحسابات UAT · المصفوفتان · النظافة · فهرس اللقطات · التنظيف · ملاحظة RC |
| `Ops/UAT/P360-NAVFIX-20260826/screenshots/*.png` | 23 لقطة بشريط ذاتيّ الوصف |
| `Ops/UAT/P360-NAVFIX-20260826/evidence/api-matrix.{py,json}` | مصفوفة الأمان الخادميّة 16/16 |
| `Ops/UAT/P360-NAVFIX-20260826/evidence/uat.mjs` · `uat-results.json` | منصّة UAT متعدّدة الأدوار ونتائجها 40/40 |
| `Ops/UAT/P360-NAVFIX-20260826/evidence/probe360.mjs` | مسبار DOM الذي حسم مسألة تصيير `/360` |
| `Ops/UAT/P360-NAVFIX-20260826/evidence/serve.mjs` · `fixture.json` | الخادم الساكن وسجلّ بيانات UAT |
