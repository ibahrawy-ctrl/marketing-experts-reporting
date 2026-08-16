# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 19: بوّابة ما قبل الدمج

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** K — Pre-Merge Gate (Backend · Frontend · EF/Migrations)
**التاريخ:** 16 أغسطس 2026
**الحكم:** **البوّابة خضراء** · انحدار المرشَّح = 0 · عيبان حاجبان في الواجهة كُشِفا وأُصلِحا هنا

---

## 1) بوّابة الخلفيّة (K.1)

شُغِّلت عبر الحارس الملتزَم `Ops/TestRegressionGate/run-full-regression.sh` على ثلاث قواعد
جديدة تمامًا (`rr_k_main` · `rr_k_cal` · `rr_k_pfe`) — وهي **أوّل تشغيل موجب** للحارس بعد
تحقّقه السلبيّ في التقرير 18.

| البند | النتيجة |
|---|---|
| فحوص الحارس السبعة | `GATE PRECHECK OK` ثمّ `GATE READY` |
| التمهيد المتتابع | main 1/1 · cal 10/10 · pfe 8/8 |
| جداول كلّ قاعدة بعد التمهيد | 78 = 78 |
| عبارة التصنيف | `UPDATE 5` |
| اختبارات الوحدة | **359 / 359** |
| اختبارات التكامل | **Failed 1 · Passed 1992 · Total 1993** · 8 د 7 ث |
| الفشل الوحيد | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` |

الفشل مطابق حرفيًّا لـ`BASELINE-DEFECT-01` اسمًا وتوقيعًا (`Expected: OK / Actual: NotFound`
عند `AdminGovernanceTests.cs:366`)، وهو مشترك مع الأبوَين ⟹ **ليس انحدار مرشَّح**، ولم يتوسّع،
ولا يوجد فشل ثانٍ غير مصنَّف.

**السجلّ:** `/tmp/lineage/reg2/K-gate.log`
**التطابق مع المرجع:** الأرقام مطابقة تمامًا لجولة `cand10` ⟹ الالتزامات الستّة للمرحلة J
**صفر انحدار**.

---

## 2) بوّابة الواجهة (K.2) — عيبان حاجبان كُشِفا هنا لأوّل مرّة

| البند | قبل | بعد |
|---|---|---|
| `npm ci` من ملفّ القفل | نجاح | نجاح |
| `tsc -b` | **TS1131** عند `types/api.ts:3798` | **نظيف** |
| `vite build` | ✓ (لا يكشف العطب) | ✓ 803 مللي |
| `vitest run` | 548/548 في 45 ملفًّا | **550/550 في 46 ملفًّا** |
| ESLint على ملفّات النطاق | 3 نتائج | 3 نتائج **سابقة للتذكرة حرفيًّا** |

### 2.1 العيب الأوّل — واجهة مفتوحة أسقطت فحص الأنواع

حلّ التعارض في `types/api.ts` أسقط **قوس الإغلاق** لواجهة `ProjectFirstExecutionReport<TRow>`
القادمة من `develop`، فابتلعت الواجهةُ كلَّ ما بعدها حتّى أوّل تعريف غير صالح داخل جسم واجهة.

**لماذا لم يُكشَف قبل الآن:** `vite build` يستعمل esbuild الذي **يجرّد الأنواع بلا فحص**،
فيُنتج حزمة سليمة رغم أنّ `npm run build` الحقيقيّ (`tsc -b && vite build`) يسقط. كلّ تقرير
سابق قاس البناء بـ`vite build` وحده فأعطى «أخضر» مضلِّلًا.

### 2.2 العيب الثاني — رابطان حيّان يؤدّيان إلى لا شيء

الدمج أسقط من `App.tsx` تسجيلَي المسارَين التاليَين **مع بقاء** صفحتَيهما وخطّافَيهما
وروابط التنقّل إليهما في `navConfig.ts`:

| المسار | الصفحة الباقية | رابط التنقّل الباقي | الدور |
|---|---|---|---|
| `/app/governance-workspace` | `GovernanceWorkspacePage.tsx` | «ورشة الحوكمة» | 7 أدوار |
| `/app/positions` | `PositionsPage.tsx` | «المناصب المرنة» | `Admin` |

هذا هو **النظير الأماميّ** لضياع `ScopeResolver.ResolvePositionScopeAsync` و`positions.manage`
الموثَّق في التقرير 06 §4.1: نفس الميزة، نفس صنف الحذف أحاديّ الجانب، طرف آخر من النظام.
لولا إصلاحه لبقيت قدرة `positions.manage` مستعادة في الخلفيّة **بلا أيّ شاشة تصل إليها**.

### 2.3 حارس الانحدار المُضاف

`reporting-frontend/src/routeRegistry.test.ts` — يقارن كلّ `to:` في `navConfig.ts` بجدول
المسارات في `App.tsx`، ويثبّت المسارَين المستعادَين صراحةً.

**التحقّق السلبيّ المُنفَّذ فعليًّا** (على شجرة ما قبل الإصلاح `ba00488`):

```
App.prefix.tsx → روابط بلا مسار: ["/app/governance-workspace","/app/positions"]
src/App.tsx    → روابط بلا مسار: []
```

### 2.4 دَين ESLint = 0 جديد

النتائج الثلاث (`navConfig.ts` `_ctx` · `api.ts` واجهتان فارغتان عند 2914 و3023) موجودة
**حرفيًّا** في `10c26f7` (`navConfig.ts:208` · `api.ts:2757` و`2866`) ⟹ لم تُدخِل التذكرة أيّ
دَين تحليل ساكن جديد. ESLint ليس بوّابة خضراء على `develop` أصلًا (26 خطأ على المستودع كلّه).

---

## 3) إثباتات EF/الهجرات (K.3)

| الإثبات | النتيجة |
|---|---|
| `dotnet ef migrations has-pending-model-changes` | **`No changes have been made to the model since the last migration.`** |
| عدد الهجرات | **38** (35 develop + 3 إنتاج) |
| رأس الهجرات | `20260811142239_AddProject360Foundation` |
| تكرار معرّفات الهجرات | **0** |
| تكرار أسماء الهجرات | **0** |
| مسار القاعدة الجديدة (`dotnet ef database update` على `rr_k_fresh`) | 38 صفًّا في `__EFMigrationsHistory` · 78 جدولًا · **صفر `42P07`** |
| بصمة المخطَّط `rr_k_fresh` (عبر `dotnet ef`) | `2f64b38dc6e6fc82435824b099b830f3` |
| بصمة المخطَّط `rr_k_main` (عبر مصنع الاختبارات) | `2f64b38dc6e6fc82435824b099b830f3` — **متطابقة** |
| عدد الأعمدة | 928 = 928 |

مساران مستقلّان للترحيل يبلغان **نفس المخطَّط بايتًا ببايت** ⟹ لا اعتماد على أداة بعينها.

---

## 4) مسح شامل لصنف الضياع أحاديّ الجانب

المسح الذي أغفله التقرير 03 أُعيد هنا على **الشجرة كاملةً** لا على الملفّات المتعارضة فقط.

| الفحص | العدد | النتيجة |
|---|---|---|
| ملفّات عدّلتها `develop` وحدها | 349 | **0** منها تراجَع إلى قاعدة التفرّع · 6 تختلف وكلّها ملفّات اختبار عدّلتها هذه التذكرة عمدًا |
| ملفّات عدّلها الإنتاج وحده | 188 | 29 اختلافًا **كلّها مفسَّرة**: 4 ملفّات هجرة استُبدِلت بمعرّفات `develop` + جسر السجلّ · 15 ملفًّا حذفها الإنتاج واستُعيدت (منظومة المناصب المرنة) · 10 ملفّات اختبار/قدرات عدّلتها التذكرة |
| مسارات الواجهة | 55 | = اتّحاد الأبوَين تمامًا (53 develop ∪ 49 إنتاج) · **0 مفقود من أيّ أب** · 0 مسار مخترَع |
| روابط تنقّل بلا مسار | — | **0** |

---

## 5) كتلة بوّابة المرحلة K

```
Backend Build                       = PASS   (0 Errors · 4 تحذيرات CS8604 سابقة للتذكرة)
Unit Tests                          = 359/359
Integration Tests                   = 1992/1993  (الفشل = BASELINE-DEFECT-01)
True Unified Candidate Regression   = 0
Unresolved                          = 0
Frontend npm ci                     = PASS
Frontend tsc -b                     = PASS   (بعد إصلاح TS1131)
Frontend vite build                 = PASS
Frontend Unit/Component Tests       = 550/550
Frontend New Lint Debt              = 0
EF Model Sync                       = CLEAN
Migration Count / Head              = 38 / 20260811142239_AddProject360Foundation
Migration Collision                 = 0
Fresh DB Path                       = PASS   (بصمة 2f64b38dc6e6fc82435824b099b830f3)
Develop Feature Regression          = 0      (بعد استعادة المسارَين)
Production Feature Regression       = 0
Ready to Merge into develop         = YES
```

---

## 6) الالتزامات المُضافة في هذه المرحلة

| الالتزام | الغرض |
|---|---|
| `a510c01` `fix(lineage): close the interface the merge left open in the api type surface` | إصلاح TS1131 |
| `bc71b19` `fix(lineage): restore the two routes the merge orphaned from their live nav links` | استعادة المسارَين + حارس الانحدار |
