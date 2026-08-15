# AMR-CLIENT-FIRST-NAVIGATION-AND-SECTION-ORDER-R1 — SAFE PAUSE / HOLD STATE

**ملحق لتقرير القبول:** `Docs/Planning/AMR-CLIENT-FIRST-NAVIGATION-AND-SECTION-ORDER-R1-RC-FINAL-ACCEPTANCE-REPORT.md`
**نوع هذا المستند:** تثبيت حالة توقّف آمن — **قراءة فقط**، بلا نشر، بلا تعديل كود، بلا commit جديد، بلا مساس بـRC أو الإنتاج أو البريد أو قاعدة البيانات.

---

## 1. وقت التوقّف

| البند | القيمة |
|---|---|
| بدء إثباتات التوقّف (الرياض) | `2026-07-29 14:17:10 +03` |
| نهاية إثباتات التوقّف (الرياض) | `2026-07-29 14:21:44 +03` |
| المقابل بتوقيت UTC | `2026-07-29T11:17:10Z` → `2026-07-29T11:21:44Z` |
| المهلة القصوى لإنهاء التوقّف | `15:15` الرياض — **مُحترَمة** |
| تجميد التغييرات المطلق | `15:30 → 16:30` الرياض |
| الانتقال لمسار مراقبة البريد | `15:45` الرياض (نافذة المراقبة `15:50 → 16:30`) |

**الوضع الزمنيّ:** التوقّف تمّ **خارج** نافذة البريد وخارج فترة التجميد، بهامش آمن.

---

## 2. هويّة المرشّح (SHA / Parent / Tree) — أُعيد إثباتها من Git

```
SHA    = be07a7a7fd30e6210f29354039c952b7c4c4cc58
PARENT = 3efbd0dc2584d2fa1bc23c5373d8e2ee1eb10457
TREE   = 71b17d7313d4e8e97526d4ab2dde8acfe87ad04f
DATE   = Wed Jul 29 13:11:44 2026 +0300
SUBJ   = feat(reports): group account manager report by client then project
```

الأب `3efbd0dc` = **خطّ أساس واجهة الإنتاج المنشورة حاليًّا** (`index-96kHwdBC.js`) ⇒ المرشّح مبنيّ فوق الإنتاج مباشرةً بلا انحراف نسب.

**سطح المرشّح — 3 ملفّات، +1019/−132:**

```
A  reporting-frontend/src/components/PresentationProfileClientNav.test.tsx   (جديد، 27 اختبارًا)
M  reporting-frontend/src/components/PresentationProfileReport.tsx
M  reporting-frontend/src/pages/SubmissionsPage.tsx
```

**حرّاس المحتوى على الـdiff (كلّها = 0):**

| الحارس | النتيجة |
|---|---|
| ملفّات Backend | `0` |
| ملفّات Migration | `0` |
| ملفّات Email / Notification / Scheduler / Reminder | `0` |
| ملفّات Preview / Fixture / Playwright / `.mjs` | `0` |
| صور أو PDF (`png/jpg/jpeg/gif/webp/pdf`) | `0` |
| ملفّات `Docs/` | `0` |
| بيانات حقيقيّة داخل الـdiff (`متجر امداد`/`مطاعم عم قاسم`/`منصة مكانة`/`amrcf-uat`/`6389957d`/`rc-report`/`emarketingacademy`) | `0` |

---

## 3. الشجرة والفرع (Worktree / Branch)

```
BRANCH   = candidate/amr-client-first-r1-20260729     ← فرع مُسمّى، وليس Detached HEAD
WORKTREE = /private/tmp/amr-cand-r1-20260729          (مسار الوصول: /tmp/amr-cand-r1-20260729)
GIT-DIR  = <repo>/.git/worktrees/amr-cand-r1-20260729
```

**قرار المرحلة 1:** المرشّح **موجود أصلًا على فرع محليّ مُسمّى**، لذا لم يُنشأ فرع جديد ولم يُنفَّذ Tag ولا Push — الشرط المشروط في التذكرة («إن كان على Detached HEAD → أنشئ فرعًا») **لم يتحقّق**. الفرع المقترح `candidate/amr-client-first-r1-production-ready` **لم يُنشأ عمدًا** لتفادي أيّ عملية Git غير ضروريّة على حالة مُجمَّدة.

الفروع التي تحوي المرشّح: `candidate/amr-client-first-r1-20260729` **حصرًا**.

---

## 4. إثبات نظافة Git (Candidate Freeze)

| الفحص | النتيجة |
|---|---|
| `git status --porcelain` | **فارغ** |
| عدد الملفّات غير المتعقَّبة | `0` |
| ملفّات غير متعقَّبة/متجاهَلة تحت `reporting-frontend/src`، `reporting-backend`، `Docs` | **لا شيء** |
| `rebase-merge` / `rebase-apply` | **غير موجودَين** |
| `MERGE_HEAD` / `CHERRY_PICK_HEAD` / `REVERT_HEAD` / `BISECT_LOG` | **غير موجودة** |
| `.git/index.lock` | **غير موجود** |
| commit جديد أُنشئ في هذه الجلسة | **لا** |
| تعديل على المرشّح | **لا** |
| Push إلى Remote | **لا** (غير مطلوب بسياسة المشروع) |
| Tag | **لا** |

**إفصاح — `stash@{0}` قائم وغير ذي صلة:**

```
stash@{0}: WIP on main: 508509a add email change for admin (any user) and self (with password confirm)
قاعدته  = 508509ad8474b321c80cbdd48eb84ecb54bee212  (نسب قديم منفصل تمامًا)
حجمه    ≈ 100 ملفّ، من ضمنها reporting-frontend/src/pages/SubmissionsPage.tsx
```

التقاطع مع ملفّات المرشّح هو **تقاطع في اسم الملفّ فقط**، لا في المحتوى ولا في النسب. الـstash **سابق الوجود، لم يُنشأ في هذه التذكرة، ولم يُطبَّق، ولن يُحذف**. يُوثَّق هنا كي لا يُلتبس مستقبلًا بأنّه تغيير غير موثَّق يخصّ المرشّح.

---

## 5. نتائج الاختبارات والبناء (مُعادة النقل — لم تُعَد التشغيل)

> إعادة تشغيل الاختبارات الطويلة **ممنوعة** في هذه التذكرة؛ الأرقام أدناه منقولة حرفيًّا من تقرير القبول §6 و§7.

| البند | النتيجة |
|---|---|
| `PresentationProfileClientNav.test.tsx` (Client-first) | **27/27 أخضر** |
| مجموعة اختبارات الواجهة الكاملة (Vitest 4.1.8) | **أخضر، صفر فشل** |
| `tsc -b` (Typecheck) | **أخضر، صفر خطأ** |
| `vite build` | **نجح** — التحذير الحميد الوحيد `/*#__PURE__*/` في `@microsoft/signalr` |

---

## 6. حزمة المرشّح — الاسم والحجم والبصمة والأصل

```
index-DaDCi1OK.js    1,329,502 bytes
  sha256 = b0728e96b27fb4f443757af1ad59bfddffa1d115df47d7c2b666e99310c085aa
index-rPl-oo4Z.css      31,115 bytes
  sha256 = 374ebcdb63a6dc103588c0044992ffdd9914da50e4d4d413e27b3b627f775840
index.html
  sha256 = 3b1596ef3ec73b3f72bb519d369c059d5c29ecfd7fe90f8925e639f76774d5a7
```

**إثبات Same-Origin `/api`** (من داخل الحزمة مباشرةً):

```
},clear(){…}},Ws=`/api`,I=Rs.create({baseU…
if(!Us.access)return;let t=Ws.replace(/\/api$/,``)+`/hubs/notifi…
```

- تكرار `/api` داخل الحزمة = `1` (تعريف واحد)، وهو **مسار نسبيّ** لا عنوان مطلق.
- عناوين مطلقة تنتهي بـ`/api` (`https?://…/api`) = **صفر**.
- تسريب `localhost:5090` = **صفر**.

**البصمة الثلاثيّة على RC (محليّ = قرص = مُقدَّم عبر HTTPS):**

```
محليّ (بناء المرشّح)        : b0728e96b27fb4f443757af1ad59bfddffa1d115df47d7c2b666e99310c085aa
قرص RC (/opt/…/dist/assets) : b0728e96b27fb4f443757af1ad59bfddffa1d115df47d7c2b666e99310c085aa
مُقدَّم عبر HTTPS من nginx    : b0728e96b27fb4f443757af1ad59bfddffa1d115df47d7c2b666e99310c085aa
```

---

## 7. حالة RC الحاليّة وثوابتها (قراءة فقط، بعد التنظيف)

| البند | القيمة |
|---|---|
| حزمة الواجهة | `index-DaDCi1OK.js` (1,329,502) + `index-rPl-oo4Z.css` (31,115) |
| sha256 (js / css / index.html) | `b0728e96…` / `374ebcdb…` / `3b1596ef…` |
| `GET /health` (loopback 5092) | **200** |
| `GET /` عبر HTTPS | **200** |
| Backend SourceLink (الأربع DLLs) | `18207480fdfb4b69d7b1a4ba50eb22bece930524` — **مطابق للإنتاج** |
| الخدمة | `ActiveState=active`، `MainPID=207030`، `NRestarts=0` |
| `ExecMainStartTimestamp` | `Wed 2026-07-29 06:58:38 UTC` — **قبل** نشر الواجهة (10:12 UTC) ⇒ Backend لم يُعَد تشغيله |
| عدد الهجرات / الرأس | `30` / `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| `EmailNotifications__Mode` | `DryRun` |
| `Email__Enabled` | `false` |
| `ReportReminderScheduler__Enabled` | `false` |
| `Reminders__Enabled` | `false` |
| `mtime`/حجم ملفّ البيئة | `2026-07-29 06:58:38 UTC` / `1361` — **بلا تغيير** |
| `email_outbox` (إجمالي) | `0` |
| `Pending / Processing / Failed` | `0 / 0 / 0` |
| `email_notifications` آخر 6 ساعات | `0` |
| ملفّات `/tmp/rc_*.sql` و`/tmp/rc_*.sh` على الخادم | **لا شيء** (نُظِّفت) |

**RC مستقرّ ونظيف، والقناة البريديّة صامتة تمامًا.**

---

## 8. لقطة خطّ أساس الإنتاج (Production Baseline) — قراءة فقط

| البند | القيمة |
|---|---|
| حزمة الواجهة | `index-96kHwdBC.js` (1,324,028) + `index-Dq23uPgW.css` (30,306) |
| sha256 (js) | `f979b8cb2692e5687da720c5f9e44ad077358d8eec62cca8d160f581af81e172` |
| sha256 (css) | `ab2795a01be9cfaeed932e7dd9b6c5e0683d925dbea7e8e193c1b6594957b23e` |
| `index.html` sha256 — على القرص **و**المُقدَّم عبر HTTPS | `057a82177680b9cfeaf4696fbccdfd9042d1036105c9426a0e652afd028a4ca4` (متطابقان) |
| `index.html` يشير إلى | `index-96kHwdBC.js` |
| `mtime` لأصول الواجهة | `Jul 27 23:01` — **لم تتغيّر منذ نشر AMR-REDESIGN-R1** |
| `GET /` عبر HTTPS | **200** |
| `/health` (loopback + عام) | **200 / 200** |
| Backend SourceLink (الأربع DLLs) | `18207480fdfb4b69d7b1a4ba50eb22bece930524` |
| الخدمة | `ActiveState=active`، `MainPID=210497`، `NRestarts=0`، بدء `Wed 2026-07-29 07:25:34 UTC` |
| عدد الهجرات / الرأس | `30` / `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| `email_outbox` | `0` |

**الحكم:** خطّ أساس واجهة الإنتاج **لم يتغيّر** بعد إنشاء المرشّح، و`sha256` الحزمة يطابق ما رُصد وقت تجميد المرشّح، وأب المرشّح `3efbd0dc` هو نفسه commit هذا الخطّ.
⇒ **لا يوجد Blocker على الاستئناف.** لا حاجة لإعادة بناء المرشّح على خطّ أساس جديد.

---

## 9. ثوابت البريد والمجدول (Email / Scheduler Invariants)

| المفتاح | RC | Production |
|---|---|---|
| `EmailNotifications__Mode` | `DryRun` | `Enabled` |
| `Email__Enabled` | `false` | `false` |
| `Reminders__Enabled` | `false` | `true` |
| `ReportReminderScheduler__Enabled` | `false` | `true` |
| `ReportReminderScheduler__PollMinutes` | — | `15` |
| `DailyDueHour` / `WeeklyDueHour` / `OverdueHour` / `SummaryHour` / `ReviewHour` | — | `16 / 9 / 9 / 9 / 9` |
| `mtime` ملفّ البيئة | `2026-07-29 06:58:38 UTC` | `2026-07-26 19:49:58 UTC` |
| `email_outbox` | `0` | `0` |

**لم يُمَسّ أيّ مفتاح بريد أو مجدول على أيّ بيئة في هذه التذكرة.** نافذة `DailyDue = 16:00` بتوقيت الرياض على الإنتاج **فعّالة وسليمة** وستُراقَب في المسار التالي.

---

## 10. النسخة الاحتياطيّة وجاهزيّة التراجع

**RC — الواجهة:**

```
/opt/reporting-rc/frontend/dist-backup-amrclientfirst-20260729-101238   (1.4M، موجود وسليم)
سجلّ الطابع الزمنيّ: /root/amr-clientfirst-rc-ts.txt = 20260729-101238
```

**إجراء التراجع على RC** (عند الحاجة، Frontend فقط، بلا إعادة تشغيل خدمة):

```bash
TS=20260729-101238
rsync -a --delete /opt/reporting-rc/frontend/dist-backup-amrclientfirst-$TS/ /opt/reporting-rc/frontend/dist/
chown -R www-data:www-data /opt/reporting-rc/frontend/dist
# تحقّق: sha256sum /opt/reporting-rc/frontend/dist/assets/*.js  +  GET / = 200
```

**الإنتاج:** لم يُنشَر شيء ⇒ **لا تراجع مطلوب**. عند النشر مستقبلًا يجب أخذ نسخة `dist-backup-*` **قبل** أيّ rsync، والتراجع بالإجراء نفسه.

**بيانات RC:** كلّ الكتابات عُكِست بـSoft Delete/إغلاق طبيعيّ، **بلا أيّ DELETE فيزيائيّ** — لا يوجد شيء ينتظر التراجع.

---

## 11. أدلّة RC المحفوظة (Phase 2) — بلا إعادة تشغيل UAT وبلا بيانات جديدة

| البند | الحالة | المرجع |
|---|---|---|
| قرار قبول RC | **`PASS`** | تقرير القبول §23 |
| توصية الإنتاج | **`GO`** بستّة شروط مُلزِمة | §24 |
| مصفوفة التجميع حسب العميل | محفوظة (التجميع بـ`ClientId` حصرًا، الترقيم يبدأ من 1 لكلّ عميل) | §14 |
| 22 سيناريو تنقّل | **22/22 نجحت** (مرساة `amr-project-{projectId}`، Focus + Highlight 2200ms) | §15 |
| سيناريو مشروعَين بالاسم نفسه تحت عميلَين | **مثبَت — لا يختلطان** (الفصل بالمعرّف لا بالنصّ) | §16 |
| ترتيب الأقسام | مُثبَت بسرد العناوين المرتَّب من الـDOM | §17 |
| Mobile / RTL / Print | **سليمة** | §18 |
| عدم التراجع: Generic Renderer / Moderation / V2 | **سليمة، بلا أثر** | §19 |
| تنظيف بيانات RC | **مكتمل** — انظر §12 أدناه | §20 |
| مسار نسخة RC الاحتياطيّة | مُوثَّق | §10 أعلاه + §22 |
| إجراء التراجع | مُوثَّق | §10 أعلاه + §22 |

**عقبة تقنيّة موثَّقة (لا تؤثّر على القبول):** nginx على RC يفرض `auth_basic` على مستوى الـserver ⇒ يرثه `location /api/` و`/hubs/` ⇒ ترويسة `Authorization` واحدة متنازَع عليها بين Basic والـBearer. حُلَّت بنفق SSH `-L 15092:127.0.0.1:5092` لمسارات الـAPI فقط، بينما ظلّت الوثيقة و`/assets/` مُقدَّمة فعليًّا من nginx عبر HTTPS ⇒ **القبول تمّ على الحزمة المنشورة الحقيقيّة** (مثبَت بالبصمة الثلاثيّة §6).

---

## 12. اكتمال تنظيف بيانات RC

| السجلّ | الحالة النهائيّة |
|---|---|
| تسليم Fixture `6389957d-6ce1-46b9-a61e-2093ec89535f` | `IsDeleted = t`، `DeletedAtUtc` مضبوط، `Status = Submitted` (التدقيق محفوظ) |
| `submission_field_values` للتسليم | **3 صفوف محفوظة** (لا حذف) |
| المستخدم المؤقّت `f4a19fe4-7211-4588-baf3-c92c76682b31` | `IsActive = f`، `PasswordHash IS NULL`، `LockoutEnd = 2099-12-31` |
| اعتماد الدخول المسحوب | تسجيل الدخول بكلمة المرور القديمة ⇒ **401** |
| `user_team_memberships` المؤقّتة | نشط `0` من إجماليّ `3` (عُطِّلت مع `EndDateUtc`) |
| `AspNetUserRoles` للمستخدم المؤقّت | **صفّان مُبقيان عمدًا** للتدقيق |
| العودة لخطّ الأساس | `live_submissions = 37`، `active_users = 35`، `active_memberships = 1` |
| حذف فيزيائيّ (`DELETE`) | **صفر — لم يُنفَّذ إطلاقًا** |

**درس مُسجَّل:** سحب اعتماد حساب Identity يجب أن يكون بـ`PasswordHash = NULL` (يُرجِع 401 نظيفًا)؛ وضع نصّ غير صالح مثل `'REVOKED-…'` يرمي استثناءً عند فكّ base64 ⇒ **500** بدل 401 (حدث فعلًا وأُصلح فورًا).

---

## 13. إيقاف العمليّات (Phase 5) — مُثبَت

| الفحص | النتيجة |
|---|---|
| خادم تطوير مرتبط بالمرشّح (`vite`/`npm run dev`) | **لا يعمل** |
| `Playwright` | **لا يعمل** |
| `npm test` / `vitest` | **لا يعمل** |
| `vite build` | **لا يعمل** |
| نفق SSH / Port-forward لـRC (`15092`) | **مغلق** — لا مستمعين |
| منافذ `5173/5174/15092/5090/5092` محليًّا | **لا مستمعين** |
| سكربت مراقبة أو Fixture قيد التشغيل | **لا شيء** |
| نشر معلَّق أو جلسة قادرة على تعديل RC/الإنتاج لاحقًا | **لا شيء** |
| سكربتات `.amrcf-*.mjs` داخل `node_modules` للشجرتين | **مُزالة** |
| ملفّات `/tmp/*.mjs` التاريخيّة | **باقية عمدًا كأدلّة خارج Git، وكلّها خاملة** |

**لم يُحذف:** شجرة المرشّح، فرع المرشّح، نسخة RC الاحتياطيّة، تقرير القبول، السجلّات، الأدلّة خارج Git.

---

## 14. سبب التوقّف

حماية **النافذة البريديّة الطبيعيّة `DailyDue = 16:00`** بتوقيت الرياض على الإنتاج، ومنع خلط مسار الواجهة (Frontend) بمسار نظام البريد. أيّ نشر أو إعادة تشغيل خدمة قرب النافذة قد يُلوِّث قراءة المراقبة أو يُحدث فجوة إرسال، لذا يُجمَّد مسار مدير الحسابات حتّى انتهاء المراقبة.

---

## 15. نقطة الاستئناف

**أوّل خطوة عند العودة:**
`AMR-CLIENT-FIRST-NAVIGATION-AND-SECTION-ORDER-R1 — PRODUCTION FRONTEND-ONLY DEPLOYMENT`

**يجب إعادة إثبات ستّة بنود قبل أيّ نشر:**

1. أنّ الوقت **خارج** نافذة البريد (`16:00` الرياض ± الهامش) وخارج فترة التجميد.
2. أنّ `Candidate SHA = be07a7a7fd30e6210f29354039c952b7c4c4cc58` لم يتغيّر (مع Parent وTree).
3. أنّ خطّ أساس واجهة الإنتاج لم يتغيّر: `index-96kHwdBC.js`، sha256 `f979b8cb…`، `index.html` sha256 `057a8217…`.
4. **إن تغيّر خطّ الأساس** ⇒ إعادة بناء الباتش نفسه فوق الأساس الجديد **واختباره** قبل النشر (لا نشر مباشر).
5. أنّ RC ما زال مستقرًّا (الحزمة، البصمة، `/health`، ثوابت البريد).
6. أنّ البريد والمجدول على الإنتاج غير متأثّرَين (`EmailNotifications__Mode`، `Email__Enabled`، مفاتيح المجدول، `email_outbox`، `mtime` ملفّ البيئة).

**شروط النشر الستّة المُلزِمة** من §24 في تقرير القبول تبقى سارية كما هي.

**لم تُبدأ هذه الخطوة الآن.**

---

## 16. الحالة النهائيّة

```
Local Implementation      : PASS
Candidate Freeze          : PASS
RC Acceptance             : PASS
Production Recommendation : GO
Production Deployment     : ON HOLD
```

**السبب:** حماية النافذة البريديّة الطبيعيّة `16:00` ومنع خلط مسار الواجهة بمسار نظام البريد.

## PAUSED SAFELY — READY FOR PRODUCTION DEPLOYMENT

---

## 17. الانتقال إلى مسار البريد

- كلّ العمل على مسار مدير الحسابات **متوقّف الآن**، ولا يُبدأ أيّ مسار آخر.
- عند `15:45` الرياض يُبدأ **حصرًا** الـprompt المعتمَد لمراقبة نافذة `DailyDue — 16:00 Riyadh`، ونافذة المراقبة `15:50 → 16:30`.
- **ممنوع خلال نافذة البريد:** نشر Frontend، نشر Backend، إعادة تشغيل أيّ خدمة، تنفيذ Recovery، إرسال يدويّ، تعديل المجدول، تعديل Email Mode، أو العودة لمسار مدير الحسابات.

**ممنوع أيضًا دون تصريح جديد ومستقلّ:** `AMR-INPUT-FIELD-GUIDANCE-AND-VALIDATION-R1`، `PROJECT-CROSS-FUNCTIONAL-READ-MODEL-R1`، `EMAIL-MISSED-NOTIFICATIONS-RECOVERY-R1 — PHASE 2`، وبدء `Email Control Center`.

---

## 18. محاولة النشر على الإنتاج — أُوقِفت ببوابة الوقت (29 يوليو 2026، 14:50 الرياض)

صدرت تذكرة `PRODUCTION FRONTEND-ONLY DEPLOYMENT AND FINAL CLOSURE`. **لم يُنفَّذ أيّ نشر** لتحقّق شرط توقّف صريح فيها.

### القياس الزمنيّ عند الاستلام

```
محليًّا : RIYADH = 2026-07-29 14:50:39 +0300   ·   UTC = 2026-07-29T11:50:39Z
الخادم  : RIYADH = 2026-07-29 14:51:12          ·   UTC = 2026-07-29T11:51:12Z   (الساعتان متّسقتان)
```

### شرط التوقّف الذي تحقّق

> «إذا وصل الوقت إلى **14:50** ولم يبدأ استبدال الواجهة: **توقف ولا تنشر**.»

عند لحظة الاستلام كان الوقت **14:50:39** — أي **بعد** البوابة — و**استبدال الواجهة لم يبدأ**، بل لم تبدأ حتّى المرحلة 0 (Preflight). ⇒ الشرط تحقّق حرفيًّا ⇒ **التنفيذ متوقّف، بلا نشر.**

### سبب مساند — الميزانيّة الزمنيّة غير كافية موضوعيًّا

المتبقّي حتّى `15:20` (بوابة اكتمال التحقّق) ≈ **29 دقيقة**، مطلوب خلالها: Preflight + Backup + `npm install` + `tsc -b` + اختبارات + `vite build` + رفع + Staging + استبدال ذرّيّ + تحقّق بصمة ثلاثيّ + Smoke حيّ على الإنتاج (تسجيل دخول + فتح تقرير مدير حسابات حقيقيّ + **21 تأكيدًا**) + Regression (Generic/Moderation/V2 + Mobile + RTL + Print + Console) + تقرير من **20 قسمًا**. الدورة المكافئة على RC استغرقت **ساعات**. ⇒ بدء التنفيذ كان سيقود حتمًا إلى `Rollback` عند `15:20` أو — الأسوأ — إلى واجهة نصف مُتحقَّق منها على الإنتاج عند دخول تجميد `15:30–16:30` ونافذة البريد `16:00`.

### إثباتات قراءة-فقط عند التوقّف (لا كتابة، لا نشر)

| البند | القيمة | الحكم |
|---|---|---|
| حزمة الإنتاج | `index-96kHwdBC.js` (1,324,028) | **لم تتغيّر** |
| sha256 الحزمة | `f979b8cb2692e5687da720c5f9e44ad077358d8eec62cca8d160f581af81e172` | **مطابق للأساس** |
| `index.html` sha256 | `057a82177680b9cfeaf4696fbccdfd9042d1036105c9426a0e652afd028a4ca4` | **مطابق** |
| `index.html` يشير إلى | `index-96kHwdBC.js` | **مطابق** |
| `mtime` الأصول | `Jul 27 23:01` | **لم يتغيّر** |
| الخدمة | `active`، `MainPID=210497`، `NRestarts=0`، بدء `07:25:34 UTC` | **لم تتغيّر** |
| `/health` داخليّ / عام | `200 / 200` | سليم |
| `email_outbox` | `0` | سليم |
| `email_notifications` آخر ساعتين | `1` — نشاط طبيعيّ لوضع `Enabled` على الإنتاج، **لا علاقة له بهذه المهمّة** | مُلاحَظ |
| عمليّات متوازية (rsync/publish/pg_dump/apt) | **لا شيء** | سليم |

**صفر كتابة:** لم يُلمس Backend، ولا الخدمة، ولا قاعدة البيانات، ولا البريد، ولا المجدول، ولا nginx، ولا أيّ ملفّ تحت `/opt/reporting/`.

### الحالة بعد هذه المحاولة — بلا تغيير

```
Local Implementation      : PASS
Candidate Freeze          : PASS   (be07a7a7… ما زال مُجمَّدًا على فرعه)
RC Acceptance             : PASS
Production Recommendation : GO
Production Deployment     : ON HOLD   ← لم يتغيّر
```

### المطلوب للاستئناف

**نافذة زمنيّة جديدة بتصريح صريح**، تُفتح بعد `16:30` بتوقيت الرياض (انتهاء تجميد نافذة البريد) وتتّسع لدورة النشر والتحقّق كاملةً بلا ضغط، مع إعادة إثبات البنود الستّة في §15 أعلاه لحظة التنفيذ.
