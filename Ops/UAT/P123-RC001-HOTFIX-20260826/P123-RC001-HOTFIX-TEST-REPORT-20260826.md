# تقرير اختبارات الإصلاح — DEF-P123-RC-001

| الحقل | القيمة |
|---|---|
| القاعدة الأساس | `8479d374238b71731996ad73d20d1485701d2053` |
| الفرع | `feature/p123-rc001-attendance-list-privacy` |
| شجرة العمل | `.claude/worktrees/p123-rc001-20260826` (معزولة) |
| التاريخ | 26 أغسطس 2026 |

---

## 1) قبل الإصلاح مقابل بعده — الحزمة المستهدفة

| القياس | قبل الإصلاح | بعد الإصلاح |
|---|---|---|
| قاعدة البيانات | `reporting_rc001_pre` (نظيفة معزولة) | `reporting_rc001_post` (نظيفة معزولة) |
| النتيجة | `Failed: 8, Passed: 5, Skipped: 0, Total: 13` | `Failed: 0, Passed: 14, Skipped: 0, Total: 14` |
| المدّة | 2 ث | 5 ث |
| الحكم | **فاشل — العيب مُعاد إنتاجه** | **ناجح** |

الاختبار الرابع عشر (`VisibleIncidentPredicate_IsTranslatedToSql_NotEvaluatedOnClient`) أُضيف بعد إثبات الفشل ليُثبِت أنّ الحجب يقع في SQL لا في الذاكرة، فلا يُقاس عليه «قبل».

---

## 2) الاختبارات الأربعة عشر — الحالة الفرديّة

الملفّ: `reporting-backend/tests/Reporting.IntegrationTests/AttendanceListVisibilityTests.cs`

| # | اسم الاختبار | ما يُثبِته | الحالة |
|---|---|---|---|
| 1 | `Subject_List_DoesNotContain_DraftIncident` | الموضوع لا يجد `Draft` في `items` | PASS |
| 2 | `Subject_List_TotalCount_DoesNotReveal_DraftIncident` | العدّاد نفسه لا يُفشي الوجود | PASS |
| 3 | `Subject_Detail_Returns404_ForDraftIncident` | التفاصيل تردّ 404 لا 403 | PASS |
| 4 | `Subject_CanSee_Incident_AfterSubmission` | الحقّ يبدأ عند مغادرة ما قبل الإرسال | PASS |
| 5 | `Subject_CannotSee_CancelledPreSubmissionIncident` | `Cancelled` القادمة من `Draft` تبقى محجوبة | PASS |
| 6 | `Reporter_CanSee_OwnDraftIncident` | المُبلِّغ لا يفقد مسودّته | PASS |
| 7 | `AuthorizedReviewer_CanSee_DraftWithinScope` | مفتاح `Attendance.Review` الصريح يفتحها | PASS |
| 8 | `UnrelatedActor_CannotDiscoverDraft` | الغريب لا يكتشفها | PASS |
| 9 | `OutOfScopeActor_Gets404` | خارج النطاق = غير موجود | PASS |
| 10 | `Pagination_DoesNotLeakHiddenDraft` | الترقيم لا يُسرِّبها | PASS |
| 11 | `Search_DoesNotLeakHiddenDraft` | المُرشِّحات لا تُسرِّبها | PASS |
| 12 | `Summary_DoesNotCountHiddenDraft` | التجميع لا يعدّها | PASS |
| 13 | `Attendance_List_And_Detail_UseEquivalentVisibilityRules` | 3 حالات × 5 صفات = 15 تقاطعًا، الثابت «القائمة ⊆ التفاصيل» | PASS |
| 14 | `VisibleIncidentPredicate_IsTranslatedToSql_NotEvaluatedOnClient` | الشرط داخل `WHERE` لا على العميل | PASS |

---

## 3) بوابات الـHotfix الكاملة

| البوّابة | الأمر/البيئة | النتيجة الفعليّة | الحكم |
|---|---|---|---|
| بناء الخلفيّة Release | `dotnet build Reporting.sln -c Release` | `0 Error(s)` · `4 Warning(s)` (كلّها `CS8604` سابقة للإصلاح في ملفّات اختبار لم تُمسّ) | PASS |
| اختبارات الوحدة الكاملة | `Reporting.UnitTests` | `Failed: 0, Passed: 556, Total: 556` (72 مللي ث) | PASS |
| تكامل الحضور/الأمن/الصلاحيات | فلترة `AttendanceListVisibilityTests` | `Failed: 0, Passed: 14, Total: 14` | PASS |
| التكامل الكامل — جولة 1 (قاعدة نظيفة) | `reporting_hf_full1` (`createdb` جديدة) | `Failed: 0, Passed: 2188, Total: 2188` — 13 د 36 ث | PASS |
| التكامل الكامل — جولة 2 (نفس القاعدة، فحص التلوّث بين الجولات) | `reporting_hf_full1` مأهولة بمخرجات الجولة 1 | `Failed: 0, Passed: 2188, Total: 2188` — 14 د 56 ث · **مطابقة تامّة للجولة 1 ⇒ صفر تلوّث بين الجولات** | PASS |
| المجموعة المرجعيّة P1/P2/P3 | مُحتواة بالكامل داخل `Reporting.IntegrationTests` | غُطّيت **مرّتين** ضمن 2188/2188 (جولتان مستقلّتان) — وهي مجموعة فائقة للمرجعيّة لا جزء منها | PASS |
| TypeScript | `npx tsc -b --force` | لا خطأ واحد | PASS |
| Vitest | `npx vitest run` | `62 passed (62)` ملفًّا · `735 passed (735)` اختبارًا — 22.92 ث | PASS |
| Playwright الكامل | `npx playwright test` | `47 passed (49.8s)` | PASS |
| فحص تاريخ الهجرات | عدّ ملفّات الهجرات على القاعدة وبعد الإصلاح | 45 = 45 — **صفر هجرة جديدة** | PASS |
| مسح الأسرار | `grep -inE "password\|secret\|api[_-]?key\|token\|connectionstring\|BEGIN (RSA\|OPENSSH\|PRIVATE)"` على الفرق وعلى ملفّ الاختبار | لا مطابقة حقيقيّة (الوحيدة: كلمة `CancellationToken` في توقيع دالّة) | PASS |
| `git diff --check` | — | نظيف | PASS |
| ملفّات خارج النطاق | `git status --porcelain` | 3 ملفّات فقط: ملفّا المنتج المقصودان + ملفّ اختبار جديد واحد | PASS |

### التوقّع المصرَّح به بشأن الهجرات

نصّ التكليف: «المتوقّع ألّا تحتاج Migration — لو ظهرت Migration توقّف واشرح أولًا».
**المقيس: لم تظهر أيّ هجرة.** الإصلاح كلّه في طبقتَي التطبيق والبنية التحتيّة، ولم يمسّ أيّ كيان أو تهيئة `DbContext` ⇒ لا تغيّر في النموذج ولا في المخطّط. لا حاجة إلى توقّف ولا إلى شرح استثنائيّ.

---

## 4) بيئات القياس المستعملة

| القاعدة | الغرض | الحالة النهائيّة |
|---|---|---|
| `reporting_rc001_pre` | إعادة إنتاج العيب قبل الإصلاح | نظيفة أُنشئت لهذا الغرض |
| `reporting_rc001_post` | إثبات نجاح الإصلاح | نظيفة أُنشئت لهذا الغرض |
| `reporting_hf_full1` | التكامل الكامل جولتان | نظيفة أُنشئت لهذا الغرض |

لم تُمَسّ `reporting_test` المشتركة الملوّثة، ولا أيّ قاعدة على TEST أو RC أو الإنتاج، في أيّ خطوة من هذه المرحلة.
