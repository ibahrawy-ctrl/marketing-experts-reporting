# نشر الإنتاج R22B والتحقّق بعده — 6 سبتمبر 2026

- **المرشَّح المنشور:** `c5e0202d0a528a1a45856790716e449b812f0184`.
- **الإنتاج السابق:** `PREVIOUS_PRODUCTION_SHA = d25dc696556bdee50508d6129b8ce290bc36aa17` (31 أغسطس).
- **وقت النشر:** `2026-09-06T17:48:13Z` (وقت إقلاع الخدمة بعد إعادة التشغيل).
- **الأصل:** `https://reports.emarketingacademy.net` — حقيقيّ، بلا نفق محلّيّ.
- **مرجع الجاهزيّة:** `RELEASE-READINESS-AND-GO-NO-GO.md` · **مرجع بوّابة RC:** `RC-OPERATIONAL-AND-VISUAL-GATE.md`.

---

## 1) النسخ الاحتياطيّ الثلاثيّ — `BACKUP_VERIFICATION = PASS`

المجلّد: `/root/backups/r22b_prod_release_20260906T174619Z` (على الخادم). الدليل: `evidence/production-deploy/backup-manifest.txt`.

| النسخة | الملفّ | الحجم | SHA-256 | التحقّق |
|---|---|---|---|---|
| الواجهة | `frontend-dist-d25dc69.tar.gz` | 415,037 B | `838cd1d80cb933e6f4c78aeec9542925c6e084ad8e57ffbfa9ac0b4910e7e10a` | `tar -tzf` سليم · 9 مدخلات |
| الخادم | `backend-publish-d25dc69.tar.gz` | 47,765,521 B | `da3da662ac628ca7dac58697e05382d6cf7c6e7ba8ba77414904d017ad10b172` | `tar -tzf` سليم · 106 مدخلة |
| القاعدة | `reporting_prod.dump` (`pg_dump -Fc`) | 1,584,182 B | `747229fe91cb03ad1b9cc6ac9c4bed1b0f984e9e893427d643cc0f88b74e251c` | `pg_restore -l` سليم · 507 مدخلة TOC · 84 جدول بيانات |

الثلاثة **موجودة وغير فارغة ومقروءة**. وإضافةً إليها نسختان مباشرتان للتراجع السريع (وفق `deployment-runbook-rules.md`):
`/opt/reporting/publish-backup-R22B-20260906T174619Z` و`/opt/reporting/reporting-frontend/dist-backup-R22B-20260906T174619Z`.

```
ROLLBACK_METHOD = rsync -a --delete /opt/reporting/publish-backup-R22B-20260906T174619Z/ /opt/reporting/publish/
                + rsync -a --delete /opt/reporting/reporting-frontend/dist-backup-R22B-20260906T174619Z/ /opt/reporting/reporting-frontend/dist/
                + chown -R www-data:www-data … + systemctl restart reporting-api
                (القاعدة لا تحتاج تراجعًا: صفر هجرة — واستعادة الـdump خيار أخير عند الضرورة فقط)
```

لا أسرار اتّصال في هذا التقرير ولا في ملفّات الأدلّة.

## 2) بوّابة ما قبل النشر — كلّها خضراء

| البوّابة | القيمة |
|---|---|
| اختبارات الواجهة (شجرة نظيفة) | `75/75` ملفًّا · `857/857` · أخطاء غير ملتقَطة 0 · `VITEST_EXIT=0` **ثلاث مرّات** |
| `tsc -b` | `EXIT=0` |
| حارس الحزمة متعدّدة الأسطر | `7/7 PASS` |
| اختبارات الخادم | وحدة `618/618` · تكامل `2311/2311` · بوّابات مسمّاة `10/10` |
| بوّابة RC التشغيليّة/البصريّة | `44/44` (Chromium + WebKit · مكتب + جوّال 390) |
| `origin/develop` · `origin/main` | `c5e0202…` · `508509ad…` — **لم يتحرّكا** |
| فرق مصدر التطبيق `5b0febf..c5e0202` | **0 ملفّ خارج `Ops/`** ⟹ `UNEXPLAINED_SOURCE_DELTA = 0` |
| الهجرات | 47 في المصدر ⊂ 49 مطبَّقة ⟹ `PENDING_MIGRATIONS = 0` |
| النسخ الاحتياطيّ | `PASS` |
| تنظيف UAT على RC | `PASS` (23 خطوة · فشل 0) |

⟹ `PRODUCTION_DEPLOYMENT = AUTHORIZED` ثمّ نُفِّذ.

## 3) النشر ومطابقة القطعة الفنّيّة

بُنيت الحزم من نسخة معزولة `/private/tmp/rel-fe-clean` (worktree عند `c5e0202`، تبعيّات `npm ci` مستقلّة)، ونُشر الخادم بـ`-p:SourceRevisionId=c5e0202… -p:ContinuousIntegrationBuild=true` بعد `rm -rf bin obj`.

```
PRODUCTION_SOURCE_SHA              = c5e0202d0a528a1a45856790716e449b812f0184
PRODUCTION_BACKEND_SOURCE_IDENTITY = EXACT   (SourceLink داخل /opt/reporting/publish/Reporting.Api.dll)
PRODUCTION_FRONTEND_ARTIFACT_MATCH = EXACT   (7/7 ملفّات dist بنفس SHA-256 محلّيًّا وعلى الخادم
                                              وعند التنزيل الحيّ عبر HTTPS من الأصل الحقيقيّ)
PRODUCTION_SERVICE                 = ACTIVE  (NRestarts=0)
PRODUCTION_HEALTH                  = 200
PENDING_MIGRATIONS                 = 0       (__EFMigrationsHistory = 49 قبل النشر وبعده · صفر "Applying migration")
PRODUCTION_5XX                     = 0
```

| ملفّ الحزمة الحيّ | SHA-256 |
|---|---|
| `assets/index-o0jQqvkU.js` | `a7f11baa9a860624c5980e5d4536efc14e0b502f46dbf21bc8e7c5caa914ca30` |
| `assets/index-ENWa4a-J.css` | `35e426369d6abfbf163eca68ad320f2f2dacad82668dd3774ad4ab650fabaf31` |
| `index.html` | `14b9a76971ecc35f859a3eaea7a790168be29dc55522f111ad71f7ce76bb022d` |

**حارس متعدّد الأسطر شُغِّل على الحزمة المنزَّلة من الإنتاج الحيّ نفسه: `7/7 PASS · BUNDLE_MULTILINE_GATE=PASS`** — أقوى إثبات لوصول إصلاح التعليقات متعدّدة الأسطر إلى المتصفّح فعلًا (لا إلى المصدر وحده). الدليل: `evidence/production-deploy/live-artifact-and-probes.txt`.
عنوان الـAPI في الحزمة الحيّة = `https://reports.emarketingacademy.net/api` · `localhost:5090` = 0 · تسرّب عناوين RC/TEST = 0.

## 4) تغيّر البيانات الناتج عن النشر — واحد فقط ومقصود

| الجدول | التغيّر |
|---|---|
| `report_submissions` | **0 صفّ** تغيّر أو أُنشئ |
| `AspNetUsers` | **0 حساب** أُنشئ · لا تصفير كلمة مرور لأيّ حساب |
| `projects` · `clients` | **0 صفّ** |
| `report_template_versions` | **قالب واحد فقط** (`46e100e3-…` — قالب SEO): أُنشئت **v7** ونُشرت عند الإقلاع، وأُلغي نشر v1..v6 |

```
PRODUCTION_DATA_CHANGE = SEO_TEMPLATE_V7_SEEDED_AT_BOOT  (تغيير منتَج مقصود، لا أثر جانبيّ)
```

**تصحيح فهم سابق يجب ألّا يُطوى:** كان مسجَّلًا أنّ «الإقلاع لا يكتب على جدول إصدارات القوالب إطلاقًا»؛ ذلك صحيح للبناء السابق `d25dc69`. أمّا هذا الإصدار فيتضمّن `TemplateSeeder.cs` ضمن فرقه، **ومن تصميمه أن يُنشئ نسخة قالب SEO رقم 7 وينشرها عند الإقلاع** — وهو التغيير المنتَجيّ نفسه الذي جرى على RC وحُفظ عمدًا أثناء تنظيف UAT (§5-ب من تقرير الجاهزيّة). لم يُمسّ أيّ قالب آخر (`templates_distinct_touched = 1`). سلوك التقارير القائمة مع v7 مقيس على RC ضمن رحلة الموظّف ودورة القرار الكاملة.

## 5) التحقّق التشغيليّ بعد النشر — ما قيس وما لم يُقس

### 5-أ) ما اجتاز فعلًا على الأصل الحقيقيّ (بلا اعتماد)
| الفحص | النتيجة |
|---|---|
| `GET /health` | **200** |
| `GET /` (تحميل SPA) | **200** · جذر React مُركَّب · نصّ عربيّ حاضر |
| أصول الحزمة | **200** لكلّ من JS وCSS · صفر 404 على الأصول |
| `GET /api/projects` · `/api/directory/users` · `/api/submissions` بلا توكن | **401** (رفض سليم، لا 500) |
| `POST /api/auth/login` باعتماد خاطئ | **401** (لا تسريب ولا استثناء) |
| مسار غير موجود `/api/reports/my` | **404** (سلوك متوقَّع) |
| توزيع رموز nginx منذ النشر | `200`×28 · `401`×4 · `404`×1 (مسبار مقصود) · **`5xx = 0`** |
| سجلّ الخادم (warning فأعلى) | **لا مدخلات** · صفر `Applying migration` · صفر `duplicate key`/`unique constraint`/`SmtpException`/`Unhandled exception` |

### 5-ب) الجولة المصادَق عليها على الإنتاج — ما أُنجِز فعلًا (6 سبتمبر · قراءة فقط)

بتصريح «الإغلاق المصادَق عليه — حسابات قائمة فقط»، فتح المالك جلسة إنتاج **بيده داخل المتصفّح** ولم يُقرأ ولا طُبِع ولا خُزِّن أيّ توكن أو كوكي أو كلمة مرور، وملفّ الجلسة خارج Git في `/private/tmp/prod-auth/`.

```
CAPTURED_SESSION_ROLE      = CEO   (7e2cb6ac… · scopeType=company)
BASELINE_UTC               = 2026-09-06T18:14:33Z
PRODUCTION_WRITES_BY_TASK  = 0     (حارس آليّ يُلغي كلّ POST/PUT/PATCH/DELETE قبل مغادرته المتصفّح)
UNSAFE_REQUESTS_ATTEMPTED  = 0     RAW_SQL_WRITE = 0     HARD_DELETE = 0
REAL_ACCOUNT_FIELDS_UNCHANGED = YES
```

خطّ الأساس قبل أيّ قياس: `TOTAL_SUBMISSIONS = 347` (Closed 215 · Submitted 79 · ApprovedByDirectManager 29 · Returned 15 · Draft 9) · `APPROVAL_STEPS = 393` · `USERS = 34` · `TEMPLATE_VERSIONS = 108` (منشورة 75) · `PROJECTS = 35` · `CLIENTS = 11` · `REAL_USERS_MD5 = 1baba08c…` · `SUBMISSIONS_MD5 = 0883f621…`.

**بوّابة الأسطح المصادَق عليها — Chromium × (1440×900 · 390×844) = 18 قياسًا · 16/16 صفحة حقيقيّة PASS:**

| السطح | مكتب | جوّال |
|---|---|---|
| `/app` الرئيسيّة | PASS | PASS |
| `/app/submissions` سطح الفريق | PASS | PASS |
| `/app/my-reports` تقاريري | PASS | PASS |
| `/app/submissions?open=…` تفاصيل تسليم قائم | PASS | PASS |
| `/app/admin/archive` الأرشيف | PASS | PASS |
| `/app/projects` المشروعات | PASS | PASS |
| `/app/report-templates` القوالب | PASS | PASS |
| `/app/report-calendar` التقويم | PASS | PASS |

المعيار في كلّ صفحة: `dir=rtl` · انسياح أفقيّ 0 · أخطاء كونسول 0 · أخطاء شبكة 0 · 5xx 0 · لا شاشة دخول · **لا مؤشّر تحميل عالق** (`spinners = 0`).

**§ عدم إفشاء الوجود (مسبار منع مقصود على مشروع غير موجود):** `HTTP_403 = 0` · `HTTP_404 = 1` ⟹ الرفض يُرجِع **404 لا 403**، والرسالة موحَّدة «المشروع غير موجود أو أنّه خارج نطاق صلاحيّتك» ⟹ `EXISTENCE_DISCLOSURE = NONE`، والتحميل ينتهي. خطأ الكونسول الوحيد هناك هو تسجيل المتصفّح للـ404 المقصود نفسه.

**§ الأسطر المتعدّدة على الإنتاج — إثبات حاكم على بيانات حقيقيّة قائمة بلا إنشاء أيّ سجلّ:**
التسليم `de3e9c56-6a01-401f-97d6-a90dafb87708` (`Closed` · `2026-W32`) يحمل تعليق اعتماد حقيقيًّا **سابقًا لهذه المهمّة**؛ فُتِح للقراءة فقط وقيس داخل متصفّح على الإنتاج الحيّ:

```
len = 2793 حرفًا     newlines = 50  ⟹ 51 سطرًا منطقيًّا
white-space   = pre-wrap        ← الأسطر محفوظة لا مطويّة
overflow-wrap = break-word
lineHeight = 20px · renderedHeight = 1140px ⟹ ≈57 سطرًا بصريًّا ≥ 51
clipped = false                 ← لا قصّ ولا إخفاء
MULTILINE_PRODUCTION_RENDERING = PASS
```
سند قاعديّ (قراءة فقط): `approval_steps` على الإنتاج تحوي تعليقات بـ51 و49 و21 و2 سطرًا ⟹ السلوك يخدم أثرًا بشريًّا فعليًّا لا حالة اصطناعيّة.

الأدلّة: `evidence/production-authenticated/` (`authenticated-readonly-baseline.txt` · `prod-auth-readonly.json` · السكربتات) و18 لقطة في `screenshots-production/authenticated/` **مع إخفاء PII بالتمويه** (`redaction-log.txt`).

### 5-ج) ما لم يُنفَّذ — ويُسجَّل صراحةً بلا تجميل
```
EMPLOYEE_LOGIN            = NOT_RUN_ROLE_NOT_SUPPLIED     (المُلتقَط CEO لا Employee)
TEAM_LEADER_LOGIN         = NOT_RUN_ROLE_NOT_SUPPLIED
PRODUCTION_WRITE_CANARY   = NOT_RUN_NO_EMPLOYEE_SESSION
WEBKIT_AUTHENTICATED      = NOT_RUN_WOULD_REQUIRE_TOKEN_EXTRACTION
```
**السبب الدقيق:** التصريح يوجب استعمال حسابات قائمة فقط وأن يكتب المالك بيانات الدخول بنفسه، ويمنع إنشاء حساب أو تصفير كلمة مرور. الجلسة الوحيدة المتاحة دور **CEO**؛ ودورة القرار تحتاج **موظّفًا** يُنشئ التقرير و**قائد فريق** يراجعه — والـCEO في قمّة السلسلة فلا مراجِع فوقه. لذلك لم تُقس على الإنتاج: دورة حياة التقرير (إدخال ← حفظ مسوّدة ← إعادة تحميل ← إرسال)، ودورة القائد (طابور ← تعليق متعدّد الأسطر ← إرجاع ← إعادة إرسال ← اعتماد). **لم يُختلق دليل عليها.** أمّا WebKit المصادَق عليه فيستلزم نقل توكن/كوكي بين المحرّكين وهو **ممنوع صراحةً**، وWebKit مُثبَت مجهولًا 2/2.

هذه المسارات **مقيسة ومغلقة على RC** الذي يشغّل **المصدر نفسه** (`c5e0202`) بقطعة واجهة **مطابقة بايتيًّا** وبلا فارق تهيئة عدا عنوان الـAPI بالتصميم.

## 6) التحقّق البصريّ بعد النشر

`evidence/production-deploy/prod-visual-anon.json` + `prod-visual-anon.mjs` + 4 لقطات في `screenshots-production/`.

| المحرّك/المقاس | RTL | تركيب الجذر | انسياح أفقيّ | أخطاء كونسول | أخطاء شبكة | 5xx | الحكم |
|---|---|---|---|---|---|---|---|
| Chromium · مكتب 1440 | `rtl` | نعم | **0** | **0** | **0** | **0** | PASS |
| Chromium · جوّال 390 | `rtl` | نعم | **0** | **0** | **0** | **0** | PASS |
| WebKit · مكتب 1440 | `rtl` | نعم | **0** | **0** | **0** | **0** | PASS |
| WebKit · جوّال 390 | `rtl` | نعم | **0** | **0** | **0** | **0** | PASS |

```
PROD_VISUAL_ANON_GATE   = PASS (4/4)
HORIZONTAL_OVERFLOW     = 0
CONSOLE_ERRORS          = 0
UNEXPECTED_NETWORK_ERRORS = 0
PRODUCTION_5XX          = 0
```

**بعد الجولة المصادَق عليها (§5-ب)** تُحدَّث الأسطح البصريّة الخمسة على الإنتاج كالآتي — كلّ تصنيف مسنود بقياس داخل المتصفّح على العنوان الحقيقيّ، بلا تجميل:

| السطح | الحكم على الإنتاج | السند |
|---|---|---|
| VIS-01 التعليقات متعدّدة الأسطر | **PASS** | `white-space: pre-wrap` · 51 سطرًا منطقيًّا · ≈57 بصريًّا · `clipped=false` على تسليم إنتاج حقيقيّ |
| VIS-02 الرفض خارج النطاق | **PASS** | 404 لا 403 · رسالة موحَّدة لا تفشي الوجود · التحميل ينتهي |
| VIS-03 RTL والانسياح والقصّ | **PASS** | 16/16 قياسًا: `dir=rtl` · انسياح 0 على 1440 و390 |
| VIS-04 إنهاء التحميل وسلامة الكونسول/الشبكة | **PASS** | `spinners=0` · كونسول 0 · شبكة 0 · 5xx 0 على ثمانية أسطح |
| VIS-05 الأسطح الحاكمة (طابور · أرشيف · قوالب · تقويم · Project 360) | **PASS جزئيّ — عرضًا فقط** | الأسطح تُحمَّل وتُصيَّر سليمة؛ **أفعال القرار لم تُجرَّب** لغياب جلسة موظّف/قائد |

```
VIS_01 = PASS   VIS_02 = PASS   VIS_03 = PASS   VIS_04 = PASS
VIS_05 = PASS_RENDERING_ONLY / DECISION_ACTIONS_NOT_EXERCISED
HORIZONTAL_OVERFLOW (مصادَق) = 0   CONSOLE_ERRORS = 0
UNEXPECTED_NETWORK_ERRORS   = 0   PRODUCTION_5XX = 0
VIS_01..VIS_05 (على RC بالمصدر نفسه) = PASS 10/10 لكلّ محرّك
```

## 7) المراقبة

`evidence/production-deploy/monitoring-window.txt` — نافذة من `17:48:13Z` إلى `17:52:59Z`:
الخدمة `active` · `NRestarts = 0` · `/health = 200` · صفر 5xx · صفر 404 على الأصول · صفر تحذير خلفيّ · صفر خطأ هجرة · صفر خطأ ازدواج تسليم · صفر خطأ بريد/إشعارات.
**لم يُنشأ أيّ canary على الإنتاج ⟹ لا شيء يُنظَّف.** لم يُحذف أيّ شيء حذفًا صلبًا، ولم يُمسّ أيّ مورد آخر.

## 8) قيود معروفة ومسجَّلة (لا تُطوى)

1. **دورة الكتابة على الإنتاج لم تُجرَّب:** `EMPLOYEE_LOGIN` و`TEAM_LEADER_LOGIN = NOT_RUN_ROLE_NOT_SUPPLIED` و`PRODUCTION_WRITE_CANARY = NOT_RUN_NO_EMPLOYEE_SESSION`. **القراءة والتصيير والصلاحيّات مُثبَتة على الإنتاج** (§5-ب)، أمّا أفعال القرار (حفظ مسوّدة · إرسال · إرجاع · اعتماد) فبرهانها قائم على RC بالمصدر المطابق. رفع القيد = جلسة **موظّف** + جلسة **قائد فريق** يراجعه، يكتبهما المالك بنفسه.
2. **البريد:** `EMAIL = PASS_AT_RENDERER / NOT_RUN_RUNTIME (CHANNEL_DISABLED)`.
3. **خيارات `work_status` بالإنجليزيّة** (`Draft/Revision/Approved/Published`) مطابِقةً لنسخة القالب المحكومة v7 — قرار المالك، ليست ارتدادًا.
4. **`report_template_versions` تغيّر عند الإقلاع** (v7 لقالب SEO) — مقصود ومشروح في §4.

## 9) الحسم

```
PRODUCTION_DEPLOYMENT              = DONE_AND_HEALTHY (17:48:13Z)
PRODUCTION_SOURCE_SHA              = c5e0202d0a528a1a45856790716e449b812f0184
PREVIOUS_PRODUCTION_SHA            = d25dc696556bdee50508d6129b8ce290bc36aa17
ARTIFACT_MATCH                     = EXACT (خادم + واجهة + حزمة حيّة منزَّلة)
PENDING_MIGRATIONS                 = 0        PRODUCTION_5XX = 0
BACKUP_VERIFICATION                = PASS     ROLLBACK_METHOD = مسجَّل في §1
RC_CLEANUP                         = PASS
SMOKE_ANONYMOUS                    = PASS     PROD_VISUAL_ANON_GATE = PASS (4/4)
PRODUCTION_AUTHENTICATED_READONLY  = PASS (16/16 قياسًا · جلسة CEO كتبها المالك بنفسه)
PRODUCTION_WRITES_BY_THIS_TASK     = 0        RAW_SQL_WRITE = 0   HARD_DELETE = 0
REAL_ACCOUNT_FIELDS_UNCHANGED      = YES      UNRELATED_SUBMISSIONS_UNCHANGED = YES
MULTILINE_PRODUCTION_RENDERING     = PASS (pre-wrap · 51 سطرًا · لا قصّ · بيانات إنتاج حقيقيّة)
ACCESS_DENIAL_NO_EXISTENCE_DISCLOSURE = PASS (404 لا 403)
VIS_01 = PASS  VIS_02 = PASS  VIS_03 = PASS  VIS_04 = PASS
VIS_05 = PASS_RENDERING_ONLY / DECISION_ACTIONS_NOT_EXERCISED
EMPLOYEE_LOGIN                     = NOT_RUN_ROLE_NOT_SUPPLIED
TEAM_LEADER_LOGIN                  = NOT_RUN_ROLE_NOT_SUPPLIED
PRODUCTION_WRITE_CANARY            = NOT_RUN_NO_EMPLOYEE_SESSION
CANARY_CLEANUP                     = NOT_APPLICABLE (لم يُنشأ canary)
EMAIL_RENDERER                     = PASS      EMAIL_RUNTIME = NOT_RUN_CHANNEL_DISABLED

R22B_REPORTING_PRODUCTION_RELEASE  = BLOCKED
EMPLOYEE_REPORTING_OPERATIONAL     = NOT_PROVEN
COMMENT_MULTILINE_PRODUCTION_RELEASE = PASS
BLOCKERS = [
  "EMPLOYEE_LOGIN = NOT_RUN_ROLE_NOT_SUPPLIED — الجلسة المتاحة دور CEO؛ ودورة القرار تحتاج
   موظّفًا يُنشئ التقرير وقائد فريق يراجعه، والـCEO في قمّة السلسلة فلا مراجِع فوقه.
   إنشاء حساب أو تصفير كلمة مرور محظور صراحةً ⟹ لم تُقس على الإنتاج أفعال:
   حفظ المسوّدة، والإرسال، وإرجاع القائد، وإعادة الإرسال، والاعتماد.",
  "TEAM_LEADER_LOGIN = NOT_RUN_ROLE_NOT_SUPPLIED — لنفس السبب."
]
```

**قراءة الحكم بلا لبس:** `BLOCKED` هنا تعني **«التحقّق ناقص»** لا **«الإصدار معطوب»**. لم يُكتشَف أيّ عيب سلوكيّ على الإنتاج: النشر سليم، والقطعة مطابقة، والأسطح تُصيَّر صحيحة، وحارس الأسطر المتعدّدة يعمل على بيانات حقيقيّة، والرفض لا يفشي الوجود. **لا يُوصى بأيّ تراجع**، والتصريح نفسه ينصّ على ألّا يُنفَّذ تراجع لمجرّد نقص دليل. رفع الحاجب = جلستا **موظّف** و**قائد فريق** يكتبهما المالك بنفسه، ثمّ canary واحد ودورة قرار كاملة.

**تفسير الحسم بلا مواربة:** النشر نفسه **نجح وصحّته مثبتة** — الهويّة مطابِقة، والخدمة حيّة، وصفر 5xx، وصفر هجرة، والحزمة الحيّة تجتاز حارس متعدّد الأسطر. **ولا يوجد عيب مكتشَف يستدعي التراجع، والتراجع غير موصى به.** لكنّ عقد الإغلاق يشترط لـ`PASS` أن يكون `EMPLOYEE_REPORTING_OPERATIONAL = YES`، وهذا يقتضي برهانًا مباشرًا على الإنتاج لم يُتَح تنفيذه ضمن الحدود الأمنيّة المفروضة في التصريح نفسه. البرهان القائم غير مباشر (RC بالمصدر نفسه ومطابقة بايتيّة للقطعة). **فالحكم `BLOCKED` هنا يعني «التحقّق ناقص»، لا «الإصدار معطوب».**

**رفع الحجز يتطلّب خطوة واحدة:** جلسة تحقّق مُعتمَدة على `https://reports.emarketingacademy.net` (دخول موظّف + قائد فريق + أدمن) لتنفيذ دورة التقرير ودورة القرار والأسطح الخمسة، ثمّ تحديث هذا التقرير إلى `PASS`/`YES`.
