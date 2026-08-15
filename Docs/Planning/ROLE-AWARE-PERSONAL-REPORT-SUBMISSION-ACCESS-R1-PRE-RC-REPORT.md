# ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1 — تقرير ما قبل RC (Pre-RC)

> **حالة التنفيذ:** المرشّح النهائي جاهز لمراجعة تصريح RC. **لم يُنفَّذ أي نشر على RC أو الإنتاج**، ولم يُنشأ أي Git tag. هذا التقرير توثيقيّ فقط ويسبق الحصول على تصريح مستقلّ للنشر.

---

## 1) رابط المصدر (Artifact SourceLink)

| العنصر | القيمة |
|--------|--------|
| الفرع | `candidate/role-aware-personal-report-final-r1` |
| Commit المرشّح | `21d397d91eb93814ee21b566faf4f210e328c03b` |
| الأب (Parent) | `e66f1c86e8e976b05c421fdbaf234d157666060d` (القاعدة المعتمَدة على RC) |
| Tree المرشّح | `d05b875e1780f7674a1dddc41328d8f88dbdfd80` |
| المرشّح الجزئي المجمَّد (المرجع) | `11a656af0c7c9f0eaeb2cfdcefb28f6fa35463ba` — write-tree `83093c0b1ae7d157fc2411c29f31a43c39cb47aa` |
| نطاق التغيير | **25 ملفًّا، +5315 / −50**، commit واحد فوق `e66f1c86` مباشرة |

**إثبات عدم المساس بـ 11a656af:** hash المرشّح الجزئي وأبيه و tree الكتابة `83093c0b…` لم تتغيّر. من ملفات الوصول الشخصيّ الأربعة عشر: 12 متطابقة بايت-ببايت، وملفّان فقط **إضافيّان (Additive)** لإدماج آليّة التجاوز: `KpiEvaluationService.cs` و`SubmissionService.cs` (المنطق الأصليّ محفوظ حرفيًّا في الفرع البديل).

---

## 2) بصمات مكتبات الخادم (DLL SHA-256) — publish `-c Release`

```
017dd0882bf2fd23e12e5b1a74a80b534355cbb94e8d8079fd7575253da2a23f  Reporting.Api.dll
abbcc1a8ce64a035f3cb75adf47043f78a8f4a91bb4d7785f518936ae3d4b1c1  Reporting.Application.dll
620328609e5d515caeb236c8bc27e36715f2900f77daf481ea3531161faea1b0  Reporting.Infrastructure.dll
a1d7b471cf5ebef90f240881bba5b4aea498b0c2bcf24f9db3fde292bde612b4  Reporting.Domain.dll
```

**تحقّق العلامات داخل `Reporting.Infrastructure.dll`:**
- `AddReportApproverAndKpiReviewerOverrides` (اسم الهجرة، UTF-8) = 2
- `KpiReviewerOverrideUserId` (UTF-16LE) = 3
- `kpi.reviewer_override_invalid` (UTF-16LE) = 1
- `approval.override_invalid` (UTF-16LE) = 1
- `ReportApproverOverrideUserId` (اسم عضو، UTF-8) = 1

---

## 3) حزمة الواجهة (Frontend bundle/hash)

| العنصر | القيمة |
|--------|--------|
| الحزمة | `dist/assets/index-DMzwSsTv.js` (1,309,944 bytes) |
| SHA-256 | `ad7f3c6177b4550e9939b5c9e1c6f5c20578399aa83006f903df4060c82563e7` |

> **ملاحظة نشر:** بُنِيت هذه الحزمة للبوّابة فقط دون `VITE_API_BASE_URL` فظهر مرجع `localhost` واحد (القيمة الافتراضيّة في `api.ts`). **حزمة RC/الإنتاج يجب أن تُبنى بـ `VITE_API_BASE_URL` لعنوان الـ API الإنتاجيّ** وفق النمط المعتمَد، وهو ما يُزيل الافتراضيّ ويغيّر الـ hash.

---

## 4) هجرة قاعدة البيانات (Migration SQL)

- الهجرة: `20260724224053_AddReportApproverAndKpiReviewerOverrides`
- الهجرة السابقة (نقطة الانطلاق): `20260716015239_KpiEvaluationPartialUniqueIndex`
- **إضافيّة بالكامل** — لا `ALTER`/`DROP` لأيّ عمود قائم. حقلان nullable + فهرسان + قيدَا FK بسلوك **RESTRICT**.

```sql
-- (idempotent، معاملة واحدة)
ALTER TABLE "AspNetUsers" ADD "KpiReviewerOverrideUserId" uuid;
ALTER TABLE "AspNetUsers" ADD "ReportApproverOverrideUserId" uuid;
CREATE INDEX "IX_AspNetUsers_KpiReviewerOverrideUserId"    ON "AspNetUsers" ("KpiReviewerOverrideUserId");
CREATE INDEX "IX_AspNetUsers_ReportApproverOverrideUserId" ON "AspNetUsers" ("ReportApproverOverrideUserId");
ALTER TABLE "AspNetUsers" ADD CONSTRAINT "FK_AspNetUsers_AspNetUsers_KpiReviewerOverrideUserId"
    FOREIGN KEY ("KpiReviewerOverrideUserId")    REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT;
ALTER TABLE "AspNetUsers" ADD CONSTRAINT "FK_AspNetUsers_AspNetUsers_ReportApproverOverrideUserId"
    FOREIGN KEY ("ReportApproverOverrideUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT;
INSERT INTO "__EFMigrationsHistory" VALUES ('20260724224053_AddReportApproverAndKpiReviewerOverrides','8.0.11');
```

`NULL` في أيّ حقل = الإبقاء على التوجيه/المراجعة الحاليّين دون تغيير. `dotnet ef migrations has-pending-model-changes` = **No changes**.

---

## 5) خطّة البيانات (Data Plan) — أداة `RoleAwareOverridePublisher`

أداة تطبيقيّة آمنة لمرّة واحدة (AppDbContext/UserManager)، خارج `Reporting.sln`، تحلّ المستخدمين بالبريد + الدور، **لا SQL خام ولا UserIds داخل الهجرة**. الأوضاع: Dry-Run (افتراضي) / `--apply` / `--rollback` / `--rollback --apply`.

**عقد الأدوار المستهدَفة:** المعتمِد ⟵ `{Ceo}`؛ GM ⟵ `{GeneralManager}`؛ HRADMIN ⟵ `{Hr,Admin}`؛ MANAGER ⟵ `{Manager,FinanceManager}`؛ CEOSUPPORT ⟵ `{CeoSupport}`. لكلٍّ: تحقّق الوجود + `IsActive` + الدور المتوقّع + منع الإسناد الذاتيّ.

**مخرجات Dry-Run (على قاعدة معزولة `reporting_role_pub`):**
```
المعتمِد: <ceo> — CEO ✓ نشط ✓
→ سيتغيّر | GM        : ReportApproverOverride NULL ⟶ <ceo> ؛ KpiReviewerOverride NULL ⟶ <ceo>
→ سيتغيّر | HRADMIN   : ReportApproverOverride NULL ⟶ <ceo> ؛ KpiReviewerOverride NULL ⟶ <ceo>
→ سيتغيّر | MANAGER   : ReportApproverOverride NULL ⟶ <ceo> ؛ KpiReviewerOverride NULL ⟶ <ceo>
→ سيتغيّر | CEOSUPPORT: ReportApproverOverride NULL ⟶ <ceo> ؛ KpiReviewerOverride NULL ⟶ <ceo>
عدد المستهدَفين: 4 — عدد التغييرات: 4
لا يُمَسّ ManagerId / TeamId / DepartmentId / BypassTeamLeaderApproval لأيّ مستخدِم.
Dry-Run — لم يُكتب شيء (Rollback). للتطبيق مرّر --apply.
```

---

## 6) مصفوفة قبل/بعد (الأربعة على الإنتاج)

| الموظّف | الدور | ManagerId (لا يتغيّر) | ReportApproverOverride | KpiReviewerOverride |
|---------|-------|----------------------|------------------------|---------------------|
| أحمد عبدالرؤوف | GeneralManager | أحمد عبدالرؤوف كما هو | NULL ⟶ إبراهيم البحراوي | NULL ⟶ إبراهيم البحراوي |
| محسن مجدي | Hr/Admin | أحمد عبدالرؤوف (**دون تغيير**) | NULL ⟶ إبراهيم البحراوي | NULL ⟶ إبراهيم البحراوي |
| محمد عبدالله | Manager | أحمد عبدالرؤوف (**دون تغيير**) | NULL ⟶ إبراهيم البحراوي | NULL ⟶ إبراهيم البحراوي |
| فاطمة محمد | CeoSupport | كما هو | NULL ⟶ إبراهيم البحراوي | NULL ⟶ إبراهيم البحراوي |

**الأثر:** يتوجّه اعتماد تقارير الأربعة ومراجعة KPI لهم مباشرةً إلى إبراهيم البحراوي، **دون** تغيير `ManagerId/TeamId/DepartmentId`، ودون تغيير نطاق الرؤية أو مسار الإجازات أو لوحات المديرين.

---

## 7) بوّابة الجودة (P12) — كل النتائج خضراء بلا تراجع

**الخادم:**
- بناء Release: **0 تحذير / 0 خطأ**؛ بناء الأداة: 0/0.
- `has-pending-model-changes` = **No changes**.
- اختبارات الوحدة: **283/283**.
- تكامل ROLE-AWARE المستهدَف: **37/37** على `reporting_role_cand`.
- تفاضل القاعدة/المرشّح (قاعدتان معزولتان): **0 فشل جديد خاصّ بالمرشّح، 0 فشل في ROLE-AWARE** (الإخفاقات المتبقّية بيئيّة تراكميّة قائمة في القاعدة نفسها).
- دورة الهجرة: apply ⟶ rollback (إلى `20260716015239`) ⟶ re-apply، مع تحقّق FK = RESTRICT، على `reporting_role_mig`. ✓
- دورة الأداة: Dry-Run (4 محتملة، 0 كتابة) ⟶ Apply (report=4, kpi=4) ⟶ Apply ثانية (0 = idempotent) ⟶ Rollback preview (4) ⟶ Rollback apply (any_nonnull=0)؛ حقول `ManagerId/TeamId/DepartmentId/Bypass` = 0 غير-فارغ. ✓

**الواجهة (تفاضل القاعدة مقابل المرشّح):**

| البوّابة | القاعدة `e66f1c86` | المرشّح `21d397d` |
|----------|--------------------|--------------------|
| `tsc --noEmit` | 0 خطأ | 0 خطأ |
| `build` | ✓ 938ms | ✓ 952ms (تحذير signalr `/*#__PURE__*/` الحميد فقط) |
| `vitest` | 271/271 (26 ملفًّا) | 271/271 (26 ملفًّا) |
| `eslint` | 32 (23 خطأ/9 تحذير) | 32 (23 خطأ/9 تحذير) — **مجموعة القضايا متطابقة**، الفارق الوحيد إزاحة أرقام أسطر 6 أخطاء `react-refresh` القائمة في `SubmissionsPage.tsx` بمقدار +10 بسبب الشيفرة الإضافيّة |

> **ملاحظة بيئيّة:** `node_modules` داخل `~/Documents` مُدار عبر iCloud (نسخ تعارض `… 2` وملفات مُفرَّغة) ما سبّب مهلات قراءة (`os error 60`). أُجريت بوّابة الواجهة على نسختَي شجرة عمل على قرص محلّي غير مُزامَن مع `npm ci` نظيف (نفس `package-lock.json` المطابق للقاعدة والمرشّح).

---

## 8) خطّة التراجع (Rollback)

1. **الأداة:** `--rollback --apply` يُعيد حقلَي التجاوز للأربعة إلى `NULL` (idempotent، لا يمسّ أيّ حقل تنظيميّ).
2. **الواجهة/الخادم:** استعادة نسخ `publish`/`dist` الاحتياطيّة السابقة + `systemctl restart reporting-api`.
3. **الهجرة (عند الضرورة القصوى فقط):** عكسها = إسقاط الفهرسَين + قيدَي FK + العمودين على `AspNetUsers` — آمن لأنّها إضافيّة بحتة و FK بسلوك RESTRICT ولا تمسّ صفوفًا قائمة؛ أو استعادة نسخة DB.

---

## 9) ترتيب النشر المقترح (عند صدور تصريح RC مستقلّ)

1. أخذ نسخ احتياطيّة (DB + backend `publish` + frontend `dist`).
2. نشر الخادم (`rsync --delete --exclude appsettings.Development.json` + `chown www-data` + `restart`) — تُطبَّق الهجرة الإضافيّة تلقائيًّا عند الإقلاع.
3. بناء ونشر الواجهة بـ `VITE_API_BASE_URL` الإنتاجيّ.
4. تشغيل الأداة **Dry-Run أولًا** بالبُرُد الحقيقيّة للأربعة + المعتمِد إبراهيم، مراجعة مصفوفة قبل/بعد، ثمّ `--apply` بموافقة صريحة.
5. تحقّق دخان قراءة-فقط: توجيه اعتماد الأربعة/مراجعة KPI إلى إبراهيم، وثبات `ManagerId/TeamId/DepartmentId` والرؤية والإجازات.

---

## الخلاصة

اكتمل مرشّح الوصول الشخصيّ واعتماد التقارير وKPI المخصّص — الأربعة يتوجّه اعتمادهم مباشرةً لإبراهيم دون تغيير `ManagerId` أو نطاق الرؤية أو الإجازات، والقادة يملكون مساراتهم الشخصيّة والإداريّة معًا — **المرشّح جاهز لمراجعة تصريح RC** (بلا أيّ تنفيذ على RC/الإنتاج، وبلا Git tag).
