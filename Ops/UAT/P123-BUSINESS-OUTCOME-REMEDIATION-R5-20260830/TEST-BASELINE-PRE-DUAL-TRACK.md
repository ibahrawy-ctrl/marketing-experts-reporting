# خطّ أساس TEST قبل نشر Dual Track — `d8666f5`

**التاريخ:** 30 أغسطس 2026 · **البيئة:** `test.emarketingacademy.net` (وحدة `khubara-reporting-test`، منفذ `127.0.0.1:5091`، مستخدم `www-data`)
**قاعدة البيانات:** `reporting_test_uat` · **الواجهة:** `/opt/reporting-test/frontend/dist` خلف `auth_basic` (ملفّ `/etc/nginx/.htpasswd-rc-test`، و`/health` مستثنى)

هذا الملفّ يُستوفى به **الشرط 2** من تصريح مالك المنتج («خذ Baseline ونسخة احتياطية لبيانات TEST») و**الشرط 3** («لا ترسل بريدًا حقيقيًّا») قبل أيّ كتابة على البيئة.

---

## 1) الإصدار العامل قبل النشر

| البند | القيمة المقيسة |
|---|---|
| `/health` | `{"status":"ok","service":"reporting-api"}` |
| بصمة الإصدار داخل `Reporting.Api.dll` | `1.0.0+36a6a5b5d8ff285f048c1f4b91c9a1f4db4d7f7f` (= `36a6a5b`، إغلاق R22A) |
| تاريخ ملفّ التجميعة | `Aug 29 19:51` |
| عدد الهجرات المطبَّقة | **46** |
| عدد تقييمات KPI القائمة | **19** |

## 2) النسخ الاحتياطيّة الثلاث (`BACKUP_TS = 20260830T113625Z`)

| النسخة | المسار على الخادم | الحجم |
|---|---|---|
| قاعدة البيانات (`pg_dump -Fc`) | `/opt/reporting-test/backups/reporting_test_uat-preR5DUAL-20260830T113625Z.dump` | 814,331 بايت |
| الخلفيّة المنشورة | `/opt/reporting-test/publish-backup-preR5DUAL-20260830T113625Z` | 111 م.ب |
| حزمة الواجهة | `/opt/reporting-test/frontend/dist-backup-preR5DUAL-20260830T113625Z` | 1.7 م.ب |

## 3) مكابح البريد — **آمنة أصلًا بلا تغيير**

```
Email__Enabled=false
EmailNotifications__Mode=DryRun
```

المصدر الموثوق الوحيد للقناة الجديدة هو `EmailNotifications__Mode`، وقيمته `DryRun` ⟹ **لا بريد حقيقيّ يُرسَل**. لم يُعدَّل أيّ متغيّر بيئة لتحقيق هذا الشرط — كان مستوفًى قبل النشر، وهذا أقوى من تعديله لأنّه لا يُدخِل تغييرًا جانبيًّا على البيئة.

## 4) جرد قوالب KPI **قبل** المصالحة (9 قوالب)

| العنوان | المسار | الحالة | فعّال | المسمّى الوظيفيّ | إصدارات | منشورة | إسنادات |
|---|---|---|---|---|---|---|---|
| مؤشرات مشتري الإعلانات | `Quarterly` | `Published` | نعم | **(عامّ — `JobRoleId` فارغ)** | 1 | 1 | **0** |
| مؤشرات مندوب المبيعات | `Quarterly` | `Published` | نعم | **(عامّ — `JobRoleId` فارغ)** | 1 | 1 | **0** |
| النبض الأسبوعي العام | `WeeklyPulse` | `Published` | نعم | (عامّ) | 1 | 1 | 0 |
| مؤشرات أداء SEO | `WeeklyPulse` | `Published` | نعم | (عامّ) | 1 | 1 | 0 |
| مؤشرات أداء الفيديو | `WeeklyPulse` | `Published` | نعم | (عامّ) | 1 | 1 | 0 |
| مؤشرات أداء المصمم | `WeeklyPulse` | `Published` | نعم | (عامّ) | 1 | 1 | 0 |
| مؤشرات أداء المودريشن | `WeeklyPulse` | `Published` | نعم | (عامّ) | 1 | 1 | 0 |
| مؤشرات أداء كاتب المحتوى | `WeeklyPulse` | `Published` | نعم | (عامّ) | 1 | 1 | 0 |
| مؤشرات مندوب مبيعات B2C | `WeeklyPulse` | `Published` | نعم | (عامّ) | 1 | 1 | 0 |

**المجموع: 9 قوالب · 9 إصدارات · 9 منشورة · 0 إسناد.**

### 4.1 اكتشاف حاكم لتخطيط بند القبول 11

المسمّيان `SALES_B2B` و`MEDIA_BUYER` **غير موجودَين أصلًا** في `job_roles` على TEST. الرموز الموجودة أحد عشر رمزًا من طقم UAT: `ACCOUNT_MGR` · `CEO` · `EMPLOYEE` · `FINANCE_EMPLOYEE` · `FINANCE_MANAGER` · `GENERAL_MANAGER` · `HR_SPECIALIST` · `MANAGER` · `SALES_EMPLOYEE` · `TEAM_LEADER` · `VIEWER`.

هذا **بعينه** أثر الهشاشة الموثَّقة في §6.2 من تقرير الإغلاق: `OrgSeeder.SeedJobRolesAsync` يبدأ بـ`if (await db.JobRoles.AnyAsync()) return;` ⟹ على قاعدة **مأهولة** لا يُنشئ الرموز الناقصة أبدًا، فيبقى القالبان الربعيّان **عامَّين** بلا ارتباط بمسمّى.

**أثره على القياس:** الحالة القائمة على TEST هي **أسوأ حالة** للعيب الذي عولج، لا أضعفها — قالب ربعيّ **عامّ** كان (قبل الإصلاح) يبتلع المسار الأسبوعيّ لكلّ موظّف بلا استثناء، لأنّ المسار الأوّليّ كان يُنتقى **بالنوع** لا بالأخصّية. لذلك يُقاس بند القبول 11 على TEST بموظّف مسارُه الربعيّ الفعّال أحدُ القالبَين المبذورَين، ويُثبَت بقاء مساره الأسبوعيّ كاملًا. ولم تُنشأ رموز مسمّيات جديدة على TEST: إنشاؤها تغيير تنظيميّ خارج نطاق «تسوية القوالب» المصرَّح بها، وغير لازم للإثبات.

## 5) حسابات UAT المتاحة (12 حسابًا بمسمّى)

| الحساب | المسمّى | المدير |
|---|---|---|
| `employee@uat.local` — موظف تنفيذ UAT | `EMPLOYEE` | قائد فريق UAT |
| `r22a.e2e.writer@r22uat.test` — كاتب محتوى R22A | `EMPLOYEE` | قائد فريق UAT |
| `sales.employee@uat.local` — موظف مبيعات UAT | `SALES_EMPLOYEE` | مدير التشغيل UAT |
| `team.leader@uat.local` | `TEAM_LEADER` | مدير التشغيل UAT |
| `ops.manager@uat.local` | `MANAGER` | المدير العام UAT |
| `gm@uat.local` · `ceo@uat.local` · `hr.manager@uat.local` · `finance.manager@uat.local` · `finance.employee@uat.local` · `account.manager@uat.local` · `viewer@uat.local` | — | — |

## 6) ما لم يُمسّ

`khubara-reporting-rc` (RC) و`reporting-api` (الإنتاج) **تعملان ولم تُلمسا**. لا تغيير على أذونات ولا على أعلام `Phase2__*` ولا على أيّ متغيّر بيئة.
