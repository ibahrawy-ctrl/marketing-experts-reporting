# FRONTEND-ARTIFACT-PROVENANCE-GATE-R1 — بوّابة نَسَب حزمة الواجهة

**الحالة:** إلزاميّة · **اعتُمِدت:** 1 سبتمبر 2026 · **النطاق:** كلّ نشر لواجهة (`reporting-frontend/dist`) إلى TEST أو RC أو Production.

> نسخة مطابقة من هذه القاعدة موجودة في `Docs/Manual-Deployment-Guide-Reporting-System.md` §3.1 (ذلك الملفّ خارج تتبّع Git بحكم `.gitignore: Docs/*`؛ **هذا الملفّ هو النسخة الحاكمة المتتبَّعة**).

---

## 1. السبب الجذريّ الذي أوجب هذه البوّابة

في **12 أغسطس 2026** نُشِر إصلاح التعليقات متعدّدة الأسطر (`00b5f3a7ba4da063f767dc803e2494f77841d4f1`) إلى الإنتاج عبر `rsync` لمجلّد `dist/` مبنيّ من الفرع المرشَّح `candidate/report-approval-comments-multiline-r1-20260811`. ذلك الـcommit كان موجودًا على **مرجع واحد فقط** ولم يُدمَج قطّ في `develop` ولا `main`:

```
git merge-base --is-ancestor 00b5f3a origin/develop  →  exit 1  (NO)
git merge-base --is-ancestor 00b5f3a origin/main     →  exit 1  (NO)
```

فعاش الإصلاح في **الحزمة المبنيّة وحدها** لا في الكود الحاكم. ولمّا أُعيد بناء الواجهة من `develop` في **23 أغسطس** (`PROJECT360-R21-PROD-DEPLOY`، الحزمة `index-CMjXSPXr.js`) دُهِس الإصلاح صامتًا. نافذة السلامة الفعليّة في الإنتاج: `2026-08-12 20:03Z → 2026-08-23 11:46Z`. لم يُكتشف السقوط إلّا بعد **11 يومًا** ببلاغ مستخدم.

**ولماذا سكتت الاختبارات؟** اختبار الحارس الوحيد (`ApprovalCommentsMultiline.test.tsx`) وُلِد ومات على نفس الفرع اليتيم، فلم يكن موجودًا أصلًا في السلالة التي تُبنى منها الحزم.

**الدرس المُلزِم:** الاختبار الأخضر على المصدر لا يُثبت شيئًا عن الحزمة التي يحمّلها المتصفّح، والحزمة التي لا يمكن إعادة إنتاجها من مرجع على `origin` هي دَين تقنيّ سيسقط دون إنذار.

---

## 2. القواعد الثمانية (لا تُخالَف)

1. **ممنوع نشر أيّ Frontend Artifact مبنيّ من commit محلّيّ غير موجود على `origin`.**
2. **`BUILD_SHA` يجب أن يكون من أسلاف فرع إصدار معتمد على `origin`** (`origin/develop` أو `origin/main` أو فرع Hotfix/Release مدفوع ومُصرَّح به)، **أو** Tag ثابت/موقَّع.
   *ملاحظة تصميميّة:* لا يُشترط `origin/develop` تحديدًا — وإلّا مُنِع Hotfix مشروع مبنيّ من سلالة الإنتاج الحيّة، وهو سيناريو صحيح ومتكرّر.
3. **تُسجَّل في تقرير النشر إلزامًا** الحقول السبعة:
   `SOURCE_SHA` · `APPROVED_ORIGIN_REF` · `FRONTEND_LOCKFILE_SHA` · `BUILD_ENVIRONMENT_KEYS` · `BUILD_COMMAND` · `DIST_MANIFEST_SHA` · `DEPLOYED_MANIFEST_SHA`.
   `DIST_MANIFEST_SHA` و`DEPLOYED_MANIFEST_SHA` **يجب أن يتطابقا** (إثبات أنّ ما بُني هو ما نُشِر).
4. **يُثبَت النَسَب بأمر قابل للتكرار:**
   ```bash
   git merge-base --is-ancestor "$BUILD_SHA" "origin/$APPROVED_ORIGIN_REF" && echo ANCESTOR=YES
   ```
5. **ممنوع `rsync` من فرع مرشَّح يتيم أو من شجرة عمل غير مُلتزَمة.** يُشترط `git status --porcelain` فارغًا لحظة البناء، والبناء من نسخة معزولة.
6. **أيّ إصلاح نُشِر عبر Hotfix يُدمَج أو يُعاد تطبيقه على `origin/develop` قبل إغلاق التذكرة**، وقبل السماح لأيّ إصدار لاحق باستبداله.
7. **بوّابة Regression قبل أيّ نشر لاحق:** تُقارَن إصلاحات الإنتاج القائمة بالسلالة الجديدة، فلا يسقط إصلاح إنتاجيّ صامتًا.
8. **اختبارات الحارس جزء من نفس السلالة الحاكمة** لا على فرع جانبيّ — ويشمل ذلك **حارس الحزمة المبنيّة نفسها**.

---

## 3. حارس الحزمة المبنيّة (Artifact Gate)

المسار: `reporting-frontend/scripts/verify-multiline-bundle.mjs`

يُشغَّل **بعد `npm run build` وقبل أيّ `rsync`**، ويقرأ `dist/assets/*.js` نفسها لا المصدر، ويفشل بخروج ≠ 0.

```bash
cd reporting-frontend
VITE_API_BASE_URL="https://<env>/api" npm run build
EXPECTED_API_BASE="https://<env>/api" node scripts/verify-multiline-bundle.mjs dist
# BUNDLE_MULTILINE_GATE=PASS  ⇒ يُسمح بالنشر
```

الفحوص الثمانية: `MULTILINE_ELEMENT` · `RESIZE_Y` · `WHITESPACE_PRE_WRAP` · `BREAK_WORDS` · `COMMENT_FIELD_PRESENT` · `NO_ENTER_BLOCKER` · `API_BASE_URL` · `NO_SECRETS`.

فحصا `MULTILINE_ELEMENT` و`NO_ENTER_BLOCKER` **موضعيّان** (جوار ±400 محرف حول مرساة `اكتب سبب القرار…` المُقيَّدة بـ`إجراء الاعتماد`) وليسا عامّين على الحزمة كلّها، وإلّا التقطا معالجات مشروعة لا علاقة لها بالتعليقات (تنقّل تبويبات بلوحة المفاتيح في `Tabs.tsx`، وحقل بحث يطبّق فلترًا عند Enter في `EmailNotificationsPage.tsx`).

---

## 4. الأعلام المطلوبة في كلّ تقرير نشر واجهة

```
ARTIFACT_SOURCE_EXISTS_ON_ORIGIN      = YES
BUILD_SHA_ON_APPROVED_RELEASE_REF     = YES
WORKTREE_CLEAN_AT_BUILD               = YES
PRODUCTION_FIX_BACKMERGED_TO_DEVELOP  = YES
REGRESSION_TESTS_IN_GOVERNING_BRANCH  = YES
DIRECT_ORPHAN_BRANCH_DEPLOYMENT       = FORBIDDEN
```

> أيّ علم ≠ القيمة أعلاه ⇒ **النشر محظور** حتّى تُصحَّح السلالة، لا حتّى تُعاد المحاولة.
