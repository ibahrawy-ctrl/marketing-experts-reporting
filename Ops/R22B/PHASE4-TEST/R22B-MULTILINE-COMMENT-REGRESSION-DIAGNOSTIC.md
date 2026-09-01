# R22B — تشخيص عودة عيب التعليقات متعدّدة الأسطر

**التاريخ:** 1 سبتمبر 2026
**الحالة المعلنة (كما أمر المستخدم، مثبَّتة بلا تفاوض):**

```
MULTILINE_COMMENT_REGRESSION = CONFIRMED
TEST_OPERATIONAL_ACCEPTANCE  = FAIL_PENDING_DIAGNOSIS
R22B_VISUAL_GATE             = BLOCKED
RC_PROMOTION                 = NOT_AUTHORIZED
PROD_PROMOTION               = NOT_AUTHORIZED
```

**نوع هذا المستند:** تشخيص قراءة-فقط. **صفر تعديل على أيّ ملفّ مصدر، صفر بناء، صفر نشر، صفر كتابة على أيّ قاعدة.**

---

## 0) الخلاصة التنفيذيّة في ثلاثة أسطر

1. الإصلاح موجود ومكتمل وصحيح — لكنّه **يقبع منذ 12 أغسطس على فرع مرشَّح وحيد لم يُدمج إطلاقًا** في `develop` ولا `main`.
2. نشر 12 أغسطس على الإنتاج كان **نشر حزمة `dist` مبنيّة من الفرع المرشَّح مباشرةً**، بلا دمج ⟹ الإصلاح عاش في الـartifact فقط لا في سلالة الإصدار.
3. أوّل نشر واجهة لاحق (23 أغسطس، سلالة `develop`) **دهس الحزمة** وأعاد العيب صامتًا؛ واليوم **البيئات الثلاث كلّها بلا الإصلاح**.

**السبب الجذريّ الواحد المثبَت:**

```
ROOT_CAUSE = FIX_NOT_MERGED_TO_GOVERNING_BRANCH
           (+ OLD_FRONTEND_ARTIFACT_REDEPLOYED كأثر لاحق مباشر لا كسبب مستقلّ)
```

---

## 1) سلاسل القرار المطلوبة

```
KNOWN_GOOD_COMMIT           = 00b5f3a7ba4da063f767dc803e2494f77841d4f1   (الوحيد الذي عمل عليه Enter)
MULTILINE_FIX_COMMIT        = 00b5f3a7ba4da063f767dc803e2494f77841d4f1
                              "fix(reports): support multiline approval comments"
                              REPORT-APPROVAL-COMMENTS-MULTILINE-HOTFIX-R1 · 2026-08-12 02:48:37 +0300
FIX_PRESENT_IN_DEVELOP      = NO
FIX_PRESENT_IN_TEST_LINEAGE = NO
FIX_PRESENT_IN_RC_LINEAGE   = NO
FIX_PRESENT_IN_PROD_LINEAGE = NO
FIRST_BAD_COMMIT            = N/A — لا يوجد التزام أعاد السلوك القديم.
                              السلالة الحاكمة لم تحمل الإصلاح يومًا (`git merge-base --is-ancestor` = NO للثلاثة).
                              الحدث الكاسر هو نشرٌ لا التزام:
FIRST_BAD_DEPLOY            = نشر واجهة الإنتاج 23 أغسطس 2026 (PROJECT360-R21-PROD-DEPLOY)
                              الحزمة index-CMjXSPXr.js (mtime 2026-08-23 11:46:08 UTC) — resize-y = 0
DEPLOYED_FRONTEND_BUNDLE    = PROD index-DMOrtqov.js  (2026-08-31 06:23:11 UTC)
                              RC   index-CMupalax.js  (2026-08-31 04:47:55 UTC)
                              TEST index-DPf3EPx4.js  (2026-08-31 02:37:42 UTC)
                              الثلاث: resize-y=0 · "whitespace-pre-wrap break-words"=0
ROOT_CAUSE                  = FIX_NOT_MERGED_TO_GOVERNING_BRANCH
```

**استبعاد بقيّة التصنيفات (كلّها مستبعَدة بدليل، لا بالترجيح):**

| التصنيف | الحكم | الدليل |
|---|---|---|
| `SOURCE_CODE_REGRESSION` | مستبعَد | لا يوجد التزام عكسيّ؛ الملفّ في `develop` لم يتغيّر عن حالته قبل الإصلاح |
| `FIX_NOT_MERGED_TO_GOVERNING_BRANCH` | **مُثبَت** | `git for-each-ref --contains 00b5f3a` ⟶ مرجع واحد فقط: `refs/heads/candidate/report-approval-comments-multiline-r1-20260811` |
| `DIVERGENT_RELEASE_LINEAGE` | مُثبَت كآليّة مساعدة | الحزمة نُشرت من فرع مرشَّح خارج `develop`؛ أساس المرشَّح `ce16666` داخل `develop`، وابنه الوحيد `00b5f3a` خارجه |
| `OLD_FRONTEND_ARTIFACT_REDEPLOYED` | مُثبَت كأثر | حزمة 23 أغسطس المبنيّة من `develop` دهست حزمة 12 أغسطس |
| `WRONG_COMPONENT_REUSED` | مستبعَد | نفس المكوّن `Input` هو الأصل قبل الإصلاح — لا استبدال لاحق |
| `KEYBOARD_HANDLER_BLOCKS_ENTER` | مستبعَد | صفر `onKeyDown`/`onKeyPress`/`preventDefault` على أيّ حقل تعليق في الواجهة كلّها |
| `API_NORMALIZES_NEWLINES` | مستبعَد | `SubmissionService.cs:635` ⟶ `step.Comment = comment;` إسناد مباشر بلا `Replace`/`Trim`/`Normalize` |
| `DATABASE_NORMALIZES_NEWLINES` | مستبعَد | دليل RC-8 السابق: `\n` مخزَّنة بايتيًّا (`0a`) و`md5(Notification.Body)=md5(ApprovalStep.Comment)` |
| `DISPLAY_CSS_COLLAPSES_NEWLINES` | **مُثبَت جزئيًّا — عيب ثانٍ مستقلّ** | `SubmissionsPage.tsx:1271` و`NotificationsBell.tsx:66` بلا `whitespace-pre-wrap` ⟹ حتى لو وصلت `\n` تُدمَج بصريًّا |

---

## 2) أين ينكسر تعدّد الأسطر — الحكم على كلّ مرحلة

الحكم مبنيّ على العنصر المُصيَّر فعليًّا في الحزمة المنشورة، والمصدر عند `HEAD`، والسلوك البنيويّ لـ`<input type="text">` (لا يستطيع حمل `\n` بنيويًّا، ويبتلع `Enter`).

```
ENTER_ACCEPTED_IN_UI                    = FAIL   (العنصر <input type="text"> — Enter لا يُدرج سطرًا)
NEWLINES_IN_REQUEST_PAYLOAD             = FAIL   (لا يمكن أن تصل \n أصلًا — تُفقد قبل الإرسال)
NEWLINES_STORED                         = PASS*  (المسار الخادميّ سليم؛ يحفظ ما يصله بلا تطبيع)
NEWLINES_RETURNED_BY_API                = PASS*  (SubmissionService.cs:751 يعيد a.Comment كما هو)
NEWLINES_RENDERED_FOR_EMPLOYEE          = FAIL   (SubmissionsPage.tsx:1271 بلا whitespace-pre-wrap)
NEWLINES_RENDERED_FOR_ACCOUNT_MANAGER   = FAIL   (نفس المكوّن — SubmissionDetail مشترك لكلّ الأدوار)
NEWLINES_RENDERED_IN_PROJECT_360        = PASS   (ProjectGovernanceTab.tsx:93 يحوي whitespace-pre-wrap)
```

`*` = المسار سليم بنيويًّا لكن **غير قابل للقياس حيًّا اليوم** لأنّ الواجهة لا تسمح بإنتاج `\n` من الأساس.
**التعليق التشخيصيّ المطلوب (السطر الأول/الثاني/الثالث) لا يمكن إدخاله على أيّ بيئة من البيئات الثلاث حاليًّا** — الحقل يرفض `Enter` بنيويًّا. لذلك القياس الحيّ لمراحل التخزين مؤجَّل إلى ما بعد إقرار الإصلاح، ويُنفَّذ ضمن رحلة الإثبات في §7.

**كسران مستقلّان لا كسر واحد:**
- **الكسر أ (الإدخال):** `<Input>` بدل `<textarea>` — يمنع إنشاء الأسطر.
- **الكسر ب (العرض):** غياب `whitespace-pre-wrap` — يدمج الأسطر حتى لو وُجدت في القاعدة.
  الكسر ب **يصيب فعليًّا التعليقات التاريخيّة متعدّدة الأسطر المكتوبة بين 12 و23 أغسطس على الإنتاج** — فهي مخزَّنة بأسطرها لكنّها تُعرض اليوم سطرًا واحدًا. هذا فقدُ عرضٍ حاضر لا افتراضيّ.

---

## 3) التحقيق التاريخيّ — الأدلّة الخام

### 3.1 التزام الإصلاح
```
$ git log --oneline --all -S'textarea' -- reporting-frontend/src/pages/SubmissionsPage.tsx
00b5f3a fix(reports): support multiline approval comments      ← الإصلاح
3d88ebe chore(baseline): sync frontend src to current production state (AMR-R1 isolation)
1b8dc7f feat(moderation): add V6 content-analysis contract, publisher tool, and UI (MOD-R1B)
67d4b20 Release Candidate RC-2 Sales Reporting System Approved on TEST
5e6b54a Initial commit
```

### 3.2 من يحتويه — الدليل القاطع
```
$ git for-each-ref --contains 00b5f3a --format='%(refname)'
refs/heads/candidate/report-approval-comments-multiline-r1-20260811      ← مرجع وحيد

$ git merge-base --is-ancestor 00b5f3a origin/develop → NO
$ git merge-base --is-ancestor 00b5f3a origin/main    → NO
$ git merge-base --is-ancestor 00b5f3a HEAD           → NO   (HEAD = hotfix/r22b-seeder-idempotency-20260831)

$ git merge-base candidate/report-approval-comments-multiline-r1-20260811 origin/develop
ce166662f46598ed3593beed0105ba67059fc3bc

$ git log --oneline origin/develop..candidate/report-approval-comments-multiline-r1-20260811
00b5f3a fix(reports): support multiline approval comments     ← التزام يتيم واحد
```

**القراءة:** الفرع المرشَّح = أساس داخل `develop` (`ce16666`) + التزام واحد يتيم فوقه. لم يُدمج ولم يُنتقَ (cherry-pick). لا وجود له في أيّ سلالة إصدار.

### 3.3 ملفّ الاختبار الحارس — غائب من السلالة الحاكمة
```
reporting-frontend/src/pages/ApprovalCommentsMultiline.test.tsx  (269 سطرًا، أُنشئ في 00b5f3a)
origin/develop : TEST_FILE_ABSENT
origin/main    : TEST_FILE_ABSENT
HEAD           : TEST_FILE_ABSENT
```
هذا هو **السبب المباشر لصمت العودة**: الحارس الوحيد وُلد ومات على نفس الفرع اليتيم.

### 3.4 مضمون الإصلاح المفقود (من `git show 00b5f3a`)
- `SubmissionsPage.tsx` — استبدال `<Input …/>` بـ`<textarea rows={4} className="w-full resize-y rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy" …/>`.
- `SubmissionsPage.tsx` — `<p className="text-ink-2">` ⟶ `<p className="whitespace-pre-wrap break-words text-ink-2">` في «ملاحظات الاعتماد».
- `NotificationsBell.tsx` — `n.body` ⟶ إضافة `whitespace-pre-wrap break-words`.
- `ApprovalCommentsMultiline.test.tsx` — 269 سطرًا من الاختبارات الحارسة.
- الإجماليّ: **3 ملفّات، +284/−3، Frontend حصرًا. صفر Backend، صفر هجرة، صفر ModelSnapshot، صفر `package-lock.json`.**

---

## 4) مقارنة المصدر بالحزمة الفعليّة (لا بأسماء الالتزامات)

### 4.1 المصدر عند `HEAD` (السلالة الحاكمة اليوم)
| الموضع | الحالة |
|---|---|
| `reporting-frontend/src/pages/SubmissionsPage.tsx:1283` | `<Input value={comment} … placeholder="اكتب سبب القرار…" />` ⟵ **أحاديّ السطر** |
| `reporting-frontend/src/pages/SubmissionsPage.tsx:1271` | `<p className="text-ink-2">{a.comment}</p>` ⟵ **بلا `whitespace-pre-wrap`** |
| `reporting-frontend/src/components/NotificationsBell.tsx:66` | `<p className="mt-0.5 text-xs text-ink-2">{n.body}</p>` ⟵ **بلا `whitespace-pre-wrap`** |
| `reporting-frontend/src/components/ui.tsx:85–92` | `Input` يُصيّر `<input type="text">` دائمًا، بلا `rows`، بلا `onKeyDown` |

### 4.2 الحزم المنشورة حيًّا — قياس بايتيّ على الخادم (1 سبتمبر 2026)
| البيئة | جذر الخدمة | الحزمة المُقدَّمة (من `index.html`) | `mtime` | `resize-y` | `whitespace-pre-wrap break-words` | `اكتب سبب القرار` |
|---|---|---|---|---|---|---|
| PROD | `/opt/reporting/reporting-frontend/dist` | `index-DMOrtqov.js` | 2026-08-31 06:23:11Z | **0** | **0** | 2 |
| RC | `/opt/reporting-rc/frontend/dist` | `index-CMupalax.js` | 2026-08-31 04:47:55Z | **0** | **0** | 2 |
| TEST | `/opt/reporting-test/frontend/dist` | `index-DPf3EPx4.js` | 2026-08-31 02:37:42Z | **0** | **0** | 2 |

### 4.3 الكود المُصيَّر فعليًّا داخل حزمة TEST (مقتطف حرفيّ من الـminified)
```
children:`إجراء الاعتماد`}),(0,M.jsx)(`div`,{className:`mb-3`,children:(0,M.jsx)(J,{
  label:`ملاحظة / سبب`,help:`مطلوب عند الإعادة للتعديل أو التصعيد`,
  children:(0,M.jsx)(K,{value:x,onChange:e=>S(e.target.value),placeholder:`اكتب سبب القرار…`})})})
```
`K` = مكوّن `Input` (`<input type="text">`). **لا `rows`، لا `className`، لا `textarea`.** هذا هو ما يحمّله متصفّح المستخدم فعلًا — لا استنتاج من اسم commit.

### 4.4 تتبّع لحظة السقوط على الإنتاج (نسخ `dist` الاحتياطيّة)
| المجلّد | `mtime` المحتوى | الحزمة | `resize-y` |
|---|---|---|---|
| `dist-backup-approval-comments-multiline-r1-20260812-200204` | 2026-08-06 18:18 | `index-CG2a9RiH.js` | 0 (ما قبل الإصلاح — نسخة تراجع 12 أغسطس) |
| `dist-pre-p123-20260826` | **2026-08-23 11:46** | `index-CMjXSPXr.js` | **0** ⟵ **أوّل حزمة سيّئة موثَّقة** |
| `dist.pre-r22a` | 2026-08-26 19:21 | `index-CTofEn_d.js` | 0 |
| `dist` (الحاليّ) | 2026-08-31 06:23 | `index-DMOrtqov.js` | 0 |

الحزمة السليمة الوحيدة التي وُجدت يومًا — `index-Bok_mmjt.js` (sha256 `119139a4bfbb…`, 1,347,950B، نُشرت 2026-08-12 20:03:17Z) — **لم يعد لها أثر على القرص**: دُهست بنشر 23 أغسطس ولم تُحفَظ لها نسخة. لا يوجد artifact سليم قابل للاسترجاع.

**⟹ نافذة السلامة على الإنتاج: 2026-08-12 20:03Z ← 2026-08-23 11:46Z (نحو 11 يومًا). خارجها العيب قائم.**

---

## 5) لماذا اعتُبر الإصلاح «منشورًا ومغلقًا» وقتها؟

سجلّ الإغلاق (12 أغسطس) ينصّ حرفيًّا على:
`REPORT-APPROVAL-COMMENTS-MULTILINE-HOTFIX-R1 PRODUCTION DEPLOYED — MULTILINE APPROVAL COMMENTS OPERATIONAL — HOTFIX CLOSED`

وكان صادقًا **في لحظته**: الحزمة `index-Bok_mmjt.js` نُشرت فعلًا، والاختبار المكوِّنيّ 12/12 نجح بعد النشر، والتحقّق الحيّ أثبت وجود `textarea rows:4 resize-y` وتطابقين لـ`whitespace-pre-wrap break-words` داخل الحزمة المُقدَّمة.

**الخلل ليس في صدق الإغلاق بل في تعريفه.** تعريف «الإغلاق» شمل:
1. بناء المرشَّح، 2. قبول RC، 3. `rsync` الـ`dist` إلى الإنتاج، 4. تحقّق حيّ من الحزمة، 5. تجهيز نسخة تراجع، 6. كتابة تقرير 27 بندًا.

ولم يشمل بندًا واحدًا حاسمًا: **دمج `00b5f3a` في `develop`**. النشر تمّ **من الـartifact مباشرةً** (`rsync -az --delete dist/ → /opt/reporting/reporting-frontend/dist/`) لا من سلالة الإصدار. فصار الإصلاح موجودًا على القرص ومعدومًا في المصدر الحاكم — وهي حالة **مستقرّة ظاهريًّا وهشّة بنيويًّا**: تصمد حتى أوّل `npm run build` من `develop`، ثمّ تختفي بلا رسالة خطأ ولا تعارض دمج ولا اختبار أحمر.

هذا يفسّر أيضًا لماذا لم يلاحظه أحد بين 23 أغسطس واليوم: **لا شيء في المنظومة كان يراقب هذا السلوك** — الحارس الوحيد (`ApprovalCommentsMultiline.test.tsx`) كان على الفرع اليتيم نفسه.

**تكييف الحادثة:** ليست Regression بالمعنى الدقيق (لا أحد كسر شيئًا)، بل **إصلاح لم يُثبَّت في المصدر الحاكم فتبخّر بأوّل إعادة بناء** — وهو الشكل الأخطر لأنّه لا يترك أثرًا في `git log` ولا في diff.

---

## 6) نطاق الشاشات المتأثّرة (جرد كامل)

### 6.1 داخل النطاق مباشرةً — التقرير ودورة اعتماده
| # | الشاشة/الموضع | العنصر | الحكم |
|---|---|---|---|
| 1 | «إجراء الاعتماد» — تعليق الاعتماد/الإرجاع/التصعيد · `SubmissionsPage.tsx:1283` | `<Input>` | **مكسور (إدخال)** |
| 2 | «ملاحظات الاعتماد» — عرض تعليقات المعتمِدين · `SubmissionsPage.tsx:1271` | `<p>` بلا `pre-wrap` | **مكسور (عرض)** |
| 3 | جرس الإشعارات — نسخة التعليق · `NotificationsBell.tsx:66` | `<p>` بلا `pre-wrap` | **مكسور (عرض)** |
| 4 | سبب الإرجاع (نفس الحقل #1 بزرّ «إعادة للتعديل») | `<Input>` | **مكسور (إدخال)** — والسبب إلزاميّ للإرجاع/التصعيد |
| 5 | تعليق الاعتماد/الإغلاق (نفس الحقل #1 بزرّ «اعتماد») | `<Input>` | **مكسور (إدخال)** |

**المكوّن مشترك:** `SubmissionDetail` واحد يخدم كلّ الأدوار وكلّ القوالب. لا يوجد تفرّع حسب التخصّص.
**⟹ التخصّصات الخمسة كلّها متأثّرة بالتساوي — نجاحه على كاتب المحتوى وحده لا يعني شيئًا، وفشله عليه يعني فشله على الخمسة.** الإثبات: صفر شرط على `templateId`/`specialty`/`vocab` في المسار من `Field label="ملاحظة / سبب"` حتى `api.post(/submissions/{id}/{kind})` (`SubmissionsPage.tsx:900`).

### 6.2 سليمة — لا تمسّها الحادثة
| الموضع | العنصر | الحكم |
|---|---|---|
| حقول القالب `longtext` · `SubmissionsPage.tsx:1067,1622,2081,2089` | `<textarea rows={2..3}>` | سليم |
| عرض إجابات القالب · `SubmissionsPage.tsx:1020,1899,2129,2135` | `whitespace-pre-wrap` | سليم |
| Project 360 — ملاحظات الحوكمة · `ProjectGovernanceTab.tsx:93` | `whitespace-pre-wrap` | **سليم — لا يدمج الأسطر** |
| Project 360 — الاستراتيجيّة · `ProjectStrategyTab.tsx:133,158` (إدخال) / `140,165` (عرض) | `<textarea rows={2}>` + `pre-wrap` | سليم |
| Project 360 — جسر التنفيذ · `ProjectExecutionBridgeTab.tsx:137` / `280` | `<textarea rows={3}>` + `whitespace-pre-line` | سليم |
| لوحة الملاحظات الإداريّة · `ManagementNotesPanel.tsx:128` / `164` | `<textarea rows={3}>` + `pre-wrap` | سليم |
| تقرير العرض · `PresentationProfileReport.tsx:555,578,624,670` | `pre-wrap` | سليم |

### 6.3 خارج نطاق R22B — مرصود ولا يُصلَح الآن (نفس النمط، وحدات أخرى)
`LeaveRequestsPage.tsx:1017` (سبب قرار الإجازة، `<Input>`) · `KpiPage.tsx:630` (ملاحظة المؤشّر، `<Input>`) · `GovernanceEscalationsPage.tsx:664,669,687,691,706,744` · `GovernanceActionItemsPage.tsx:568,588,593,613`.
**لا تُمسّ في هذه التذكرة** — تُسجَّل كبند طابور مستقلّ حتى لا يتضخّم نطاق حاجب R22B.

### 6.4 قيد موروث معروف (موثَّق منذ 12 أغسطس، لا يُصلَح الآن)
قالب بريد الإشعار يضع النصّ في `<p>` بلا `white-space: pre-line` ⟹ قد يظهر سطرًا واحدًا في عميل البريد رغم سلامة `\n` في القاعدة. **عرض فقط، لا فقد بيانات، خارج النطاق.**

---

## 7) الاختبارات الغائبة التي سمحت بعودة العيب

| الفجوة | الأثر | ملاحظة |
|---|---|---|
| `ApprovalCommentsMultiline.test.tsx` غير موجود في `develop` | لا حارس مكوِّنيّ إطلاقًا في السلالة الحاكمة | **الفجوة الأمّ** |
| لا اختبار يؤكّد أنّ حقل «ملاحظة / سبب» عنصر متعدّد الأسطر | استبدال العنصر يمرّ صامتًا | — |
| لا اختبار يؤكّد `whitespace-pre-wrap` على عرض التعليق | دمج الأسطر يمرّ صامتًا | يغطّي الكسر ب |
| لا اختبار على **الحزمة المبنيّة** | نجاح المصدر لا يضمن الـartifact المنشور | جوهر هذه الحادثة |
| لا بوّابة تمنع نشر artifact من فرع خارج `develop` | الفجوة الإجرائيّة التي أنتجت الحادثة | **حوكمة لا كود** |
| لا فحص «الالتزام المنشور من أسلاف `develop`» ضمن قائمة النشر | إغلاق تذكرة بلا دمج | — |

---

## 8) الإصلاح المقترح (للاعتماد — لم يُنفَّذ منه شيء)

### المبدأ
**استرجاع لا إعادة اختراع.** الإصلاح مكتوب ومُختبَر ومقبول على RC ومنشور على الإنتاج سابقًا. المطلوب تثبيته في السلالة الحاكمة، لا كتابته من جديد.

### المرحلة أ — استرجاع الإصلاح إلى السلالة الحاكمة
1. `git cherry-pick 00b5f3a` فوق رأس فرع الهوتفكس الحاليّ (المبنيّ فوق `d25dc69` الذي تشغّله البيئات الثلاث).
2. حلّ أيّ انجراف في `SubmissionsPage.tsx` يدويًّا (الملفّ تغيّر كثيرًا منذ 12 أغسطس؛ التعارض مرجَّح ويُحلّ بالمزج لا بالاستبدال).
3. النتيجة المستهدفة حرفيًّا: `<textarea rows={4} … resize-y …>` عند `:1283` + `whitespace-pre-wrap break-words` عند `:1271` + `NotificationsBell.tsx:66`.

### المرحلة ب — سدّ الكسر ب في المواضع المتبقّية
4. `NotificationsBell.tsx:66` ⟵ ضمن المرحلة أ.
5. **لا توسيع خارج الشاشات الثلاث.** §6.3 يبقى خارج النطاق.

### المرحلة ج — الحارس
6. استرجاع `ApprovalCommentsMultiline.test.tsx` (269 سطرًا) وتحديثه للـAPI الحاليّ.
7. إضافة اختبارات §9 الغائبة.

### المرحلة د — بوّابة الحزمة (منع تكرار النمط)
8. اختبار على `dist/assets/*.js` المبنيّ: `resize-y ≥ 1` و`whitespace-pre-wrap break-words ≥ 2`.
9. **قاعدة حوكمة جديدة مقترحة للاعتماد:** يُحظر نشر أيّ حزمة واجهة ما لم يكن التزامها من أسلاف `origin/develop` — يُتحقَّق بـ`git merge-base --is-ancestor <sha> origin/develop` **قبل** `rsync`، وتُسجَّل النتيجة في تقرير النشر.

### حجم الأثر المتوقّع
Frontend حصرًا · 3 ملفّات مصدر + ملفّات اختبار · **صفر Backend، صفر هجرة، صفر ModelSnapshot، صفر مساس بعقد الـAPI، صفر مساس بسير الاعتماد.**

### التراجع
استعادة `dist` السابقة + `chown www-data:www-data` + التحقّق من `index.html`. لا هجرة تُعكَس، لا إعادة تشغيل خدمة.

---

## 9) الاختبارات التي ستمنع رجوعه (عقد إلزاميّ)

| # | الاختبار | المستوى |
|---|---|---|
| 1 | حقل «ملاحظة / سبب» عنصره `textarea` (لا `input`) وله `rows` | Component |
| 2 | ثلاث ضغطات `Enter` ⟶ القيمة تحوي `\n` مرّتين وثلاثة أسطر | Component |
| 3 | `Enter` داخل الحقل **لا** يرسل النموذج ولا يغلق البطاقة ولا يستدعي `api.post` | Component |
| 4 | جسم الطلب المرسل إلى `/submissions/{id}/{approve\|return\|escalate}` يحوي `\n` حرفيًّا | Component/Contract |
| 5 | تعليق فيه `\n` يُعاد من الـAPI ⟹ يُعرض في ثلاثة أسطر بصريًّا (`whitespace-pre-wrap` حاضر) | Component |
| 6 | عرض الأسطر للموظّف — نفس `SubmissionDetail` بدور Employee | Component |
| 7 | عرض الأسطر للمراجع/Account Manager — نفس المكوّن بدور المراجع | Component |
| 8 | عرض التعليق في Project 360 بلا دمج أسطر | Component |
| 9 | سبب الإرجاع متعدّد الأسطر يبقى بعد الإرجاع وإعادة الإرسال | Integration |
| 10 | حفظ ثمّ إعادة تحميل (Refresh + خروج/دخول) ⟹ `\n` باقية | E2E |
| 11 | العربيّة والإنجليزيّة والأرقام وعلامات الترقيم تمرّ بلا تشويه | Component |
| 12 | **على الحزمة المبنيّة `dist/assets/*.js` لا على المصدر:** `resize-y ≥ 1` و`whitespace-pre-wrap break-words ≥ 2` | Artifact Gate |
| 13 | **بوّابة سلالة:** `git merge-base --is-ancestor <deploy_sha> origin/develop` قبل أيّ `rsync` | Deploy Gate |

---

## 10) إضافة إلى الرحلة البصريّة لبوّابة R22B (للتخصّصات الخمسة)

سيناريو مُدرَج لكلّ تخصّص من الخمسة، **مع إثبات اشتراك المكوّن مسبقًا (§6.1) فلا يُقبل نجاح تخصّص واحد كدليل على الباقي**:

1. الموظّف يكتب تعليقًا من ثلاثة أسطر (`السطر الأول` / `السطر الثاني` / `السطر الثالث`).
2. يحفظ ⟵ يعيد فتح التقرير ⟵ الأسطر الثلاثة باقية.
3. يرسل التقرير.
4. المراجع/Account Manager يفتح التقرير ⟵ يرى ثلاثة أسطر لا سطرًا واحدًا.
5. المراجع يكتب سبب إرجاع من ثلاثة أسطر ⟵ يُرجع.
6. الموظّف يرى سبب الإرجاع بثلاثة أسطر بنفس التنسيق.
7. Project 360 يعرض التعليق بثلاثة أسطر بلا دمج.
8. إثبات بايتيّ لكلّ خطوة: `\n` في الـpayload (`preview_network`)، وفي القاعدة (`encode(convert_to("Comment",'UTF8'),'hex')` والبحث عن `0a`)، وفي الاستجابة، وقياس الأسطر البصريّة عبر `Range.selectNodeContents(p).getClientRects()`.

---

## 11) الأدلّة البصريّة

| الحالة | التوفّر |
|---|---|
| الحالة الحاليّة (مكسورة) — **دليل على مستوى الـartifact** | متوفّر: §4.2 و§4.3 (مقتطف حرفيّ من حزمة TEST المُقدَّمة يُظهر `Input` بلا `rows`) — أقوى من لقطة شاشة لأنّه يُثبت الكود المُصيَّر لا مظهره |
| الحالة الحاليّة — لقطات شاشة | **غير ملتقطة بعد.** تتطلّب جلسة متصفّح مُوثَّقة على TEST؛ تُلتقط عند التصريح ضمن نفس رحلة §10 |
| الحالة السليمة السابقة — لقطات | **غير متوفّرة.** الحزمة السليمة `index-Bok_mmjt.js` دُهست ولا نسخة منها على القرص |
| الحالة السليمة السابقة — دليل بديل | متوفّر: بصمة الحزمة `sha256 119139a4bfbb77a399fa4ada472eb0370f11483661a835fe33de6ae3737269c8` (1,347,950B)، وتوثيق «12/12 اختبار مكوِّنيّ بعد النشر»، و«تطابقان اثنان بالضبط لـ`whitespace-pre-wrap break-words` في الحزمة الحيّة» — من سجلّ إغلاق 12 أغسطس |

---

## 12) التوقّف الإلزاميّ

**تمّ التشخيص. لم يُعدَّل أيّ ملفّ ولم تُبنَ أيّ حزمة ولم يُنشر أيّ شيء.**

بوّابة TEST **لا تُعلن ناجحة**. الحالة تبقى كما ثُبِّتت في رأس المستند.

المطلوب من المستخدم قبل أيّ خطوة تالية:
1. **إقرار السبب الجذريّ** `FIX_NOT_MERGED_TO_GOVERNING_BRANCH`.
2. **إقرار نطاق الإصلاح**: الشاشات الثلاث في §6.1 فقط، وإبقاء §6.3 خارج النطاق.
3. **تصريح صريح** بتنفيذ `cherry-pick` على فرع الهوتفكس الحاليّ.
4. **قرار** بشأن قاعدة الحوكمة المقترحة في §8 بند 9 (بوّابة السلالة قبل النشر).
