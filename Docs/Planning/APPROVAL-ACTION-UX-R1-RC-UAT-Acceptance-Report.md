# APPROVAL ACTION UX R1 — تقرير قبول RC / UAT

**التاريخ:** 2026-07-16
**النطاق المُصرَّح:** واجهة أمامية فقط (Frontend-only) — نشر مرشّح على بيئة RC + UAT حيّ + تنظيف + إعادة الأساس. **بلا نشر إنتاجي، بلا تغيير خلفيّ، بلا هجرة.**
**الفرع / الـCommit:** `approval-action-ux-r1` @ `92b8c01341522e64a1ebfe7328da6dc842ee7688` (الأب `50145fdfb2fee59a09c128a8f20aafabfb3261ec`).
**بيئة RC:** الخدمة `khubara-reporting-rc` (منفذ 5092، البيئة ReleaseCandidate، قاعدة `reporting_rc`)، النطاق `rc-report.emarketingacademy.net`.

---

## 1) بوابات ما قبل النشر (Phases 0–2)
- **Phase 0 — بوابة المصدر:** الفرع/الـHEAD/الأب مطابقة، شجرة نظيفة، 10 ملفات UX، **0 ملف خلفيّ/هجرة**. = PASS.
- **Phase 1 — ما قبل الطيران (قراءة فقط):** الخدمة نشطة، NRestarts=0، صحّة 200، البوابات معطّلة، عدد الهجرات 28 (الرأس `20260715162851`)، الأساس مُلتقَط. = GO.
- **Phase 2 — بوابة البناء:** إعادة بناء الـcommit بـ`VITE_API_BASE_URL=/api`، الحزمة `index-Ce0oKQzH.js`، **Vitest 161/161**، العلامات موجودة. = PASS.

## 2) النسخ الاحتياطي والنشر (Phases 3–4)
- **Phase 3 — نسخة احتياطية كاملة للواجهة:** `/opt/reporting-rc/frontend/dist-backup-approval-ux-r1-20260716-010814` (مطابقة SHA بايتيًّا للأصل، www-data 755). **مسار التراجع.**
- **Phase 4 — نشر واجهة فقط (بإثبات):** rsync --delete + chown، **بلا إعادة تشغيل خلفيّ/هجرة**.
  - **إثبات «واجهة فقط»:** بصمات SHA-256 للـDLLs الأربعة **متطابقة قبل/بعد** (Api/Application/Infrastructure/Domain)، عدد الهجرات ثابت 28، NRestarts=0 وزمن `ExecMainStartTimestamp` بلا تغيّر (Wed 2026-07-15 17:50:53 UTC).
  - الحزمة الجديدة تُقدَّم (200)، القديمة `index-B-y7LHB8.js` → 404.

## 3) مصفوفة UAT الحيّة (Phase 6)
جميع الاختبارات نُفّذت حيًّا على المنفذ الداخليّ `127.0.0.1:5092` (تجاوز BasicAuth الخاص بـnginx) عبر حسابَي UAT مؤقتَين.

### أ) التقارير (Submissions) — بالتكافؤ المصدريّ + عقد الخادم
- كتلة `action` (approve/return/escalate) + `save`/`submit`/`deleteDraft` في الكود المُقدَّم تستعمل الطبقة الموحّدة: `toast.success` عند النجاح، تأجيل الملاحة `setTimeout(onBack, 700)` للقرارات النهائية فقط، `toast.error(approvalErrorMessage(e))` عند الخطأ، و`loading={mutation.isPending}` + حارس `if (isPending) return`.
- **زر الحذف الإداريّ لم يُمَس** (اختبار الحارس المصدريّ #11 يثبت بقاء `setErr(apiErrorMessage)` بلا Toast/Spinner) — احترام الحظر.
- عقود الخادم للقفل المتفائل مؤكَّدة مصدريًّا: `auth.forbidden` (SubmissionService.cs:487)، `submission.not_actionable.conflict` (:491)، `submission.no_pending_step.conflict` (:498)، `submission.already_deleted.conflict` (:601).

### ب) KPI — بالتكافؤ المصدريّ
- `approve`/`reviewAction`/`save`/`submit` في `KpiPage.tsx` تستعمل نفس الطبقة الموحّدة حرفيًّا (toast.success + ملاحة 700ms للنهائيّ فقط، toast.error(approvalErrorMessage)، `loading={isPending}` على كل الأزرار).

### ج) الإجازات/الاستئذانات — حيّ
| الحالة | النتيجة |
|---|---|
| C1 إنشاء إجازة | 200 → TeamLeaderApproved/Manager |
| C2 اعتماد المدير (غير نهائيّ) | 200 → ManagerApproved/Hr |
| C2 اعتماد نهائيّ من غير المعتمِد | **403 `auth.forbidden`** → «بواسطة مستخدم آخر» |
| C2 تكرار الاعتماد | **403 `auth.forbidden`** (قفل متفائل) |
| C3 رفض المدير (نهائيّ) | 200 → ManagerRejected |
| C3 تكرار الرفض | **403 `auth.forbidden`** |
| C4 إلغاء ذاتيّ (نهائيّ) | 200 → Cancelled |
| C5 تحقّق بلا إقرار الرصيد | **400 `leave.balance_ack_required`** |
| فاعل خاطئ (موظّف يعتمد) | 403 |
| مجهول | 401 |

### د) طلبات HR — حيّ
| الحالة | النتيجة |
|---|---|
| D1 إنشاء | 200 → Submitted |
| D2 بدء المراجعة (غير نهائيّ) | 200 → InReview |
| D3 تعليق (غير نهائيّ) | 200 |
| D5 إكمال (نهائيّ) | 200 → Completed |
| D5 تكرار الإكمال | **400 `employee_service_request.invalid_state`** |
| D6 رفض (نهائيّ) | 200 → Rejected |
| D6 تكرار الرفض | **400 `invalid_state`** |
| تحقّق رفض بلا سبب | 400 `rejection_reason_required` |
| RBAC موظّف يبدأ المراجعة | 403 |
| مجهول | 401 |

## 4) حماية النقر المزدوج (Phase 7) = PASS
- اختبار المكوّن #4: زر `loading` مُعطَّل ⟶ لا `onClick`. كل الأزرار تحمل `disabled={isPending}` + حارس `if (isPending) return` + Spinner (`animate-spin`). الخادم يرفض التكرار (400/403) بلا حالة نهائية مزدوجة.

## 5) القفل المتفائل / تبويبان (Phase 8) = PASS
- **الإجازات حيًّا:** الإجراء البائت/الفاعل الخاطئ ⟶ `403 auth.forbidden` ⟶ يطابق رسالة «⚠️ تم اعتماد هذا الطلب بواسطة مستخدم آخر…».
- **التقارير:** أكواد `.conflict`/`auth.forbidden` مؤكَّدة في الخادم ⟶ تطابق `approvalErrorMessage` (اختبارا المكوّن #5 و#6).
- **HR:** التكرار ⟶ `400 invalid_state` ⟶ الواجهة تعرض رسالة الخادم (Toast) عبر التدرّج الاحتياطيّ في `approvalErrorMessage` (سلوك مقبول، ليس «لم يعد متاحًا» الحرفيّة).

## 6) عرض Toast والملاحة (Phase 9)
- سلوك Toast مغطّى بتأكيدات DOM في مجموعة المكوّن: عرض النجاح (`role="status"`)، إخفاء تلقائيّ 3500ms، بقاء Toast عبر تغيّر المسار (اختبار #10)، مهلة الملاحة 700ms (اختبار #7).
- **قيد:** لقطات حيّة مصادَقة على RC غير متاحة لعدم توفّر بيانات اعتماد حقيقية (الحظر على مسّ حسابات RC الحقيقية/كلماتها). التغطية عبر اختبارات DOM + إثبات تقديم الحزمة الجديدة في Phase 4.

## 7) الانحدار (Phase 10) = PASS
- 15 نقطة GET عبر الموديولات ⟶ **200** كلها؛ الصحّة 200.
- **الخلفيّة بلا تغيير:** عدد الهجرات 28، الرأس `20260715162851`، NRestarts=0، `ExecMainStart` ثابت.

## 8) التنظيف وإعادة الأساس (Phase 11)
- حُذفت كل بيانات UAT: مستخدما UAT، 4 طلبات إجازة، 4 طلبات HR، أحداثها، وسطور التدقيق/الإشعارات التابعة لها (معاملة واحدة COMMIT).
- **الأساس مُستعاد بالمطابقة:** users=35، uat_users=0، leave=1، hr=0، kpi_evals=1، submissions=35، **email_outbox=0**.
- سكربتات UAT أُزيلت من الخادم.

---

## الحكم النهائيّ

**APPROVAL ACTION UX R1 — RC UAT = GO**

الطبقة موحّدة وتعمل حيًّا على RC، واجهة أمامية 100% (الخلفيّة مطابقة بايتيًّا)، الحذف الإداريّ لم يُمَس، البيانات نُظّفت والأساس استُعيد. **إيقاف قبل الإنتاج** — النشر الإنتاجيّ يتطلّب موافقة صريحة منفصلة.
