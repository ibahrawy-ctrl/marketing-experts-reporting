# أدلّة UAT الحرج على المرشّح النهائيّ — TEST وحدها

**الـSHA المنشور والمُختبَر:** `0067b0ebbdc8cf78713b37d4ab10aa788b678097`
(= `9c7b82a` + التزام توثيقيّ واحد تحت `Ops/` — دلتا التشغيل بينهما **فارغة** مقيسةً بـ`git diff --stat 0067b0e 9c7b82a -- reporting-backend reporting-frontend`).
**البيئة:** `test.emarketingacademy.net` **وحدها**. RC والإنتاج **لم يُمسّا إطلاقًا** في هذه الجولة.
**الأدلّة الخام:** `uat-evidence/` بجوار هذا الملفّ (37 لقطة + 13 ملفّ نتائج + 3 بصمات خطّ أساس + سجلّ شبكة).

---

## 1) النشر على TEST — سلسلة عهدة بايتيّة كاملة

**نسخة احتياطيّة ثلاثيّة قبل أيّ لمس:** `/opt/reporting-test/backup-20260831T023800`
(مفرغة قاعدة البيانات + `publish.tar.gz` + `frontend-dist.tar.gz`).

الحزمة بُنِيت من `git archive` للـSHA بعينه مع `-p:SourceRevisionId=0067b0eb… -p:ContinuousIntegrationBuild=true` بعد `rm -rf bin obj`.

| البصمة | محلّيًّا | في التمهيد (staging) | حيًّا على الخادم |
|---|---|---|---|
| `PKG_ALL` (86 ملفًّا) | `89fc4c3e2a12740a` | `89fc4c3e2a12740a` | `89fc4c3e2a12740a` |
| `DLLS` (37 مكتبة) | `0d7a5c0d44c96133` | `0d7a5c0d44c96133` | `0d7a5c0d44c96133` |
| `FE_PKG` (حزمة الواجهة) | `f1f7556b1c1c3071` | `f1f7556b1c1c3071` | `f1f7556b1c1c3071` |

⟹ **Local == Staging == Live** على مستوى المجموعة وعلى مستوى كلّ ملفّ منفردًا.

**ختم SourceLink مقروءًا من المكتبة المنشورة فعلًا على الخادم:**
`1.0.0+0067b0ebbdc8cf78713b37d4ab10aa788b678097` ⟹ ما جرى عليه UAT هو الـSHA نفسه لا نسخة قريبة منه.

| الفحص بعد الإقلاع | القيمة |
|---|---|
| `/health` | `200` · `{"status":"ok","service":"reporting-api"}` |
| `NRestarts` | `0` |
| عدد الهجرات في `reporting_test_uat` | `47 → 47` (بلا هجرة جديدة عند الإقلاع) |
| خطّ الأساس بعد النشر مقابل قبله | **متطابق** (`baseline-pre-deploy.txt` ≡ `baseline-post-deploy.txt`) |

---

## 2) منهج القياس

الحزمة المُختبَرة هي **بايتات `dist` المنشورة على TEST نفسها**، تُخدَم محلّيًّا على `127.0.0.1:4420`،
وكلّ نداء إلى `https://test.emarketingacademy.net/api/**` و`/hubs/**` يُعترَض ويُحوَّل عبر نفق SSH
(`127.0.0.1:15091 → 127.0.0.1:5091` على TEST) ثمّ يُفَى بـ`fulfill`.
السبب: `auth_basic` لنطاق TEST تجزئته غير قابلة للاسترجاع وتغيير `htpasswd` محظور.
**المنطق والبيانات كلّها من خادم TEST الحقيقيّ — لا محاكاة ولا بذر مصطنع للنتائج.**

---

## 3) مصفوفة النتائج — 30 بندًا · 30 PASS · 0 FAIL · 0 NA

| # | البند | النتيجة | السكربت | الدليل |
|---|---|---|---|---|
| U1 | صفحة KPI تفتح ومساراها سليمة | PASS | s1 | `shots/U1-kpi-landing.png` |
| U1b | مرشّح المسار في نظرة عامّة | PASS | s1 | `shots/U1b-overview-track-filter.png` |
| U15 | المسارَان معروضان معًا (Dual Track) | PASS | s1 | `shots/U15-dual-track.png` |
| U2 | التنقّل بين المسارَين بلا خلط | PASS | s1 | `results-s1.json` |
| U3 | لا قائمة `cadence` تقنيّة في الواجهة | PASS | s1 | `results-s1.json` |
| U16 | إنشاء تقييم على المسار الأسبوعيّ | PASS | s15 | `shots/M-U16-weekly-before.png` · `M-U16-weekly-after.png` |
| U16b | حارس مُسمّى عند وجود تقييم قائم (أسبوعيّ) | PASS | s15 | `shots/M-U16-weekly-guard.png` |
| U16c | «الأسبوع 36 — 2026» بـ7 قوالب | PASS | s15 | `results-s15.json` |
| U17 | إنشاء تقييم على المسار الربعيّ | PASS | s15 | `shots/M-U17-quarterly-before.png` · `M-U17-quarterly-after.png` |
| U17b | حارس مُسمّى عند وجود تقييم قائم (ربعيّ) | PASS | s15 | `shots/M-U17-quarterly-guard.png` |
| U4/U5 | المسارَان يُدرجان معًا بلا تسرّب متبادل | PASS | s15 | `shots/M-U4U5-both-listed.png` |
| U18 | سلّم الأولويّة يُطبَّق **مستقلًّا لكلّ مسار** | PASS | s3 | `shots/U18-weekly.png` · `U18-quarterly.png` · `U18-auto.png` |
| U18b | لا تراكم بين المسارَين | PASS | s3 | `results-s3.json` |
| U18v | التحقّق العكسيّ لسلّم الأولويّة | PASS | s4 | `shots/U18v-weekly.png` · `U18v-quarterly.png` |
| U13 | صحّة العرض بعد تبديل المسار | PASS | s3 | `results-s3.json` |
| U14 | Drill-down متّسق مع المسار المختار | PASS | s4 | `shots/U14-pre.png` · `U14-drilldown.png` |
| U8 | نافذة التوظيف (`HireDate`/`ExitDate`) تُحترم | PASS | s5 | `shots/U8-employment-window.png` |
| U9 | تحرير التقييم موضعيًّا وحفظه | PASS | s6 | `shots/U9-editor-inplace.png` · `U9-saved.png` |
| U9b | القيم المحفوظة تعود بعد إعادة التحميل | PASS | s6 | `results-s6.json` |
| U9c | لا انحراف في الحساب بعد التحرير | PASS | s6 | `results-s6.json` |
| U11 | سجلّ التدقيق يُسجّل الحدث | PASS | s6 | `shots/U11-audit.png` |
| U12 | لا إعادة كتابة للتاريخ | PASS | s6 | `shots/U12-no-history-rewrite.png` |
| U19 | التصدير الماليّ — الوجه الموجب: `2026-Q2` = صفّان من مفتاحَين أسبوعيَّين | PASS | s8 | `shots/U19-q2-quarterly-included.png` |
| U19b | التصدير الماليّ — الوجه السالب: `2026-Q3` = صفر صفّ · CSV بترويسة فقط · بلا "Weekly" | PASS | s8 | `shots/U19-q3-pulse-excluded.png` |
| U19c | المتوسّط الرسميّ يستهلك المسار الربعيّ وحده | PASS | s7 · s8 | `shots/U19-financial-export.png` |
| U19d | متوسّطان منفصلان بلا خلط | PASS | s8 | `shots/U19d-two-averages.png` |
| U19e | عدّادان مستقلّان لكلّ مسار | PASS | s8 | `shots/U19e-two-independent-counters.png` |
| U6 | لا إنشاء تقييم بلا إعداد نشط — زرّان معطَّلان · `CREATE_POSTS = 0` | PASS | s11c | `shots/U6-before-active-setup.png` · `U6-after-no-active-setup.png` |
| U7 | سلطة الخادم على القيم المعدَّلة من العميل | PASS | s9 | `shots/U7-server-authority.png` |
| U20 | جرد القوالب مطابق للمتوقَّع | PASS | s9 | `shots/U20-templates-inventory.png` |
| U10 | موظّف عاديّ: ملفّه للقراءة فقط · المسار المباشر مرفوض | PASS | s12b | `shots/U10-own-profile-readonly.png` · `U10-direct-route-denied.png` |
| U10b | ستّة أسطح إداريّة: `403` بجلسة الموظّف مقابل `401` بلا جلسة | PASS | s12b | `net-s12b.json` |

**المحاولات المُستبدَلة (مسجَّلة للشفافيّة لا للاحتساب):** `U19 FAIL` في `results-s3/s5/s7.json`
و`U6 FAIL` في `results-s9/s11.json` هي محاولات أدوات سابقة أُصلحت ثمّ أُعيد قياسها في `s8` و`s11c` — بنفس صورة الإغلاق المعتمَد سابقًا.

---

## 4) البند U10 — الربط الزمنيّ والمسارِيّ لخطأ الوحدة الواحد

المستخدم اشترط ألّا يُقبَل سطر الخطأ في الوحدة إلّا مربوطًا بالطلب المتوقَّع. القياس:

| المؤشّر | القيمة |
|---|---|
| `CONSOLE_ERRORS` | `1` |
| `NET_403` | `1` على `PATCH /api/directory/users/11f07eba…/employment-window` |
| `DELTA_MS` (بين الطلب وسطر الوحدة) | `3` |
| `NET_5xx` | `0` |

⟹ سطر الوحدة الوحيد **هو** انعكاس الرفض المقصود `403`، لا خطأ تطبيقيّ عرَضيّ.

---

## 5) التنظيف وإعادة TEST إلى ما كانت عليه بالبايت

| الخطوة | النتيجة |
|---|---|
| إزالة إسنادات الحساب التركيبيّ | `REMOVED_ASSIGNMENTS = 9` · `200` |
| حذف الحساب التركيبيّ | `DELETE_USER = 200` |
| خطّ الأساس بعد UAT مقابل ما قبل النشر | **`IDENTICAL = YES`** (10 عدّادات + 4 بصمات MD5) |
| تدوير اعتماد المشرف بعد الانتهاء | `FP 263010318229AF25 → 05E1072C926CBC01` · `ROLES_EQUAL = True` · 48 مستخدمًا |
| ملفّات الأسرار المحلّيّة | حُذِفت (`.pw` · `.synthpw` · `.conn.local`) |
| الأنفاق وخادم الملفّات المحلّيّ | أُغلِقت |

**خطّ الأساس المرجعيّ (`baseline-pre-deploy.txt` ≡ `baseline-post-deploy.txt` ≡ `baseline-post-uat.txt`):**

```
ASSIGN_MD5|d41d8cd98f00b204e9800998ecf8427e
EVAL_MD5|ad458e767b9e0e87ef7c277569a66feb
TEMPLATE_MD5|870383b4ec6bd6096b408b38889929a2
USERS_MD5|e4e8e25f3f358fad550319deb0582f4b
kpi_evaluations|21     kpi_results|17            kpi_review_events|7
kpi_template_assignments|0                       kpi_template_versions|9
kpi_templates|9        uat_domain_users|0        userclaims|0
userroles|52           users|48
```

**ملاحظة معروفة ومسجَّلة سابقًا:** `CreateOrGetAsync` عديمة الأثر التكراريّ و`AdminDeleteAsync` حذف ناعم فقط،
ولا مفتاح أجنبيّ من `kpi_evaluations` إلى `AspNetUsers` ⟹ التقييمات لا تُنتزَع من TEST أصلًا؛
ولذلك تُقاس المطابقة على العدّادات والبصمات وقد جاءت **متطابقة تمامًا**.

---

## 6) تنقية الأدلّة من الأسرار

فحص أنماط (JWT · Bearer · كلمة مرور · سلسلة اتصال) على كلّ ملفّات `uat-evidence/`:
عُثِر على رمز JWT واحد داخل ترويسة `Authorization` في `net-s12b.json` فاستُبدِل بـ`<REDACTED-JWT>`
(الاعتماد المقابل مُدوَّر بعد UAT أصلًا). **المتبقّي بعد التنقية = `0`.**

---

## 7) إعادة النشر بختم الـSHA النهائيّ

هذا الملفّ ومجلّد `uat-evidence/` هما آخر إضافة توثيقيّة على فرع R5، ورأس الفرع بعد تثبيتهما هو
**الـSHA النهائيّ الذي سيدخل `develop`**. لأنّ ختم SourceLink يحمل الـSHA حرفيًّا، أُعيد بناء الخلفيّة
ونشرها على TEST من ذلك الالتزام بعينه كي يطابق **الختم المنشور** ما يدخل `develop` تمامًا.

- دلتا التشغيل بين الـSHA النهائيّ و`0067b0e` الذي خضع لـUAT: **فارغة** (كلّ الفارق تحت `Ops/`)
  ⟹ لا يتغيّر أيّ بايت من منطق التطبيق، والفارق الوحيد المتوقَّع في الحزمة هو سلسلة الختم داخل `Reporting.Api.dll`.
- أرقام إعادة النشر المقيسة (البصمات · الختم · `/health` · عدد الهجرات · خطّ الأساس)
  مسجَّلة في `R5-DEVELOP-MERGE-CLOSURE-REPORT.md` §3.
