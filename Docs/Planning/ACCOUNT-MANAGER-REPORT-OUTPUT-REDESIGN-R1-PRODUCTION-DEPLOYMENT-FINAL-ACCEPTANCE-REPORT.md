# ACCOUNT-MANAGER-REPORT-OUTPUT-REDESIGN-R1 — تقرير القبول النهائي لنشر Production (Frontend فقط)

**التاريخ:** 28 يوليو 2026 • **البيئة:** Production حصرًا (`reports.emarketingacademy.net`) • **طبيعة النشر: Frontend فقط.**
**بلا Migration، بلا تعديل Backend، بلا تعديل قوالب/بيانات، بلا Email، بلا Workflow.**

---

## 1) Preflight — مُثبَت

- Backend Production لم يتغيّر منذ إقلاعه: `reporting-api.service` بدأ `2026-07-26 23:04:53 UTC` وظلّ كذلك طوال هذه المهمة (لم يُعَد تشغيله في أي لحظة).
- آخر Migration مُطبَّقة على `reporting_prod` قبل وبعد النشر: **`20260724224053_AddReportApproverAndKpiReviewerOverrides`** (بلا تغيير).
- شجرة العمل المحلية تحوي 116 ملفًا غير ملتزَم (WIP سابق غير متعلق بهذه المهمة) — لم تُستخدَم؛ البناء تمّ حصرًا من الالتزام المجمَّد عبر `git archive`.

## 2) إثبات المرشّح (Candidate) — مُثبَت

- **الالتزام المُعتمَد:** `3efbd0dc2584d2fa1bc23c5373d8e2ee1eb10457`
- **الأصل (Parent):** `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6`
- **الشجرة (Tree):** `5db81d290ba27965d8db06ac081236419cdf3fa5`
- **4 ملفات فقط في النطاق**: `reportPresentationProfiles.ts` (جديد)، `PresentationProfileReport.tsx` (جديد)، `PresentationProfileReport.test.tsx` (جديد)، `SubmissionsPage.tsx` (+14/−2).
- العزل تمّ عبر `git archive -o /tmp/amr-r1.tar <commit> -- reporting-frontend` (بعد تعطّل `git worktree` بسبب ضغط ذاكرة macOS مؤقّت)، ثم استخراج نظيف في `/tmp/release-amr-r1-20260728-015753/`.

## 3) Backup قبل النشر — مُثبَت

- المسار: `/opt/reporting/reporting-frontend/dist-backup-amrredesign-20260728-015508/`.
- البندل القديم المحفوظ: `index-CYZF_eI9.js` — **SHA256 = `b7902ba6a951f0bfa13e64b2dde6586324d3aff810f776166d558e89c506ea8e`**.
- تحقّق لاحق (الخطوة 9): النسخة الاحتياطية سليمة بايتيًّا، و`index.html` بداخلها يشير لنفس البندل القديم (نسخة ذاتية الاتساق قابلة للاستعادة الفورية).

## 4) البناء من الالتزام المجمَّد + الاختبارات — أخضر بالكامل

- `npm ci` + `npx tsc -b` ⇒ **0 أخطاء**.
- `npx vitest run` ⇒ **293/293 ناجحة** (تشمل الـ22 اختبارًا الخاصة بـ`PresentationProfileReport`).
- `VITE_API_BASE_URL=/api npm run build` ⇒ نجاح (تحذير signalr الحميد فقط + تحذير حجم chunk).
- الناتج: `dist/assets/index-96kHwdBC.js` (1,324,028 bytes) + `dist/assets/index-Dq23uPgW.css` (30,306 bytes).
- **قاعدة الـAPI المدمجة = `/api` (same-origin)** — لا `localhost`، لا منفذ خاطئ.

## 5) النشر إلى Production — مُثبَت

- **طابع زمني للنشر (UTC):** `2026-07-27T23:02:11Z` (مخزَّن `/root/amr-redesign-deploy-timestamp-utc.txt`).
- rsync من مخرجات البناء المجمَّد إلى `/opt/reporting/reporting-frontend/dist/` + `chown www-data`.
- **البندل الجديد:** `assets/index-96kHwdBC.js` — 1,324,028 bytes.
- **تطابق ثلاثيّ بايتًا ببايت** (بناء محليّ / على القرص / مُقدَّم عبر HTTPS) = نفس **SHA256 `f979b8cb2692e5687da720c5f9e44ad077358d8eec62cca8d160f581af81e172`** — مطابق تمامًا للبندل المعتمَد على RC.
- لا Backend restart، لا Migration، لا تعديل env.

## 6) Smoke Test بعد النشر — أخضر بالكامل

| الفحص | النتيجة |
|---|---|
| `/health` الداخلي (127.0.0.1:5090) | 200 |
| `/health` العامّ عبر nginx (`https://reports.emarketingacademy.net/health`) | 200 |
| الصفحة الرئيسية / deep-link SPA fallback | يخدم `index.html` يشير للبندل الجديد |
| البندل المُقدَّم علنًا عبر HTTPS | SHA256 مطابق تمامًا |
| فحص `/api` (proxy حيّ) | يستجيب (خلف المصادقة كما متوقَّع) |

## 7) UAT على Production (قراءة فقط) — النتيجة والاكتشاف الجوهريّ

استُعلمت `reporting_prod` مباشرة (SELECT فقط) + طلبات GET موثَّقة عبر تسجيل دخول للقراءة فقط (لم يُطبَع أي توكن)، **بلا أي إنشاء/تعديل/حذف بيانات**.

### (A) قالب مدير الحسابات — تاريخ التسليمات الحقيقية على Production

المُرسِلة الحقيقية الوحيدة = **سماح ابوالمجد**، 3 تسليمات على قالب «🤝 تقرير إدارة الحسابات العملاء» (كلها Closed):

| الفترة | نسخة القالب | مفاتيح المخطط | حالة توافق الـProfile |
|---|---|---|---|
| 2026-W28 | V2 | `status, achievements, blockers, needsTeam, needsClient, decisions, priority` (قديم) | **لا يطابق ⇒ المصيّر العامّ (Generic)** |
| 2026-W29 | V3 | نفس المخطط القديم | **لا يطابق ⇒ المصيّر العامّ (Generic)** |
| 2026-W30 | **V4** | `deliverables_sent, deliverables_approved, deliverables_pending, decisions_required, client_relationship, risk_severity, project_status, …` (مخطط الـProfile الكامل) | **يطابق ⇒ الـProfile الجديد يُفعَّل فعليًّا** |

- **اكتشاف جوهريّ يختلف عمّا وُجد على RC**: على RC كانت آخر نسخة منشورة (V5) بلا أي تسليم. على **Production**، النسخة V4 (المنشورة والمستخدَمة فعليًّا في آخر تسليم حقيقيّ W30، بتاريخ 2026-07-23) **تحوي بالفعل** مخطط الـProfile الكامل — أي أن الـProfile الجديد **له بيانات حيّة حقيقية جاهزة للعرض فور النشر**، لا ينتظر تسليمًا مستقبليًّا.
- تحقّق محتوى W30 (SELECT فقط من `submission_field_values`): **6 مشاريع حقيقية**، حالاتها [مكتمل، متأخر، على المسار ×4]، **5 قرارات مطلوبة ذات معنى** (غير فارغة/غير نافية). هذه بالضبط الأرقام التي سيعرضها ملخّص المحفظة والبطاقات الجديدة.
- تحقّق API (GET فقط): التسليمات الثلاثة (W28/W29/W30) تُرجِع 200 مع `templateTitle`/`status`/`periodKey` صحيحة — لا خطأ 500، لا فقدان بيانات.

### (B) قالب المودريشن — عدم تأثّر مُثبَت

- «تقرير المديرشن الأسبوعي» V6 (تسليم حقيقي W30, Closed) يحمل مفتاح `decisions_required` (تصادف لفظي) لكنه **يفتقر** لـ`deliverables_sent` و`client_relationship`، والعنوان لا يحوي «إدارة الحسابات»/«مدير الحسابات» ⇒ **توقيع الـProfile لا يتطابق ⇒ يبقى على المصيّر العامّ** تمامًا كما كان، بلا أي تغيير في السلوك. GET التسليم = 200.

### (C) قالب آخر بلا Profile — تحقّق إضافي

- «تقرير كاتب المحتوى الأسبوعي» W30 (Closed): GET = 200، `templateTitle` صحيح — يُعرض بالمصيّر العامّ دون أي تأثّر.

### (D) صفحة التقارير العامّة

- `GET /api/submissions?page=1&pageSize=5` = 200، `GET /api/report-templates` = 200، `GET /api/dashboard/me` = 200 — التنقّل والتحميل سليمان، لا 4xx/5xx غير متوقَّع.

### ملاحظة بيئة (شفافية)

تعذّر إجراء فحص متصفّح حيّ (Console/Network DevTools) لدومين الإنتاج الخارجي داخل هذه البيئة (أدوات المعاينة مقصورة على خادم تطوير محليّ ولا يمكن توجيهها لنطاق خارجي). عُوِّض هذا القيد عبر: (1) تطابق SHA256 بايتيًّا مع بندل RC الذي خضع لفحص متصفّح فعليّ ومُعتمَد بصريًّا مسبقًا؛ (2) 293/293 اختبار وحدات/تكامل ناجح على نفس الكود؛ (3) صفر استجابات 4xx/5xx عبر كل الفحوصات القرائية أعلاه، شاملةً التسليم الحيّ الجديد ذا الـ6 مشاريع.

## 8) ثبات الأنظمة الأخرى بعد النشر — مُثبَت

| البند | النتيجة |
|---|---|
| Migration head | `20260724224053_AddReportApproverAndKpiReviewerOverrides` (بلا تغيير) |
| Backend DLL | `Reporting.Api.dll` بنفس sha256 وبنفس تاريخ التعديل `Jul 26 23:04` (لم يُمَسّ) |
| Backend service | `ActiveEnterTimestamp = 2026-07-26 23:04:53 UTC` (لم يُعَد تشغيله إطلاقًا خلال هذه المهمة) |
| Email settings | `Email__Enabled=false`، `EmailNotifications__Mode=Enabled` (بلا تغيير، لم تُقرأ/تُعدَّل) |
| Scheduler | `ReportReminderScheduler__Enabled=true` (بلا تغيير) |
| Outbox | `email_outbox` = 0 صف (بلا أثر لهذا النشر) |
| Audit Logs | 0 حدث جديد خلال نافذة الفحص بالكامل (لا كتابات ناتجة عن فحوصاتنا القرائية) |
| بيانات Report/KPI/Workflow | **صفر تعديل** — كل الاستعلامات SELECT، وكل طلبات API كانت GET فقط |

## 9) جاهزية Rollback — مُثبَتة ومُتحقَّق منها

- النسخة الاحتياطية `dist-backup-amrredesign-20260728-015508/` **سليمة بايتيًّا** (SHA256 للبندل القديم = `b7902ba6a951f0bfa13e64b2dde6586324d3aff810f776166d558e89c506ea8e`، مطابق للمُسجَّل قبل النشر) و**ذاتية الاتساق** (`index.html` داخلها يشير لنفس البندل القديم).
- أمر الاستعادة الجاهز (لم يُنفَّذ — النشر ناجح):
  ```bash
  rsync -a --delete /opt/reporting/reporting-frontend/dist-backup-amrredesign-20260728-015508/ /opt/reporting/reporting-frontend/dist/
  chown -R www-data:www-data /opt/reporting/reporting-frontend/dist
  ```
- لا حاجة لإعادة تشغيل أي خدمة (Frontend ثابت، Backend غير معنيّ بهذا النشر إطلاقًا).

## 10) القرار النهائي

# **PASS**

- الخطوات 1–9 كلها مُثبَتة وخضراء بالكامل على Production.
- تطابق بايتيّ ثلاثيّ للبندل المنشور مع البندل المعتمَد على RC.
- صفر انحدار على كل التسليمات القديمة (V2/V3 AM + المودريشن + قوالب أخرى) — تستمر على المصيّر العامّ كما كانت تمامًا.
- **إضافة إيجابية غير متوقَّعة**: الـProfile الجديد له بالفعل بيانات حيّة حقيقية على Production (تسليم W30، 6 مشاريع، 5 قرارات) بعكس RC — أي أن الإدارة سترى المخرج المُعاد تصميمه **فورًا** عند فتح ذلك التقرير، بلا حاجة لانتظار تسليم مستقبلي.
- لم يُمَسّ Backend/Migration/Email/Scheduler/بيانات التقارير أو KPI إطلاقًا.
- جاهزية Rollback مُثبَتة وفوريّة عند الحاجة.

---

**التوقّف هنا حسب التعليمات.** لا بدء لأي مهمة أخرى، وتحديدًا **ممنوع البدء بـ PROJECT-CROSS-FUNCTIONAL-READ-MODEL-R1** في نفس هذه الجلسة/المهمة.
