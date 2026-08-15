# SUBMITTED-REPORTS-MISSING-EXPECTED-OVERDUE-R1 — RC DEPLOYMENT AND ACCEPTANCE (Post-Deploy Manifest)

> النطاق: **RC فقط**. الإنتاج لم يُمَسّ إطلاقًا. لا هجرة، لا كتابة بيانات، لا git tag.

## هوية المرشّح
- Candidate HEAD: `24575f6be17f5cf722f1b329d2c9cc4a75c2c297`
- Legal parent: `50113efbcc88630064e0d2bbb827af39deec3204`
- Branch (worktree): `candidate/missing-r1-50113` — شجرة نظيفة، 9 ملفات مُعدَّلة (+2702/-83)، بلا هجرة جديدة.

## بيئة RC (معزولة ومُثبَتة)
- Service: `khubara-reporting-rc.service` (≠ `reporting-api.service`)
- Bind: `127.0.0.1:5092` (≠ 5090)
- DB: `reporting_rc` / user `reporting_rc_app` (≠ `reporting_prod`)
- Backend path: `/opt/reporting-rc/publish` — Frontend: `/opt/reporting-rc/frontend/dist`
- Env file: `/etc/khubara-reporting-rc.env` — `ASPNETCORE_ENVIRONMENT=ReleaseCandidate`
- Controls: `Email__Enabled=false`, `Reminders__Enabled=false`, `Scheduler__Enabled=false`, Basic Auth ON, SSL valid, `X-Robots-Tag noindex`, robots `Disallow: /`
- Domain: `rc-report.emarketingacademy.net` → 5092
- الإنتاج غير الممسوس: `reporting-api.service` / 5090 / `reporting_prod` / `/opt/reporting/publish` / `/opt/reporting/reporting-frontend/dist` / `reports.emarketingacademy.net`

## الـartifacts المنشورة (المرشّح 24575f6)
- Backend DLLs:
  - `Reporting.Api.dll` = `1510845e6415ab3a6ca57762671751e38b3482340ec7b64017b160f5831b4f8c`
  - `Reporting.Application.dll` = `9483a8dce52fd85b3a88d6520ec5acdf5502a4d8ae4749fe21800acbd8b92ae3`
  - `Reporting.Infrastructure.dll` = `b5f746dd996751058b05341d8b1e7c17820cad026a8b0161ddfef17db828c8d8`
- Frontend:
  - bundle `index-D6wU43rH.js` = `6d2a6fb89ceb502ccd8e4053c859ce75ae75c324cb7011c362a5e50fb3cc14d4`
  - `index.html` = `3fd462e61315da79841535ea60beb486b91574f0052d072466be686b855548af`
  - apiBase=`/api` (same-origin) — localhost:5090=0, 127.0.0.1=0, prod_domain=0
- `appsettings.Development.json` غائب عن الخادم.

## خط الأساس للرجوع (قبل النشر)
- TS النسخ: `20260723-095037`
  - DB dump: `/root/rc-backups/reporting_rc-premissingr1-20260723-095037.dump` (532238 bytes)
  - Backend: `/opt/reporting-rc/publish-backup-premissingr1-20260723-095037` (107M)
  - Frontend: `/opt/reporting-rc/frontend/dist-backup-premissingr1-20260723-095037` (1.4M)
- DLL سابقة (هدف الرجوع): Api `f675b09e…`, App `6e29bc30…`, Infra `ad44dcc9…`
- bundle سابق: `index-C2NwduoY.js`، index.html `cf200f3a…`

## سلامة الهجرة/البيانات (قبل=بعد)
- migrations = 29 (رأس `20260716015239_KpiEvaluationPartialUniqueIndex`) — **بلا تغيير، لا هجرة طُبِّقت**
- report_submissions = 35 — submission_field_values = 434 — **بلا تغيير**
- توزيع الحالة: Submitted 26 / Closed 4 / Draft 3 / Returned 2 — **مطابق**
- صفر تحوّر بيانات (نشر كودي بحت).

## مصفوفة القبول (A-G)
- **A** المسار `/api/submissions/overview` منشور ومؤمَّن: GET→401، POST→405، مع كل الفلاتر (period/quickFilter/page/pageSize/search)→401 (لا 400/500)، health→200.
- **B** رموز خادمية منشورة: `WaitingMyApprovalCount` (App×3)، `IsWaitingMyApproval` (Infra×1)، `GetOverviewAsync`/`SubmissionQuickFilter`/`MineApproval` (App×1)، overview route (Api).
- **C** الـbundle الحيّ: بطاقة `MineApproval`، `waitingMyApprovalCount`، عنوان «بانتظار اعتمادي»، صفوف مفقودة display-only «لا يوجد تسليم بعد».
- **D** enum + interface method في Application DLL المنشورة.
- **E** ركيزة البيانات: 35 إجمالي / 26 بمعتمِد حالي / 6 معتمِدين متمايزين.
- **F** سلوك العدّاد المُصفَّى مُثبَت في Phase 0 عبر W01-W08 (backend) + T11-T13 (frontend).
- **G** انحدار: `/api/submissions`, `/dashboard/me`, `/report-templates`, `/kpi-templates`, `/notifications` كلها 401 (منشورة/مؤمَّنة، لا 500/404).
- **قيد موثَّق**: التدفق الحيّ المُصادَق (JWT) غير قابل للتنفيذ — لا اعتماد لأي حساب RC متاح، والبذر/إعادة تعيين كلمات المرور ممنوعان بلا تفويض منفصل. العقد أُثبِت عبر الرموز المنشورة + فرض المصادقة + محتوى الـbundle الحيّ + ركيزة البيانات + اختبارات Phase 0 السلوكية.

## المراقبة
- `NRestarts=0`, ActiveState=active, SubState=running.
- صفر أسطر خطأ/استثناء/هجرة/بريد/تذكير/مجدول منذ إعادة التشغيل (`09:52:14 UTC`).
- الصحة الداخلية 200 + العامة HTTPS 200، SSL صحيح، Basic Auth يفرض 401 بلا مصادقة.

## الرجوع (Rollback)
- Backend: استعادة `publish-backup-premissingr1-20260723-095037` → `/opt/reporting-rc/publish` + chown www-data + restart.
- Frontend: استعادة `dist-backup-premissingr1-20260723-095037` → `/opt/reporting-rc/frontend/dist` + chown.
- DB: لا هجرة طُبِّقت ⇒ لا عكس مطلوب؛ dump متاح إن لزم.

## القبول الوظيفي المُصادَق (RC AUTHENTICATED FUNCTIONAL ACCEPTANCE) — مَحجوب بفجوة اعتماد

> النطاق: **RC فقط**، بلا نشر جديد، بلا تعديل كود/قاعدة، الإنتاج ممنوع. لم تُنشأ/تُعاد ضبط أي حسابات.

### الخطوة 1 — إعادة تأكيد الهوية الحيّة (قبل الدخول): **نجحت**
- الخدمة `khubara-reporting-rc.service` active/running، `NRestarts=0`.
- الصحة الداخلية 200.
- DLLs الحيّة مطابقة للمرشّح 24575f6 (Api `1510845e…`, App `9483a8dc…`, Infra `b5f746dd…`).
- الـbundle الحيّ `index-D6wU43rH.js`.
- الهجرة: 29، الرأس `20260716015239_KpiEvaluationPartialUniqueIndex` (بلا تغيير).
- `report_submissions=35`، `submission_field_values=434` (بلا تغيير).

### الخطوة 2 — الجلسة المُصادَقة: **محجوبة (فجوة اعتماد)**
- محاولة تسجيل الدخول عبر `POST /api/auth/login` بكل مصادر الاعتماد المتاحة أُرجعت **401**:
  - `Seed__AdminEmail` (نطاق `@test.local`) + `Seed__AdminPassword` (من `rc-env-setup.sh`) → 401.
  - `Seed__AdminEmail` + `admin_pass.txt` → 401.
  - المدير الافتراضي `admin@marketingexperts.local` + كلمة المرور الافتراضية → 401.
- **السبب الجذري**: قاعدة RC `reporting_rc` **استُعيدت من نسخة شبيهة بالإنتاج** (3 مديرين فعليين: `b***@gmail.com`، `a***@marketingexperts.local`، `M***@gmail.com` — كلهم نشطون/غير مقفلين/لهم PasswordHash)، وكلمات مرورها **مُدوَّرة** لا تطابق مخزن الأسرار. حساب البذر `@test.local` **غير موجود إطلاقًا في القاعدة** (`count=0`) لأن env التشغيل `/etc/khubara-reporting-rc.env` **بلا مفاتيح Seed__** فلم يُطبَّق البذر، والاستعادة طمست أي بذر سابق.
- لا تتوفّر كلمات مرور لأي حساب طبيعي (Employee×20 وغيرها). **بحسب التعليمات الصريحة: لم يُنشأ/يُعاد ضبط أي حساب بلا تفويض منفصل.**

### الخطوات 3-9 — محجوبة تبعًا للخطوة 2
تعذّر تنفيذ التدفّق المُصادَق (قبول الفترة/الفلاتر، عقد Overdue، عقد NeedsAction، Waiting-My-Approval، سلامة الصفوف المفقودة، الفلاتر المركّبة، نطاق الصلاحية) لعدم توفّر جلسة مُصادَقة. (العقد أُثبِت سابقًا عبر الرموز المنشورة + فرض المصادقة + محتوى الـbundle الحيّ + ركيزة البيانات + اختبارات Phase 0 السلوكية — راجع مصفوفة القبول A-G أعلاه.)

### الخطوة 10 — سلامة ما بعد المحاولة: **نجحت (صفر تحوّر)**
- سكربت الاختبار `/root/rc-uat.mjs` أُزيل من الخادم.
- `NRestarts=0`, active/running, الصحة الداخلية 200.
- الهجرة 29 / الرأس نفسه، `report_submissions=35`، `submission_field_values=434` — **بلا تغيير**.
- `AccessFailedCount` الإجمالي = 0 (محاولات 401 لم تُحدِث أي حالة).
- صفر أسطر خطأ/استثناء/بريد/تذكير/مجدول منذ إعادة التشغيل.

### الحكم
**RC AUTHENTICATED ACCEPTANCE BLOCKED — VALID RC CREDENTIALS OR NATURAL TEST DATA REQUIRED — RC REMAINS DEPLOYED, PRODUCTION NOT AUTHORIZED**

المطلوب لرفع الحجب (بتفويض منفصل صريح): إمّا (أ) كلمة مرور صالحة لحساب RC قائم (يُفضَّل حساب واسع النطاق + آخر محدود النطاق للخطوة 9)، أو (ب) تفويض صريح لإعادة ضبط كلمة مرور حساب RC واحد للاختبار (Backup أولًا، بلا مساس بالإنتاج).

**الإنتاج لم يُمَسّ. لا git tag. RC فقط.**

---

## القبول الوظيفي المُصادَق — مُنجَز عبر تمكين اعتماد مؤقّت مُفوَّض (RC فقط) — 2026-07-23

> النطاق: **RC فقط** (`reporting_rc`)، تذكرة مُفوَّضة بكتابة محدودة: إعادة ضبط مؤقّتة لكلمتَي مرور حسابَين قائمَين، تنفيذ UAT مُصادَق، ثم **استعادة حالة الاعتماد الأصلية**. الإنتاج ممنوع، لا هجرة/DDL، لا بذر جديد، لا تعديل أدوار/نطاق/فريق/بيانات تقارير، لا كلمات مرور ثابتة، لا احتفاظ بكلمات مؤقتة بعد UAT.

### 1) هوية الحسابَين (مُقنّعة)
- **الواسع (A):** `7e2cb6ac…` — الدور **CEO**، نشط/غير مقفل، نطاق واسع على مستوى الشركة.
- **المحدود (B):** `8284241a…` — الدور **TeamLeader**، نشط/غير مقفل، نطاق فريق واحد + **15 تعيين CurrentApproverId طبيعيًّا**.

### 2) مبرّرات الملاءمة (قراءة فقط قبل أي كتابة)
- A (CEO) يغطّي الفلاتر/الأعداد/المفقود-المتأخّر/NeedsAction على كامل النطاق (32 مُقدِّمًا).
- B (TeamLeader) هو الأعلى في توزيع CurrentApproverId (15 تعيينًا حقيقيًّا) + فريق فعلي ⇒ يثبت WaitingMyApproval وعزل النطاق. لم تُغيَّر أي أدوار/علاقات.

### 3) النسخ الاحتياطي (المرحلة 2)
- المجلّد الجذري (700/الملفات 600): `/root/rc-test-secrets/uat-credential-backup-20260723-101923/`.
- Dump كامل لـ`reporting_rc`: `reporting_rc-preuat-20260723-101923.dump` (377652 bytes)، SHA256 `3be43390…150f89`، `pg_restore --list` خرج 0.
- لقطة حقول Identity للحسابَين: `identity-snapshot-20260723-101923.tsv`، SHA256 `cd94fd43…7a55a0` (بلا طباعة قيم).
- مانيفست بلا أسرار: `manifest.txt`.

### 4) إعادة الضبط المؤقّتة (المرحلة 3 — عبر مسار ASP.NET Identity الرسمي)
- أداة RC-only لمرة واحدة (`GeneratePasswordResetTokenAsync`+`ResetPasswordAsync`)، كلمتان عشوائيّتان قويّتان مستقلّتان (لم تُطبَعا/تُسجَّلا).
- `RESET user=7e2cb6ac => SUCCEEDED role=CEO accessFailed=0 active=True locked=False`
- `RESET user=8284241a => SUCCEEDED role=TeamLeader accessFailed=0 active=True locked=False`
- تسجيل الدخول بعد الضبط: **A=200، B=200**. بلا تغيير بريد/اسم/دور/تنظيم، بلا بذر، بلا بريد، بلا مجدول.

### 5) مصفوفة UAT الكاملة (المرحلة 4 — بمقارنات enum نصّية صحيحة، violations=0 في كل العقود)
- **آخر 12 أسبوعًا:** total=143، `TOTAL_EQ_TOTALCOUNT=true`، توزيع النوع {مفقود:108، قائم:35}، الحالة {NotSubmitted:108, Draft:3, Returned:2, Closed:4, Submitted:26}. أسابيع: W26/W27/W28/W29/W30.
- **أسبوعان تاريخيان:** W30 (total=30, overdue=19) وW29 (total=30, overdue=30)، `EQ=true`، `onlyThisWk=true`، `WEEKS_DIFFER=true`.
- **الترقيم (Paging):** pageSize=5 صفحة1≠صفحة2، الملخّص ثابت.
- **Overdue:** W30={NotSubmitted:19}، W29={NotSubmitted:30}، آخر12أ={NotSubmitted:97, Draft:2, Returned:2}=101؛ النوع {مفقود:97، قائم:4}. **صفر تسرّب Submitted/Closed/Escalated/Approved**. total==totalCount==rows.
- **NeedsAction:** آخر12أ=5 ({Draft:3, Returned:2})، كلها قائمة، distinctSub=5، **بلا تسرّب مفقود**، card=5.
- **Overdue/NeedsAction عقد:** Overdue = فقط (مسودة/مُعاد متأخّر) + (مفقود متوقَّع متأخّر)؛ NeedsAction = فقط مسودة/مُعاد/مصعّد قائم فعليّ.
- **الفلاتر المركّبة (8):** Period+Overdue / Team+Period+Overdue / Department+Period / Submitter / ReportTemplateId / بحث اسم موظف / بحث عنوان قالب / مسح الكل — كلها `EQ=true`، والمسح الكامل=143=آخر12أ.

### 6) الأدلّة
كل الاستجابات 200، `Summary.Total == TotalCount == rows` في كل الحالات، توزيعات النوع/الحالة متّسقة مع خط أساس القاعدة، صفر مخالفات عقد.

### 7) دليل WaitingMyApproval
- **الواسع (CEO):** card=0 في (all/wk1/wk2) — لا تعيينات CurrentApproverId له (صحيح، بلا تسرّب).
- **المحدود (TeamLeader):** `LIMITED_MINE total=15 card=15 allMine=true violations=0` — **كل الصفوف currentApproverId==المستخدم المُصادَق**، قائمة، بلا مفقود ⇒ العقد مُثبَت بتعيينات طبيعية.

### 8) دليل صلاحية النطاق المحدود
- `LIMITED_ALL=29` مقابل `WIDE_SUBMITTERS=32`، `LIMITED_SUBMITTERS=5`، `SUBSET_OF_WIDE=true`، `STRICTLY_NARROWER=true`، `SINGLE_TEAM=true`، `LIMITED_OUT_OF_WIDE_SCOPE=0`.
- الـendpoint الإداري القديم `/api/directory/users` أرجع نطاق القائد الخاص (count=5) لا كامل الشركة ⇒ **الـoverview لم يوسّع الوصول**، لا تسرّب نطاق.

### 9) دليل الصفوف المفقودة عرض-فقط
- `MISSING_ROWS=108 ALL_DISPLAY_ONLY=true` (subId=null، hasSubmission=false، isExpectedSubmission=true، الحقول employee/template/period/due/statusLabel حاضرة)، `MISSING_WITH_APPROVER=0` ⇒ المفقود **لا يدخل أبدًا** NeedsAction ولا WaitingMyApproval.

### 10) أعداد البيانات قبل/بعد (المرحلة 5 — بلا تحوّر)
- الهجرة 29 / الرأس `20260716015239_KpiEvaluationPartialUniqueIndex` — بلا تغيير.
- report_submissions=35، submission_field_values=434، approval_steps=34 — بلا تغيير.
- توزيع الحالة Submitted 26/Closed 4/Draft 3/Returned 2 — مطابق.
- صفر تسليم مُصطنَع (created بعد النافذة=0)، صفر تسليم مُعدَّل (updated=0)، صفر قفل، NRestarts=0، health=200×3.
- Email/Reminders/Scheduler = false؛ صفر أخطاء/استثناء/500/هجرة/بريد/تذكير/مجدول في السجلّ منذ 09:52.

### 11) دليل استعادة الاعتماد (المرحلة 6)
- الاستعادة عبر الأداة من اللقطة للحسابَين فقط: `RESTORE … FIELDS_SET` ×2، `RESTORE_DONE restored=2`.
- **بصمة الحقول الحيّة بعد الاستعادة = بصمة اللقطة تمامًا**: A `5c04f1c7…003de0b`، B `a8ac4b96…8e2568` (SHA256 على PasswordHash|SecurityStamp|ConcurrencyStamp، بلا طباعة قيم).
- **كلمتا المرور المؤقتتان لم تعودا تُصادِقان: 401/401**. AccessFailedCount=0، LockoutEnd=null، LockoutEnabled=t، الأدوار CEO/TeamLeader دون تغيير.

### 12) دليل التنظيف (المرحلة 7)
- حُذفت سكربتات UAT (`rc-uat2/rc-uat3/rc-diag.mjs`) + أداة `--reset/--restore` (`/root/rc-uat-tool`) عبر shred/rm — بلا بقايا.
- حُذف ملفّا كلمات المرور المؤقتة (`temp-passwords.tsv`, `uat-creds.tsv`) عبر shred **بعد** نجاح الاستعادة. المُحتفَظ به: Dump + اللقطة + المانيفست + user-ids (بلا أسرار).
- صفر ترويسات Authorization أو رموز كلمة مرور في السجلّ، صفر توكن/كوكي على القرص.
- الملفات `.mjs` المتبقّية في `/root` (9) تخصّ مهامّ سابقة (r1b-prod-*/v6-*/gate-weekly) لا هذه التذكرة — تُركت (خارج النطاق).

### 13) الإقرارات النهائية
RC فقط ✓ | الإنتاج لم يُمَسّ (خدمة `reporting-api`/5090/`reporting_prod` منفصلة نشطة، RC على 5092) ✓ | لا هجرة/DDL ✓ | لا تحوّر بيانات تقارير (35/434/34 وتوزيع الحالة ثابتة) ✓ | كلمات المرور المؤقتة أُزيلت ✓ | حالة الاعتماد الأصلية استُعيدت (بصمات مطابقة + 401) ✓ | لا طباعة أسرار في أي مرحلة ✓.

### الحكم النهائي
**RC AUTHENTICATED ACCEPTANCE COMPLETE — FILTERED COUNTERS, MISSING OVERDUE, NEEDS ACTION, WAITING-MY-APPROVAL, DISPLAY-ONLY ROWS AND AUTHORIZATION SCOPE VERIFIED LIVE — TEMPORARY RC CREDENTIALS REMOVED — READY FOR PRODUCTION AUTHORIZATION REVIEW**

**الإنتاج لم يُمَسّ. لا git tag. RC فقط.**
