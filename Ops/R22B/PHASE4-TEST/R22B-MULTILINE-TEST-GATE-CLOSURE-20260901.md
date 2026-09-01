# R22B — تقرير إغلاق بوّابة TEST لعيب التعليقات متعدّدة الأسطر

**التاريخ:** 1 سبتمبر 2026 · **البيئة:** TEST وحدها · **التشخيص المرجعيّ:** `R22B-MULTILINE-COMMENT-REGRESSION-DIAGNOSTIC.md`

```
ROOT_CAUSE                    = FIX_NOT_MERGED_TO_GOVERNING_BRANCH   (معتمَد)
APPROVED_SCOPE                = REPORT_APPROVAL_COMMENTS_ONLY        (مُلتزَم به)
MULTILINE_COMMENT_REGRESSION  = RESOLVED_ON_TEST
TEST_OPERATIONAL_ACCEPTANCE   = PASS
R22B_VISUAL_GATE              = PASS_ON_TEST
RC_PROMOTION                  = NOT_AUTHORIZED
PROD_PROMOTION                = NOT_AUTHORIZED
```

---

## 1. الفرق النهائيّ — داخل النطاق حصرًا

`HOTFIX_DIFF = IN_SCOPE_ONLY` · 6 ملفّات · +904 / −3 (`git diff --stat 986cc3b 00b1204`):

| الملفّ | الطبيعة | الأسطر |
|---|---|---|
| `reporting-frontend/src/pages/SubmissionsPage.tsx` | **إصلاح: الإدخال + العرض** | +12 / −2 |
| `reporting-frontend/src/components/NotificationsBell.tsx` | **إصلاح: العرض** | +3 / −1 |
| `reporting-frontend/src/pages/ApprovalCommentsMultiline.test.tsx` | اختبار حارس (جديد) | +346 |
| `reporting-frontend/scripts/verify-multiline-bundle.mjs` | حارس الحزمة المبنيّة (جديد) | +134 |
| `Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md` | قاعدة الحوكمة (جديد) | +75 |
| `Ops/R22B/PHASE4-TEST/R22B-MULTILINE-COMMENT-REGRESSION-DIAGNOSTIC.md` | تقرير التشخيص (جديد) | +334 |

**التغيير الوظيفيّ كلّه في ثلاث نقاط، ولا شيء غيرها:**

1. `SubmissionsPage.tsx:1285` — `<Input …/>` ⟸ `<textarea rows={4} className="… resize-y …"/>`
   (`<input type="text">` **بنيويًّا** لا يحمل `\n` ويبتلع Enter؛ فتضيع الأسطر قبل مغادرة المتصفّح).
2. `SubmissionsPage.tsx:1271` — عرض `a.comment`: أُضيف `whitespace-pre-wrap break-words`.
3. `NotificationsBell.tsx:66` — عرض `n.body` (نسخة من التعليق): أُضيف `whitespace-pre-wrap break-words`.

**إثباتات عدم الخروج عن النطاق:**
- **صفر** تعديل على الخادم أو الهجرات أو الـSeeder ⟹ `MIGRATIONS_ADDED = 0` · `MODEL_SNAPSHOT_DIFF = NONE`.
- `DEFECT-IDEMPOTENCY-01` سليم كما هو: المرشَّح مبنيّ **فوق** `986cc3b` فوق `d25dc69`، ولم يُمَسّ أيّ ملفّ يخصّه.
- `LEAVE_KPI_GOVERNANCE_CODE_CHANGES = NOT_AUTHORIZED` — لم يُعدَّل أيّ ملفّ في مسار KPI/الحوكمة (جرد للقراءة فقط).
- `ProjectGovernanceTab.tsx:93` كان أصلًا `whitespace-pre-wrap` وسليمًا ⟹ **لم يُمَسّ** كما اشترط التصريح، واكتُفي بإثبات سلامته حيًّا (§4).
- الاستعادة كانت `cherry-pick --no-commit 00b5f3a` ميكانيكيّة بلا أيّ تعارض ولا نقل يدويّ.

---

## 2. نتائج الاختبارات — البوّابات المحلّيّة قبل النشر

```
MULTILINE_COMPONENT_TESTS  = 14/14 PASS      (ApprovalCommentsMultiline.test.tsx)
FRONTEND_FULL_TEST_SUITE   = PASS            (72 ملفّ · 840 اختبارًا · exit 0)
TYPESCRIPT                 = PASS            (npx tsc -b · exit 0)
PRODUCTION_FRONTEND_BUILD  = PASS
BUNDLE_MULTILINE_GATE      = PASS 8/8        (مرّتين: بعد البناء، وعلى المرآة المطابقة للمنشور)
BACKEND_IDEMPOTENCY_TESTS  = PASS 9/9
FULL_BACKEND_TEST_SUITE    = PASS            (610 وحدة + 2301 تكامل = 2911 · exit 0 · قاعدة نظيفة معزولة)
MIGRATIONS_ADDED           = 0
MODEL_SNAPSHOT_DIFF        = NONE
HOTFIX_DIFF                = IN_SCOPE_ONLY
```

**حارس الحزمة المبنيّة (لا المصدر)** — الفحوص الثمانية على `dist/assets/*.js` نفسها:
`MULTILINE_ELEMENT` ✓ · `RESIZE_Y` ✓ · `WHITESPACE_PRE_WRAP` ✓ · `BREAK_WORDS` ✓ · `COMMENT_FIELD_PRESENT` ✓ · `NO_ENTER_BLOCKER` ✓ · `API_BASE_URL` ✓ (`https://test.emarketingacademy.net/api` — لا `localhost` ولا سقوط احتياطيّ) · `NO_SECRETS` ✓.

---

## 3. رحلة القبول الوظيفيّة — 5/5 تخصّصات على الحزمة المنشورة

السكربت: `reporting-frontend/e2e/r22b-multiline-acceptance.mjs` · الأدلّة: `multiline-20260901/multiline-acceptance.json` + 40 لقطة.
شُغِّلت على **الحزمة المنشورة نفسها بالبايت** (مرآة `MIRROR_MANIFEST_SHA == DEPLOYED_MANIFEST_SHA`)، من نفس الـOrigin، بحسابات حقيقيّة عبر الـAPI الرسميّة. **لم يُعتمد على أيّ لقطة أو نتيجة سابقة** — كلّ ما هنا أُنتِج بعد استبدال الحزمة.

النصّ التشخيصيّ الموحّد لكلّ تخصّص:
`السطر الأوّل — R22BML-<slug> — سبب القرار` / `السطر الثاني — Q3/2026 · 45.7% ✓ (مقبول)` / `السطر الثالث — يُرجى التنقيح؟ نعم!`

| التخصّص | عنصر الإدخال | `rows` | `resize` | معالج يحجب Enter | `\n` في الحمولة | `\n` من الـAPI | أسطر عند الموظّف | أسطر في الإشعار | أسطر في P360 | أسطر لقارئ بارد |
|---|---|---|---|---|---|---|---|---|---|---|
| content | `TEXTAREA` | 4 | vertical | **لا** | نعم | نعم | 3 | 3 | 3 | 3 |
| design | `TEXTAREA` | 4 | vertical | **لا** | نعم | نعم | 3 | 3 | 3 | 3 |
| video | `TEXTAREA` | 4 | vertical | **لا** | نعم | نعم | 3 | 3 | 3 | 3 |
| moderation | `TEXTAREA` | 4 | vertical | **لا** | نعم | نعم | 3 | 3 | 3 | 3 |
| seo | `TEXTAREA` | 4 | vertical | **لا** | نعم | نعم | 3 | 3 | 3 | 3 |

قياس مرجعيّ حرفيّ (`content`، التسليم `0c80e4e6-a269-408b-b928-e1428a9e947d`):

```json
"probe_reasonField": { "tagName": "TEXTAREA", "type": null, "rows": "4",
  "resize": "vertical", "whiteSpace": "pre-wrap", "hasInlineKeyHandler": false }
"valueAfterKeyboard": "السطر الأوّل — R22BML-content — سبب القرار\nالسطر الثاني — …\nالسطر الثالث — …"
"requestPayload":    "{\"comment\":\"…\\n…\\n…\"}"
"render_employee":   { "tag": "P", "className": "whitespace-pre-wrap break-words text-ink-2",
                       "whiteSpace": "pre-wrap", "overflowWrap": "break-word",
                       "offsetHeight": 60, "lineHeight": 20, "renderedLines": 3, "innerTextNewlines": 2 }
```

`renderedLines = offsetHeight / lineHeight = 60/20 = 3` — أي **ارتفاع مقيس فعليًّا في المتصفّح**، لا مجرّد وجود صنف CSS.

### الأعلام المطلوبة

```
MULTILINE_INPUT             = PASS  (TEXTAREA · Enter مقبول · لا معالج يحجبه)
MULTILINE_SAVE_RELOAD       = PASS  (\n في الحمولة ⟹ مخزَّن ⟹ يعود من الـAPI ⟹ يُعرَض 3 أسطر بعد إعادة التحميل)
MULTILINE_ACCOUNT_MANAGER   = N/A_BY_DESIGN   (انظر §5 — إفصاح صريح، لا ادّعاء نجاح)
MULTILINE_THIRD_PARTY_READER= PASS  (بديل مُثبِت: قارئ مستقلّ في سياق بارد جديد)
MULTILINE_NOTIFICATION      = PASS
MULTILINE_PROJECT_360       = PASS
HISTORICAL_COMMENT_RENDERING= PASS  (بقيد مُفصَح عنه في §5)
FIVE_TEMPLATE_VISUAL_UAT    = 5/5
```

### هل المكوّن مشترك بين القوالب الخمسة؟

**نعم، ومُثبَت مرّتين.** بنيويًّا: مسار الاعتماد كلّه يمرّ بمكوّن واحد `SubmissionDetail` داخل `SubmissionsPage.tsx` بلا أيّ تفرّع حسب القالب أو التخصّص. وسلوكيًّا: الرحلة قاست العنصر في التخصّصات الخمسة فأعطت **نفس البصمة حرفيًّا** (`TEXTAREA` · `rows=4` · نفس `className`). ⟹ الإصلاح واحد يغطّي الخمسة، ولا يوجد سطح تعليق ثانٍ منسيّ.

---

## 4. الرحلة البصريّة — مراجعة اللقطات واحدة واحدة

40 لقطة (8 لكلّ تخصّص). فُتِحت **24 لقطة بصريًّا واحدة واحدة** ورُوجِعت بالعين، لا بنتائج Playwright وحدها:

| اللقطة | ما رُئي بالعين |
|---|---|
| `M02-*` ×5 | مربّع «إجراء الاعتماد» — `textarea` يحمل **ثلاثة أسطر متمايزة** ظاهرة داخله قبل الحفظ |
| `M03-*` ×5 | بعد الإعادة للتعديل — «ملاحظات الاعتماد» كلّ مدخل **3 أسطر**، والحالة «مُعاد للتعديل» |
| `M04-*` ×5 | شاشة الموظّف — التعليق **3 أسطر** كما كُتِب |
| `M05-content` | لوحة جرس الإشعارات — نصّ الإشعار **3 أسطر** داخل العرض الثابت `w-80` بلا فيض أفقيّ |
| `M06-content` | شريحة مشروع مدير الحساب — تأكيد بصريّ أنّه **لا يوجد قسم تعليقات اعتماد أصلًا** (أساس §5) |
| `M07-*` ×5 | Project 360 ⟸ «القرارات والحوكمة» — ملاحظة إداريّة **3 أسطر** محفوظة |
| `M08-*` ×5 | سياق بارد جديد (قارئ مستقلّ) — التعليقات المخزَّنة **3 أسطر** |

اللقطات غير المفتوحة فرديًّا: `M01-*` ×5 (تقرير مُرسَل لا يحتوي تعليقًا أصلًا) و`M05-*` ×4 و`M06-*` ×4 — وكلّها مغطّاة بقياس DOM الرقميّ في ملفّ الأدلّة. لا لقطة بائتة في المجموعة: لقطات الجولات الفاشلة السابقة حُذِفت قبل التوثيق.

---

## 5. إفصاحات صريحة — ما لم يُثبَت، ولماذا

### 5.1 `MULTILINE_ACCOUNT_MANAGER = N/A_BY_DESIGN` (لا PASS ولا FAIL)

الحساب `r22c-am@r22uat.test` دوره `AccountPortfolioReader` وحده. قياسات حيّة:

- `GET /api/submissions/{id}` ⟸ **HTTP 403** في التخصّصات الخمسة كلّها. هذا **تخويل صحيح بالمورد** (Resource-Based Authorization) لا انحدارًا.
- سطحه الفعليّ `ProjectReportSlicePage.tsx` **لا يحتوي أيّ إشارة** إلى `comment` أو `approval` أو `whitespace` — أي أنّ **دور مدير الحساب لا يملك أصلًا سطحًا يعرض تعليقات الاعتماد**.

فلا يمكن إثبات «عرض متعدّد الأسطر لمدير الحساب» لأنّ العرض نفسه غير موجود بحكم التصميم الأمنيّ. **لم أدّعِ نجاحًا غير موجود**، وسُجِّلت الحقيقة كما هي (`ACCOUNT_MANAGER_COMMENT_SURFACE_EXPOSED = false` · `amSubmissionDetailStatus = 403`)، واستُعيض عنها بإثبات **قارئ طرف ثالث مستقلّ** (`r22b-hotfix-admin@r22uat.test`، ليس كاتب التعليق ولا صاحب التقرير) في سياق متصفّح بارد جديد ⟹ `MULTILINE_THIRD_PARTY_READER = PASS`.

> إن كان المطلوب أن يرى مدير الحساب تعليقات الاعتماد، فذلك **طلب منتج جديد** (كشف سطح غير موجود) خارج `APPROVED_SCOPE`، ويحتاج قرارًا وتصريحًا مستقلّين.

### 5.2 قيد `HISTORICAL_COMMENT_RENDERING`

أُثبِت على تعليقات **مخزَّنة مسبقًا في قاعدة TEST**، قُرِئت في سياق متصفّح جديد كليًّا **بلا أيّ تعديل للبيانات**. لكن للأمانة: جدول `approval_steps` على TEST فيه **32 صفًّا، 21 منها بتعليق، و0 منها يحتوي `\n` قبل هذه الجلسة**. الحالة التاريخيّة الأصيلة (تعليقات حقبة 12–23 أغسطس المكتوبة أيّام سلامة الإنتاج) **موجودة على الإنتاج وحده**، والإنتاج غير مصرَّح به. ⟹ ما أُثبِت هو استرجاع العرض لبيانات مخزَّنة قُرِئت باردة، لا فحص تعليق إنتاجيّ من تلك الحقبة.

### 5.3 أثر التجهيزات على TEST (شفافيّة كاملة)

| الأثر | الحالة | كيف يُسترجَع |
|---|---|---|
| 7 حسابات R22C فُعِّلت مؤقّتًا | **استُرجِعت** إلى `IsActive=false` | تمّ (`r22bml-activate.mjs restore`) |
| 5 حسابات جديدة `r22bml-*@r22uat.test` | **عُطِّلت** (`IsActive=false`) | تمّ |
| كلمات مرور 7 حسابات R22C | أُعيد ضبطها — **غير قابل للتراجع** | تُعاد بضبط جديد عند الحاجة |
| عميل R22C + 5 مشاريع | أُعيد تفعيلها من `Closed` ⟸ **باقية `Active` عمدًا** | `POST /api/clients/{id}/archive` + `POST /api/projects/{id}/archive` — أو `node /tmp/r22bml-projects.mjs archive` |
| 5 ملاحظات إداريّة + 5 تسليمات بتعليقات | باقية كأدلّة قابلة للمراجعة | تُؤرشَف مع المشاريع |

تُرِكت المشاريع نشطة **عمدًا** ليتمكّن المالك من التحقّق الحيّ بنفسه من نفس الأدلّة؛ أمر إعادة الأرشفة أعلاه ينفَّذ متى شئت. جميع التعديلات تمّت عبر **الـAPI الرسميّة حصرًا** — لا كتابة مباشرة على القاعدة إطلاقًا.

---

## 6. إثبات أنّ الإصلاح واختباراته داخل السلالة الحاكمة لا على فرع يتيم

| الفحص | النتيجة |
|---|---|
| `git merge-base --is-ancestor 00b5f3a origin/develop` (الفرع اليتيم — الحالة القديمة) | **exit 1 = NO** ← هذا هو السبب الجذريّ |
| `git merge-base --is-ancestor 00b1204 origin/hotfix/r22b-multiline-comments-20260901` | **exit 0 = YES** |
| `git rev-parse origin/hotfix/r22b-multiline-comments-20260901` | `00b1204fb2a248afd7d141d5632e5b6ed9ba1fd1` = HEAD |
| موضع `ApprovalCommentsMultiline.test.tsx` (14 اختبارًا) | **داخل `00b1204` نفسه** — لا على فرع جانبيّ |
| موضع `verify-multiline-bundle.mjs` (حارس الحزمة) | **داخل `00b1204` نفسه** |
| موضع `r22b-multiline-acceptance.mjs` (رحلة القبول) | مثبَّت في السلالة (انظر التزام التوثيق التالي فوق `00b1204`) |
| موضع قاعدة الحوكمة | `Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md` **متتبَّع في Git** + نسخة في الـRunbook التشغيليّ |

**الفارق الجوهريّ عن 12 أغسطس:** يومها عاش الإصلاح في الحزمة المبنيّة وحدها ومات اختبار الحارس على نفس الفرع اليتيم، فسقط الاثنان صامتين عند أوّل بناء من `develop`. اليوم الإصلاح **وحارساه** (اختبار المكوّن + حارس الحزمة) على مرجع مدفوع إلى `origin`، ولا يمكن لبناء لاحق أن يدهسه دون أن يفشل الحارس بخروج ≠ 0.

---

## 7. الالتزام المعلَّق الوحيد

`PRODUCTION_FIX_BACKMERGED_TO_DEVELOP` ما زال **معلَّقًا**: القاعدة 6 توجب دمج `00b1204` (أو إعادة تطبيقه) على `origin/develop` **قبل إغلاق التذكرة** وقبل السماح لأيّ إصدار لاحق باستبداله. الدمج يحتاج **تصريحًا صريحًا جديدًا** ولم يُنفَّذ.

## 8. التوقّف

`RC_PROMOTION = NOT_AUTHORIZED` · `PROD_PROMOTION = NOT_AUTHORIZED` — لم تُمسّ أيّ من البيئتين. التوقّف هنا امتثالًا للتصريح.
