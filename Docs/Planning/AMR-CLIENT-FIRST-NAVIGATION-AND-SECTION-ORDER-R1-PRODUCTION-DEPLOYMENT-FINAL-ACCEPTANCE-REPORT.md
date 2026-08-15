# AMR-CLIENT-FIRST-NAVIGATION-AND-SECTION-ORDER-R1
# تقرير القبول النهائيّ لنشر الإنتاج (Frontend فقط)

**التاريخ:** الأربعاء 29 يوليو 2026
**النطاق:** واجهة فقط (Frontend-only) — بلا Backend، بلا Migration، بلا كتابة على قاعدة البيانات
**المرشّح:** `be07a7a7fd30e6210f29354039c952b7c4c4cc58`
**القرار النهائيّ:** `PRODUCTION PASS`

---

## 1. بوابة الوقت (Time Gate)

| البند | القيمة |
|---|---|
| وصول التذكرة | 14:50:39 الرياض |
| التوقّف الأوّل ببوابة 14:50 | نُفِّذ — صفر نشر، صفر كتابة |
| تصريح المستخدم الصريح بالمتابعة | «كمل نشر» |
| بدء التنفيذ بعد التصريح | 14:57:34 الرياض (11:57:34 UTC) |
| بدء رفع الملفّات إلى Staging | 14:57:53 |
| انتهاء الرفع | 14:57:56 |
| **التبديل الذرّيّ** | **14:58:17.300 → 14:58:17.305 (5 مللي‑ثانية)** |
| اكتمال تحقّق البصمة | 14:59 |
| اكتمال Smoke + Regression | 15:04 |
| إعادة إثبات الثوابت | 15:04:53 |
| الموعد النهائيّ للتحقّق (15:20) | **مُحقَّق قبله** |
| التوقّف الصلب (15:25) | لم يُبلَغ |
| نافذة التجميد 15:30–16:30 | لم تُمَسّ — كل العمليّات انتهت قبل 15:10 |

بوابة الوقت الأصليّة (14:50) وُقِّف عندها التنفيذ حرفيًّا أوّلًا، ثمّ استُؤنِف بتصريح مستقلّ صريح من المستخدم. جميع الأعمال اكتملت داخل نافذة ما قبل 15:20.

---

## 2. خطّ أساس الإنتاج قبل النشر (Preflight — قراءة فقط)

| البند | القيمة قبل النشر |
|---|---|
| النطاق | `reports.emarketingacademy.net` |
| الحزمة | `index-96kHwdBC.js` |
| SHA256 للحزمة | `f979b8cb2692e5687da720c5f9e44ad077358d8eec62cca8d160f581af81e172` |
| `index.html` SHA256 | `057a82177680b9cfeaf4696fbccdfd9042d1036105c9426a0e652afd028a4ca4` |
| CSS | `index-Dq23uPgW.css` (30,306 بايت) |
| mtime للأصول | Jul 27 23:01 |
| الحجم / عدد الملفّات | 1.4M / 7 ملفّات |
| Backend SourceLink (4 DLLs) | `18207480fdfb4b69d7b1a4ba50eb22bece930524` |
| الخدمة | `active` · MainPID `210497` · NRestarts `0` |
| Health | داخليّ `200` · عامّ `200` |
| الهجرات | 30 · الرأس `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| البريد | `EmailNotifications__Mode=Enabled` · `Email__Enabled=false` |
| المجدول | `Enabled=true` · Poll=15 · Daily=16 · Weekly/Overdue/Summary/Review=9 |
| `email_outbox` | 0 |
| عمليّات متوازية | `NO_PARALLEL_OPS` |

**الحكم:** خطّ أساس الإنتاج **مطابق تمامًا** لأب المرشّح `3efbd0dc2584d2fa1bc23c5373d8e2ee1eb10457` ⇒ لا حاجة لإعادة بناء على خطّ أساس مختلف، ولا Blocker.

---

## 3. إثبات المرشّح (Candidate Proof)

الشجرة النظيفة المعزولة: `/tmp/amr-cand-r1-20260729`

```
SHA    = be07a7a7fd30e6210f29354039c952b7c4c4cc58
PARENT = 3efbd0dc2584d2fa1bc23c5373d8e2ee1eb10457
TREE   = 71b17d7313d4e8e97526d4ab2dde8acfe87ad04f
DATE   = Wed Jul 29 13:11:44 2026 +0300
SUBJ   = feat(reports): group account manager report by client then project
BRANCH = candidate/amr-client-first-r1-20260729   (فرع مُسمّى — لا Detached HEAD)
git status : 0 سطر   ·   Untracked : 0
غائب: rebase-merge · rebase-apply · MERGE_HEAD · CHERRY_PICK_HEAD · index.lock
```

`git diff --name-status 3efbd0dc..be07a7a7`:

```
A  reporting-frontend/src/components/PresentationProfileClientNav.test.tsx   +604
M  reporting-frontend/src/components/PresentationProfileReport.tsx           447 +/-
M  reporting-frontend/src/pages/SubmissionsPage.tsx                          100 +/-
3 files changed, 1019 insertions(+), 132 deletions(-)
```

**حرّاس المحتوى — كلّها صفر:** Backend · Migration · Email · Preview · Fixture · Screenshots · PDFs · بيانات حقيقيّة.

---

## 4. النسخة الاحتياطيّة (Backup)

```
PATH  = /opt/reporting/reporting-frontend/dist-backup-amr-client-first-prer1-20260729-115438
FILES = 7        SIZE = 1.4M
OWNER = www-data:www-data      MODE = dir 755 / file 644
index.html                sha256 = 057a8217 7680b9cf e4696fbc cdfd9042 d1036105 c9426a0e 652afd02 8a4ca4
assets/index-96kHwdBC.js  sha256 = f979b8cb 2692e568 7da720c5 f9e44ad0 77358d8e ec62cca8 d160f581 af81e172
assets/index-Dq23uPgW.css 30,306 بايت
readable  : "<!doctype html>\n<html lang=\"ar\" dir=\"rtl\">"
ROLLBACK_TARGET = index-96kHwdBC.js  ✅ مؤكَّد
TS مسجَّل في /root/amr-clientfirst-prod-ts.txt = 20260729-115438
```

بالإضافة إلى ذلك، حُفِظت الواجهة السابقة أيضًا في `dist-old-amr-clientfirst-20260729` (نسخة ثانية غير مطلوبة، أُبقيت احتياطًا).
**لا Backup لقاعدة البيانات** — المهمّة لا تنفّذ أيّ كتابة على القاعدة.

---

## 5. البناء من المرشّح المجمّد (Build)

```
rm -rf dist && npx tsc -b && VITE_API_BASE_URL=/api npx vite build
tsc -b       : 0 خطأ
vite build   : ✓ built in 704ms   (التحذير الوحيد: /*#__PURE__*/ في @microsoft/signalr — حميد ومعروف)
```

| الحارس | النتيجة |
|---|---|
| `127.0.0.1` داخل الحزمة | 0 |
| `localhost:5090` | 0 |
| نطاق RC (`rc-report`) مُدمَج | 0 |
| نطاق الإنتاج مُدمَج | 0 |
| Source maps (`*.map`) | 0 |
| ثابت الـAPI | ``Ws=`/api``` (Same-Origin) |
| أثر Preview/Fixture | 0 |

**إثبات الحتميّة (Determinism):** إعادة البناء من نفس الـcommit أنتجت الحزمة **مطابقة بايتًا ببايت** للحزمة المقبولة على RC (`b0728e96…`) ⇒ القطعة المنشورة تقابل حرفيًّا الـcommit `be07a7a7`، وهي نفسها التي مرّت بـ22/22 سيناريو قبول على RC.

---

## 6. اسم الحزمة والحجم والبصمة

| الملفّ | الحجم (بايت) | SHA256 |
|---|---:|---|
| `index.html` | 858 | `3b1596ef3ec73b3f72bb519d369c059d5c29ecfd7fe90f8925e639f76774d5a7` |
| `assets/index-DaDCi1OK.js` | 1,329,502 | `b0728e96b27fb4f443757af1ad59bfddffa1d115df47d7c2b666e99310c085aa` |
| `assets/index-rPl-oo4Z.css` | 31,115 | `374ebcdb63a6dc103588c0044992ffdd9914da50e4d4d413e27b3b627f775840` |

أصول ثابتة مرافقة: `favicon.svg` (9,522) · `icons.svg` (5,031) · `logo-arabic.png` (31,250) · `logo-mark.png` (11,839) — إجمالي 7 ملفّات.

---

## 7. الجدول الزمنيّ للنشر (Deployment Timeline)

| الحدث | التوقيت (الرياض) |
|---|---|
| بدء النسخ إلى Staging | 14:57:53 |
| انتهاء النسخ إلى Staging | 14:57:56 |
| إثبات تطابق Staging = Local (بالبصمة) | 14:58:0x |
| ضبط الملكيّة/الصلاحيّات (`www-data:www-data`, 755/644) | 14:58:1x |
| **بدء التبديل الذرّيّ** | **14:58:17.300** |
| **انتهاء التبديل الذرّيّ** | **14:58:17.305** |
| مدّة الانقطاع النظريّة | **5 مللي‑ثانية** |

المسار الوسيط: `/opt/reporting/reporting-frontend/dist-staging-amr-clientfirst-20260729`
آليّة التبديل: `mv dist dist-old-amr-clientfirst-20260729 && mv dist-staging-… dist` (إعادة تسمية على نفس نظام الملفّات — ذرّيّة).

**الملفّات المستبدَلة (7):** `index.html`, `assets/index-DaDCi1OK.js`, `assets/index-rPl-oo4Z.css`, `favicon.svg`, `icons.svg`, `logo-arabic.png`, `logo-mark.png`.

**ما لم يُمَسّ:** لا `systemctl restart` للـBackend · لا تعديل nginx · لا تعديل config · لا حذف Backup · لا `rsync --delete` على مسار مشترك (الـ`--delete` اقتصر على مجلّد Staging الجديد الفارغ).

---

## 8. تحقّق البصمة (Hash Verification)

| المقياس | القيمة | الحالة |
|---|---|---|
| Local build → `index-DaDCi1OK.js` | `b0728e96…` | ✅ |
| قرص الإنتاج → `index-DaDCi1OK.js` | `b0728e96…` | ✅ مطابق |
| مُقدَّم عبر HTTPS → `index-DaDCi1OK.js` | `b0728e96…` | ✅ مطابق |
| `index.html` (قرص = HTTPS = محليّ) | `3b1596ef…` | ✅ تطابق ثلاثيّ |
| CSS (قرص = HTTPS = محليّ) | `374ebcdb…` | ✅ تطابق ثلاثيّ |
| `/` يُعيد نفس `index.html` | `3b1596ef…` | ✅ لا Cache قديم |
| `index.html` يشير إلى الحزمة الجديدة | مرجع واحد | ✅ |
| الحزمة القديمة `index-96kHwdBC.js` عبر HTTPS | `404` | ✅ استُبدِلت فعلًا |
| الحزمة القديمة داخل الـBackup | `f979b8cb…` | ✅ سليمة |
| Same-Origin `/api` | مؤكَّد | ✅ |

**رموز الحالة الحيّة:** `/`=200 · `/index.html`=200 · `/assets/index-DaDCi1OK.js`=200 · `/assets/index-rPl-oo4Z.css`=200 · `/login`=200 · `/app/submissions`=200 · `/app/submissions?open=…`=200 (يُعيد SPA index مع مرجع الحزمة الجديدة).

---

## 9. الـSmoke الحيّ على تقرير مدير الحسابات (قراءة فقط)

التقرير المستهدَف: **سماح ابوالمجد — `2026-W30`** · `3c9d647a-c694-4fbc-8688-3cf12e5389d0` · الحالة `Closed`.

| # | التأكيد | النتيجة |
|---|---|---|
| 1 | تحميل صفحة الدخول | ✅ |
| 2 | تسجيل الدخول ⇒ `/app` | ✅ |
| 3 | الحزمة المُقدَّمة = `index-DaDCi1OK.js` | ✅ |
| 4 | «الوصول السريع» `#amr-quick-nav` ظاهر | ✅ |
| 5 | كل عميل يظهر **مرّة واحدة** (4 عملاء) | ✅ |
| 6 | عدد المشاريع لكلّ عميل صحيح | ✅ |
| 7 | 6 مراسي مشاريع بالضبط | ✅ |
| 8 | كل `ProjectId` متوقَّع له مرساة | ✅ |
| 9 | العملاء مطويّون افتراضيًّا (`aria-expanded=false` × 4) | ✅ |
| 10 | فتح العميل يكشف مشاريعه | ✅ (`مطاعم عم قاسم` ⇒ «تحسين محركات البحث» + «الحملات الاعلانية») |
| 11 | النقر على مشروع يفتح البطاقة الصحيحة | ✅ |
| 12 | جسم البطاقة يُفتح فعليًّا (ارتفاع > 0) | ✅ |
| 13 | التركيز ينتقل لعنوان المشروع | ✅ `activeElement = amr-pcard-743e4751-…-title` |
| 14 | الإبراز يظهر | ✅ `ring-2 ring-orange ring-offset-2` |
| 15 | الإبراز يختفي تلقائيًّا | ✅ بعد ~2200ms ⇒ `NO_RING` |
| 16 | فتح مشروع ثانٍ لا يُغلِق الأوّل | ✅ كلاهما مفتوح |
| 17 | رابط «↑ العودة إلى قائمة العملاء والمشروعات» يعمل | ✅ (6 عناصر — واحد لكل بطاقة) والـnav داخل الشاشة بعده |
| 18 | أسماء المشاريع في جدول «نظرة عامة» قابلة للنقر | ✅ 6 خلايا |
| 19 | النقر من الجدول ينتقل للمشروع الصحيح | ✅ التركيز والإبراز على `07f24c95…` |
| 20 | الأرقام لم تتغيّر | ✅ (انظر §10) |
| 21 | القيمة `0` ما زالت معروضة | ✅ `0/6` · `0/2` · `0/0` |

**لم يُعدَّل التقرير ولم تُنشأ أيّ بيانات.** الحالة بعد الـSmoke: `Closed` · `IsDeleted=false` (كما قبله).

---

## 10. مصفوفة التجميع حسب العميل (Client Grouping Matrix)

مستخرَجة من بيانات الإنتاج الحقيقيّة (قراءة فقط) ومؤكَّدة حيًّا في الواجهة:

| العميل | `ClientId` | عدد المشاريع | المشاريع | `ProjectId` |
|---|---|---:|---|---|
| متجر امداد | `60ac427c-089d-476f-8ad8-51aafb09616d` | **2** | ادارة الحملات الاعلانية | `166b30d5-a6c1-4eb5-9e46-21abd8a640a0` |
| | | | تعديلات ع المتجر | `5efd9b6f-cfbe-4918-8cb7-f44a2123ab3d` |
| مطاعم عم قاسم | `d8981e27-b8d6-4295-8bc2-b7e89eedadc1` | **2** | الحملات الاعلانية | `3a3d49ea-e550-4dbc-8945-a128b30e383f` |
| | | | تحسين محركات البحث | `743e4751-3a7f-4b4b-ac77-04e28d27140c` |
| منصة مكانة | `f7d17303-a078-45b7-8e1b-2f871750ef8e` | 1 | تحسين محركات البحث | `07f24c95-54e5-4c38-bbed-e3b8307d17e2` |
| جيم كرافت | `95277e39-a74a-4112-9743-d6bab12604cd` | 1 | سوشيال ميديا | `645eeefa-3e3d-4368-9408-c15883dfe14d` |

نصّ الـQuick Nav الحيّ كما ظهر على الإنتاج:

```
▼ متجر امداد     2 مشروعات   ⚠ مخاطر (1)  ⚑ قرارات (2)
▼ منصة مكانة     مشروع واحد  🟡 متابعة (1) ⚠ مخاطر (1) ⚑ قرارات (1)
▼ مطاعم عم قاسم  2 مشروعات   ⚑ قرارات (2)
▼ جيم كرافت      مشروع واحد  ⚠ مخاطر (1)
```

✅ **متجر إمداد بمشروعيه** · ✅ **مطاعم عم قاسم بمشروعيه** · ✅ كل عميل مرّة واحدة · ✅ 4 عملاء / 6 مشاريع.

---

## 11. التنقّل عند تطابق الأسماء (Duplicate-Name Navigation)

مشروعان يحملان **الاسم نفسه** «تحسين محركات البحث» تحت عميلين مختلفين:

| العميل | `ProjectId` | المرساة |
|---|---|---|
| مطاعم عم قاسم | `743e4751-3a7f-4b4b-ac77-04e28d27140c` | `amr-project-743e4751-3a7f-4b4b-ac77-04e28d27140c` |
| منصة مكانة | `07f24c95-54e5-4c38-bbed-e3b8307d17e2` | `amr-project-07f24c95-54e5-4c38-bbed-e3b8307d17e2` |

| الاختبار | النتيجة |
|---|---|
| المرساتان مختلفتان ومتواجدتان معًا | ✅ |
| النقر على «تحسين محركات البحث» تحت **مطاعم عم قاسم** | التركيز ⇒ `amr-pcard-743e4751-…-title` ✅ الصحيح |
| الإبراز على الهدف | `ring-2 ring-orange ring-offset-2` ✅ |
| الإبراز على المشروع المتطابق الاسم الآخر | `NO_RING` ✅ **لا اختلاط** |
| النقر من جدول «نظرة عامة» على «تحسين محركات البحث — منصة مكانة» | التركيز ⇒ `amr-pcard-07f24c95-…-title` ✅ الصحيح |
| المشروع الآخر وقتها | `NO_RING` ✅ **لا اختلاط** |

**الآليّة المُثبَتة:** التنقّل يعتمد `ProjectId` **حصرًا** (لا مطابقة نصّيّة، لا parsing) ⇒ الاختلاط مستحيل بنيويًّا.

---

## 12. ترتيب الأقسام (Section Ordering)

قياس فِهرِس النصّ داخل الصفحة الحيّة:

```
«الوصول السريع…»      index = 568
أوّل مشروع (سوشيال ميديا) index = 1651
«نظرة عامة»            index = 3084
⇒ overviewAfterProjects = true
```

✅ تفاصيل المشروعات تسبق «نظرة عامة / الملخّص / التحدّيات».
✅ بطاقات المشاريع مجمّعة تحت كل عميل.
✅ جدول «نظرة عامة» يعرض 6 صفوف بصيغة «المشروع — العميل».

---

## 13. Mobile / RTL / Print

| البند | النتيجة |
|---|---|
| اتجاه الوثيقة | `dir="rtl"` ✅ |
| Mobile 390×844 — تمرير أفقيّ | لا يوجد (`scrollWidth ≤ innerWidth`) ✅ |
| Mobile — الـQuick Nav ظاهر وبعرض > 0 | ✅ |
| Print — الـQuick Nav مخفيّ | ✅ (`display:none` عبر `print:hidden`) |
| Print — المشاريع كلّها ظاهرة | ✅ 6/6 |
| Print — التجميع محفوظ | ✅ |
| Print — رابط العودة مخفيّ | ✅ (`print:hidden`) |

---

## 14. الـRegression المحدود (Generic / Moderation / قوالب أخرى)

| القالب | الفترة | المعرّف | الطول | انهيار | يستعمل مُصيِّر AMR |
|---|---|---|---:|---|---|
| 🤝 تقرير إدارة الحسابات العملاء | `2026-W29` | `80eba5c0-…` | 5,550 | ❌ لا | ❌ لا (Generic Renderer) ✅ |
| تقرير المديرشن الأسبوعي | `2026-W30` | `4c50480a-…` | 5,518 | ❌ لا | ❌ لا ✅ |
| تقرير فريق الفيديو | `2026-W31` | `ea04ce56-…` | 818 | ❌ لا | ❌ لا ✅ |

- التقرير القديم لمدير الحسابات (W29، مخطّط قديم) يُصيَّر عبر الـGeneric Renderer بلا تغيير ولا انهيار.
- المودريشن غير متأثّر.
- قالب بلا Presentation Profile غير متأثّر.
- **الرابط العميق + إعادة التحميل:** بعد `reload` عاد `#amr-quick-nav` و6 مراسي كاملة ✅.

**Console:** خطآن فقط، كلاهما **نفس الخطأ المعروف سابق الوجود**:
```
Error: Failed to complete negotiation with the server: TypeError: Failed to fetch
Error: Failed to start the connection: …
```
مصدره SignalR (`/hubs/notifications`) وهو معروف وموثَّق منذ ما قبل هذا النشر، **ولا علاقة له بالحزمة** (لا يظهر أيّ خطأ وظيفيّ في التصيير أو التنقّل). صفر أخطاء وظيفيّة جديدة.

---

## 15. ثوابت الـBackend بعد النشر

| البند | قبل | بعد | الحالة |
|---|---|---|---|
| ActiveState | active | active | ✅ |
| MainPID | 210497 | **210497** | ✅ لم يتغيّر |
| NRestarts | 0 | **0** | ✅ لم يتغيّر |
| ExecMainStartTimestamp | 2026-07-29 07:25:34 UTC | نفسه | ✅ |
| SourceLink (Api/Application/Domain/Infrastructure) | `18207480…` | `18207480…` | ✅ الأربعة |
| Health داخليّ | 200 | 200 | ✅ |
| Health عامّ | 200 | 200 | ✅ |
| عدد الهجرات | 30 | 30 | ✅ |
| رأس الهجرات | `20260724224053` | `20260724224053` | ✅ |
| عدد التسليمات | 146 | 146 | ✅ |

**لا Restart · لا Migration · لا نشر Backend · لا تعديل publish.**

---

## 16. ثوابت البريد والمجدول

| المفتاح | القيمة | الحالة |
|---|---|---|
| `EmailNotifications__Mode` | `Enabled` | ✅ لم يُمَسّ |
| `Email__Enabled` | `false` | ✅ لم يُمَسّ |
| `ReportReminderScheduler__Enabled` | `true` | ✅ |
| `ReportReminderScheduler__PollMinutes` | `15` | ✅ |
| `DailyDueHour` | `16` | ✅ |
| `WeeklyDueHour` / `OverdueHour` / `SummaryHour` / `ReviewHour` | `9` | ✅ |
| `mtime` لملفّ البيئة | `2026-07-26 19:49:58Z` | ✅ لم يتغيّر |
| `email_outbox` | 0 | ✅ |
| `email_notifications` منذ 11:54 UTC | **0** | ✅ |
| آخر إشعار بريد | `2026-07-29 10:17:04 UTC` (Sent) — قبل النافذة بـ~1h40m | ✅ ليس بسبب المهمّة |
| إرسال SMTP بسبب المهمّة | **0** | ✅ |

**لا Recovery · لا Job يدويّ · لا تعديل Email Control Center · لا مساس بأيّ ملفّ بريد.**

---

## 17. دليل صفر كتابة (Zero-Write Evidence)

| القياس منذ 11:54:00 UTC (بدء النافذة) | القيمة |
|---|---:|
| صفوف `email_notifications` جديدة | **0** |
| صفوف `audit_logs` جديدة | **0** |
| صفوف `email_outbox` | **0** |
| عدد `report_submissions` | 146 (ثابت) |
| حالة تقرير سماح W30 | `Closed` · `IsDeleted=false` (كما كانت) |
| هجرات مطبَّقة | 0 |
| تسليم تجريبيّ مُنشأ | 0 |
| مستخدم/بيانات اختبار على الإنتاج | 0 |

كلّ عمليّات الـSmoke والـRegression كانت **قراءة فقط** عبر تسجيل دخول Break-glass Admin (`admin@marketingexperts.local`) — لم تُطبَع كلمة المرور ولا التوكن في أيّ مخرَج، ولم تُنفَّذ أيّ عمليّة كتابة (`POST`/`PATCH`/`DELETE`) على الإطلاق.

---

## 18. جاهزيّة التراجُع (Rollback Readiness — مُثبَتة بلا تنفيذ)

النشر سليم ⇒ **لم يُنفَّذ Rollback**. الجاهزيّة مُثبَتة:

```bash
D=/opt/reporting/reporting-frontend
mv $D/dist $D/dist-failed-$(date -u +%Y%m%d-%H%M%S)
cp -a $D/dist-backup-amr-client-first-prer1-20260729-115438 $D/dist
chown -R www-data:www-data $D/dist
```

| البند | الحالة |
|---|---|
| النسخة الاحتياطيّة موجودة وقابلة للقراءة | ✅ 7 ملفّات · 1.4M |
| `index-96kHwdBC.js` داخلها بالبصمة الصحيحة | ✅ `f979b8cb…` |
| `index.html` القديم داخلها | ✅ `057a8217…` |
| الملكيّة والصلاحيّات محفوظة | ✅ `www-data:www-data` 755/644 |
| نسخة ثانية إضافيّة | ✅ `dist-old-amr-clientfirst-20260729` |
| Rollback للـBackend | غير مطلوب — لم يُمَسّ |
| Rollback لقاعدة البيانات | غير مطلوب — صفر كتابة |
| Health بعد أيّ Rollback | يبقى سليمًا (لا علاقة للواجهة بالخدمة) |
| زمن التراجُع المقدَّر | ثوانٍ (نسخ مجلّد 1.4M) |

---

## 19. القرار النهائيّ

| معيار القبول | النتيجة |
|---|---|
| المرشّح المنشور = `be07a7a7` | ✅ (إثبات الحتميّة: الحزمة مطابقة بايتًا) |
| الأب صحيح `3efbd0dc` | ✅ |
| الحزمة مطابقة محليًّا + على القرص + عبر HTTPS | ✅ تطابق ثلاثيّ |
| التنقّل Client-first يعمل | ✅ |
| المشاريع المتشابهة لا تختلط | ✅ (تنقّل بـ`ProjectId` حصرًا) |
| الأرقام لم تتغيّر | ✅ (بما فيها القيمة `0`) |
| تقرير مدير الحسابات القديم يعمل | ✅ Generic Renderer سليم |
| المودريشن والـGeneric غير متأثّرين | ✅ |
| Mobile / RTL / Print سليمة | ✅ |
| لا Restart للـBackend | ✅ MainPID/NRestarts ثابتان |
| لا Migration | ✅ 30/`20260724224053` |
| لا كتابة على قاعدة البيانات | ✅ 0 صفّ جديد |
| لا تغيير في البريد | ✅ عشرة مفاتيح ثابتة |
| Rollback جاهز | ✅ |
| اكتمال التحقّق قبل 15:20 | ✅ (15:04:53) |
| Console بلا خطأ وظيفيّ جديد | ✅ (SignalR فقط — سابق الوجود ومُفسَّر) |

# القرار: `PRODUCTION PASS`

---

## 20. حالة الإغلاق

```
Ticket    : AMR-CLIENT-FIRST-NAVIGATION-AND-SECTION-ORDER-R1
Status    : PRODUCTION PASS — CLOSED
Candidate : be07a7a7fd30e6210f29354039c952b7c4c4cc58
Parent    : 3efbd0dc2584d2fa1bc23c5373d8e2ee1eb10457
Tree      : 71b17d7313d4e8e97526d4ab2dde8acfe87ad04f
Bundle    : index-DaDCi1OK.js  sha256 b0728e96b27fb4f443757af1ad59bfddffa1d115df47d7c2b666e99310c085aa
CSS       : index-rPl-oo4Z.css sha256 374ebcdb63a6dc103588c0044992ffdd9914da50e4d4d413e27b3b627f775840
index.html: sha256 3b1596ef3ec73b3f72bb519d369c059d5c29ecfd7fe90f8925e639f76774d5a7
Backup    : /opt/reporting/reporting-frontend/dist-backup-amr-client-first-prer1-20260729-115438
Scope     : Frontend-only · No Backend · No Migration · No DB write · No Email change
Deployed  : 2026-07-29 14:58:17 الرياض (5ms atomic switch)
Verified  : 2026-07-29 15:04:53 الرياض
```

**مسارات المسار المتوقّفة (لا تُبدأ دون تصريح جديد):**
`AMR-INPUT-FIELD-GUIDANCE-AND-VALIDATION-R1` · `PROJECT-CROSS-FUNCTIONAL-READ-MODEL-R1` · `EMAIL-MISSED-NOTIFICATIONS-RECOVERY-R1 Phase 2` · أيّ مسّ بملفّات البريد أو Email Control Center.

**التالي المسموح فقط:** مراقبة نافذة البريد 16:00 اعتبارًا من 15:45 الرياض بالمطالبة المعتمَدة — بلا أيّ تعديل.
