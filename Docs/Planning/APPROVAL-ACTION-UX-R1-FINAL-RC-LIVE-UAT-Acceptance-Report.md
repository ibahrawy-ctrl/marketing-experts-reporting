# APPROVAL ACTION UX R1 — تقرير قبول RC LIVE UAT النهائيّ (التقارير + KPI)

**التاريخ:** 2026-07-16
**النطاق المُصرَّح:** UAT حيّ فعليّ على RC للتقارير و KPI بإنشاء تركيبات مؤقتة وتشغيل دورات حياة تحوّل حقيقية، ثم تنظيف كامل وإيقاف قبل الإنتاج. **بلا تغيير كود، بلا إعادة بناء حزمة، بلا نشر RC جديد، بلا تغيير خلفيّ/هجرة، بلا مسّ حسابات RC الحقيقية أو الحذف الإداريّ، بلا نشر إنتاجيّ.**
**سبب هذه الجولة:** رفض إثبات التكافؤ المصدريّ السابق للتقارير و KPI — طُلب إثبات حيّ فعليّ بدورات تحوّل كاملة.
**بيئة RC:** الخدمة `khubara-reporting-rc` (منفذ 5092، البيئة ReleaseCandidate، قاعدة `reporting_rc`). UAT عبر المنفذ الداخليّ `127.0.0.1:5092/api` (تجاوز BasicAuth الخاص بـnginx).

---

## Phase 0 — إعادة تأكيد حالة RC + التقاط الأساس (قراءة فقط) = GO
الخدمة نشطة، الصحّة 200، البوابات معطّلة، عدد الهجرات 28 (الرأس `20260715162851_AddBypassTeamLeaderApproval`)، NRestarts=0، `ExecMainStart = Wed 2026-07-15 17:50:53 UTC`.
**الأساس المُلتقَط:** users=35، uat_users=0، report_submissions=35، submission_field_values=434، approval_steps=34، kpi_evaluations=1، kpi_results=10، kpi_evaluation_review_events=0، audit_logs=559، notifications=70، email_outbox=0، leave_requests=1، employee_service_requests=0، report_templates=41، kpi_templates=15، departments=5، teams=9.

## Phase 1 — نسخة احتياطية كاملة + مسار تراجع
- `DUMP=/root/db-backups/reporting_rc-prefinaluat-20260715-224922.dump`
- `SHA-256=54c37f5d08b54faac27a6bd07d8c85831b56063d3dd3546374c59f4a7bbf857b`
- **التراجع:** `sudo -u postgres pg_restore --clean --if-exists -d reporting_rc /root/db-backups/reporting_rc-prefinaluat-20260715-224922.dump`

## Phase 2 — بناء التركيبات (فعليّ حيّ)
سلسلة اعتماد من 4 مستخدمين + إدارة + فريق + قالب تقرير + قالب KPI. المعرّفات المسجّلة:
- deptId=`4017737a-…`، teamId=`5aeb99ae-…`
- admin=`6f594bb1-…`، manager=`18a2c516-…`، teamlead=`4ee9ae77-…`، employee=`f895adfb-…`
- reportTemplateId=`e55af705-…` (نسخة `fc2f227b-…`، حقل `e8e3caea-…`، إسناد `f0ccbbe2-…`)
- kpiTemplateId=`3a5efccf-…` (نسخة `494a5977-…`، مؤشر `5e3f4163-…`)

## Phase 3 — التقارير حيًّا = ALL PASS
| الإجراء | النتيجة الحيّة |
|---|---|
| R1 حفظ مسودة | create=Draft، بعد الحفظ=Draft |
| R2 إرسال | Submitted |
| R3 اعتماد (قائد الفريق = مستوى واحد) | 200 → Closed |
| R4 إعادة للتعديل | 200 → Returned |
| R5 تصعيد سالب (قائد على تقرير عضو) | 409 `submission.no_escalation_target.conflict` — **غير مُغيِّر للحالة** (يبقى Submitted)؛ يُمرِّن مسار toast.error/409 |
| R5 تصعيد موجب (مدير على تقرير قائد) | 200 → Escalated |

## Phase 4 — KPI حيًّا = ALL PASS
دورة حياة تقييم KPI أسبوعيّ كاملة: Save (Draft) → Submit (Submitted، النتيجة محتسَبة غير صفرية) → Approve → Reject → Close → Reactivate، كلها استجابات حيّة صحيحة والحالات متطابقة مع عقد الخادم. النتيجة تُحتسَب عند الإرسال فقط (Save-then-Submit) — لا نتيجة صفرية.

## Phase 5 — حماية النقر المزدوج = PASS
كل أزرار الإجراء تحمل `loading={isPending}` + `disabled` + حارس `if (isPending) return` + Spinner (`animate-spin`). النقرة الثانية أثناء التنفيذ لا تُطلق `onClick`. الخادم يرفض التكرار (403/409) بلا حالة نهائية مزدوجة.
**DOUBLE SUBMIT PROTECTION = PASS**

## Phase 6 — تبويبان / القفل المتفائل = PASS
الإجراء البائت على تقرير أُغلِق مسبقًا (تبويب ثانٍ) ⟶ الخادم يُرجِع `403 auth.forbidden` ⟶ الواجهة تعرض عبر `approvalErrorMessage` رسالة «⚠️ تم اعتماد هذا الطلب بواسطة مستخدم آخر…». بلا استثناء وبلا حالة تالفة.
**OPTIMISTIC LOCK UX = PASS**

## Phase 7 — الدليل البصريّ = PASS
لقطات حيّة من الواجهة (منفذ معاينة 5197، وكيل same-origin عبر نفق SSH إلى RC 5092):
- Toast نجاح أخضر (`bg-green-50 text-alert→text-success`) أعلى-وسط الشاشة (top=16px، مركز أفقيّ ≈ نصف العرض)، `dir="rtl"`، نصّ «✅ تم حفظ البيانات بنجاح».
- تكديس Toastين نجاح + تكديس Toastين خطأ أحمر (`bg-red-50 text-alert`) من 409 حيّ فعليّ (`no_escalation_target` على تقرير عضو — **غير مُغيِّر للحالة**).
- الإخفاء التلقائيّ ~3.5s مُثبَت (موجود عند t+3200ms، مختفٍ بحلول t+3700ms).
- الإغلاق اليدويّ عبر زرّ `aria-label="إغلاق"` (`×`): العدد 1→0.
**VISUAL EVIDENCE = PASS**

## Phase 8 — الانحدار = PASS
الصحّة 200؛ 14 نقطة GET عبر الموديولات ⟶ 200. **الخلفيّة مطابقة بايتيًّا:** عدد الهجرات 28 (الرأس `20260715162851`)، NRestarts=0، `ExecMainStart` ثابت (Wed 2026-07-15 17:50:53 UTC)، بصمات SHA-256 للـDLLs الأربعة (Api/Application/Infrastructure/Domain) مطابقة لـPhase 0.
**REGRESSION = PASS**

## Phase 9 — التنظيف وإعادة الأساس = PASS
حُذفت كل بيانات UAT في معاملة محروسة (COMMIT واحد): 4 مستخدمين، 8 تقارير + توابعها (submission_field_values، approval_steps)، 4 تقييمات KPI + توابعها (kpi_results، kpi_evaluation_review_events)، القالبان (cascade للنسخ/الحقول/المؤشرات/الإسناد)، الإشعارات وسطور التدقيق التابعة، الفريق، الإدارة.
**التحقّق بعد التنظيف — الأساس مُستعاد بالمطابقة التامّة:**
- users=35، uat_users=0، report_submissions=35، kpi_evaluations=1، leave_requests=1، employee_service_requests=0، **email_outbox=0**، departments=5، teams=9، report_templates=41، kpi_templates=15.
- كل معرّفات التركيبات (users/dept/team/rt/kt) = 0.
- **بلا يتامى:** submission_field_values / approval_steps / kpi_results / kpi_evaluation_review_events المعزولة = 0.
- سكربتات UAT أُزيلت من الخادم (0 متبقٍّ)، الأنفاق (5092/5099) ومعاينة الواجهة (5197) أُوقِفت، التوكن المحقون مُسِح من المتصفّح.

---

## الحكم النهائيّ

**APPROVAL ACTION UX R1 — FINAL RC LIVE UAT = GO**

نُفِّذت دورات حياة تحوّل حيّة فعليّة على RC للتقارير (حفظ/إرسال/اعتماد/إعادة/تصعيد ±) و KPI (حفظ/إرسال/اعتماد/رفض/إغلاق/إعادة تفعيل) — كلها PASS؛ حماية النقر المزدوج والقفل المتفائل مؤكَّدتان حيًّا؛ الدليل البصريّ للـToast (الموضع/RTL/الإخفاء التلقائيّ/الإغلاق/التكديس) مُلتقَط؛ الانحدار سليم والخلفيّة مطابقة بايتيًّا؛ كل بيانات UAT نُظّفت والأساس استُعيد بالمطابقة (email_outbox=0). **إيقاف قبل الإنتاج** — النشر الإنتاجيّ يتطلّب موافقة صريحة منفصلة.
