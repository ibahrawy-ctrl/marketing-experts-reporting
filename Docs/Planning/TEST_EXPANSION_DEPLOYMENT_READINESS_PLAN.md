# خطة جاهزية بيئة TEST للتوسعة الكاملة — TEST Expansion Deployment Readiness Plan

**معرّف الوثيقة:** `TEST-EXPANSION-RC4-PREFLIGHT-20260711`
**التاريخ:** 2026-07-11
**الطور:** تخطيط/تحليل فقط (Read/Analysis/Planning) — **لا نشر، لا تعديل TEST، لا هجرة، لا إعادة تشغيل، لا commit/push لهذه الوثيقة قبل موافقة المالك.**
**مصدر الحقيقة المعتمَد للتخطيط:** `develop @ ffb511906f0b523ebf59fbfa27a51be66189109a` (اختصار `ffb5119`).
**سياسة الطور الحاكمة:** TEST هي بيئة الترحيل للتوسعة الكبيرة؛ RC محجوزة للـHotfixes فقط؛ التوسعة الكاملة لا تمرّ عبر RC؛ الإنتاج الحالي = `reports` (reports.emarketingacademy.net).

> **تصنيف الأدلّة في هذه الوثيقة:**
> - **Proven-Local** = مُثبَت محليًّا من مستودع Git (HEAD نظيف) أو من تقارير النشر الموثّقة.
> - **Documented** = من تقارير نشر TEST السابقة (`RC-Test-Deployment-*`, `P0P1-TEST-*`, `P2P25-TEST-*`) بتاريخ ≤ يوم واحد، موثوقة لكنها ليست استعلامًا حيًّا لحظيًّا.
> - **Unverified** = يتطلّب استعلامًا حيًّا للقراءة فقط على `reporting_test_rc` أو فحص الخادم؛ لم يُنفَّذ في هذا الطور (ممنوع التعديل — الاستعلام قراءة فقط يحتاج موافقة صريحة).

---

## نتيجة تنفيذية (الخلاصة أولًا)

**الاكتشاف المحوري:** «التوسعة الكبيرة» (RC-4: Project-First execution + Execution Taxonomy + Project Workstreams + Workstream Deliverables + Rollup) **منشورة فعليًّا على TEST بتكافؤ تشغيليّ تامّ مع `ffb5119`**.

- آخر هجرة على TEST (Documented, 2026-07-10) = `20260709231845_AddWorkstreamDeliverables` = **نفس آخر هجرة في HEAD** (Proven-Local).
- الدلتا من `3114e45` (RC-4 Phase 3، وهو ما نُشر على TEST) إلى `ffb5119` (HEAD) في `reporting-backend/src` + `reporting-frontend/src` = **ملف واحد فقط: `reporting-frontend/src/pages/HomePage.test.tsx`** (ملف اختبار، لا يدخل الحزمة). بقية الدلتا كلها وثائق واختبارات تكامل. **صفر كود تشغيل جديد، صفر هجرة جديدة، صفر bundle جديد** (Proven-Local عبر `git diff --name-status 3114e45 HEAD`).

**الأثر على القرار:** هذه ليست عملية «نشر توسعة» بل عملية **تثبيت + توثيق منهجي + بوّابة UAT** لِما هو منشور بالفعل على TEST. لا يلزم — بالنسبة إلى `ffb5119` — بناء backend جديد أو تطبيق هجرة جديدة على TEST. ما يلزم هو التحقّق الرسمي من التكافؤ، وتثبيت الـartifacts، وإجراء UAT، وتسجيل نقاط القرار.

**الحكم المبدئي:** **CONDITIONAL GO** — الجاهزية مرتفعة، لكنها مشروطة بحسم قرارات المالك (خاصة استراتيجية قاعدة البيانات A/B وبيئة تشغيل TEST) وبتنفيذ فحوص التحقّق الحيّة قراءةً-فقط. التفاصيل في القسم 11.

> **تحديث بعد الفحص الحيّ (2026-07-11):** تمّ تنفيذ فحص حيّ قراءةً-فقط على TEST بموافقة المالك. **نتائجه الكاملة والحكم النهائي في «ملحق الفحص الحيّ» بنهاية الوثيقة.** تصحيح لواقعة وردت أعلاه بعدد `29`: العدد الفعليّ للهجرات = **30** (تطابق تامّ بين HEAD وTEST — كان خطأ عدّ تجميليًّا). كل بنود Unverified الأساسية أُغلقت، وحاصمتا العزل B-ISO-1/B-ISO-2 أُغلقتا.

---

## 1) جرد بيئة TEST الحالي (قراءة فقط)

> المصدر: تقارير `RC-Test-Deployment-test.emarketingacademy.net-Report.md` (2026-07-06)، `P0P1-TEST-Deployment-Report.md` (2026-07-10)، `P2P25-TEST-Deployment-Report.md` (2026-07-10). كلها **Documented**. القيم اللحظية (الأحجام/العدّات) موسومة **Unverified**.

| العنصر | القيمة | التصنيف |
|---|---|---|
| النطاق | `test.emarketingacademy.net` | Documented |
| الخادم | `187.127.72.232` (نفس مضيف الإنتاج، معزول منطقيًّا) | Documented |
| إعداد Nginx | موقع مستقلّ + Certbot TLS؛ Basic Auth على النطاق العام؛ `/api` و`/hubs` و`/health` مستثناة من Basic Auth | Documented |
| وحدة systemd | `khubara-reporting-test` (User=www-data) | Documented |
| مسار الخادم (backend) | `/opt/reporting-test/publish` | Documented |
| مسار الواجهة (frontend) | `/opt/reporting-test/frontend/dist` | Documented |
| المنفذ الداخلي | 5091 | Documented |
| قاعدة البيانات | `reporting_test_rc` | Documented |
| مستخدم القاعدة | `reporting_test_app` | Documented |
| اسم البيئة | `ASPNETCORE_ENVIRONMENT=Development` (⇒ OrgSeeder يبذر بيانات ديمو) | Documented — **بند قرار (انظر القسم 5 و10)** |
| ملف البيئة | `/etc/khubara-reporting-test.env` (640/600، root) | Documented |
| SSL/TLS | Let's Encrypt عبر Certbot | Documented |
| Basic Auth | `/etc/nginx/.htpasswd-rc-test`، المستخدم `khubara` | Documented |
| robots/noindex | `X-Robots-Tag: noindex` مفعّل | Documented |
| الملكية/الصلاحيات | publish + dist مملوكة `www-data:www-data` | Documented |
| نقطة الصحّة | `/health` = 200 (عامة، بلا Basic Auth) | Documented |
| إصدار الخادم (backend) | مبنيّ من `develop` بآخر هجرة `AddWorkstreamDeliverables` | Documented |
| حزمة الواجهة (frontend bundle) | `index-Cx7wlJTz.js` (P2P25) | Documented |
| سلسلة الهجرات | حتى `20260709231845_AddWorkstreamDeliverables` (آخر هجرة) | Documented |
| آخر نشر مُثبَت | `p2p25-test-20260710-012057` (P2 + P2.5) | Documented |
| مسار مستندات HR | `/var/lib/reporting-test/employee-service-requests/final-documents` | Documented |
| الأدمن | `admin@test.local` (كلمة المرور من env — غير مطبوعة) | Documented |
| أسرار TEST | `/root/rc-test-secrets/` | Documented (لا تُقرأ) |

**الحالة:** بيئة **نشطة (active)** ومحدَّثة (آخر نشر قبل يوم واحد). لا مؤشّرات على أنها stale/abandoned.
**بنود Unverified تتطلّب فحصًا حيًّا قراءةً-فقط:** حالة `systemctl is-active khubara-reporting-test` اللحظية، ناتج `/health` اللحظي، حجم `reporting_test_rc`، هاش الحزمة المُقدَّمة فعليًّا الآن، بصمة DLL المنشورة.

---

## 2) التحقّق من العزل (Isolation) — TEST مقابل بقية البيئات

| المحور | الحالة | التصنيف | ملاحظة |
|---|---|---|---|
| قاعدة بيانات الإنتاج | **Isolated** | Documented | TEST=`reporting_test_rc`/`reporting_test_app`؛ Prod=`reporting_prod`. قاعدتان منفصلتان على نفس مضيف PostgreSQL. |
| قاعدة بيانات RC | **Isolated** | Documented | RC بيئة منفصلة؛ لا مشاركة قاعدة مع TEST. |
| JWT / التوقيع | **Isolated** (متوقَّع) | Unverified | يجب إثبات أن `Jwt:Key` في `/etc/khubara-reporting-test.env` ≠ مفتاح الإنتاج. **فحص حيّ قراءةً-فقط مطلوب (بلا طباعة القيمة، مقارنة hash فقط).** |
| الكوكيز / DataProtection keys | **Unverified** | Unverified | يجب إثبات أن دليل مفاتيح DataProtection لـTEST منفصل عن الإنتاج (مسار مختلف لكل خدمة). خطر إن كان مشتركًا: إبطال جلسات متبادل. |
| تخزين الملفات | **Isolated** | Documented | TEST=`/var/lib/reporting-test/...`؛ Prod=`/var/lib/reporting/...`. مساران مختلفان. |
| البريد (Email) | **Isolated + مُعطَّل** | Documented | `Email__Enabled=false`, Mode=DryRun, `email_outbox=0`. لا إرسال. |
| التذكيرات (Reminders) | **Isolated + مُعطَّل** | Documented | `Reminders__Enabled=false`. |
| المجدولات (Schedulers/BackgroundServices) | **Unverified** | Unverified | EmailOutboxDispatcher + SubmissionReminderService يعملان لكن بوّاباتهما مغلقة؛ يجب تأكيد عدم وجود مجدوِل يمسّ الإنتاج. |
| الإشعارات (Notifications/SignalR) | **Isolated** | Documented | `/hubs` خاص بخدمة TEST على 5091. |
| التكاملات الخارجية | **Isolated** | Documented | لا تكاملات خارجية مفعّلة في TEST. |
| API base للواجهة | **Isolated** | Documented | الحزمة مبنيّة بـ`VITE_API_BASE_URL=https://test.emarketingacademy.net/api`؛ P2P25: 0 تسريب localhost، 0 إشارة لـAPI الإنتاج. |
| حزم الواجهة (bundles) | **Isolated** | Documented | dist منفصل في `/opt/reporting-test/frontend/dist`. |
| المضيف الفيزيائي | **Shared intentionally** | Documented | نفس الخادم `187.127.72.232` للإنتاج وTEST — عزل منطقيّ (خدمة/منفذ/قاعدة/مسار مختلفة). **مقبول لكنه يفرض حذرًا شديدًا: أي أمر خاطئ على الخادم قد يمسّ الإنتاج.** |

**حاصمات (Blockers) عزليّة محتملة تحتاج حسمًا قبل UAT:**
- **B-ISO-1 (Unverified):** إثبات انفصال `Jwt:Key` بين TEST والإنتاج (مقارنة hash قراءةً-فقط).
- **B-ISO-2 (Unverified):** إثبات انفصال مفاتيح DataProtection (مسارات مختلفة لكل خدمة).

لا توجد مشاركة خطِرة (Shared unsafely) **مؤكَّدة**؛ المشاركة الوحيدة المقصودة = المضيف الفيزيائي. البندان أعلاه يبقيان Unverified حتى الفحص الحيّ.

---

## 3) جرد قاعدة بيانات TEST (قراءة فقط، بلا أي كتابة)

| العنصر | القيمة | التصنيف |
|---|---|---|
| الاسم | `reporting_test_rc` | Documented |
| الحجم | ~509K (وقت نسخة `p2p25-test-20260710-012057`) | Documented (لحظيًّا **Unverified**) |
| عدد الجداول | يتطابق مع سلسلة 29 هجرة | Unverified (يحتاج `\dt`) |
| عدد الهجرات المطبَّقة | حتى `20260709231845_AddWorkstreamDeliverables` | Documented |
| آخر هجرة | `20260709231845_AddWorkstreamDeliverables` | Documented |
| عدّ المستخدمين | بيانات ديمو من OrgSeeder (Development) | Unverified |
| عدّ العملاء/المشاريع | بيانات ديمو | Unverified |
| عدّ التقارير/التسليمات | بيانات ديمو + بيانات UAT سابقة محتملة | Unverified |
| عدّ القوالب | من TemplateSeeder | Unverified |
| عدّ الإشعارات | — | Unverified |
| `email_outbox` | 0 | Documented |
| Execution Taxonomy | 6 نطاقات مبذورة idempotent بـ`(Domain,Code)` | Documented |

**بيانات تشغيلية يدوية يجب صونها؟** — **Unverified، وهو سؤال قرار للمالك:** هل توجد بيانات UAT يدوية أُدخلت على TEST يجب حفظها قبل أي إعادة تهيئة؟ (عملاء/مشاريع/تقارير أُنشئت يدويًّا في جولات مراجعة سابقة). يُحسَم عبر استعلام قراءة-فقط + قرار المالك.

**Reset مقابل Upgrade؟** — بما أن TEST بالفعل على تكافؤ تشغيليّ مع HEAD، **لا يلزم Upgrade للـschema**. الخيار الحقيقي هو: إبقاء بيانات TEST الحالية (بما فيها ديمو OrgSeeder + أي بيانات UAT) أم البدء بقاعدة نظيفة لـUAT منضبط. (تفصيل في القسم 5.)

**انحراف البذور (Seed drift) عن الـSeeder الحالي:** بما أن TEST يعمل `Development` ⇒ OrgSeeder نشط ⇒ بيانات ديمو موجودة لا يبذرها الإنتاج. Execution Taxonomy تُبذَر idempotent. **Unverified:** هل TemplateSeeder على TEST يطابق قائمة قوالب HEAD بالضبط (احتمال قوالب قديمة متراكمة من نشرات سابقة).

---

## 4) تقرير الدلتا — TEST مقابل `develop @ ffb5119`

> الأساس: TEST منشور من `develop` حتى هجرة `AddWorkstreamDeliverables` (P2P25). أقرب لقطة كود مطابقة = `3114e45` (RC-4 Phase 3). الدلتا محسوبة **Proven-Local**.

**دلتا الكود التشغيليّ (`git diff --name-status 3114e45 HEAD -- reporting-backend/src reporting-frontend/src`):**
```
M  reporting-frontend/src/pages/HomePage.test.tsx      (ملف اختبار فقط — لا يدخل حزمة الإنتاج)
```

| المحور | النتيجة | التصنيف |
|---|---|---|
| ملفات backend التشغيلية | **لا تغيير** بين `3114e45` وHEAD | Proven-Local |
| حزمة frontend | **لا تغيير تشغيليّ** (التغيير الوحيد ملف `.test.tsx`) | Proven-Local |
| الهجرات | **لا هجرة جديدة**؛ آخرها `AddWorkstreamDeliverables` على الجانبين | Proven-Local |
| الكيانات/Controllers/Routes/Services | **متطابقة** | Proven-Local |
| إعداد البيئة / API base | **متطابق** | Documented |
| Schema قاعدة البيانات | **متطابق** (نفس آخر هجرة) | Documented |
| حالة القوالب/البذور | متطابقة منطقيًّا؛ فرق بيانات الديمو بسبب `Development` على TEST | Documented/Unverified |
| Project-First / Execution Taxonomy / Workstreams / Deliverables / Rollup | **موجودة على الجانبين** | Proven-Local |

**التصنيف المطلوب:**
- **موجود على TEST وليس في الجديد:** لا شيء تشغيليّ (بيانات ديمو OrgSeeder فقط، وهي بيئية لا كوديّة).
- **في الجديد وليس على TEST:** لا شيء تشغيليّ. (وثائق واختبارات فقط — لا تُنشَر.)
- **تعارضات:** لا شيء.
- **ملفات قديمة (stale):** احتمال قوالب قديمة/بيانات UAT سابقة في قاعدة TEST — **Unverified**.
- **هجرات مفقودة:** لا شيء.
- **إعداد يجب تغييره:** بند القرار الوحيد المهمّ = بيئة تشغيل TEST (`Development` مقابل `Testing/Production-like`) لأغراض UAT — انظر القسم 10.
- **بيانات تحتاج ترحيلًا/صونًا:** يُحسَم عبر استعلام قراءة-فقط + قرار المالك (القسم 3).

**الخلاصة:** الدلتا التشغيليّة بين TEST و`ffb5119` = **صفر**. الفارق كله بيئيّ/بياناتيّ لا كوديّ.

---

## 5) استراتيجية قاعدة البيانات — الخيار A مقابل الخيار B

### الخيار A — الإبقاء على قاعدة TEST الحالية (`reporting_test_rc`) كما هي (Upgrade-in-place / No-op schema)
- **المزايا:** لا عمل schema (التكافؤ قائم)؛ حفظ أي بيانات UAT سابقة؛ أدنى كلفة تشغيلية؛ لا مخاطرة DROP.
- **المخاطر:** تراكم بيانات ديمو + بيانات UAT سابقة قد يشوّش سيناريوهات UAT النظيفة؛ احتمال قوالب/حسابات قديمة متراكمة؛ Seed drift غير مُتحقَّق منه.
- **فقدان البيانات:** لا شيء.
- **انحراف البذور:** يبقى كما هو (يحتاج تدقيقًا).
- **تعقيد الهجرة:** منعدم (لا هجرة معلّقة).
- **سهولة الـRollback:** عالية (لا تغيير schema أصلًا).
- **ملاءمة UAT:** متوسطة (بيئة غير نظيفة).
- **الكلفة التشغيلية:** الأدنى.

### الخيار B — قاعدة نظيفة معزولة جديدة لـUAT (Fresh clean DB)
- **المزايا:** بيئة UAT نظيفة ومنضبطة؛ بذور محدَّثة مطابقة للـSeeder الحالي؛ لا تلوّث تاريخيّ؛ سيناريوهات قابلة للتكرار.
- **المخاطر:** فقدان أي بيانات UAT يدوية سابقة إن لم تُصَن؛ يتطلّب إنشاء قاعدة/دور جديدين + إقلاع يطبّق كل 29 هجرة + بذر؛ عملية DROP/CREATE على نفس مضيف الإنتاج (حذر شديد).
- **فقدان البيانات:** بيانات TEST الحالية (ما لم تُنسَخ احتياطيًّا أولًا — إلزاميّ).
- **انحراف البذور:** يُحَلّ (بذور نظيفة).
- **تعقيد الهجرة:** يطبّق كامل السلسلة من الصفر عند الإقلاع (مُختبَر، additive).
- **سهولة الـRollback:** عالية (القاعدة القديمة تبقى محفوظة حتى الاعتماد).
- **ملاءمة UAT:** الأعلى.
- **الكلفة التشغيلية:** متوسطة.

### التوصية (لا تُنفَّذ الآن)
**الخيار B مع صون احتياطيّ كامل للقاعدة الحالية أولًا**، بشرط بيئة تشغيل مناسبة لـUAT. المبرّر: بما أن الكود على تكافؤ بالفعل، القيمة المضافة الوحيدة من هذا الطور هي **UAT نظيف وموثّق**؛ والخيار B يوفّر بيئة منضبطة قابلة للتكرار وبذورًا محدَّثة، مع بقاء القاعدة القديمة نسخةً احتياطية للرجوع. **إن كانت هناك بيانات UAT يدوية ثمينة على TEST (Unverified) ورغب المالك في صونها ⇒ الخيار A مقبول** مع تدقيق بذور/قوالب. **القرار للمالك** (انظر القسم 11).

---

## 6) بيان الإصدار الأوّليّ (Release Manifest — Preliminary)

| الحقل | القيمة |
|---|---|
| معرّف الإصدار (داخليّ، ليس بيئة RC) | `TEST-EXPANSION-RC4-PREFLIGHT-20260711` |
| فرع المصدر / SHA | `develop` / `ffb511906f0b523ebf59fbfa27a51be66189109a` |
| الوسم المحلّي المرتبط | `rc4-source-stabilized-20260711` (annotated، محليّ فقط، غير مدفوع) |
| نطاق الالتزامات | من `508509a` (أساس الإنتاج) حتى `ffb5119`: `6fd2253` (RC-4 Sales) → `d922e59` (RC-4 Baseline) → `3114e45` (Phase 3) → `668d8ca` (frontend test) → `31f98a4` (docs) → `ffb5119` (rollup test fixtures) |
| بصمة artifact الخادم | مبنيّ بـ`dotnet publish -c Release`؛ يحوي `WorkstreamDeliverable(Service/Controller)`, `AddWorkstreamDeliverables`, `CanManagePlanAsync`, Execution Taxonomy, Project Workstreams |
| بصمة artifact الواجهة | مبنيّ بـ`VITE_API_BASE_URL=https://test.emarketingacademy.net/api`؛ آخر هاش موثّق `index-Cx7wlJTz.js` (يُعاد التحقّق عند البناء) |
| الهجرات المطلوبة | 29 هجرة، آخرها `20260709231845_AddWorkstreamDeliverables` (**كلها مطبَّقة على TEST بالفعل**) |
| متغيّرات البيئة | `Jwt:*`, `ConnectionStrings:Default`, `Email__Enabled=false`, `Email__Mode=DryRun`, `Reminders__Enabled=false`, `ASPNETCORE_ENVIRONMENT`, `FileStorage__*` |
| ميزات مُضمَّنة | Project-First execution, Execution Taxonomy Catalog (6 نطاقات: workstream_type=12, deliverable=21, usage_context=12, workflow_step=15, delay_reason=11, platform_channel=11), Project Workstreams (P1), Workstream Deliverables (P2), تجميد واجهة التخطيط (P2.5), محرّك Rollup (SEO/Media Buyer), TEMPLATE-ROLE-GUARD-R1, APPROVAL-FALLBACK-R1 |
| ميزات مُستبعَدة | لا شيء جديد يُستبعَد عن HEAD (كل ما في HEAD منشور) |
| قيود معروفة | TEST يعمل `Development` ⇒ بيانات ديمو OrgSeeder (بند قرار)؛ Rollup مُختبَر بـfixtures؛ لا تنفيذ فعليّ للمخرجات (تخطيط فقط في P2/P2.5) |
| بنود مؤجَّلة | تفعيل تقارير التنفيذ الفعليّ للمخرجات (خارج نطاق هذا الإصدار) |
| اختبارات الدخان | Auth، RBAC للأدوار، Project-First، Workstreams/Deliverables CRUD تحت `ManagementOnly`، Rollup، أمان البريد (outbox=0) |
| سيناريوهات UAT | القسم 10 |
| مرجع الـRollback | القسم 9 |

---

## 7) خطة النسخ الاحتياطي (مسارات/أسماء فقط — لا تُنشأ الآن)

طابع زمنيّ مقترَح: `test-expansion-preflight-20260711-<HHMMSS>`.

| النسخة | المسار/الاسم المقترَح | ملاحظة |
|---|---|---|
| dump قاعدة `reporting_test_rc` | `/root/test-backups/reporting_test_rc-test-expansion-preflight-<TS>.dump` | `pg_dump` (redirect stdout؛ postgres لا يكتب /root مباشرة) |
| runtime الخادم | `/opt/reporting-test/publish-backup-<TS>` | `cp -a` |
| dist الواجهة | `/opt/reporting-test/frontend/dist-backup-<TS>` | `cp -a` |
| إعداد Nginx | `/root/test-backups/nginx-test-<TS>.conf` | نسخ موقع TEST فقط |
| ملف البيئة (بلا أسرار في الوثيقة) | `/root/test-backups/khubara-reporting-test.env.bak-<TS>` (600) | لا تُطبع القيم |
| مفاتيح DataProtection | مسار مفاتيح خدمة TEST (يُحدَّد عند الفحص الحيّ) | إن وُجد على القرص |
| الملفات المرفوعة | `/var/lib/reporting-test/employee-service-requests/final-documents` → أرشيف `<TS>` | |
| لقطة سجلّ الهجرات | ناتج `__EFMigrationsHistory` قراءةً-فقط → ملف نصّي `<TS>` | |
| لقطة حالة الخدمة | ناتج `systemctl status khubara-reporting-test` → ملف `<TS>` | |
| فحوص الصحّة | ناتج `/health` + قائمة hashes الحزمة → ملف `<TS>` | |

---

## 8) خطة النشر المستقبلية (تفصيليّة — لا تُنفَّذ)

> ملاحظة حاكمة: بالنسبة إلى `ffb5119`، **لا يلزم نشر backend/هجرة جديدة على TEST** (التكافؤ قائم). الخطوات أدناه هي القالب المعياريّ الكامل، وتُقلَّص فعليًّا إلى: تحقّق تكافؤ → (اختياريًّا) إعادة بناء وتثبيت من `ffb5119` لضمان بصمة artifact مثبَّتة → UAT. إن اعتُمد الخيار B ⇒ تُضاف خطوة تهيئة قاعدة نظيفة.

| # | الخطوة | الأمر المتوقَّع | المخاطر | شرط النجاح | شرط التوقّف | إجراء الـRollback |
|---|---|---|---|---|---|---|
| 1 | Preflight | فحوص قراءة-فقط: `systemctl is-active`, `/health`, آخر هجرة، أمان البريد، عزل Jwt/DataProtection | لمس خطأ للإنتاج | كل الفحوص خضراء + التكافؤ مؤكَّد | أي فحص أحمر أو عزل غير مُثبَت | لا شيء (قراءة فقط) |
| 2 | Backup | حسب القسم 7 (TEST فقط) | كتابة في مسار خاطئ | كل النسخ موجودة وسليمة | فشل أي نسخة | حذف النسخ الجزئية |
| 3 | بناء backend | `dotnet publish src/Reporting.Api -c Release -o /tmp/<TS>-publish` | تلوّث بيئة البناء | DLLs تحوي بصمات RC-4 المتوقَّعة | فشل البناء/بصمة ناقصة | لا نشر |
| 4 | بناء frontend | `VITE_API_BASE_URL=https://test.emarketingacademy.net/api npm run build` | تسريب localhost/API إنتاج | 0 تسريب، 1 إشارة test API، bundle جديد | أي تسريب | لا نشر |
| 5 | تحقّق الـartifacts | grep على DLLs + bundle | — | تطابق البصمات | عدم تطابق | لا نشر |
| 6 | تجميد/صيانة | إشعار تجميد TEST لـUAT | — | لا كتابة متزامنة | — | — |
| 7 | (الخيار B) تهيئة قاعدة نظيفة | `CREATE DATABASE`/`CREATE ROLE` جديدين ثم إقلاع يطبّق الهجرات | DROP خاطئ للإنتاج | القاعدة الجديدة مبذورة | خطأ في الاسم/الدور | استخدام القاعدة القديمة المحفوظة |
| 8 | نشر backend | `rsync -az --delete --exclude 'appsettings.Development.json'` → `/opt/reporting-test/publish` + `chown www-data` | حذف إعداد بيئيّ | rsync نظيف + ملكية صحيحة | فشل rsync | استعادة `publish-backup-<TS>` |
| 9 | تطبيق الهجرات | تلقائيّ عند الإقلاع (`MigrateAsync`) | — | «already up to date» أو تطبيق نظيف | خطأ هجرة | استعادة DB dump |
| 10 | نشر frontend | `rsync` → `/opt/reporting-test/frontend/dist` + `chown www-data` | حذف dist خاطئ | index.html يشير للحزمة الجديدة | فشل rsync | استعادة `dist-backup-<TS>` |
| 11 | إعادة التشغيل | `systemctl restart khubara-reporting-test` | إعادة تشغيل خدمة خاطئة | active + listening 5091 | فشل الإقلاع | استعادة backup + restart |
| 12 | صحّة | `/health` = 200 | — | 200 | ≠200 | Rollback |
| 13 | Auth | login `admin@test.local` = 200، anon `/api` = 401 | — | كما هو متوقَّع | فشل | Rollback |
| 14 | دخان الأدوار/الصلاحيات | RBAC 403 للأدوار غير المصرّحة | — | مطابق | فشل | Rollback |
| 15 | دخان Project-First | إنشاء/قراءة workstreams/deliverables تحت `ManagementOnly` | — | مطابق | فشل | Rollback |
| 16 | دخان Rollup | تجميع SEO/Media Buyer | — | أرقام صحيحة | فشل | Rollback |
| 17 | أمان البريد | `email_outbox=0`, Enabled=false | إرسال غير مقصود | 0 صف | أي صف | تعطيل فوريّ + تحقيق |
| 18 | تسليم UAT | حسب القسم 10 | — | البوّابة خضراء | — | — |

---

## 9) خطة الـRollback (لا تُنفَّذ)

- **Rollback backend:** استعادة `/opt/reporting-test/publish-backup-<TS>` عبر `rsync`/`cp -a` + `chown www-data` + `systemctl restart khubara-reporting-test`.
- **Rollback frontend:** استعادة `dist-backup-<TS>` + `chown www-data` (لا restart — nginx static).
- **Rollback DB:** استعادة `reporting_test_rc-...dump` عبر `pg_restore` إلى قاعدة نظيفة، أو (الخيار B) الرجوع إلى القاعدة القديمة المحفوظة بتبديل `ConnectionStrings:Default`.
- **معالجة الهجرات الإضافية (additive):** كل هجرات RC-4 additive (CREATE TABLE / AddColumn nullable)؛ عكسها آمن عبر `DropTable`/`DropColumn` أو استعادة dump. **لا هجرة معلّقة بالنسبة لـ`ffb5119`.**
- **متى الاستعادة الكاملة للقاعدة مقابل عكس runtime فقط:** إن لم تُطبَّق هجرة جديدة (الحالة الراهنة) ⇒ عكس runtime كافٍ؛ إن اعتُمد الخيار B وأُنشئت قاعدة جديدة ⇒ الرجوع بتبديل الاتصال للقاعدة القديمة.
- **البيانات المكتوبة أثناء الاختبار:** بيانات UAT التي تُنشأ بعد النسخة الاحتياطية ستُفقد عند Rollback DB — يجب إعلام فريق UAT.
- **نقطة اللاعودة:** لا توجد نقطة لا رجعة فيها ما دامت النسخ الاحتياطية سليمة والقاعدة القديمة محفوظة.
- **تحقّق ما بعد الـRollback:** `/health`=200، آخر هجرة صحيحة، login=200، أمان البريد سليم، bundle الصحيح مُقدَّم.

---

## 10) بوّابة الدخول لـUAT (UAT Entry Criteria)

TEST جاهزة لـUAT حين تتحقّق **كل** البنود:

- [ ] البناء مثبَّت من `ffb5119` (بصمة artifact backend + hash bundle frontend مثبَّتان في البيان).
- [ ] سلسلة الهجرات مثبَّتة عند `20260709231845_AddWorkstreamDeliverables`.
- [ ] البريد مُعطَّل (`Email__Enabled=false`, Mode=DryRun, `email_outbox=0`).
- [ ] التذكيرات مُعطَّلة (`Reminders__Enabled=false`).
- [ ] التكاملات الخارجية مُعطَّلة.
- [ ] عزل Jwt + DataProtection مُثبَت (B-ISO-1، B-ISO-2 مُغلقان).
- [ ] **قرار بيئة التشغيل مَحسوم:** هل يبقى TEST على `Development` (بيانات ديمو OrgSeeder، ملائم لعرض سريع) أم يُنقَل لـبيئة أقرب للإنتاج (`Testing`/بلا OrgSeeder) لـUAT واقعيّ؟ **قرار مالك.**
- [ ] حسابات اختبار جاهزة (لكل دور: Admin/CEO/GM/Manager/TeamLeader/Employee/HR/CeoSupport/Viewer).
- [ ] عملاء/مشاريع ديمو جاهزة لسيناريوهات Project-First.
- [ ] سيناريوهات Project-First قابلة للتنفيذ (إنشاء مشروع → workstreams → deliverables).
- [ ] Workstreams/Deliverables تعمل تحت `ManagementOnly` + وصول مدير الحسابات المُنَطَّق.
- [ ] Rollups (SEO/Media Buyer) تُنتج أرقامًا صحيحة.
- [ ] مصفوفة الصلاحيات (RBAC) مُتحقَّقة لكل الأدوار.
- [ ] التقارير التاريخية (Legacy) قابلة للقراءة والتجميع.
- [ ] سِجِلّ المشكلات المعروفة موثَّق ومتاح لفريق UAT.
- [ ] مالك توقيع UAT محدَّد.

---

## 11) الحاصمات، والقرارات المطلوبة، والحكم

### الحاصمات (Blockers)
| المعرّف | الوصف | التصنيف | الأثر |
|---|---|---|---|
| B-ISO-1 | إثبات انفصال `Jwt:Key` بين TEST والإنتاج | Unverified | يجب إغلاقه قبل UAT (فحص hash قراءةً-فقط) |
| B-ISO-2 | إثبات انفصال مفاتيح DataProtection بين TEST والإنتاج | Unverified | يجب إغلاقه قبل UAT |
| B-DATA-1 | تحديد وجود/قيمة بيانات UAT يدوية على TEST يجب صونها | Unverified | يحكم اختيار A/B |

### القرارات المطلوبة من المالك
1. **استراتيجية القاعدة:** الخيار A (إبقاء `reporting_test_rc`) أم الخيار B (قاعدة نظيفة مع صون احتياطيّ)؟ — التوصية: **B مع backup كامل أولًا**، أو A إن وُجدت بيانات UAT ثمينة.
2. **بيئة تشغيل TEST لـUAT:** إبقاء `Development` (ديمو OrgSeeder) أم `Testing`/أقرب-للإنتاج (بلا OrgSeeder)؟
3. **الإذن بالفحوص الحيّة قراءةً-فقط** على `reporting_test_rc` والخادم (لإغلاق B-ISO-1/2 وB-DATA-1 وتحويل بنود Unverified إلى مُثبَتة) — كلها SELECT/status بلا أي كتابة.
4. **صون بيانات UAT السابقة** (إن وُجدت): تُحفَظ أم تُطرَح؟

### الحكم النهائي
**CONDITIONAL GO.**
- **الجاهزية الكوديّة:** GO — التكافؤ بين TEST و`ffb5119` **مُثبَت محليًّا** (صفر دلتا تشغيليّة/هجرات).
- **الجاهزية العزليّة:** CONDITIONAL — مشروطة بإغلاق B-ISO-1/2 عبر فحص حيّ قراءةً-فقط.
- **جاهزية القاعدة/UAT:** CONDITIONAL — مشروطة بقرارات المالك (القاعدة A/B، بيئة التشغيل، صون البيانات).

بمجرّد حسم القرارات الأربعة وإغلاق الحاصمات الثلاثة عبر فحص حيّ قراءةً-فقط ⇒ يتحوّل الحكم إلى **GO** لبدء UAT على TEST دون الحاجة إلى نشر توسعة جديدة.

---

**ملاحظة حَوكميّة:** هذه الوثيقة تخطيطيّة بحتة. لم يُنفَّذ أي نشر أو تعديل على TEST/RC/الإنتاج، ولا هجرة، ولا إعادة تشغيل، ولا تغيير Nginx/SSL/DNS/البريد/المجدولات. لن تُدفَع/تُلتزَم هذه الوثيقة (commit/push) قبل موافقة المالك.

---

# ملحق الفحص الحيّ (Live Inspection Results — 2026-07-11)

> **نطاق الفحص:** قراءة فقط (Read-Only) على TEST بموافقة المالك. **لم يُنفَّذ أي تعديل ملفّ، ولا تعديل env، ولا إعادة تشغيل خدمة، ولا هجرة، ولا كتابة قاعدة، ولا حذف/إعادة تهيئة، ولا نشر، ولا تغيير Nginx/SSL/اسم البيئة، ولم تُمَسّ RC/الإنتاج، ولم تُطبَع أي أسرار/كلمات مرور/مفاتيح كاملة.** كل ما دون = SELECT/`systemctl status`/قراءة ملفّات إعداد/بصمات hash.
> **تصنيف الأدلّة هنا = Proven-Live** (استعلام/فحص حيّ لحظيّ)، ما لم يُذكر خلافه.

## L0) تصحيحات جوهريّة على متن الوثيقة أعلاه

| البند في المتن | ما ورد | التصحيح الحيّ (Proven-Live) |
|---|---|---|
| عدد الهجرات (القسم 3/6) | «29 هجرة» | **30 هجرة** — تطابق تامّ بين HEAD وTEST. الخطأ كان تجميليًّا في عدّ محلّي (regex `[A-Za-z]+` بتر `1A`→`` و`T1`→`` في `FlexiblePositionsPhase1A`/`KpiTemplateAssignmentsPhaseT1`). قائمة `__EFMigrationsHistory` الحيّة = قائمة ملفّات الهجرة في HEAD **حرفًا بحرف** (30/30). |
| Execution Taxonomy (القسم 3/6) | «6 نطاقات» | **19 نطاقًا / 170 سجلًّا** فعليًّا على TEST (التفصيل في L3). الرقم 6 كان عيّنة أمثلة لا الإجمالي. |
| حزمة الواجهة (القسم 1/6) | `index-Cx7wlJTz.js` | الحزمة المُقدَّمة حيًّا = **`index-DlS_VbOD.js`** (بناء Jul 10 09:41) — إعادة بناء لاحقة لنفس مصدر التشغيل (frontend src متطابق byte-for-byte من `3114e45` حتى `ffb5119` عدا `HomePage.test.tsx` وهو اختبار لا يدخل الحزمة). تحوي كل علامات RC-4/P2.5. |

## L1) التحقّق الحيّ من بيئة TEST (Proven-Live)

| العنصر | القيمة الحيّة | التصنيف |
|---|---|---|
| النطاق | `test.emarketingacademy.net` | Isolated |
| DNS | يحلّ إلى `187.127.72.232` | مؤكَّد |
| SSL | Let's Encrypt، `/etc/letsencrypt/live/test.emarketingacademy.net/`، سارٍ | مؤكَّد |
| Nginx | `/etc/nginx/sites-available/reporting-test`؛ `server_name test.emarketingacademy.net`؛ `root /opt/reporting-test/frontend/dist`؛ `auth_basic` على `/` بـ`/etc/nginx/.htpasswd-rc-test`؛ `X-Robots-Tag noindex` على الكلّ؛ `/api/` `/hubs/` `/health` بـ`auth_basic off` وproxy إلى `127.0.0.1:5091` | مؤكَّد |
| Basic Auth | مفعّل على النطاق العام (`.htpasswd-rc-test`) | مؤكَّد |
| noindex/robots | `X-Robots-Tag: noindex` على كل المواقع | مؤكَّد |
| وحدة systemd | `/etc/systemd/system/khubara-reporting-test.service`؛ **active (running)** منذ Fri 2026-07-10 01:22:54 UTC | مؤكَّد |
| المستخدم | `User=www-data` | مؤكَّد |
| ExecStart | `/usr/bin/dotnet /opt/reporting-test/publish/Reporting.Api.dll` | مؤكَّد |
| العنوان/المنفذ | يستمع على `127.0.0.1:5091` | مؤكَّد |
| مسار الخادم | `/opt/reporting-test/publish` (37 DLL؛ بناء backend 2026-07-10 01:21:30) | مؤكَّد |
| مسار الواجهة | `/opt/reporting-test/frontend/dist`؛ الحزمة `index-DlS_VbOD.js` (Jul 10 09:41) | مؤكَّد |
| اسم البيئة | `ASPNETCORE_ENVIRONMENT=Development` | مؤكَّد — **بند قرار** |
| ملف البيئة | `/etc/khubara-reporting-test.env` (root:root 600) | مؤكَّد |
| نقطة الصحّة | `/health` = **200** | مؤكَّد |
| API base | `https://test.emarketingacademy.net/api` (مضمّن في الحزمة، 0 تسريب localhost) | مؤكَّد |
| SignalR | `/hubs` على 5091 عبر نفس النطاق | مؤكَّد |

**علامات RC-4 في الحزمة الحيّة:** «أهداف العمل داخل المشروع»=1، «المخرجات المطلوبة»=1، «إضافة مخرج»=1، test API base=1، localhost-leak=0.
**علامات RC-4 في DLLs الحيّة:** `WorkstreamDeliverablesController`=6، `ExecutionTaxonomyController`=7، `ProjectWorkstreamsController`=6، `AddWorkstreamDeliverables` في Infrastructure=2.
**ملاحظة نظافة تشغيليّة (غير وظيفيّة):** `index.html` مملوك `501:staff` (بقايا uid من rsync لم يُعَد chown لـwww-data) — nginx يقدّمه بلا مشكلة؛ **بند نظافة لا حاصمة**.

## L2) إثبات العزل الحيّ (Proven-Live)

| المحور | TEST | الإنتاج | الحالة |
|---|---|---|---|
| اسم القاعدة | `reporting_test_rc` | `reporting_prod` | **Isolated** |
| دور القاعدة | `reporting_test_app` | `reporting_app` | **Isolated** |
| مضيف القاعدة | `127.0.0.1` | `127.0.0.1` | **Isolated** (قاعدتان منفصلتان على نفس المضيف) |
| بصمة `Jwt:Key` (hash فقط) | `d70dc4e6…` | `5cf56639…` | **Isolated** — **B-ISO-1 مُغلق** (المفتاحان مختلفان؛ القيم لم تُطبَع) |
| أسماء الكوكيز | JWT في localStorage (لا كوكيز جلسة مشتركة) | نفسه | **Isolated** |
| DataProtection keys | عابرة لكل عمليّة (ephemeral per-process)؛ لا keyring مشترك على القرص لخدمة التقارير | نفسه | **Isolated** — **B-ISO-2 مُغلق** (بتحفّظ: التوكنات العابرة لا تصمد عبر إعادة التشغيل — مقبول لبيئة TEST) |
| تخزين الملفات | `/var/lib/reporting-test/...` | `/var/lib/reporting/...` | **Isolated** |
| البريد | `Email__Enabled=false`, DryRun, outbox=0 | مستقلّ | **Isolated + مُعطَّل** |
| التذكيرات | `Reminders__Enabled=false` | مستقلّ | **Isolated + مُعطَّل** |
| المجدولات/BackgroundServices | تعمل ببوّابات مغلقة؛ لا مجدوِل يمسّ الإنتاج | مستقلّ | **Isolated** |
| SignalR | `/hubs` على 5091، نطاق TEST | 5090 | **Isolated** |
| التكاملات الخارجية | لا شيء مفعّل | — | **Isolated** |
| API base | `test.emarketingacademy.net/api` | `reports.emarketingacademy.net/api` | **Isolated** |
| حزمة الواجهة | dist منفصل، 0 تسريب | منفصل | **Isolated** |
| `Jwt:Issuer`/`Audience` | `khubara-reporting-test` | مختلف | **Isolated** |
| المضيف الفيزيائي | `187.127.72.232` (5091/`reporting_test_rc`) | نفسه (5090/`reporting_prod`) | **Shared intentionally** (عزل منطقيّ كامل) |

**النتيجة:** **لا مشاركة خطِرة (Shared unsafely) واحدة.** المشاركة الوحيدة المقصودة = المضيف الفيزيائي. **B-ISO-1 وB-ISO-2 مُغلقان.**

## L3) جرد قاعدة TEST الحيّ (Read-Only، بلا أي كتابة)

| العنصر | القيمة الحيّة |
|---|---|
| الاسم | `reporting_test_rc` |
| الحجم | ~12 MB |
| عدد الجداول | 63 |
| `__EFMigrationsHistory` | **30** (مطابق لـHEAD حرفًا بحرف) |
| آخر هجرة | `20260709231845_AddWorkstreamDeliverables` |
| السابقة | `20260709222126_AddProjectWorkstreams`، `20260708232456_AddExecutionTaxonomyCatalog` |
| المستخدمون | 36 (كلهم active؛ 1 `@test.local`=admin؛ 35 `@marketingexperts.local` ديمو؛ **0 يدويّ/غير ديمو**) |
| العملاء | 6 |
| المشاريع | 21 |
| Workstreams | 3 (أُنشئت Jul 9–10 على مشروعَي ديمو «تطوير موقع»/«سوشيال ميديا» — بقايا UAT) |
| Deliverables | 1 (بقايا UAT) |
| التسليمات | 16 (Closed=10، ApprovedByDirectManager=3، ApprovedByNextLevel=1، Draft=2) |
| القوالب | 35 (Published=25، **Archived=10** = قوالب Legacy) |
| نسخ القوالب | 47 |
| Execution Taxonomy | **170 سجلًّا / 19 نطاقًا** (activity_type=6، content_goal=9، content_type=12، delay_reason=11، deliverable=21، design_status=4، design_tool=5، design_type=13، edit_type=7، interaction_result=6، platform_channel=11، response_time=4، usage_context=12، video_duration=4، video_status=4، video_type=10، work_status=4، workflow_step=15، workstream_type=12) |
| `email_outbox` | 0 |
| الإشعارات | 57 |
| kpi_templates | 9 |
| kpi_evaluations | 0 |
| leave_requests | 0 |
| audit_logs | 234 |
| الإدارات/الفِرق/المسمّيات | 5 / 9 / 21 |

**مؤشّرات Seed Drift:** OrgSeeder نشط (Development) ⇒ 35 مستخدم ديمو + 6 عملاء + 21 مشروع. Execution Taxonomy مبذورة idempotent (170/19). لا حسابات يدويّة غير ديمو. **الفرق عن الإنتاج = بيئيّ (بيانات ديمو) لا كوديّ.**

## L4) إثبات التكافؤ التشغيليّ (Proven-Live)

| المحور | النتيجة |
|---|---|
| ملفّات التشغيل | DLLs الحيّة (بناء 2026-07-10 01:21:30) تحوي كل Controllers/Services لـRC-4 (أرقام أعلاه) |
| Controllers/Services/Routes | حاضرة ومطابقة لـHEAD |
| الهجرات | 30/30 متطابقة حرفًا بحرف |
| Project-First / Execution Taxonomy / Workstreams / Deliverables / Rollup | موجودة على الجانبين |
| نسخ القوالب / القوالب المؤرشفة (Legacy) | 47 نسخة، 10 مؤرشفة — متاحة لتقارير Legacy |
| مسارات/صفحات الواجهة | الحزمة الحيّة تحوي كل علامات P2.5 |
| هويّة الحزمة | `index-DlS_VbOD.js` = إعادة بناء لنفس مصدر التشغيل (frontend src متطابق byte-for-byte عدا ملف اختبار) |

**الحكم:** **Equivalent except tests-docs only** — الدلتا التشغيليّة بين TEST و`ffb5119` = **صفر**؛ الفرق الوحيد في المصدر منذ `3114e45` = `HomePage.test.tsx` (اختبار لا يدخل الحزمة).

## L5) جرد بيانات UAT الحالية وتصنيفها

| الصنف | الوصف | العناصر على TEST |
|---|---|---|
| **A — بيانات UAT مهمّة تُصان** | بيانات مُدخلة يدويًّا ذات قيمة | **لا شيء مؤكَّد** (0 حساب يدويّ؛ الـ3 Workstreams + 1 Deliverable = بقايا UAT على مشاريع ديمو، قيمتها تجريبيّة لا إنتاجيّة) |
| **B — حسابات اختبار أساسيّة** | حسابات لازمة لـUAT | `admin@test.local` + 35 حساب ديمو (`@marketingexperts.local`) لكل الأدوار |
| **C — بيانات قابلة لإعادة الإنشاء** | تُعاد بالبذر | 6 عملاء + 21 مشروع + 9 قوالب KPI + 170 سجلّ Taxonomy + الإدارات/الفِرق/المسمّيات (كلها من Seeders) |
| **D — بيانات قديمة/ملوّثة** | تراكم يُنظَّف | الـ3 Workstreams + 1 Deliverable + 16 تسليمًا تجريبيًّا + 57 إشعارًا + 234 سجلّ تدقيق (تراكم جولات سابقة) |
| **E — بيانات تاريخيّة لازمة لاختبارات Legacy** | قوالب مؤرشفة + تسليمات مغلقة | **10 قوالب مؤرشفة + 10 تسليمات Closed** (لازمة لتقارير Legacy — **يجب صونها/إعادة بذرها**) |
| **F — يتعذّر تصنيفها بلا قرار مالك** | تحتاج حسمًا | أيّ من الـ3 Workstreams/1 Deliverable يريد المالك اعتباره سيناريو UAT مرجعيًّا |

**لم يُحذَف شيء.**

## L6) توصية استراتيجية القاعدة (بعد الفحص)

- **الواقع المُثبَت:** ~99% من بيانات TEST قابلة لإعادة الإنشاء بالبذر (Seed/Demo)؛ 0 حساب يدويّ؛ بقايا UAT ضئيلة (3 Workstreams + 1 Deliverable)؛ لكن **10 قوالب مؤرشفة + 10 تسليمات Closed لازمة لتقارير Legacy**.
- **الخيار A (إبقاء `reporting_test_rc`):** أدنى كلفة، لا فقد، لكن يبقى التلوّث التاريخيّ (Seed drift + بقايا) ويصعّب UAT النظيف.
- **الخيار B (قاعدة نظيفة + backup للحالية):** بيئة UAT منضبطة قابلة للتكرار؛ **بشرط إعادة بذر/صون بيانات Legacy** (القوالب المؤرشفة + التسليمات المغلقة) وإلا تنكسر اختبارات Legacy Reporting.
- **التوصية (مائلة لـB، والقرار للمالك):** **الخيار B مع backup كامل إلزاميّ للقاعدة الحالية أولًا + ضمان إعادة بذر بيانات Legacy** (الصنف E). المبرّر: القيمة المضافة الوحيدة من هذا الطور = **UAT نظيف موثّق**، والبيانات الحاليّة 99% معاد إنشاؤها، فالتكلفة منخفضة والعائد أنظف. **الخيار A يبقى مقبولًا** إن رغب المالك في أدنى تدخّل وقبِل التلوّث. **لا تنفيذ الآن — قرار مالك.**

## L7) تقييم اسم البيئة (Environment Name)

**الواقع الحيّ:** TEST يعمل `ASPNETCORE_ENVIRONMENT=Development`.

| البُعد | الأثر على `Development` (الحاليّ) | لو `Staging`/`Testing` |
|---|---|---|
| Seeders | **OrgSeeder نشط** ⇒ 35 مستخدم + 6 عملاء + 21 مشروع ديمو | OrgSeeder لا يعمل ⇒ بيئة نظيفة (تحتاج بذرًا يدويًّا لحسابات UAT) |
| صفحات الأخطاء | **Developer Exception Page** (تفاصيل stack علنيّة خلف Basic Auth) | صفحة خطأ عامّة (أقرب للإنتاج) |
| Logging | **Verbose/Debug** | أقلّ إسهابًا (واقعيّ) |
| رؤوس الأمان | سلوك متساهل أقرب للتطوير | أقرب لصلابة الإنتاج |
| تحميل الإعداد | `appsettings.Development.json` (مُستبعَد من النشر) | `appsettings.{Env}.json` |
| السلوك العامّ | مريح للعرض السريع، **غير واقعيّ لـUAT تمثيليّ** | يعكس سلوك الإنتاج بدقّة أعلى |

**التوصية (تقييم فقط، لا تغيير):** لـUAT تمثيليّ للإنتاج، يُفضَّل الانتقال إلى ملفّ بيئة `Staging`/`Testing` (يُطفئ OrgSeeder وصفحات المطوّر ويقارب سلوك الإنتاج) مع بذر حسابات UAT صراحةً. **لكن إن كان الهدف عرضًا سريعًا ببيانات ديمو جاهزة ⇒ `Development` أسرع.** **القرار للمالك — لا تغيير مُنفَّذ.**

## L8) مصفوفة الحكم النهائيّة (بعد الفحص الحيّ)

| البند | الحكم | الأساس |
|---|---|---|
| **TEST runtime equivalence** | **GO** | 30/30 هجرة متطابقة؛ دلتا المصدر = ملف اختبار واحد؛ DLLs + الحزمة الحيّة تحملان كل علامات RC-4/P2.5 |
| **Isolation** | **GO** | كل المحاور Isolated؛ **B-ISO-1** (Jwt `d70dc4e6…`≠`5cf56639…`) و**B-ISO-2** (DataProtection عابر/معزول) **مُغلقان**؛ لا Shared unsafely؛ المضيف الفيزيائي Shared intentionally فقط |
| **Database preservation decision** | **Requires Owner Decision** (توصية: **B مع backup كامل + صون بيانات Legacy**) | 99% بيانات معاد إنشاؤها + 0 حساب يدويّ + بقايا UAT ضئيلة؛ لكن 10 قوالب مؤرشفة + 10 تسليمات Closed لازمة لـLegacy |
| **UAT data readiness** | **CONDITIONAL GO** | حسابات الاختبار حاضرة (admin + 35 ديمو)؛ لكن Environment=Development يبذر ديمو + بقايا UAT تحتاج قرارًا |
| **Ready for UAT freeze** | **CONDITIONAL GO** | مشروط بحسم استراتيجية القاعدة (A/B) وقرار بيئة التشغيل |
| **Ready for any TEST change** | **GO for no-op / CONDITIONAL for DB-B** | بالنسبة لـ`ffb5119` لا يلزم أي تغيير runtime/هجرة (التكافؤ قائم)؛ إن اعتُمد الخيار B ⇒ مشروط بتهيئة نظيفة + backup |

### الحاصمات الدقيقة المتبقّية
- **B-ISO-1:** ~~إثبات انفصال Jwt~~ — **مُغلق** (`d70dc4e6…` ≠ `5cf56639…`).
- **B-ISO-2:** ~~إثبات انفصال DataProtection~~ — **مُغلق** (عابر/معزول؛ تحفّظ: التوكنات لا تصمد عبر إعادة تشغيل — مقبول).
- **B-DATA-1:** ~~وجود بيانات UAT يدويّة~~ — **مُغلق واقعيًّا** (0 حساب يدويّ؛ فقط 3 Workstreams + 1 Deliverable بقايا تجريبيّة).
- **B-LEGACY-1 (جديد):** ضمان صون/إعادة بذر **10 قوالب مؤرشفة + 10 تسليمات Closed** إن اعتُمد الخيار B — وإلا تنكسر اختبارات Legacy Reporting. **يجب حسمه ضمن قرار القاعدة.**

### القرارات المطلوبة من المالك (بعد الفحص)
1. **استراتيجية القاعدة:** A (إبقاء `reporting_test_rc`) أم **B** (نظيفة + backup كامل + صون Legacy)؟ — التوصية: **B**.
2. **بيئة تشغيل TEST:** إبقاء `Development` (ديمو سريع) أم `Staging`/`Testing` (UAT تمثيليّ بلا OrgSeeder)؟
3. **مصير بقايا UAT** (3 Workstreams + 1 Deliverable): تُصان كسيناريو مرجعيّ أم تُطرَح؟
4. **إن اعتُمد B:** تأكيد آليّة صون/إعادة بذر بيانات Legacy (الصنف E / B-LEGACY-1).

**الخلاصة:** الجاهزية الكوديّة والعزليّة = **GO** (كل حاصمات العزل مُغلقة بالفحص الحيّ). المتبقّي كلّه **قرارات مالك** (قاعدة/بيئة/بقايا) لا عيوب تقنيّة. بمجرّد حسمها ⇒ **GO كامل لبدء UAT** دون الحاجة إلى نشر توسعة جديدة.

---

**تأكيد حَوكميّ لملحق الفحص الحيّ:** كل ما ورد أعلاه نتيجة **قراءة/فحص لحظيّ فقط**. **لم يُعدَّل أي ملفّ ولا env، ولم تُعَد أي خدمة، ولم تُطبَّق أي هجرة، ولم تُكتب القاعدة، ولم يُحذَف/يُعَد تهيئة شيء، ولم يُنشَر backend/frontend، ولم يُغيَّر Nginx/SSL/اسم البيئة، ولم تُمَسّ RC/الإنتاج، ولم تُطبَع أسرار/كلمات مرور/مفاتيح كاملة** (بصمات hash فقط). هذه الوثيقة لن تُلتزَم/تُدفَع (commit/push) قبل موافقة المالك.
