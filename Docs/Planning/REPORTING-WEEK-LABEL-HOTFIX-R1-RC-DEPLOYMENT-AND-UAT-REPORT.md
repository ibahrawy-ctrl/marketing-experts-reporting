# REPORTING-WEEK-LABEL-HOTFIX-R1 — RC DEPLOYMENT & UAT REPORT

**التاريخ:** 2026-07-19 — **النطاق:** Frontend فقط على RC حصرًا (لا Production / لا Backend / لا DB / لا Migration / لا Seeder).
**البيئة:** `khubara-reporting-rc.service` @ `http://127.0.0.1:5092` (`ASPNETCORE_ENVIRONMENT=ReleaseCandidate`) — قاعدة `reporting_rc` — دومين `https://rc-report.emarketingacademy.net` (Basic Auth + noindex) — VPS 187.127.72.232.

---

## الحالة النهائية (Interim)

```
RC DEPLOYMENT PASSED
AUTHENTICATED UAT PENDING
PRODUCTION BLOCKED
```

النشر إلى RC **مكتمل ومُثبَت** على مستوى الـArtifact والـInfrastructure. UAT المصادَق (A–F) **معلّق** بانتظار حسابَي RC مؤقّتَين (موظّف أسبوعيّ + Manager) يوفّرهما المالك عبر قناة آمنة خارج المحادثة، أو يسجّل الدخول بنفسه تحت التوجيه. **لا نشر Production قبل اجتياز UAT المصادَق.**

---

## 1) إثبات حالة RC قبل النشر (Step 1)

| الفحص | القيمة |
|---|---|
| Health (`/health`) | `{"status":"ok","service":"reporting-api"}` |
| حالة الخدمة | `active` |
| `ASPNETCORE_ENVIRONMENT` | `ReleaseCandidate` |
| `Email__Enabled` | `false` |
| `Reminders__Enabled` | `false` |
| `EmailNotifications__Mode` | `DryRun` |
| `Scheduler__Enabled` | `false` |
| `BackgroundJobs__Enabled` | `false` |
| `Notifications__Realtime__Enabled` | `false` |
| آخر migration على `reporting_rc` | `20260716015239_KpiEvaluationPartialUniqueIndex` |
| الحزمة الحالية (قبل) | `index-DeyC2mZh.js` — sha256 `5edd66d36339eb784e531c86dc7433fd03b31dea1977a26e950fe85d350f8452` |
| زمن إقلاع الخدمة (ActiveEnterTimestamp) | `Sat 2026-07-18 21:17:54 UTC` |

جميع المُجدوِلات/البريد/التذكيرات **معطّلة** كما هو مطلوب.

---

## 2) النسخة الاحتياطية لـ dist (Step 2)

**المسار:** `/opt/reporting-rc/frontend/dist-backup-reporting-week-label-hotfix-r1-20260719-103153` (مملوكة `www-data:www-data`).

بصمة النسخة الاحتياطية (= الحزمة الحيّة السابقة، سليمة للـRollback):

| ملف | sha256 |
|---|---|
| `assets/index-DeyC2mZh.js` | `5edd66d3…f8452` |
| `assets/index-BwtYHcZA.css` | `ad62995a…501a` |
| `index.html` | `6f76d87b…9191` |
| `favicon.svg` | `61bc9a16…3a66` |
| `icons.svg` | `b45fa506…b93a` |
| `logo-arabic.png` | `55f8b339…5738` |
| `logo-mark.png` | `018b5def…ea19` |

---

## 3) إثبات hash الحزمة المعتمدة — قبل/بعد النقل (Step 3)

**الأرتيفاكت المعتمد الحصريّ = `index-Bpd--Clz.js`.**

| ملف | محليًّا (قبل النقل) | على RC (بعد النقل) | مطابقة |
|---|---|---|---|
| `assets/index-Bpd--Clz.js` | `21263c5a8ce9c37a3a174a4e1d8c773b20da02858209796c434f309981d6f632` | `21263c5a…d6f632` | ✅ byte-identical |
| `assets/index-cJ0IzMl5.css` | `25895648…42bae` | `25895648…42bae` | ✅ |
| `index.html` | `3b73ef92…ecf1b2` | `3b73ef92…ecf1b2` | ✅ (يشير إلى `index-Bpd--Clz.js`) |
| `favicon.svg` / `icons.svg` / `logo-arabic.png` / `logo-mark.png` | مطابقة | مطابقة | ✅ |

حجم الحزمة = 1,426,549 بايت. النقل عبر `rsync -az --delete` ثم `chown -R www-data:www-data`.

---

## 4) النشر والتحقّق (Steps 4–7)

- **Frontend فقط** — لم تُعَد أي خدمة backend تشغيلًا.
- **HTTPS (`https://rc-report.emarketingacademy.net`)**:
  - `GET /index.html` → **200**، `Cache-Control: no-cache, no-store, must-revalidate`، `X-Robots-Tag: noindex, nofollow, noarchive` — يشير إلى `index-Bpd--Clz.js` + `index-cJ0IzMl5.css`.
  - `GET /assets/index-Bpd--Clz.js` → **200**، الحجم 1,426,549، sha256 `21263c5a…d6f632` (**مطابق للأرتيفاكت المعتمد byte-for-byte عبر HTTPS**).
  - `GET /assets/index-cJ0IzMl5.css` → **200**.
  - **لا كاش قديم:** `GET /assets/index-DeyC2mZh.js` → **404** (أزالها `rsync --delete`؛ مجلد `assets/` يحوي الحزمة الجديدة + CSS حصرًا).
- **Backend لم يُمَسّ (إثبات):**
  - `ActiveEnterTimestamp` = `Sat 2026-07-18 21:17:54 UTC` (**بلا تغيير** قبل/بعد النشر).
  - الخدمة `active`، `/health` = `ok`.
  - آخر migration = `20260716015239_KpiEvaluationPartialUniqueIndex` (**بلا تغيير**).

---

## 5) توصيل مسارات التقويم على RC (إثبات جاهزية الـBackend للنظام المُستعاد)

فحص غير مصادَق (401 = المسار موجود وموصول ويتطلّب مصادقة صحيحة):

| المسار | النتيجة |
|---|---|
| `GET /api/reporting-calendar/my-cycles` | **401** ✅ موصول |
| `GET /api/reporting-calendar/my-cycles?context=Report` | **401** ✅ |
| `GET /api/reporting-calendar/my-days` | **401** ✅ موصول |
| `GET /api/kpi-evaluations/evaluatable-subjects` | **401** ✅ موصول |
| `GET /api/auth/me` | **401** ✅ موصول |
| `POST /api/auth/login` (بيانات خاطئة) | **401** ✅ موجود |

⟹ backend الـRC يدعم واجهة `reporting-calendar` التي تستهلكها الواجهة المُستعادة.

---

## 6) إثبات الاتحاد على الحزمة المُقدَّمة فعليًّا من RC (عبر HTTPS)

فُحِصت العلامات على الحزمة المُنزَّلة من `https://rc-report.emarketingacademy.net/assets/index-Bpd--Clz.js`:

**علامات التقويم المُستعاد (كانت مفقودة في الحزمة الحيّة السابقة):**
| علامة | العدد |
|---|---|
| `my-cycles` | 2 |
| `my-days` | 2 |
| `reporting-calendar` | 4 |
| `cycleLabel` | 1 |
| «دورة التقارير من السبت إلى الجمعة» | 1 |
| «تقويم دورات التقارير الأسبوعية» | 1 |

**علامات ميزات الإنتاج المحفوظة:**
| علامة | العدد |
|---|---|
| `incoming_messages` | 3 |
| `avg_response_minutes` | 4 |
| `converted_opportunities` | 3 |
| `cases_grid` | 2 |
| «الرسائل الواردة» | 1 |
| «جهات الاتصال» | 3 |
| «القنوات» | 3 |

**بوّابات التسرّب:** `5092`=0، `reporting_test`=0، `http://localhost`=2 (ثابت axios/signalr الحميد، مطابق للأرتيفاكت المعتمد). قاعدة API = `/api` (بحكم تطابق الـhash مع الأرتيفاكت المعتمد الذي فيه `Us="/api"`).

⟹ الحزمة الحيّة على RC = **اتحاد** (التقويم المُستعاد ∪ كل ميزات الإنتاج) بلا تسرّبات.

---

## 7) الأدلّة الداعمة للسلوك (قبل الدخول الحيّ)

- **A/B/C على مستوى الصفحات (mocked vitest):** `WeekLabelHotfix.test.tsx` — SubmissionsPage تعرض منتقي الدورة الأسبوعيّة + عنوان الدورة الكامل بلا إدخال `2026-W25`؛ ReportCalendarPage تستهلك `my-cycles` وتعرض نصّ السبت→الجمعة؛ KpiPage تعرض المنتقي داخل نموذج الإدارة بلا إدخال يدويّ. **الحزمة الكاملة: 275/275 اختبار خضراء، tsc نظيف، build ناجح.**
- هذه أدلّة سلوكيّة على مصدر الحزمة نفسها المبنيّة في الأرتيفاكت المعتمد؛ **لا تُغني** عن UAT الحيّ المصادَق المطلوب في §8.

---

## 8) UAT المصادَق A–F — **PENDING (معلّق)**

**السبب:** لا تُرسَل بيانات دخول حسابات حقيقية داخل المحادثة (قرار المالك). سيوفّر المالك حسابَين مؤقّتَين لـRC فقط (موظّف أسبوعيّ + Manager) عبر قناة آمنة، أو يسجّل الدخول بنفسه تحت التوجيه.

| السيناريو | المطلوب | الحالة |
|---|---|---|
| A — SubmissionsPage | منتقي الدورة بدل الإدخال، الجلب من `my-cycles`، العنوان الكامل «الأسبوع N — YYYY (السبت … — الجمعة …)»، periodKey صحيح عند الاختيار | ⏳ PENDING |
| B — ReportCalendarPage | ظهور المنتقي، تحديث البيانات عند الاختيار، عرض السبت→الجمعة، لا رجوع لمنطق الخميس→الأربعاء | ⏳ PENDING |
| C — KpiPage (Manager) | ظهور المنتقي داخل نموذج إنشاء التقييم، عمل الاختيار، لا حقل أسبوع يدويّ | ⏳ PENDING |
| D — التقارير اليومية | `DailyCalendarPicker` يجلب `my-days`، تطابق اليوم/التاريخ/periodKey | ⏳ PENDING |
| E — تكافؤ الميزات | Moderation المجمّع، Client 360، Execution، Governance تعمل؛ لا أخطاء console؛ لا 404/500 في الشبكة؛ SignalR/Auth بلا تغيير | ⏳ PENDING |
| F — بوّابات الأرتيفاكت الحيّة | الحزمة الحيّة = `index-Bpd--Clz.js` ✅ (مُثبَت §4)، my-cycles/my-days/cycleLabel موجودة ✅ (§6)، API=/api ✅، لا نقاط نهاية TEST/Production/localhost وقت التشغيل ✅ (§6) — **جزئيًّا مُثبَت أرتيفاكتيًّا؛ يتبقّى التحقّق الحيّ أثناء الجلسة المصادَقة** |

---

## 9) تأكيد عدم المساس بـ Backend/DB

- لم يُعَد تشغيل أي خدمة backend (ActiveEnterTimestamp ثابت).
- لا migration جديد (آخره `20260716015239` بلا تغيير).
- لا تعديل env/DB/seeder؛ لا إنشاء/حذف مستخدمين.
- التغيير الوحيد = ملفّات dist للـFrontend على RC حصرًا.

---

## 10) خطة الـRollback (جاهزة)

نظرًا لكون التغيير Frontend-only، الرجوع فوريّ وبلا إعادة تشغيل backend:

```bash
ssh -i ~/.ssh/academy_vps_ed25519 root@187.127.72.232 \
  'rsync -a --delete /opt/reporting-rc/frontend/dist-backup-reporting-week-label-hotfix-r1-20260719-103153/ \
   /opt/reporting-rc/frontend/dist/ && chown -R www-data:www-data /opt/reporting-rc/frontend/dist'
```

**سلامة الـRollback مُتحقَّق منها:** بصمة النسخة الاحتياطية تطابق الحزمة الحيّة السابقة (`index-DeyC2mZh.js` = `5edd66d3…f8452`) المُثبَتة في §1؛ الاستعادة تُعيد بالضبط نفس الحالة المعروفة العاملة. لا حاجة لإعادة تشغيل خدمة (nginx static + index.html بـ no-cache).

---

## 11) الحكم

**RC DEPLOYMENT PASSED — AUTHENTICATED UAT PENDING — PRODUCTION BLOCKED.**

النشر إلى RC نظيف ومُثبَت (أرتيفاكت مطابق byte-identical عبر HTTPS، لا كاش قديم، backend/DB بلا مساس، اتحاد الميزات مُثبَت، مسارات التقويم موصولة). **لا يُتّخذ قرار Production** قبل استكمال UAT المصادَق A–F بحسابَي RC المؤقّتَين، وعندها يُصدَر الحكم النهائي: «RC UAT PASSED — READY FOR PRODUCTION DEPLOYMENT DECISION» أو «RC UAT FAILED — ROLLBACK RC AND BLOCK PRODUCTION».
