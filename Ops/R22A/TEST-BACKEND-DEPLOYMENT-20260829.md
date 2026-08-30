# R22A — نشر الخلفيّة على TEST وحده (29 أغسطس 2026)

**النتيجة: `TEST_BACKEND_DEPLOYMENT_PASS`** — بلا تراجع، بلا هجرات جديدة، بلا أخطاء.

الطابع الزمنيّ الموحّد للعمليّة: `20260829T195019Z`.
**لم تُمسّ RC ولا Production ولا `origin/main`، ولم يُدفَع شيء إلى `origin/develop`.**

---

## 1) المرشَّح المنشور

| البند | القيمة |
|---|---|
| الالتزام | `36a6a5b5d8ff285f048c1f4b91c9a1f4db4d7f7f` (محلّيّ على `develop`، **غير مدفوع**) |
| سلفه | `16983d2d4f5a3116aeea630af5b7c5fbcf21ce10` |
| نطاق التغيير | ملفّان فقط: `ReportTemplateService.cs` (+9/−3) و`TemplateVersionManagementTests.cs` (+80) |
| هجرات جديدة | **صفر** — التغيير منطقيّ بحت لا يمسّ المخطّط |

## 2) فحوص ما قبل النشر — عزل TEST

| الفحص | القيمة المقيسة | الحكم |
|---|---|---|
| قاعدة البيانات | `reporting_test_uat` | معزولة (الإنتاج `reporting`، RC `reporting_rc`) |
| العنوان | `http://127.0.0.1:5091` | معزول (الإنتاج 5090، RC 5092) |
| البيئة | `ASPNETCORE_ENVIRONMENT=Staging` | ليست Production |
| المستخدم | `www-data` | مطابق للسياسة |
| ملفّ البيئة | `/etc/khubara-reporting-test.env` | خاصّ بـTEST وحده |
| `EmailNotifications__Mode` | `DryRun` | **البريد مكبوح** — مصدر الحقيقة الوحيد |
| `Email__Enabled` | `false` | مكبح ثانٍ مستقلّ |

### المجدولات (خدمات خلفيّة تعمل بلا علم تشغيل/إيقاف)
أربع خدمات مسجَّلة في `DependencyInjection.cs:83,102-104`: `AttendanceSlaSweepService`،
`EmailOutboxDispatcher`، `SubmissionReminderService`، `ReportReminderSchedulerService`.

**قناتها الخارجيّة الوحيدة هي البريد، وهي مكبوحة عند المصدر:**
`EmailNotificationService.cs:612-616` — في وضع `DryRun` يُكتَب الصفّ بحالة `DryRun` ولا يُرسَل شيء،
ومفتاح الترابط يُسبَق ببادئة `DryRun` (`:565-567`) فلا يحجب أيّ إرسال حقيقيّ لاحق.
أمّا كتاباتها فمحصورة كلّها داخل `reporting_test_uat` المعزولة.
⇒ **لا أثر خارجيّ ممكن من أيّ مجدول.**

## 3) النسخ الاحتياطيّ الثلاثيّ (قبل أيّ لمس)

| # | المسار | الحجم | التحقّق |
|---|---|---|---|
| 1 | `/opt/reporting-test/publish-backup-r22a-20260829T195019Z` | 111M | 86 ملفًّا · بصمة كلّ الـDLLs `9b19f7b1e2a43f6ababe45b6a75971adc3fbb379824c10b1e15715494685c8ab` |
| 2 | `/opt/reporting-test/frontend/dist-backup-r22a-20260829T195019Z` | 1.7M | 7 ملفّات |
| 3 | `/root/db-backups/reporting_test_uat-r22a-20260829T195019Z.dump` | 792K | `sha256=130ff5ca1e925c024dcb3d05974f80fdee30ec710f48a6a95dda702b2d68a567` · `pg_restore -l`: **506 مُدخَلًا، 84 منها `TABLE DATA`** ⇒ التفريغ سليم وقابل للاستعادة |

## 4) البناء من نسخة معزولة نظيفة

```
/tmp/release-r22a-20260829T195019Z   ←  git archive 36a6a5b  (1641 ملفًّا)
```
- **ليست شجرة التطوير** — أُنشئت من `git archive` للالتزام، فلا تحمل أيّ ملفّ غير متتبَّع.
- **`bin`/`obj` = 0 مجلّد** داخلها قبل البناء (تحقُّق مقيس، لا مجرّد `rm -rf`).
- الأمر:
```
dotnet publish src/Reporting.Api/Reporting.Api.csproj -c Release \
  -o /tmp/release-r22a-20260829T195019Z/publish \
  -p:SourceRevisionId=36a6a5b5d8ff285f048c1f4b91c9a1f4db4d7f7f \
  -p:ContinuousIntegrationBuild=true
```
- النتيجة: `PUBLISH_EXIT=0` · **0 خطأ**.

## 5) إثبات هويّة الحزمة (Artifact Identity + SourceLink)

السلاسل داخل الـDLLs مُرمَّزة UTF-16LE ⇒ `strings` بالـASCII لا يجدها؛ استُخرِجت بفكّ ترميز صريح.

| الدليل | القيمة |
|---|---|
| `InformationalVersion` في `Reporting.Api.dll` | **`1.0.0+36a6a5b5d8ff285f048c1f4b91c9a1f4db4d7f7f`** |
| `SourceRevisionId` داخل `Reporting.Api.dll` | مطابقة واحدة للـSHA |
| `SourceRevisionId` داخل `Reporting.Infrastructure.dll` | مطابقة واحدة للـSHA |
| رسالة الحارس `«يوجد إصدار مسودة مفتوح بالفعل.»` | موجودة داخل بايتات `Reporting.Infrastructure.dll` |
| بصمة كلّ الـDLLs محلّيًّا قبل النقل | `fae866cfebfaa97f29fb740b0cbe140e6f87e9b6416046c782bde97891079eb1` |
| **البصمة نفسها على الخادم بعد النقل** | `fae866cfebfaa97f29fb740b0cbe140e6f87e9b6416046c782bde97891079eb1` ✔ **تطابق تامّ** |

## 6) النشر والتحقّق

`rsync -az --delete` → `chown -R www-data:www-data` → `systemctl restart khubara-reporting-test`.

| القياس | قبل | بعد |
|---|---|---|
| `/health` | 200 | **200** |
| الإصدار الحيّ | `1.0.0+f8c4ad298a06e13e2f8c793110f17aef0822910a` | **`1.0.0+36a6a5b5…`** |
| عدد الهجرات | 46 | **46** (بلا تغيير — كما هو متوقَّع) |
| عدد ملفّات `publish` | 86 | 86 |
| مالك الملفّات | — | `www-data:www-data` |
| `NRestarts` | — | **0** (لا حلقة إقلاع) |
| سطور بمستوى `err` | — | **0** |

> ملاحظة: أوّل استطلاع لـ`/health` بعد 6 ثوانٍ أعاد `000` لأنّ التطبيق كان لا يزال يُقلِع؛
> الاستطلاع التالي أعاد `200` والخدمة `active running` بـ`NRestarts=0`. لا علاقة له بالحزمة.
>
> تحذير `No XML encryptor configured` لـDataProtection سابق الوجود على TEST وغير مرتبط بهذا التغيير.

## 7) خطة التراجع (مقيسة، ≤ دقيقتين)

التغيير **منطقيّ بحت بلا هجرة**، فالتراجع يقتصر على استبدال البايتات:

```bash
# 1) استعادة الحزمة السابقة بالكامل
rsync -a --delete /opt/reporting-test/publish-backup-r22a-20260829T195019Z/ \
                  /opt/reporting-test/publish/
chown -R www-data:www-data /opt/reporting-test/publish
systemctl restart khubara-reporting-test

# 2) التحقّق من نجاح التراجع
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5091/health          # متوقَّع 200
cd /opt/reporting-test/publish && find . -type f -name '*.dll' | sort | \
  xargs sha256sum | sha256sum                                                   # متوقَّع 9b19f7b1e2a4...
# متوقَّع أن يعود الإصدار إلى 1.0.0+f8c4ad298a06e13e2f8c793110f17aef0822910a
```

**قاعدة البيانات لا تحتاج تراجعًا** (46 هجرة قبلًا وبعدًا، ولا كتابة تمّت بعد النشر).
وإن لزم لأيّ سبب لاحق:
`pg_restore -c -d reporting_test_uat /root/db-backups/reporting_test_uat-r22a-20260829T195019Z.dump`
— **يتطلّب تصريحًا صريحًا جديدًا** لأنّه كتابة على قاعدة حيّة.

الواجهة لم تُلمَس أصلًا؛ نسختها الاحتياطيّة محفوظة احترازًا في
`/opt/reporting-test/frontend/dist-backup-r22a-20260829T195019Z`.

## 8) ما لم يُمسّ

- RC (`khubara-reporting-rc` / `reporting_rc`) — لم يُقرأ ولم يُكتَب.
- Production (`reports.emarketingacademy.net`) — لم يُقرأ ولم يُكتَب.
- `origin/main` و`origin/develop` — لا دفع، لا وسم، لا دمج.
- واجهة TEST (`/opt/reporting-test/frontend/dist`) — بايتاتها كما هي.
- بيانات TEST — لا كتابة حتّى هذه اللحظة؛ مسودات القالب `v1`/`v2`/`v3` لم تُحذف ولم تُنشَر ولم تتغيّر حالتها.
