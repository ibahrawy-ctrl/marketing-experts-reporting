# LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1 — تقرير قبول RC

**القرار النهائي:** `LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1 RC PASS — PRODUCTION TOOL READY`

**التاريخ:** 2026-08-05 · **البيئة:** Release Candidate (`reporting_rc`) حصرًا · **الإنتاج لم يُمَسّ.**

---

## 1. الملخّص التنفيذي
أداة Console آمنة لمرّة واحدة (one-shot) تطوي **خطوة المدير** لطلبَي الإجازة العالقَين في جمود حقيقيّ عند خطوة Manager بلا بديل تشغيليّ، باستخدام **نفس منطق P2 المنشور** حرفيًّا. الأداة تنقل الطلب إلى خطوة HR وتكتب حدث تدقيق واحدًا صحيحًا، **دون** اعتماد HR أو إنشاء Ledger أو خصم رصيد أو مساس أيّ طلب آخر. الوضع الافتراضي `Plan` (قراءة فقط)، ووضع `--execute` مُبوَّب بأربع بوّابات إلزاميّة، مع Idempotency كامل. **لا Endpoint/UI/Migration/Scheduler/SQL مباشر.** قبول RC اكتمل بنجاح على ستّ حالات خياليّة تغطّي كلّ الأصناف، مع تنظيف تامّ وإثبات عدم مساس الإنتاج.

## 2. النطاق الدقيق (In-Scope)
- طيّ خطوة المدير **فقط** للطلبَين العالقَين: أحمد نصار `9d445a3e`، سمر مجدي `2407739b` (على الإنتاج مستقبلًا بتصريح مستقلّ — **ليس الآن**).
- الانتقال من `TeamLeaderApproved/Manager` إلى `ManagerApproved/Hr` + حدث `manager_step_auto_folded_no_operational_manager`.
- Plan افتراضيّ، Execute مُبوَّب، Idempotency، أكواد خروج محدّدة.

## 3. خارج النطاق (Out-of-Scope)
- محمد إبراهيم ونور الدين رجب (كلاهما عند Hr — اعتماد HR نهائيّ، ليسا جمودًا).
- ريم جاب الله / عائشة كمال / بسنت محمد (استمرار طبيعيّ — يوجد بديل تشغيليّ).
- أيّ طلب آخر، أيّ اعتماد HR، أيّ إنشاء Ledger، أيّ خصم رصيد.
- **بلا** نشر على الإنتاج، **بلا** Plan على الإنتاج، **بلا** Manifest إنتاجيّ، **بلا** بدء تذكرة أخرى.

## 4. سلسلة النَّسب (Baseline Lineage)
- الأساس المثبّت: `2d282cebf0a22f65b78cd751de17d6c927128d0d` (P2 المنشور فعليًّا على الإنتاج، LEAVE-WORKFLOW-DEADLOCK-HOTFIX).
- المرشّح المُجمَّد: `976575672939396e40d86c926a676bbc6418e114` فوق الأساس مباشرة (parent = `2d282ce`).
- Tree: `6fc12b03…` · 13 ملفًّا (+1895/−32) · **0 هجرة** (الرأس ثابت `20260724224053`).

## 5. البنية الهندسيّة (Extract-and-Delegate)
- دوال نقيّة مشتركة داخليّة: `OperationalManagerResolver` + `LeaveWorkflowFoldSemantics`.
- خدمة داخليّة `LeaveWorkflowReconciliationService` تعيد استخدامها؛ و`LeaveRequestService.cs` الإنتاجيّ يفوّض إليها ⇒ **مصدر واحد للحقيقة** لمنطق الطيّ (لا تكرار، لا انحراف عن سلوك الإنتاج).
- المُغلِّف CLI في `Program.cs` (أوضاع متعارضة، Plan افتراضيّ، Manifest إلزاميّ في كلّ الأوضاع).

## 6. عقد الـManifest
```json
{"schemaVersion":1,"batchId":"leave-deadlock-r1-20260805","maxItems":2,
 "items":[{"requestId","expectedEmployeeUserId",
 "expectedStatus":"TeamLeaderApproved","expectedCurrentStep":"Manager","expectedLedgerCount":0}]}
```
القواعد: `schemaVersion==1`، `batchId` إلزاميّ، `maxItems ∈ [1,2]`، عدد البنود `∈ [1,2]` و`≤ maxItems`، لا `requestId` مكرّر أو غير صالح، `expectedLedgerCount==0` (أيّ حركة رصيد تمنع الطيّ)، `expectedStatus` إلزاميّ، لا بريد/سرّ في النصّ. أيّ إخلال ⇒ exit 3 (Manifest) أو exit 9 (أمان).

## 7. بوّابات وضع Execute
`ExecuteGate.Evaluate` تتطلّب **جميع**: `--execute` + `--manifest` + `--expected-count 2` + `--batch-id` (يطابق الـManifest) + `--confirm LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1`. أيّ نقص ⇒ **لا كتابة** + خروج غير صفريّ (2 لنقص/رمز خاطئ، 4 لعدم تطابق العدد/الـbatch).

## 8. ترتيب التصنيف (ClassifyAsync)
منطق حاسم مرتّب: `manifest_employee_mismatch` → `ledger_count_mismatch` → `ledger_row_exists` → `fold_event_present` (AlreadyApplied) → `already_past_manager_step` → InvalidState الطرفيّة → `not_manager_step_pending` → `manifest_state_mismatch` → ManualReview → `operational_manager_alternative_exists` (Natural) → `structural_deadlock_foldable` (Eligible). فحوص AlreadyApplied/LedgerExists/InvalidState **تسبق** `manifest_state_mismatch` ⇒ D/E/F تُصنَّف صحيحًا حتى لو أعلن الـManifest توقّعات طيّ.

## 9. حارس أمان Execute (exit 5 / exit 6)
كلّ البنود تُقيَّم قراءةً أوّلًا؛ إن كان أيّ بند **ليس** Eligible/AlreadyApplied ⇒ **exit 5 بصفر كتابة**. ثمّ تنفيذ تسلسليّ يتوقّف عند أوّل فشل ⇒ exit 6 (فشل جزئيّ — لا يُستدعى إلا بتغيّر حالة متزامن أثناء التسلسل، مُغطّى حتميًّا باختبارات التكامل).

## 10. النسخة الاحتياطيّة قبل RC (Preflight Backup)
`/opt/reporting-rc/db-backups/reporting_rc-preleaverecon-20260805-090305.dump` — محفوظ (دليل نسخ RC المخصّص).

## 11. تحقّق بصمة الأداة على خادم RC
`SHA256` لـDLL الأداة على خادم RC = `5a5519e979d6e6f978ae8ec67781a161c5061f90ea933860f8200fbc25b75b35` — **مطابق** لبصمة المرشّح المُجمَّد محليًّا.

## 12. الحالات الخياليّة المبذورة (RC Seed)
ملفّ `rc-seed.sql` (بادئة `fec0`، وسم `[RECON-RC-FIX]`) بذر 13 مستخدمًا + 6 طلبات + حدث فولد واحد (D) + صفّ Ledger واحد (F):
| الحالة | الطلب | الوضع المبذور | التصنيف المتوقَّع |
|---|---|---|---|
| A | `fec00002-…a0` | TeamLeaderApproved/Manager، TL=المدير المباشر، لا بديل | Eligible |
| B | `fec00002-…b0` | مثل A | Eligible |
| C | `fec00002-…c0` | فوق TL مديرٌ تشغيليّ حقيقيّ (Mgr C) | Natural |
| D | `fec00002-…d0` | ManagerApproved/Hr + حدث فولد سابق | AlreadyApplied |
| E | `fec00002-…e0` | Submitted/TeamLeader | InvalidState |
| F | `fec00002-…f0` | TeamLeaderApproved/Manager + صفّ Ledger | LedgerExists |

## 13. ملفّات الـManifest للقبول
ثلاثة (بسبب `maxItems≤2`): `rc-manifest-AB.json` (A,B)، `rc-manifest-CD.json` (C,D)، `rc-manifest-EF.json` (E,F). كلّها `batchId=leave-deadlock-r1-20260805`، بنودها تُعلن `expectedStatus=TeamLeaderApproved / expectedCurrentStep=Manager / expectedLedgerCount=0`.

## 14. الخطوة 1 — Plan (قراءة فقط)
- A/B ⇒ **Eligible** (`structural_deadlock_foldable`).
- C ⇒ **Natural** (`operational_manager_alternative_exists`).
- D ⇒ **AlreadyApplied** (`fold_event_present`).
- E ⇒ **InvalidState** (`not_manager_step_pending`).
- F ⇒ **LedgerExists** (`ledger_count_mismatch`).
- **صفر كتابة** على القاعدة (تحقّق بعدّاد التدقيق/الحالة قبل وبعد Plan).

## 15. الخطوة 2 — Execute المُبوَّب (A+B فقط)
- بوّابة سلبيّة أوّلًا: Execute بلا `--confirm` ⇒ **exit 2، صفر كتابة**.
- Execute كامل البوّابات (`--execute --manifest rc-manifest-AB.json --expected-count 2 --batch-id leave-deadlock-r1-20260805 --confirm LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1`):
  - A و B ⇒ `Status=ManagerApproved`، `CurrentStep=Hr`، `ManagerReviewerId` مضبوط.
  - **حدث تدقيق واحد بالضبط لكلٍّ**: `manager_step_auto_folded_no_operational_manager` (`TeamLeaderApproved→ManagerApproved`).
  - **لا اعتماد HR، لا صفّ Ledger جديد** (بقي 1 = صفّ F فقط)، **لا خصم رصيد**.
  - بقيّة الحالات (C/D/E/F) **دون مساس**.

## 16. الخطوة 3 — Idempotency (Execute ثانٍ على A+B)
تشغيل Execute ثانٍ بنفس الوسائط ⇒ **AlreadyApplied=2، صفر كتابة**؛ عدد أحداث التدقيق بقي **1 لكلّ طلب** (لا تكرار).

## 17. الخطوة 4 — حارس الأمان الحيّ (exit 5)
Execute على `rc-manifest-CD.json` (C=Natural) ⇒ **exit 5، صفر كتابة**، C دون مساس ⇒ يثبت أنّ Natural **لا يمكن** دفعها عبر Execute، وأنّ الكتابة الجزئيّة مستحيلة. (exit 6 موثَّق كمُغطّى باختبارات التكامل / تزامن فقط.)

## 18. الخطوة 5 — إثبات الأمان (Security)
مُخرَجات الأداة تُظهر **فقط**: RequestId مُقنَّع (أوّل 8 أحرف + «…»)، القرار، رمز السبب، الحالة قبل/بعد، علم الكتابة، الأزمنة. **لا** أسرار/عمليّات/منافذ/خدمات/cron في أيّ مخرَج.

## 19. مصفوفة أكواد الخروج
`0` نجاح · `2` بوّابة (نقص/رمز خاطئ) · `3` Manifest غير صالح · `4` عدم تطابق count/batch · `5` بند غير مؤهّل (صفر كتابة) · `6` فشل جزئيّ · `7/8` أخطاء تشغيليّة · `9` انتهاك أمان.

## 20. الاختبارات النقيّة والتكامليّة
- `LeaveWorkflowReconciliationManifestGateTests.cs`: 27 اختبارًا نقيًّا (Manifest 1-7f، بوّابات Execute، أمان 43-48) — تُدرَج مصادر `ManifestLoader.cs` + `ExecuteGate.cs` عبر `<Compile Include>` (لا Program.cs).
- اختبارات تكامل الطيّ/التصنيف (≥54 إجماليًّا مع النقيّة) — كلّها خضراء على الأساس.

## 21. تنظيف RC (Cleanup)
حُذف: 3 أحداث + 1 Ledger + 6 طلبات + 1 UserRole + 13 مستخدمًا (كلّ عدّادات `fec0` = 0). أُزيلت من `/tmp` ملفّات الـManifest + `rc-seed.sql` + SQL التنظيف، ومجلّد `/opt/reporting-rc/leave-recon-tool`.

## 22. صحّة RC بعد التنظيف
health = **200**؛ الهجرات = **30**، الرأس `20260724224053`؛ outbox pending = **0**؛ المجدول دون تغيير.

## 23. إثبات عدم مساس الإنتاج (قراءة فقط)
- prod health = **200**؛ prod migrations = **30**، الرأس `20260724224053_AddReportApproverAndKpiReviewerOverrides`.
- الطلبان الحقيقيّان `9d445a3e` و`2407739b` **ما زالا** `TeamLeaderApproved/Manager` (دون تغيير) ⇒ الأداة لم تُشغَّل على الإنتاج إطلاقًا.
- **0** حالات `fec0` تسرّبت إلى `reporting_prod`.

## 24. المخاطر المتبقّية والتخفيف
- الطلبان الحقيقيّان يبقيان عالقَين حتى **تصريح إنتاجيّ مستقلّ** — هذا مقصود (الأداة جاهزة، النشر محظور الآن).
- exit 6 غير قابل للتكرار في تشغيل RC نظيف أحاديّ العمليّة ⇒ يُغطّى حتميًّا باختبارات التكامل.

## 25. مسار التراجع (Rollback)
لا يوجد أثر إنتاجيّ للتراجع عنه (لم يُنشَر شيء). على RC: النسخة الاحتياطيّة §10 متاحة؛ القاعدة نُظِّفت بالفعل إلى حالتها قبل البذر.

## 26. المحظورات المؤكَّدة (ما لم يُفعَل)
لا نشر إنتاجيّ · لا Plan/Execute إنتاجيّ · لا كتابة على الإنتاج · لا اعتماد HR · لا إنشاء Ledger · لا خصم رصيد · لا معالجة محمد إبراهيم/نور الدين · لا Manifest إنتاجيّ · لا بدء تذكرة أخرى · لا SQL مباشر · لا Migration/Endpoint/UI/Scheduler.

## 27. الأدلّة المرجعيّة
- المرشّح: `976575672939396e40d86c926a676bbc6418e114` (parent `2d282ce`).
- بصمة DLL: `5a5519e979d6e6f978ae8ec67781a161c5061f90ea933860f8200fbc25b75b35`.
- Backup RC: `/opt/reporting-rc/db-backups/reporting_rc-preleaverecon-20260805-090305.dump`.

## 28. معايير القبول — الحصيلة
| المعيار | النتيجة |
|---|---|
| Plan يصنّف الأصناف الخمسة صحيحًا بصفر كتابة | PASS |
| Execute مُبوَّب يطوي Eligible فقط (HR/CurrentStep + تدقيق واحد) | PASS |
| لا اعتماد HR/Ledger/خصم | PASS |
| Idempotency (AlreadyApplied ثانيًا، صفر تغيير) | PASS |
| حارس الأمان يوقف بأمان (exit 5) | PASS |
| الأمان: لا أسرار/عمليّات/منافذ/خدمات/cron | PASS |
| تنظيف RC كامل + صحّة/هجرات/outbox | PASS |
| الإنتاج لم يُمَسّ (قراءة فقط) | PASS |

## 29. القرار النهائي
جميع معايير القبول الخمسة والفرعيّة **PASS**؛ التنظيف تامّ؛ الإنتاج مُثبَت عدم مساسه.

**`LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1 RC PASS — PRODUCTION TOOL READY`**
