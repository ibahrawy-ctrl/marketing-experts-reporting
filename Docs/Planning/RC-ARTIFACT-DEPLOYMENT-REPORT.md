# RC-ARTIFACT-DEPLOYMENT-REPORT
## RECONCILE-PROD-DEVELOP-LINEAGE — نشر المصنوعات على RC (Phase T)

**التاريخ:** 16 أغسطس 2026 · **البيئة:** RC · **الإنتاج: لم يُمسّ.**

---

## 1) هويّة الإصدار

| العنصر | القيمة |
|---|---|
| شيفرة التطبيق المنشورة (Application Release SHA) | **`4fddc20`** |
| ما كان منشورًا على RC قبل ذلك | `ce166662f46598ed3593beed0105ba67059fc3bc` (نَسَب الإنتاج) |
| الوسم | `rc-lineage-unified-20260816` |
| قاعدة الواجهة المبنيّة داخل الحزمة | `https://rc-report.emarketingacademy.net/api` |

الوسم على الشجرة التي **بُنِيت ونُشِرت فعلًا**؛ ما بعده على `develop` التزامات توثيق و`Ops`
فقط، بصفر تغيير في شيفرة المنتج.

## 2) بصمات المصنوعات المنشورة (مقروءة من الخادم بعد النشر)

| المصنوع | المسار | البصمة |
|---|---|---|
| ثنائيّ الخادم | `/opt/reporting-rc/publish/Reporting.Api.dll` | `md5 1c2e2fcac282866ec686136aec12a146` |
| حزمة الواجهة | `/opt/reporting-rc/frontend/dist` (7 ملفّات) | `md5 cf0382f175fa0901d686400425178228` |
| حزمة الواجهة المبنيّة محلّيًّا للمقارنة | — | `sha256 64eb45573d31c2d1342750eeeb5724e5…` |

## 3) النسخ الاحتياطيّة المأخوذة قبل الاستبدال

كلّها في `/root/backups/20260816-rc-deploy/`:

- `publish-before.tgz` (46.9MB) · `frontend-dist-before.tgz` (350KB) · `storage-before.tgz`
- `reporting_rc.dump` (نسخة `-Fc` قابلة للاستعادة) · `reporting_rc.sql` · `reporting_rc.schema.sql`
- `rc.env.FULL` (مقيّد 600) · `rc.env.masked` · `khubara-reporting-rc.service` · `nginx-reporting-rc.conf`
- `migrations-before.tsv` · `table-counts-before.txt` · `baseline-counts.env` · `storage-md5-before.txt`
- `CHECKSUMS.sha256` لكلّ ما سبق
- نسخ لاحقة أُنشئت أثناء التنفيذ: `publish-backup-20260816T175606Z/` · `rc.env.FULL.pre-seed` ·
  `rc.env.FULL.pre-unseed` · `nginx-reporting-rc.BEFORE-authfix`

## 4) الإقلاع بعد النشر

| القياس | النتيجة |
|---|---|
| `systemctl is-active khubara-reporting-rc` | `active` |
| `/health` | `200` |
| استثناءات عند الإقلاع | **0** |
| تصادم هجرات | **0** |

**RC Boot = PASS.**

## 5) عيب بيئيّ اكتُشف أثناء النشر — `DEFECT-RC-02` (أُصلِح)

**العَرَض:** كلّ نداءات `/api` عبر المضيف العامّ لـRC تعود `401` بعد تسجيل دخول ناجح،
و`SignalR negotiate` يفشل، والواجهة تدور في `ERR_TOO_MANY_RETRIES`.

**السبب الجذريّ:** إعداد nginx لـRC يطبّق `auth_basic` على مستوى `server` بلا استثناء داخل
`location /api/` و`location /hubs/`. فتتنازع مصادقة nginx الأساسيّة ومصادقة التطبيق
(`Bearer`) على **ترويسة `Authorization` نفسها**، فيبتلع nginx ترويسة التطبيق.

**الإثبات (ثلاثيّ):** الخادم مباشرةً + `Bearer` = `200` · عبر nginx + `Bearer` = `401` ·
عبر nginx + `Basic` = `401`.

**العلاج:** مطابقة نمط TEST — `auth_basic off;` داخل `location /api/` و`location /hubs/` فقط،
بنسخة احتياطيّة قبل التعديل و`nginx -t` قبل إعادة التحميل.

**التحقّق بعد العلاج:** `/api` بلا رمز = `401` (سليم) · الصفحة بلا `Basic` = `401` (سليم) ·
UAT البصريّ: **0 خطأ في الطرفيّة و0 طلب فاشل**.

**أثر الإنتاج: لا شيء.** `grep -c auth_basic /etc/nginx/sites-available/reporting` = **0**؛
الإنتاج لا يستعمل المصادقة الأساسيّة إطلاقًا، فالعيب بيئيّ خاصّ بـRC لا عيب منتج.

## 6) الحكم

**RC Artifact Deployment = PASS · RC Boot = PASS · 1 عيب بيئيّ اكتُشف وأُصلِح ووُثِّق.**
