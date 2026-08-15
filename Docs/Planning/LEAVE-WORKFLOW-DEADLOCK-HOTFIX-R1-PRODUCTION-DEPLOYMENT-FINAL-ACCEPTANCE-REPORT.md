# LEAVE-WORKFLOW-DEADLOCK-HOTFIX (P2) — تقرير القبول النهائي لنشر الإنتاج

> **الحالة النهائية:**
> `LEAVE-WORKFLOW-DEADLOCK-HOTFIX P2 PRODUCTION PASS — HOTFIX DEPLOYED / EXISTING REQUESTS: READ-ONLY RECONCILIATION COMPLETE / CONTROLLED REPAIR NOT EXECUTED`

- **التاريخ:** 2026-08-04 (UTC).
- **النوع:** Backend فقط — **بلا Migration، بلا Frontend، بلا تغيير إعداد، بلا مساس بأيّ طلب إجازة قائم.**
- **معرّف نافذة النشر (TS):** `20260804-215038`.

---

## 1) الملخّص التنفيذي
نُشر المرشّح المقبول سلفًا في RC (`P2`) على الإنتاج بنجاح تامّ. الإصلاح يعالج جمود سير عمل الإجازات حين يكون **قائد الفريق هو نفسه المدير المباشر للموظّف**: بعد اعتماد خطوة قائد الفريق يُبحَث في سلسلة `ManagerId` صعودًا عن **بديل مدير تشغيليّ** (نشط + دور `Manager` + ضمن السلسلة الفعلية + ≠ قائد الفريق المُعتمِد)؛ فإن وُجد ⇒ **لا طيّ** (المسار الطبيعيّ)، وإن انعدم ⇒ **طيّ خطوة المدير تلقائيًّا** إلى `HR` مع حدث تدقيق `manager_step_auto_folded_no_operational_manager`. **لا خصم/Ledger قبل اعتماد HR النهائيّ.** النشر تمّ بإعادة تشغيل واحدة (زمن توقّف ≈ ثانية واحدة)، وكل ثوابت ما بعد النشر خضراء، ثم أُجري حصر قراءة-فقط للطلبات القائمة **دون تنفيذ أيّ إصلاح**.

## 2) الثوابت المرجعية المعتمدة
| العنصر | القيمة |
|---|---|
| أساس الإنتاج (Baseline) | `f3ee32f24323d61258ef15844f66c66adaf279df` |
| المرشّح المعتمد (Candidate) | `2d282cebf0a22f65b78cd751de17d6c927128d0d` |
| الشجرة (Tree) | `2074db3d1993511671c4b559d5b20786997b9d81` |
| patch-id | `f5dea3c5247a9d6fd80015f0dce65e759117aced` |
| رأس الهجرات (Migration head) | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| عدد الهجرات | 30 |
| المرشّح فوق الأساس | commit واحد؛ ملفّ إنتاج واحد + ملفّ اختبار واحد؛ **0 هجرة / 0 واجهة / 0 إعداد** |

## 3) المرحلة 0 — الفحص القبْليّ للإنتاج (قراءة فقط)
- أساس الإنتاج المُثبَت من SourceLink داخل DLLs الحيّة قبل النشر = `f3ee32f2` (مطابق للشرط).
- رأس الهجرات قبل النشر = `20260724224053` بعدد 30 (مطابق للشرط).
- **لم تُستوفَ أيّ حالة توقّف** ⇒ التقدّم آمن.

## 4) المرحلة 1 — التحقّق من المرشّح
- شجرة عمل مجمّدة معزولة: `/private/tmp/cand-leave-deadlock-hotfix-r1-20260803/reporting-backend/`.
- HEAD=`2d282ceb`، Parent=`f3ee32f2`، Tree=`2074db3d`، الفرع `candidate/leave-workflow-deadlock-hotfix-r1-20260803`، شجرة نظيفة.
- سطح الدلتا = **ملفّان** (+625/−3): إنتاج `src/Reporting.Infrastructure/Services/LeaveRequestService.cs` (+74/−3) + اختبار `tests/Reporting.IntegrationTests/LeaveWorkflowDeadlockHotfixTests.cs` (21 اختبارًا). **بلا Migration / بلا Frontend / بلا إعداد.** patch-id=`f5dea3c5`.

## 5) المرحلة 2 — النسخة الاحتياطية
- نسخة الـ Backend قبل النشر: `/opt/reporting/publish-backup-leavedeadlock-prod-20260804-215038/` (86 ملفًّا، مملوكة `www-data:www-data`).
- SourceLink داخلها = `1.0.0+f3ee32f2…` (الأساس).
- بصمات SHA256 للأساس: Infra `35ab74d5378c6ff71da6618c5f539c4c2161e62a78ab6726dc235970aac93f2f`، Api `81b969609f14b6130522da8da86ae8c8e212a2a73105d0d75365c06479a11cfa`، App `08101ea620a5204d0a502e9dd9429709a1c5f63705ef632858f2c7d6e7f39a22`، Domain `1ab800e7be6417ecbbd7f8cd007dde661f8cfbf6ca453c63db27fc893388f808`.
- **لا حاجة لنسخة قاعدة بيانات** (لا هجرة، لا كتابة على القاعدة).

## 6) المرحلة 3 — البناء والتجهيز (Staging)
- بناء Release نظيف (بعد `rm -rf bin obj`) بأمر:
  `dotnet publish src/Reporting.Api/Reporting.Api.csproj -c Release -o ./publish-prod-candidate -p:SourceRevisionId=2d282cebf0a22f65b78cd751de17d6c927128d0d -p:ContinuousIntegrationBuild=true`.
- **صفر تحذير جديد.** SourceLink مضمَّن `1.0.0+2d282ceb…` في الـ DLLs الأربعة.
- بصمات SHA256 للمرشّح: Api `8669bca20680e31a896be5b5d91bb95cac9df2356c95d172627d506db2bdc578`، Infra `83c30928b4dff8503ba77aaf691ecd3215ed2e5764e439b63abdfee45c184fbf`، App `424f73bb3400e866c02bfe0bbcd8021fa1c2996c883a6860af5c665d00795719`، Domain `d36701b57e7272489d843923fe72bb686f3883fea0f1976774b4a089fd394af2`.
- التجهيز في `/opt/reporting/publish-staging-leavedeadlock-20260804-215038/` وتحقّق byte-for-byte قبل الاستبدال.
- `appsettings.json` = md5 `d51e726f6d06e1fa41db71cf8ed9a4c9` (مطابق تمامًا للحيّ — لا انزياح إعداد؛ الإعداد الفعليّ من `/etc/reporting-api.env`).

## 7) المرحلة 4 — النشر
- **إيقاف واحد** للخدمة، استبدال مجلّد `publish` فقط (rsync)، مع الحفاظ على env/appsettings/الملكية/الصلاحيات، ثم **بدء واحد**.
- زمن التوقّف المقيس ≈ **0.99 ثانية**.

## 8) المرحلة 5 — التحقّق الفوريّ (كلّه أخضر)
| ثابت | القيمة |
|---|---|
| ActiveState / SubState | active / running |
| MainPID الجديد | 505567 |
| NRestarts | 0 |
| health (داخليّ 5090) | 200 |
| health (عامّ HTTPS) | 200 |
| SourceLink الحيّ | `1.0.0+2d282ceb…` |
| رأس الهجرات بعد النشر | `20260724224053` (30) — **«No migrations were applied»** |
| عدد ملفّات publish | 86 (بلا تغيير) |
| Environment | Production |
| قاعدة البيانات | reporting_prod |
| الأخطاء عند الإقلاع | 0 |
| Frontend / env / Email / Scheduler / Outbox | بلا تغيير |

## 9) المرحلة 6 — الدخان قراءة-فقط
- مسارات الإجازات (`GET /api/leave-requests/my`, `/{id}`, `/pending`, `/api/me/balances`, `/api/balances/employees`) استُجوِبت قراءةً فقط.
- RBAC: مجهول = **401**؛ الأدوار المخوَّلة = **200**.
- **صفر عمليات POST/PUT/DELETE.**

## 10) المرحلة 7 — حصر الطلبات القائمة (قراءة فقط)
### 10.1 المخطط التنظيميّ ذو الصلة
- فريق «سوشيال — البود الأول - 777» (`698c5e0e`): قائدة الفريق = **بسنت محمد** (TeamLeader، مديرها = أحمد عبدالرؤوف GeneralManager). أعضاء: شيماء صالح، محمد إبراهيم (مديرهما المباشر = بسنت محمد).
- فريق «تحسين محركات البحث SEO» (`a7ef8832`): قائدة الفريق = **شيماء عيد** (TeamLeader، مديرها = أحمد عبدالرؤوف). عضو: نور الدين رجب (مديره = شيماء عيد).
- مستخدمو دور **Manager** النشطون (بدائل تشغيلية محتملة): محمد عبدالله، **محمد عبدالقوي**، محمود القوصي.

### 10.2 جدول الحصر (المعرّفات منقّحة بأول 8 خانات)
| # | مقدّم الطلب | الحالة/الخطوة | قائد الفريق المُعتمِد | المدير المباشر (الدور) | Manager تشغيليّ أعلى؟ | هل P2 يعمل لو قُرِّر الآن؟ | تجاوز نقطة الطي؟ | Ledger | رصيد متأثّر | القرار المقترح |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | بسنت محمد | ManagerApproved/Hr | — (طيّ ذاتيّ T-WF1) | أحمد عبدالرؤوف (GM) | لا ينطبق | لا | نعم | 0 | لا | اعتماد HR النهائيّ (طبيعيّ) |
| 2 | شيريهان القاضي | ManagerApproved/Hr | أحمد عبدالرؤوف | أحمد عبدالرؤوف (GM) | لا ينطبق | لا | نعم | 0 | لا | اعتماد HR النهائيّ (طبيعيّ) |
| 3 | حبيبة | Submitted/TeamLeader | — | محمد عبدالقوي (Manager) | نعم (المدير نفسه) | لا (ليس تصادم TL) | لا | 0 | لا | استمرار طبيعيّ |
| 4 | عائشة كمال | TeamLeaderApproved/Manager | خالد مجدي (TL) | خالد مجدي (TL) | **نعم → محمد عبدالقوي** | **لا طيّ** (بديل موجود) | لا | 0 | لا | استمرار طبيعيّ (محمد عبدالقوي يعتمد خطوة المدير) |
| 5 | نور الدين رجب | ManagerApproved/Hr | شيماء عيد (TL) | شيماء عيد (TL) | لا | لا (تجاوز مسبقًا) | نعم | 0 | لا | اعتماد HR النهائيّ (طبيعيّ) |
| 6 | ريم جاب الله | TeamLeaderApproved/Manager | خالد مجدي (TL) | خالد مجدي (TL) | **نعم → محمد عبدالقوي** | **لا طيّ** (بديل موجود) | لا | 0 | لا | استمرار طبيعيّ (محمد عبدالقوي يعتمد خطوة المدير) |
| 7 | أحمد نصار | TeamLeaderApproved/Manager | أمير عادل (TL) | أمير عادل (TL) | **لا** (السلسلة: TL→GM→Admin) | نعم (كان سيطوي) | لا (**عالق**) | 0 | لا | **إصلاح انتقاليّ محكوم (مؤجَّل)** |
| 8 | بسنت محمد | TeamLeaderApproved/Manager | — (طيّ ذاتيّ T-WF1) | أحمد عبدالرؤوف (GM) | لا ينطبق | لا | لا | 0 | لا | استمرار طبيعيّ (اعتماد المدير/النطاق بواسطة GM) |
| 9 | محمد إبراهيم | ManagerApproved/Hr | بسنت محمد (TL) | بسنت محمد (TL) | لا | لا (تجاوز مسبقًا) | نعم | 0 | لا | اعتماد HR النهائيّ (طبيعيّ) |
| 10 | سمر مجدي | TeamLeaderApproved/Manager | بسنت محمد (TL) | بسنت محمد (TL) | **لا** (السلسلة: TL→GM→Admin) | نعم (كان سيطوي) | لا (**عالق**) | 0 | لا | **إصلاح انتقاليّ محكوم (مؤجَّل)** |

### 10.3 الخلاصة
- **جميع الطلبات العشرة: Ledger = 0 ورصيد غير متأثّر** (الخصم لا يقع إلا عند `HrApproved`، ولم يبلغه أيّ طلب).
- **حالتا جمود حقيقيّتان** عالقتان عند خطوة المدير بلا بديل تشغيليّ في السلسلة: **أحمد نصار** (`9d445a3e`) و**سمر مجدي** (`2407739b`). كلاهما تجاوز خطوة قائد الفريق تحت الكود القديم قبل النشر؛ و**P2 لا يطوي الطلبات العالقة رجعيًّا** (يعمل عند اتّخاذ قرار جديد فقط) ⇒ يحتاجان **إصلاحًا انتقاليًّا محكومًا** — **لم يُنفَّذ** (يتطلّب تصريحًا مستقلًّا).
- عائشة كمال وريم جاب الله **ليستا جمودًا**: يوجد فوق قائد فريقهما خالد مجدي مديرٌ تشغيليّ حقيقيّ (محمد عبدالقوي) في السلسلة ⇒ خطوة المدير تُكمَل طبيعيًّا.
- **لم يُنفَّذ أيّ إصلاح** على أيّ طلب.

## 11) المرحلة 8 — جاهزية التراجع
- النسخة الاحتياطية سليمة: `/opt/reporting/publish-backup-leavedeadlock-prod-20260804-215038/` (86 ملفًّا = عدد الحيّ)، SourceLink = الأساس `f3ee32f2`، بصمة Infra `35ab74d5…` مطابقة للأساس.
- `appsettings.json` في النسخة الاحتياطية والحيّ = md5 `d51e726f` (متطابق).
- **لا حاجة لتراجع قاعدة بيانات** (لا هجرة مُطبَّقة، الرأس ثابت 30/`20260724224053`).
- **لم يُنفَّذ التراجع.**
- **أوامر التراجع الموثّقة** (عند اللزوم فقط):
  1. `systemctl stop reporting-api`
  2. `rsync -az --delete /opt/reporting/publish-backup-leavedeadlock-prod-20260804-215038/ /opt/reporting/publish/`
  3. `chown -R www-data:www-data /opt/reporting/publish`
  4. `systemctl start reporting-api` ثم تحقّق `/health`=200 وSourceLink=`f3ee32f2`.

## 12) معايير تفعيل التراجع
- health غير 200 بشكل مستمرّ، أو فشل الإقلاع، أو تطبيق هجرة غير متوقَّع، أو انحدار في سلوك سير العمل يظهر في الدخان قراءة-فقط، أو عدم تطابق SourceLink الحيّ مع المرشّح.

## 13) ما لم يُمَسّ (ثبات عدم الانحدار)
Frontend، env، Email/Scheduler/Outbox، الهجرات (30/الرأس ثابت)، ScopeResolver، CurrentApproverId، خصم الرواتب الآليّ، KPI، قوالب التقارير — **كلّها بلا تغيير.**

## 14) منطق P2 المنشور (تفصيل)
`LeaveRequestService.DecideAsync` عند اعتماد خطوة قائد الفريق: إذا `Requester.ManagerId == المُعتمِد` **و** `!HasOperationalManagerAlternativeAsync` (صعود سلسلة `ManagerId` بحثًا عن نشط + دور `Manager` + ≠ المُعتمِد؛ Admin/CEO/GM/CeoSupport/HR ليست بديلًا تشغيليًّا) ⇒ يُضبَط `Status=ManagerApproved / CurrentStep=Hr` مع حدث `manager_step_auto_folded_no_operational_manager`. لا خصم/Ledger قبل HR النهائيّ.

## 15) بصمات القطع الأثريّة (Artifacts)
- DLLs المرشّح (حيّ): كما في §6.
- DLLs الأساس (نسخة احتياطية): كما في §5.
- SourceLink الحيّ: `2d282ceb`؛ نسخة احتياطية: `f3ee32f2`.

## 16) قيود المهمّة المحترَمة
- **ممنوع نُفِّذ فعلًا:** لا Migration، لا نشر Frontend، لا تغيير إعداد، لا تعديل أيّ طلب إجازة، لا اعتماد يدويّ، لا تعديل Status/CurrentStep، لا Ledger يدويّ، لا خصم رصيد، **لا أيّ Controlled Repair**، لا تغيير Scheduler/Email.

## 17) البنود المؤجَّلة (تتطلّب تصريحًا مستقلًّا)
- **EXISTING REQUESTS CONTROLLED RECONCILIATION** لطلبَي الجمود الحقيقيّين (أحمد نصار `9d445a3e`، سمر مجدي `2407739b`) — لم يُبدأ.
- لا يُبدأ أيّ تذكرة أخرى (بما فيها TEST-DB-HYGIENE-R1) دون تصريح صريح.

## 18) بيئة النشر
- الخادم `root@187.127.72.232`، Backend في `/opt/reporting/publish`، systemd `reporting-api` (User=www-data)، منفذ داخليّ 5090 خلف Nginx/TLS، الإعداد من `/etc/reporting-api.env`. قاعدة `reporting_prod`.

## 19) الأسرار
لم تُطبع أيّ أسرار/توكنات/كلمات مرور/سلاسل اتصال في أيّ خطوة من خطوات هذا النشر أو التقرير.

## 20) الحالة النهائية
`LEAVE-WORKFLOW-DEADLOCK-HOTFIX P2 PRODUCTION PASS — HOTFIX DEPLOYED / EXISTING REQUESTS: READ-ONLY RECONCILIATION COMPLETE / CONTROLLED REPAIR NOT EXECUTED`
