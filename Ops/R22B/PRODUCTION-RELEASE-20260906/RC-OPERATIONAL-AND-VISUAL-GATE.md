# البوّابة التشغيليّة والبصريّة على RC — §9 · §10 · §11 · §12

- **`RC_DEPLOYED_SOURCE_SHA = c5e0202d0a528a1a45856790716e449b812f0184`**
- الأصل الحقيقيّ المُختبَر: `https://rc-report.emarketingacademy.net` (لا نفق ولا أصل محلّيّ).
- التاريخ: 6 سبتمبر 2026.

## 1) رحلة الموظّف عبر الواجهة (§9) — `ui/rc-ui-journey.json` · 21/27
موظّف المحتوى `r22brel-content@rc-uat.local` على التقرير `de13df0f-5d56-42aa-ad98-2f77c7e48b72` (2026-W37):

| الفحص | النتيجة |
|---|---|
| الدخول · «تقاريري» غير فارغة زورًا (قوالب=1) · الاستحقاق للقالب الصحيح | PASS |
| فتح دورة الفترة الحاليّة · الرابط مقيَّد بالتقرير · مشروع داخل النطاق | PASS |
| بندَا عمل مستقلّان (بنود=2) | PASS |
| إدخال نصّ متعدّد الأسطر في `textarea` (أسطر: بند1=4، بند2=3) | PASS |
| الحقل `textarea` فعليًّا `{"ws":"pre-wrap","rows":3,"resize":"vertical"}` | PASS |
| **VIS-04** تأكيد حفظ حقيقيّ: `✅ حُفظت المسودّة — آخر حفظ ٦ سبتمبر ٢٠٢٦، ٠٤:٠٥ م` | PASS |
| إعادة التحميل: البنود تبقى · الأسطر محفوظة (4/3) · تحرير مستقلّ | PASS |
| الإرسال والحالة الظاهرة «مُرسَل» | PASS |
| القائد: الدخول · الطابور غير فارغ زورًا · يقرأ كلّ البيانات متعدّدة الأسطر | PASS |
| الترميز الآمن: `{"injected":false,"scriptTags":0,"textHasTag":true}` | PASS |
| صفر فيض أفقيّ · صفر خطأ شبكة غير متوقَّع | PASS |

الفحوص الستّة الباقية في تلك الجولة كانت **إخفاقات مِشْبَك اختبار** عولجت في §2 و§4 أدناه، لا عيوب منتَج.

## 2) دورة القرار الكاملة بتعليقات متعدّدة الأسطر (§9/§10) — `ui/rc-ui-decision-cycle.json` · **19/19 PASS**
التقرير `289621dd-b5ef-4284-b9a9-bf07b040b371`.

| الفحص | النتيجة | الدليل |
|---|---|---|
| `RC_UI_DECISION_CARD_PRESENT` | PASS | `اعتماد · إعادة للتعديل · تصعيد` |
| `RC_UI_RETURN_DISABLED_UNTIL_REASON` | PASS | `disabled=true, title="اكتب سبب الإعادة أولًا"` |
| `RC_UI_RETURN_COMMENT_TYPED_IN_CARD` | PASS | `len=125, nl=4` |
| `RC_UI_RETURN_ENABLED_AFTER_REASON` | PASS | `disabled=false` |
| `RC_UI_LEAD_RETURN_WITH_MULTILINE_COMMENT` | PASS | `✅ تم إرجاع التقرير للتعديل` |
| `RC_UI_STATUS_RETURNED` | PASS | «مُعاد للتعديل» |
| `RC_UI_LEFT_PENDING_QUEUE` | PASS | `sidInQueue=false` |
| `RC_UI_EMP_SEES_RETURN_REASON` | PASS | نصّ السبب كاملًا |
| `RC_UI_RETURN_COMMENT_LINE_BREAKS_RENDERED` | PASS | `white-space=pre-wrap · nl=4` |
| `RC_UI_RETURN_COMMENT_HTML_ESCAPED` | PASS | `<b>` و`<script>` نصًّا حرفيًّا |
| `RC_UI_EMP_STATUS_RETURNED_VISIBLE` | PASS | «مُعاد للتعديل» |
| `RC_UI_EMP_RESUBMIT` | PASS | `✅ تم إرسال التقرير للاعتماد` |
| `RC_UI_APPROVE_COMMENT_TYPED_IN_CARD` | PASS | `len=58, nl=2` |
| `RC_UI_LEAD_APPROVE_WITH_MULTILINE_COMMENT` | PASS | `✅ تم اعتماد التقرير بنجاح` |
| `RC_UI_FINAL_STATUS_CLOSED_VISIBLE` | PASS | «مُغلق» |
| `RC_UI_BOTH_DECISION_COMMENTS_VISIBLE` | PASS | تعليقا الإرجاع والاعتماد معًا بـ`pre-wrap` |
| `RC_UI_NO_SCRIPT_EXECUTION` · `RC_UI_NO_HORIZONTAL_OVERFLOW` · `RC_UI_CONSOLE_ERRORS_ZERO` | PASS | `{"doc":0,"dir":"rtl"}` |

### تصحيح ادّعاء سابق (لا يُطوى)
في جولة `rc-ui-return-cycle.mjs` السابقة سُجِّل `RC_UI_LEAD_APPROVE_WITH_MULTILINE_COMMENT = PASS`، وهو **PASS زائف**: المِشْبَك ملأ `textarea` خارج بطاقة «إجراء الاعتماد» فبقيت حالة `comment` فارغة. الإثبات: `GET /api/submissions/dc76b6c8-…` أعاد `approvalSteps[0].comment = null`. لذلك أُبطِل الادّعاء وأُعيد التنفيذ بمنتقٍ مربوط بالبطاقة (`__card('إجراء الاعتماد')`) وسُجِّلت النتيجة الصحيحة أعلاه. **`SubmissionsPage.tsx:1307` `disabled={action.isPending || !comment.trim()}` سلوك منتَج صحيح** وهو سبب `null` السابق.

## 3) أسطح §10 السبعة
| السطح | النتيجة | الدليل |
|---|---|---|
| الإدخال (Input) | PASS | `textarea` بـ`pre-wrap`، الأسطر تُكتب وتُقرأ |
| الحفظ (Save) | PASS | `✅ حُفظت المسودّة` + بقاء الأسطر بعد إعادة التحميل |
| قاعدة البيانات (Database) | PASS | `approvalSteps[1].comment` `nl=4 len=125` · `[2]` `nl=2 len=58` حرفيًّا |
| المراجعة (Review) | PASS | القائد يقرأ النصّ كاملًا بأسطره |
| الإرجاع/الاعتماد | PASS | 19/19 أعلاه |
| الإشعار (Notification) | PASS | `submission.returned` و`submission.approved` بـ`body` يحفظ `\n` حرفيًّا |
| العرض النهائيّ + أرشيف الإدارة | PASS | التعليقان معًا بـ`pre-wrap` · صفحة `/app/admin/archive` تُصيَّر بلا فيض |
| **البريد (Email)** | **`PASS_AT_RENDERER · NOT_RUN_RUNTIME (CHANNEL_DISABLED_ALL_ENVIRONMENTS)`** | أدناه |

### تصنيف سطح البريد — بلا رفع إلى PASS
- المُصيِّر `EmailHtml.EncodeWithLineBreaks` (`EmailModels.cs:103-107`) يُرمِّز HTML أوّلًا ثمّ يوحّد `CRLF/CR → LF` ثمّ `LF → <br />`. مُغطّى باختبارات وحدة مُسمّاة في `EmailHtmlMultilineTests.cs` (سطر واحد بلا فاصل · CRLF/CR/LF · `<script>` يُرمَّز `&lt;script&gt;` · `&`/`"` · العنوان لا يُكسَر) ضمن **618/618**.
- لكنّ بريد قرارات الاعتماد يمرّ عبر `EmailOutbox` المحكوم بـ`Email__Enabled`، وهو **`false` في البيئات الثلاث بما فيها الإنتاج**. قناة `EmailNotifications__Mode` تُغذّيها التذكيرات (`ReportReminderService`) لا قرارات التقارير — وسجلّ RC خالٍ من أيّ رسالة بعد 16 أغسطس (135 قيدًا، أحدثها `report-*-overdue-summary`).
- ⟹ **لا يمكن توليد بريد قرار فعليّ في RC ولا في الإنتاج.** حدّ إعداديّ قائم قبل الإصدار وغير متغيّر به، وليس عيبًا يُحدثه هذا الإصدار.

## 4) البوّابة البصريّة (§11) — محرّكان × مقاسان
`ui/rc-ui-surfaces-webkit.json` · `ui/rc-ui-surfaces-chromium.json` — **12/12 لكلّ محرّك**

| الفحص | WebKit | Chromium |
|---|---|---|
| `P360_PAGE_RENDERS` · `P360_LINKED_REPORTS_TAB` | PASS | PASS |
| `REPORT_STATUS_CLOSED` · `REPORT_BOTH_COMMENTS_PRE_WRAP` | PASS | PASS |
| `REPORT_NO_OVERFLOW_DESKTOP` (1440) | PASS `{"doc":0}` | PASS `{"doc":0}` |
| `MOBILE_390_NO_HORIZONTAL_OVERFLOW` | PASS `{"doc":0}` | PASS `{"doc":0}` |
| `MOBILE_390_MULTILINE_PRESERVED` | PASS | PASS |
| `ADMIN_ARCHIVE_PAGE_RENDERS` · `ADMIN_ARCHIVE_NO_OVERFLOW` | PASS | PASS |
| `CONSOLE_ERRORS_ZERO` · `UNEXPECTED_NETWORK_ERRORS_ZERO` | PASS | PASS |

**`WEBKIT_GATE = PASS` (ليس `NOT_RUN`).** Firefox = `NOT_RUN` (غير مطلوب؛ WebKit وChromium يغطّيان الحدّ الملزم).

### `VIS-01..VIS-05` صراحةً — `ui/rc-vis-gate-*.json` · **10/10 لكلّ محرّك**
| المعرّف | الوصف | الدليل |
|---|---|---|
| VIS-01 | بتر أسماء المشاريع بلا التفاف | `PROJECT_NAME_NOT_TRUNCATED` مكتب+جوّال: `bad=[]` (لا `clipped` ولا `ellipsis` ولا `line-clamp`) |
| VIS-02 | Project 360 بلا قسم تقارير | تبويب **«التقارير المرتبطة»** موجود مكتب+جوّال |
| VIS-03 | دوّار دائم بدل رسالة رفض | `spinners=0` ورسالة **«المشروع غير موجود»** العامّة؛ الرفض يُرجِع **404 لا 403** (سلوك مقصود، `expected404=2`) |
| VIS-04 | لا تأكيد لحفظ المسوّدة | `✅ حُفظت المسودّة — آخر حفظ …` |
| VIS-05 | «الحالة» و«تاريخ التسليم» نصّ حرّ | `<select>` بخيارات `["اختر…","Draft","Revision","Approved","Published"]` + `<input type="date">` بعنوان **«تاريخ التسليم \*»** |

**ملاحظة مسجَّلة (لا تُخفَّض ولا تُرفَع):** خيارات `work_status` تُعرض بالإنجليزيّة (`Draft/Revision/Approved/Published`) مطابِقةً حرفيًّا لتهيئة النسخة المحكومة v7 التي اعتُمدت في إغلاق R22B بموافقات 5/5. ليست ارتدادًا يُحدثه هذا الإصدار، وتُترك قرارًا للمالك.

## 5) تفسير خطأ الكونسول المتكرّر — مِشْبَك اختبار لا عيب منتَج
`Failed to complete negotiation with the server: Unauthorized 401` على `/hubs/notifications/negotiate`. القياس المضبوط:

| التهيئة | حالة `negotiate` |
|---|---|
| WebKit + `httpCredentials` في Playwright | **401** بعد الدخول مباشرة، 200 بعد إعادة التحميل |
| Chromium + `httpCredentials` | 200 / 200 |
| WebKit **بلا** `httpCredentials` (Basic يُحقن للمستند/الأصول فقط) | **200** |
| إعادة تشغيل التوكن نفسه حرفيًّا بـ`fetch` خارج المتصفّح | **200** |

الآليّة: RC وحده محميّ بـ`auth_basic` على مستوى الخادم (`/api/` و`/hubs/` بـ`auth_basic off`)، وWebKit يستبق فيستبدل ترويسة `Authorization: Bearer` بـ`Basic` على `/hubs/`، فيرفضها التطبيق بـ401. **لا `auth_basic` في الإنتاج ⟹ الآليّة غير قابلة للحدوث هناك.** بعد تصحيح المِشْبَك: `CONSOLE_ERRORS = 0` على المحرّكين.

## 6) بوّابة التقارير على RC (§12) — `rc-journey-api2.json` + `rc-journey-api2-delta.json`
57 فحصًا: 50 PASS مباشرة + 7 أُعيد التحقّق منها بتوقّعات مصحَّحة (9/10 في ملفّ الدلتا + واحد حُسم يدويًّا). التصحيحات كانت في **توقّعات السكربت** لا في المنتَج:
- `NumericNormalizer.NormalizeDigits` يحوّل الأرقام العربيّة-الهنديّة `١`/`٢` إلى ASCII — **سلوك منتَج قائم قبل الإصدار** (`git diff --name-only d25dc69..c5e0202` لا يمسّ الملفّ).
- الحالة النهائيّة في مرشِّح التقارير هي `Closed` لا `Approved` (`Approved` تُرجِع 400 — قيمة غير صالحة في التعداد).
- عناصر النظرة العامّة تُفهرَس بـ`submissionId` لا `id`.
- `configJson` سلسلة مُهرَّبة متداخلة ⟹ يجب تحليلها قبل المقارنة؛ بعد التحليل: `["draft","revision","approved","published"]` تطابق تامّ.

## الخلاصة
```
OPERATIONAL_AND_VISUAL_GATE = PASS
UI_EMPLOYEE_JOURNEY         = PASS
UI_DECISION_CYCLE           = PASS (19/19)
MULTILINE_SURFACES          = 7 PASS · EMAIL = PASS_AT_RENDERER / NOT_RUN_RUNTIME (CHANNEL_DISABLED)
VIS_01..VIS_05              = PASS (chromium + webkit · desktop + mobile390)
WEBKIT_GATE                 = PASS
HORIZONTAL_OVERFLOW         = 0
CONSOLE_ERRORS              = 0
UNEXPECTED_NETWORK_ERRORS   = 0
```
