# CPW-UNIFIED-UAT-R1 — التقرير 1: إغلاق حادثة أسرار UAT

**التذكرة:** `CPW-R2-R3-UNIFIED-UAT-SECURITY-CLOSURE-AND-RC-READINESS`
**المرحلة:** A — UAT Credential Security Closure
**التاريخ:** 16 أغسطس 2026
**البيئة المتأثّرة:** TEST/UAT **حصرًا**

---

## 1) بطاقة الحادثة

```
Incident              = تسريب كلمة مرور حساب UAT إلى مخرجات الجلسة
Exposure Channel      = رسالة خطأ الصدفة عند `source` لملفّ .env بقيم غير مقتبسة
Affected Environment  = TEST/UAT only
Accounts Rotated      = 11 / 11
Old Sessions Revoked  = YES (0 جلسة قديمة نشطة بعد التدوير)
Secret Store Remediated = YES
Git Exposure          = NO
Production Exposure   = NO
Verification          = ROTATION_GATE = PASS (15/15) · ROLE_GATE = PASS (143/143)
```

**التصنيف:** حادثة أمنيّة تشغيليّة — **وليست `BASELINE-DEFECT`** ولا عيب اختبار.

---

## 2) السبب الجذريّ

ملفّ الأسرار `/root/uat-prep-runtime/uat-role-accounts.env` كان يحمل **قيمًا غير مقتبسة** تحتوي رموز صدفة (`&`, `$`, `%`, `)`).

عند تحميله بـ`source` (وهو ما فعله سطر التحميل في سكربت بوّابة الأدوار السابق)، حاولت الصدفة **تفسير** محتوى القيمة بوصفه بناءً لغويًّا، ففشل التحليل وطبعت الصدفة **سطر الخطأ متضمّنًا القيمة السرّيّة نفسها** على `stderr`:

```
uat-role-accounts.env: line 4: syntax error near unexpected token `)'
```

**النقطة الجوهريّة:** التسريب لم يأتِ من طباعة متعمَّدة للسرّ، بل من **رسالة خطأ المفسِّر**. أيّ ضابط يعتمد «لا تطبع السرّ» يفشل هنا، لأنّ المُسرِّب هو الصدفة لا السكربت. ⟹ **العلاج الصحيح هو منع الصدفة من ملامسة محتوى السرّ إطلاقًا، لا تشديد قواعد الطباعة.**

---

## 3) نطاق الانكشاف

| البند | القيمة |
|---|---|
| السرّ الذي ظهر فعليًّا في المخرجات | كلمة مرور `ceo@uat.local` (**لا تُذكر قيمتها في أيّ مكان**) |
| قناة الظهور | `stderr` داخل جلسة العمل |
| هل وصل إلى Git؟ | **لا** — أُثبت بالمسح (القسم 6) |
| هل وصل إلى تقرير أو Screenshot؟ | **لا** |
| هل يمسّ RC أو Production؟ | **لا** — الحسابات `*@uat.local` موجودة على TEST فقط |

**قرار توسيع النطاق:** لعدم إمكان الجزم بحدود ما التُقط من المخرجات، عوملت **كلّ** حسابات UAT الأحد عشر بوصفها منكشفة، لا حساب CEO وحده. هذا اختيار متعمّد لصالح الاحتياط.

---

## 4) التدوير

**الآليّة المستعملة — المسار المعتمد في التطبيق نفسه، لا كتابة مباشرة على القاعدة:**

`POST /api/directory/users/{id}/reset-password` ← `DirectoryService.ResetUserPasswordAsync`

وهو يؤدّي ثلاثة أمور مجتمعة:
1. `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` — إعادة تعيين ذرّيّة عبر Identity مع التحقّق من قوّة كلمة المرور (وتجديد `SecurityStamp`).
2. إبطال **كلّ** رموز التجديد النشطة للمستخدم (`RefreshTokens.RevokedAtUtc`).
3. تسجيل Audit بلا أيّ إشارة إلى كلمة المرور.

**النتيجة — 11/11:**

| المفتاح | الحساب | القديمة بعد التدوير | الجديدة | الحالة |
|---|---|---|---|---|
| CEO | `ceo@uat.local` | 401 | 200 | PASS |
| GM | `gm@uat.local` | 401 | 200 | PASS |
| OPS_MGR | `ops.manager@uat.local` | 401 | 200 | PASS |
| AM | `account.manager@uat.local` | 401 | 200 | PASS |
| TL | `team.leader@uat.local` | 401 | 200 | PASS |
| EMP | `employee@uat.local` | 401 | 200 | PASS |
| HR | `hr.manager@uat.local` | 401 | 200 | PASS |
| FIN_MGR | `finance.manager@uat.local` | 401 | 200 | PASS |
| FIN_EMP | `finance.employee@uat.local` | 401 | 200 | PASS |
| SALES | `sales.employee@uat.local` | 401 | 200 | PASS |
| VIEWER | `viewer@uat.local` | 401 | 200 | PASS |

`ROTATION_GATE = PASS (15/15)` — شاملًا فحوص صلاحيّات المخزن.
**لم تُمَسّ** الأدوار ولا النطاق ولا الملكيّة لأيّ حساب. ولم تُستعمل بيانات أيّ مستخدم حقيقيّ.

### 4.1 حادثة فرعيّة أثناء التدوير — وتصحيحها

اصطدمت الجولة الأولى بحدّ المعدّل على مسارات المصادقة (**30 نداء / 60 ثانية**، `RateLimiting:AuthPermitLimit`)، فأعادت **429** أثناء التحقّق من حسابَي `SALES` و`VIEWER`.

**الخطر الحقيقيّ:** السكربت كان **يحفظ كلمة المرور الجديدة بعد نجاح التحقّق فقط**. ومع فشل التحقّق بسبب 429، كانت كلمة المرور قد **تغيّرت فعليًّا على الخادم دون أن تُحفَظ** ⟹ الحسابان على حافّة فقد وصول دائم.

**التصحيح المطبَّق:**
1. **قلب الترتيب: الحفظ يسبق التحقّق.** بمجرّد نجاح إعادة التعيين تُكتب القيمة في المخزن فورًا، فيستحيل الفقد مهما فشل ما بعده.
2. مباعدة نداءات المصادقة (`sleep`) واحترام نافذة الـ60 ثانية في كلّ أدوات UAT.

**النتيجة:** `SALES` و`VIEWER` → `reset=200 · persisted=YES · verify=200`. **صفر فقد وصول.**

### 4.2 إبطال الجلسات — إثبات دقيق

الفحص الساذج (مقارنة بأحدث وقت تدوير **عموميّ**) أظهر «20 جلسة قديمة نشطة»، وهو **استنتاج خاطئ**: الحسابات التسعة الأولى دُوِّرت قبل حسابَي `SALES`/`VIEWER`، فوقعت جلسات التحقّق المشروعة الخاصّة بها «قبل» أحدث تدوير عموميّ.

**الفحص الصحيح — ربط كلّ مستخدم بوقت تدويره هو:**

```sql
with r as (select "EntityId"::uuid uid, max("CreatedAtUtc") reset_at
           from audit_logs where "Action"='user.password.reset' group by 1)
select u."Email", count(*) from refresh_tokens rt
  join "AspNetUsers" u on u."Id"=rt."UserId" join r on r.uid=rt."UserId"
where rt."RevokedAtUtc" is null and rt."CreatedAtUtc" < r.reset_at group by 1;
```

**النتيجة: `(0 rows)`** ⟹ **صفر جلسة سابقة للتدوير بقيت نشطة.** الرموز النشطة المتبقّية (2 لكلّ حساب) نشأت من نداءات التحقّق التالية للتدوير نفسها.

**درس منهجيّ مسجَّل:** لا يُقاس إبطال الجلسات بمقارنة عموميّة؛ يجب الربط لكلّ مستخدم على حدة، وإلّا وُلِّد إنذار كاذب.

---

## 5) المخزن الآمن الجديد

| البند | القيمة | الحالة |
|---|---|---|
| المسار | `/root/uat-secrets/uat-accounts.json` | — |
| صيغة التخزين | JSON — **بيانات لا كود** | PASS |
| صلاحيّات الملفّ | `600` | PASS |
| صلاحيّات المجلّد | `700` | PASS |
| المالك | `root:root` | PASS |
| موجود في Git؟ | **لا** | PASS |

**المُشغِّل الآمن:** `/root/uat-secrets/with_uat_secrets.py`

```
python3 /root/uat-secrets/with_uat_secrets.py <command> [args...]
```

يقرأ JSON (تحليل بيانات بحت)، يحقن `UAT_PW_<KEY>` في بيئة العمليّة الابنة، ثمّ ينفّذ عبر **`os.execvpe`** مباشرةً.

| الضابط المطلوب | كيف تحقّق |
|---|---|
| لا `source` | لا وجود لأيّ استدعاء صدفة في المسار |
| لا `eval` | المحتوى يُفكَّك بـ`json.load` ولا يُنفَّذ أبدًا |
| لا shell interpolation | `execvpe` يستبدل العمليّة بلا صدفة وسيطة |
| لا command substitution | لا صدفة أصلًا |
| Parsing حرفيّ | `str.partition('=')` للملفّات النصّيّة، و`json` للمخزن |
| لا سرّ في `argv` | الحقن عبر بيئة العمليّة لا سطر الأوامر ⟹ لا يظهر في `ps` |
| لا سرّ في URL | المصادقة عبر جسم `POST` حصرًا |
| لا طباعة للقيمة ولا لطولها | مطبَّق في كلّ الأدوات |

**إثبات عمليّ:** تشغيل المُشغِّل أظهر حقن 12 مفتاحًا وجميعها غير فارغة — **دون طباعة أيّ قيمة**.

---

## 6) إزالة الانكشاف وإثبات عدم الانتشار

### 6.1 مسح انتشار القيم

مسح **14,447 ملفًّا** عبر `/root` و`/opt/reporting-test` و`/etc`، بالبحث عن **القيم نفسها** (لا عن أسمائها) وبإبلاغ العدد دون المحتوى:

| الفحص | قبل المعالجة | بعد المعالجة |
|---|---|---|
| ملفّات تحوي القيم القديمة (المنكشفة) | 3 | **0** |
| ملفّات تحوي القيم الجديدة خارج المخزن | 0 | **0** |

### 6.2 الملفّات المُزالة

أُزيلت بـ`shred -u` بعد ترحيل كلّ المستهلكين:

| الملفّ | المفاتيح |
|---|---|
| `/root/uat-prep-runtime/uat-role-accounts.env` | 11 |
| `/root/uat-prep-runtime/cpwr2-am.env` | 1 |
| `/root/uat-prep-runtime/uat-role-accounts.def01-rotated-20260810T163657Z.env` | 3 |

**حفظ أدلّة التدقيق بلا أسرار:** لكلّ ملفّ مُزال بيانٌ في `/root/uat-secrets/incident-evidence/*.manifest` (وضع `600`) يحوي: المسار، وقت الإزالة، الصلاحيّات والمالك قبلها، **بصمة `sha256`**، و**أسماء المفاتيح فقط**. ⟹ سلسلة التدقيق سليمة والسرّ غير محفوظ.

### 6.3 المستهلكون المُرحَّلون

| الأداة | الإجراء |
|---|---|
| `/root/uni-role-scope.sh` | **حُذفت** — استُبدلت بـ`/root/uat-role-gate.py` (يقرأ المخزن مباشرةً) |
| `/root/am-probe.sh` | **حُذفت** — أُدمجت في بوّابة الأدوار الجديدة |
| سكربتات `*.mjs` لتجهيز UAT | تعمل كما هي عبر المُشغِّل الآمن الذي يوفّر `UAT_PW_*` بيئيًّا (شاملًا `UAT_PW_AM_R2` للتوافق الرجعيّ) |

### 6.4 انكشاف Git

| الفحص | النتيجة |
|---|---|
| أيّ ملفّ أسرار متتبَّع في المستودع | **لا** |
| أيّ قيمة سرّيّة في التقارير أو الوثائق | **لا** |
| `Git Exposure` | **NO** |

---

## 7) التحقّق النهائيّ بعد التدوير

أُعيد تشغيل بوّابة الأدوار كاملةً **بالبيانات الجديدة** عبر الأداة الآمنة:

```
ROLE_GATE = PASS
PASS = 143 · FAIL = 0
matrix -> /root/uat-role-matrix.json
```

وشملت 11 دورًا × (3 فحوص CPW-R2 + 4 فحوص CPW-R3 + 5 فحوص مكافحة تعداد). المورد المجهول أعاد **404 لكلّ دور بلا استثناء**.

---

## 8) ملاحظة أمنيّة أوسع — مسجَّلة ولم يُتصرَّف فيها

كشف تدقيق «القيم غير المقتبسة» أنّ **الخلل نمطيّ لا محصور في ملفّ UAT**:

| الملفّ | البيئة | مفاتيح سرّيّة غير مقتبسة (أسماء فقط) |
|---|---|---|
| `/etc/reporting-api.env` | **Production** | `ConnectionStrings__Default` · `Seed__AdminPassword` · `Email__Password` |
| `/etc/khubara-reporting-rc.env` | **RC** | `ConnectionStrings__Default` |
| `/etc/khubara-reporting-test.env` | TEST | `Seed__AdminPassword` |
| `/root/uat-prep-runtime/khubara-reporting-test.uat.env` | TEST | `Seed__AdminPassword` |
| `/root/uat-prep-runtime/uat-admin.env` | TEST | `UAT_ADMIN_PASSWORD` |
| `/root/rc-r1e-test-accounts.env` | TEST | `PW_A` · `PW_B` |

**الإجمالي: 9 مفاتيح.** جميع الملفّات بصلاحيّات `600` ومملوكة لـ`root` — فلا انكشاف قائم؛ والخطر **مشروط** بأن يقوم أحدهم بـ`source` عليها.

**لم أُجرِ أيّ تعديل على هذه الملفّات، عمدًا:**
1. ملفّا Production وRC **محظور تعديلهما** صراحةً في هذه الحزمة.
2. تعديل `/etc/khubara-reporting-test.env` يستلزم إعادة تشغيل خدمة TEST في منتصف UAT بلا مكسب وظيفيّ — إذ لم تعد أيّ أداة من أدواتي تستعمل `source` إطلاقًا.

**التوصية المرفوعة للمالك:** اقتباس كلّ القيم السرّيّة (`KEY='value'`) في ملفّات RC وProduction ضمن نافذة صيانة مصرَّح بها، ومنع `source` في كلّ أدوات التشغيل. **يُسجَّل كـ`SEC-FINDING-02` — مفتوح، خارج نطاق هذه الحزمة.**

---

## 9) الخلاصة

```
Security Incident Closed        = YES
All UAT Passwords Rotated       = YES (11/11)
Old Sessions Revoked            = YES (0 stale active)
Unsafe Secret Loading Removed   = YES
Old Secret Files Eradicated     = YES (3 shredded, 0 residual across 14,447 files)
New Secrets Outside Store       = 0
Git Exposure                    = NO
Production Exposure             = NO
Access Lost                     = NONE
SEC-FINDING-02 (RC/Prod .env)   = OPEN — يحتاج قرار المالك
```
