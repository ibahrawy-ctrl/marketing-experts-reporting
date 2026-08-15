# EMAIL-DOMAIN-AUTHENTICATION-R1
## DKIM + DMARC PRODUCTION CONFIGURATION + DELIVERY VALIDATION

| البند | القيمة |
|---|---|
| التذكرة | `EMAIL-DOMAIN-AUTHENTICATION-R1` |
| النطاق المستهدف | `marketingexperts.com.sa` |
| الحساب المُرسِل | `info@marketingexperts.com.sa` |
| مزوّد البريد | Google Workspace |
| تاريخ التنفيذ | 2026-07-31 (00:37 → 00:42 UTC / 03:37 → 03:42 الرياض) |
| نوع التنفيذ | **قراءة فقط + تصميم تغيير — لم يُكتب أي سجلّ DNS** |
| القرار النهائي | **PARTIAL — DNS/AUTHENTICATION VERIFICATION PENDING** |
| نقطة التوقّف | `READY FOR DNS CHANGE` |
| التذكرة الأمّ | `EMAIL-DELIVERY-RECONCILIATION-R1` (مغلقة، `PARTIAL — PROVIDER ACCEPTED / INBOX PLACEMENT UNKNOWN`) |

### منهجيّة التنقيح المعتمدة في هذا التقرير
- لا يُطبَع أيّ Password / Gmail App Password / JWT / ConnectionString / Token / Secret.
- لم يُستخدم `cat /etc/reporting-api.env` إطلاقًا؛ استُخرِجت **أسماء مفاتيح غير سرّية محدّدة سلفًا فقط** عبر `grep` مُقيَّد بالمفتاح.
- عناوين البريد تُعرَض مُقنَّعة (`ahm***@gmail.com`) حتى لو كانت منشورة علنًا في DNS.
- قيم DKIM العامّة (عند توليدها لاحقًا) **مفاتيح عامّة وليست أسرارًا**، لكنّها لا تُخترَع هنا ولا تُفترَض.

---

## 1. Production preflight

تنفيذ قرائيّ بالكامل على خادم الإنتاج (`reporting-api.service`) قبل أيّ عمل على DNS.

| # | الفحص | النتيجة | الحكم |
|---|---|---|---|
| 1 | الوقت UTC | `2026-07-31 00:37:56 UTC` | مُثبَت |
| 2 | الوقت الرياض | `2026-07-31 03:37:56 +03` | مُثبَت |
| 3 | حالة الخدمة | `active / running` | PASS |
| 4 | `MainPID` | `258585` | PASS (بلا تغيير) |
| 5 | `NRestarts` | `0` | PASS |
| 6 | بدء العمليّة | `2026-07-29 18:54:53 UTC` | PASS (لم يُعَد التشغيل) |
| 7 | health داخليّ | `200` | PASS |
| 8 | health عامّ | `200` | PASS |
| 9 | `EmailNotifications__Mode` | `Enabled` | PASS |
| 10 | `ReportReminderScheduler__Enabled` | `true` | PASS |
| 11 | `EmailNotifications__RecipientSafetyMode` | `Disabled` | PASS (لا إعادة توجيه) |
| 12 | `Email__FromEmail` | `info@marketingexperts.com.sa` | PASS |
| 13 | `Email__SmtpHost:Port` | `smtp.gmail.com:587` (`UseStartTls=true`) | PASS |
| 14 | `Email__Provider` | `GoogleWorkspace` | PASS |
| 15 | `Email__ReplyTo*` | **0 مفاتيح** | مُثبَت |
| 16 | `Pending / Processing / Failed` | `0 / 0 / 0` | PASS |
| 17 | `email_outbox` | `0` | PASS |
| 18 | إجماليّ `email_notifications` | `224` (Sent=85، DryRun=139) | PASS |
| 19 | آخر `SentAt` | `2026-07-30 18:51:33.218301+00` (دفعة الاسترداد) | مُثبَت |
| 20 | بصمة الصفوف md5 | `a593e2382d77fe9efb9b99ecf12a3136` | مطابقة لإغلاق التذكرة السابقة |
| 21 | عدد الهجرات / الرأس | `30` / `20260724224053_AddReportApproverAndKpiReviewerOverrides` | PASS |
| 22 | أداة الاسترداد قيد التشغيل | `0 عمليّة` | PASS |
| 23 | `fail:` / `crit:` (آخر 24س) | `0 / 0` | PASS |
| 24 | `SmtpCommandException` / `AuthenticationException` (24س) | `0 / 0` | PASS |
| 25 | `Email send failed` (24س) | `0` | PASS |
| 26 | `Email sent to` (24س) | `25` | مُثبَت (إرسال طبيعيّ) |
| 27 | `env` mtime / sha256 | `2026-07-26 19:49:58 UTC` / `f42cb619…f5a61f23` | PASS |

**حكم المرحلة 0: PASS — النظام مستقرّ، صفر أخطاء SMTP، صفر عمليّات معلّقة، وكلّ الثوابت مطابقة لحالة إغلاق `EMAIL-DELIVERY-RECONCILIATION-R1`.**

---

## 2. Current DNS inventory

المصدر: `dig` مقابل **ثلاثة Resolvers عامّة** (`8.8.8.8`, `1.1.1.1`, `9.9.9.9`) **+ خادم الأسماء المرجعيّ** `ns1.marketingexperts.com.sa`. لم يُعتمَد على Cache واحد.

### 2.1 خوادم الأسماء المرجعيّة وموفّر DNS

| السجلّ | القيمة | عنوان IP | PTR |
|---|---|---|---|
| NS 1 | `ns1.marketingexperts.com.sa` | `104.152.171.111` | `ns111d.hostwhitelabel.com` |
| NS 2 | `ns2.marketingexperts.com.sa` | `192.206.54.211` | `ns211d.hostwhitelabel.com` |
| NS 3 | `ns3.marketingexperts.com.sa` | `148.113.134.134` | `ns311d.hostwhitelabel.com` |

**موفّر DNS المُستنتَج بدليل: `hostwhitelabel.com`** — استضافة/DNS بعلامة بيضاء (whitelabel reseller)، وخوادم الأسماء أسماء vanity تحت نطاق العميل لكنّ الـPTR يكشف المالك الحقيقيّ. لوحة التحكّم غالبًا cPanel/WHM لدى الموزِّع.

### 2.2 SOA

```
marketingexperts.com.sa. 3600 IN SOA ns1.marketingexperts.com.sa. ahm***@gmail.com.
                                     2026050600 3600 3600 1209600 86400
```

| الحقل | القيمة | الأثر التشغيليّ |
|---|---|---|
| serial | `2026050600` | آخر تعديل للمنطقة ≈ 2026-05-06 |
| refresh / retry | `3600 / 3600` | — |
| expire | `1209600` | — |
| **minimum (negative-cache TTL)** | **`86400` = 24 ساعة** | **حرج**: الاستعلامات الحاليّة عن DKIM/DMARC تُرجِع `NXDOMAIN`، وهذه النتيجة السالبة تُخزَّن في الـResolvers العامّة **حتى 24 ساعة**. لذلك بعد إضافة السجلَّين قد يستمرّ `8.8.8.8` في إرجاع `NXDOMAIN` ليوم كامل بينما المرجعيّ يُجيب فورًا. **لا يجوز إعادة إنشاء السجلّ بسببها.** |
| جهة الاتصال بالمنطقة | `ahm***@gmail.com` | صاحب الوصول الإداريّ المرجَّح للوحة DNS |

### 2.3 MX

| الأولويّة | الخادم | TTL |
|---|---|---|
| 1 | `ASPMX.L.GOOGLE.COM.` | 3600 |
| 5 | `ALT1.ASPMX.L.GOOGLE.COM.` | 3600 |
| 5 | `ALT2.ASPMX.L.GOOGLE.COM.` | 3600 |
| 10 | `ALT3.ASPMX.L.GOOGLE.COM.` | 3600 |
| 10 | `ALT4.ASPMX.L.GOOGLE.COM.` | 3600 |

مطابق تمامًا على الـResolvers الثلاثة والمرجعيّ. **MX = Google Workspace كامل — PASS.**

### 2.4 TXT (الجذر)

| القيمة | الغرض | TTL |
|---|---|---|
| `v=spf1 a mx ip4:104.152.168.202 include:spf.hostwhitelabel.com include:_spf.google.com -all` | SPF | 3600 |
| `google-site-verification=QsYKF5g…F3cy8UA` | تحقّق ملكيّة Google | 3600 |
| `google-site-verification=nMG5Vju…e3i8Cxo` | تحقّق ملكيّة Google | 3600 |

**سجلّ SPF واحد فقط** — لا ازدواج، لا تعارض.

### 2.5 A / AAAA / CAA وسجلّات البريد الفرعيّة

| الاسم | النوع | القيمة |
|---|---|---|
| `marketingexperts.com.sa` | A | `104.152.168.202` (PTR `drh1.hostwhitelabel.com`) |
| `marketingexperts.com.sa` | AAAA | — (لا يوجد) |
| `marketingexperts.com.sa` | CAA | — (لا يوجد) |
| `mail.marketingexperts.com.sa` | alias → الجذر | `104.152.168.202` |
| `webmail.marketingexperts.com.sa` | A | `104.152.168.202` |
| `smtp.marketingexperts.com.sa` | A | `104.152.168.202` |
| `autodiscover` / `autoconfig` | — | `NXDOMAIN` |

### 2.6 سجلّات المصادقة (الأساس)

| الاسم | النتيجة على `8.8.8.8` | على `1.1.1.1` | على المرجعيّ `ns1` |
|---|---|---|---|
| `_dmarc.marketingexperts.com.sa` | `NXDOMAIN` | `NXDOMAIN` | **`NXDOMAIN`** |
| `google._domainkey.marketingexperts.com.sa` | — | — | **`NXDOMAIN`** |
| `_domainkey.marketingexperts.com.sa` | — | — | `NXDOMAIN` |

**مسح 24 Selector شائعًا مقابل الخادم المرجعيّ — جميعها `NXDOMAIN`:**
`google`, `default`, `selector1`, `selector2`, `s1`, `s2`, `k1`, `k2`, `dkim`, `mail`, `smtp`, `email`, `mandrill`, `zoho`, `everlytickey1`, `sendgrid`, `pm`, `mailjet1`, `mailjet2`, `hs1`, `hs2`, `cm`, `20230601`, `20240101`.

### 2.7 التصنيف المطلوب

| المؤشّر | التصنيف | الدليل |
|---|---|---|
| **SPF** | **PASS** (سجلّ واحد صالح، بلا ازدواج) | نصّ السجلّ + عدّ 1 على ثلاثة Resolvers والمرجعيّ |
| **DKIM** | **NOT FOUND** | 24 selector × `NXDOMAIN` على الخادم المرجعيّ |
| **DMARC** | **NOT FOUND** | `NXDOMAIN` مؤكَّد من المرجعيّ + Resolvers |

### 2.8 تحليل صحّة SPF

```
v=spf1 a mx ip4:104.152.168.202 include:spf.hostwhitelabel.com include:_spf.google.com -all
```

| البند | التقييم |
|---|---|
| عدد سجلّات SPF | 1 (المطلوب: 1) — **صحيح** |
| عدد استعلامات DNS | `a`(1) + `mx`(1) + `include:spf.hostwhitelabel.com`(1، وداخله `+a`+`+mx` = 2) + `include:_spf.google.com`(1) = **6 من 10** — **ضمن حدّ RFC 7208** |
| آليّة النهاية | `-all` (hardfail) — سياسة صارمة وصحيحة |
| تغطية Google | `include:_spf.google.com` ⇒ `74.125.0.0/16`, `209.85.128.0/17`, وستّ نطاقات IPv6 — **تغطّي مسار الإرسال الفعليّ** |
| تغطية الاستضافة | `include:spf.hostwhitelabel.com` ⇒ `+a +mx +ip4:104.152.168.0/22 +ip4:192.199.0.0/24 +ip4:192.206.54.0/23 ~all` |

**الحكم: SPF سليم ولا يحتاج أيّ تعديل ⇒ `KEEP`.**

---

## 3. Sending sources

### 3.1 المصدر الأساسيّ — نظام Khubara Reports عبر Google Workspace (مُثبَت بالمصدر والسجلّ)

الدليل من `reporting-backend/src/Reporting.Infrastructure/Services/MailKitEmailSender.cs`:

| البند | الإثبات | السطر |
|---|---|---|
| `From` | `message.From.Add(new MailboxAddress(_options.FromName, _options.EffectiveFromAddress))` ⇒ `info@marketingexperts.com.sa` | 38 |
| `Envelope From` (MAIL FROM) | **لا يُضبَط `message.Sender` إطلاقًا** ⇒ MailKit يشتقّ مُرسِل الظرف من `From[0]` ⇒ نفس النطاق | 37–41 |
| `Reply-To` | **غير مضبوط** في الكود، ولا يوجد مفتاح `Email__ReplyTo*` في البيئة (0 مفاتيح) | — |
| مسار الإرسال | `ConnectAsync(smtp.gmail.com, 587, StartTls)` ثمّ `AuthenticateAsync(EffectiveUsername, …)` | 45–51 |
| السجلّ التشغيليّ | `Email sent to {ToEmail} via smtp.gmail.com:587` — و74 سطرًا منها طُوبقت 1:1 مع صفوف القاعدة في التذقيق السابق | 57–58 |

**⇒ `From domain` = `Envelope From domain` = `marketingexperts.com.sa` ⇒ SPF alignment مُحقَّق بنيويًّا (`aspf=r` وحتى `aspf=s` سيمرّان).**
**⇒ عند تفعيل DKIM سيوقّع Google بـ`d=marketingexperts.com.sa` ⇒ DKIM alignment مُحقَّق أيضًا.**

### 3.2 مصادر إرسال شرعيّة أخرى مُكتشَفة من DNS (لا تُفترَض غير موجودة)

| # | المصدر | الدليل | حالة DKIM | الأثر تحت DMARC |
|---|---|---|---|---|
| S1 | **Google Workspace** (النظام + صناديق الموظّفين) | MX + `include:_spf.google.com` + مسار الإرسال | سيُفعَّل في هذه التذكرة | يُتوقَّع PASS بعد التفعيل |
| S2 | **خادم الاستضافة cPanel `104.152.168.202`** — نماذج الموقع، `mail()` من PHP، وWebmail | `smtp.` و`webmail.` و`mail.` كلّها تُشير إليه + مُصرَّح له في SPF عبر `a` و`ip4` و`include:spf.hostwhitelabel.com` | **لا DKIM** | SPF قد يمرّ لكن **DKIM سيفشل** ⇒ محاذاة جزئيّة ⇒ **هذا بالضبط سبب إلزاميّة `p=none`** |
| S3 | نطاقات الموزِّع الأوسع `192.199.0.0/24` و`192.206.54.0/23` | داخل `include:spf.hostwhitelabel.com` | لا DKIM | مصرَّح لها في SPF دون أن نعرف مَن يستخدمها فعليًّا — **مخاطرة موثَّقة** |

### 3.3 حكم حصر المصادر

**لم يُتَح حصر كامل ومؤكَّد لمصادر الإرسال الشرعيّة** لسببين موثَّقين:
1. `include:spf.hostwhitelabel.com` يُصرِّح لنطاقات IP عريضة يملكها الموزِّع (`/22` + `/24` + `/23`) ولا يمكن معرفة مَن يرسل منها فعليًّا دون تقارير DMARC.
2. لا يمكن تأكيد عدم وجود CRM/Newsletter/Helpdesk يرسل باسم النطاق دون بيانات aggregate.

**⇒ تطبيقًا لنصّ أمر العمل حرفيًّا: التوقّف عن أيّ تشديد لـDMARC، والاستمرار بـ`p=none` للمراقبة فقط بعد توثيق المخاطر أعلاه.** هذا ليس قيدًا مؤقّتًا بل هو الغرض الأساسيّ من `p=none`: اكتشاف S2/S3 قبل أن يُسبّب تشديد السياسة حجب بريد شرعيّ.

---

## 4. SPF result

| البند | النتيجة |
|---|---|
| التصنيف | **PASS** |
| عدد السجلّات | 1 (لا `MULTIPLE`، لا `NOT FOUND`) |
| الصياغة | صالحة، `-all`، 6/10 استعلامات |
| المحاذاة مع المُرسِل | مُحقَّقة (نفس النطاق في `From` والظرف) |
| الإجراء المطلوب | **`KEEP` — لا تعديل، لا حذف، لا استبدال** |

**تحذير صريح:** أيّ عمليّة على DNS يجب ألّا تلمس هذا السجلّ. إضافة سجلّ SPF ثانٍ تُبطِل SPF بالكامل (`permerror`) وتُسقِط ما هو ناجح الآن.

---

## 5. DKIM baseline

| البند | النتيجة |
|---|---|
| التصنيف | **NOT FOUND** |
| عدد الـSelectors المفحوصة | 24 |
| النتيجة على الخادم المرجعيّ | `NXDOMAIN` لكلّ selector بلا استثناء |
| `_domainkey.marketingexperts.com.sa` | `NXDOMAIN` (الشجرة الفرعيّة غير موجودة أصلًا) |
| الأثر الحاليّ | كلّ رسالة صادرة تصل بلا توقيع مُصادَق على النطاق ⇒ لا يمكن للمستقبِل إثبات عدم العبث ولا نسبة الرسالة للنطاق إلّا عبر SPF وحده |

**هذا هو السبب الجذريّ المرجَّح رقم 1 لشكاوى عدم الوصول، وفق التذقيق الأمّ.**

---

## 6. DMARC baseline

| البند | النتيجة |
|---|---|
| التصنيف | **NOT FOUND** |
| `_dmarc.marketingexperts.com.sa` | `NXDOMAIN` مؤكَّد من `8.8.8.8` و`1.1.1.1` و`ns1` المرجعيّ |
| السياسة الفعّالة | لا شيء — المستقبِل يطبّق اجتهاده الخاصّ |
| التقارير المتاحة | **صفر** — لا رؤية إطلاقًا لمَن يرسل باسم النطاق ولا لنِسَب النجاح/الفشل |
| الأثر | Gmail/Yahoo يشدّدان متطلّبات المُرسِلين منذ 2024؛ غياب DMARC كلّيًّا يرفع احتماليّة التصنيف كـPromotions/Spam خصوصًا مع دفعات مركّزة |

---

## 7. Google Workspace configuration

### 7.1 حالة الوصول — **حاجز التنفيذ**

| المتطلَّب | الحالة | الأثر |
|---|---|---|
| وصول إداريّ إلى `Google Admin Console` (Super Admin) | **غير متاح** في بيئة التنفيذ | لا يمكن توليد مفتاح DKIM ولا قراءته ولا الضغط على `Start authentication` |
| بيانات اعتماد Workspace موثَّقة في المستودع | **غير موجودة** (بحث نصّيّ في كامل المستودع = 0 نتيجة ذات صلة) | — |
| وصول إداريّ إلى لوحة DNS لدى `hostwhitelabel.com` | **غير متاح وغير موثَّق** | لا يمكن كتابة أيّ سجلّ |
| نقل المنطقة AXFR (كبديل قرائيّ شامل) | **مرفوض من الخادم المرجعيّ** (`Transfer failed`) — وهذا سلوك أمنيّ صحيح | استُعيض عنه بجرد لكلّ نوع سجلّ على حدة |

**⇒ تطبيقًا لنصّ المرحلة 5 حرفيًّا: التوقّف عند `READY FOR DNS CHANGE`، وتقديم القيم الدقيقة الواجب إضافتها يدويًّا، دون ادّعاء تنفيذها.**

### 7.2 الإجراء المطلوب من مالك حساب Google Workspace (خطوة بخطوة)

1. الدخول إلى `admin.google.com` بحساب **Super Admin** — دون كتابة كلمة المرور في أيّ سجلّ أو قناة غير آمنة.
2. المسار: **Apps → Google Workspace → Gmail → Authenticate email**.
3. اختيار النطاق: **`marketingexperts.com.sa`**.
4. عند وجود زرّ `GENERATE NEW RECORD`: ضبط الخيارات كالتالي قبل التوليد:
   - **DKIM key bit length = `2048`** (وليس 1024).
   - **Prefix selector**: **إبقاء القيمة التي تقترحها Google كما هي** (الافتراضيّ `google`). **ممنوع اختراع selector يدويًّا.**
5. نسخ ثلاث قيم من الشاشة:
   - `DNS Host name (TXT record name)` — يظهر عادةً كـ `google._domainkey`
   - `TXT record value` — نصّ يبدأ بـ`v=DKIM1; k=rsa; p=…` (مفتاح عامّ طويل، ليس سرًّا، لكن لا يُنشَر في قنوات غير موثوقة)
   - الـSelector المستخدَم
6. **عدم الضغط على `START AUTHENTICATION` في هذه المرحلة** — يُؤجَّل إلى المرحلة 8 بعد إثبات انتشار السجلّ.

**حالة خاصّة:** إن ظهر أنّ DKIM **مولَّد مسبقًا داخل Google Admin لكنّه غير منشور في DNS** (وهو احتمال قائم لأنّ المسح أثبت غيابه من DNS فقط لا من Google) ⇒ **تُستخدم القيمة الحاليّة كما هي بعد التأكّد من أنّها تخصّ النطاق `marketingexperts.com.sa` بالذات لا نطاقًا آخر في الحساب**، ولا يُولَّد مفتاح جديد.

---

## 8. DKIM selector

| البند | القيمة المخطَّطة | ملاحظة |
|---|---|---|
| Selector | `google` (الافتراضيّ من Google) | يُستبدَل حرفيًّا بما تُصدِره Google إن اختلف |
| اسم المضيف الكامل | `google._domainkey.marketingexperts.com.sa` | — |
| اسم المضيف في لوحة DNS | **`google._domainkey`** | انظر التحذير أدناه |
| النوع | `TXT` | — |
| طول المفتاح | `2048-bit` | إلزاميّ |
| TTL المقترح | `3600` | مطابق لبقيّة سجلّات المنطقة |
| القيمة | **`v=DKIM1; k=rsa; p=<PUBLIC_KEY_FROM_GOOGLE>`** | **غير معروفة بعد — تُولَّد من Google Admin حصرًا. لم تُخترَع ولم تُفترَض في هذا التقرير.** |

### 8.1 تحذيرات تنفيذيّة إلزاميّة عند إدخال السجلّ

| # | الخطر | القاعدة |
|---|---|---|
| 1 | ازدواج اسم النطاق | معظم لوحات cPanel/WHM **تُلحِق النطاق تلقائيًّا**. أدخِل `google._domainkey` فقط. إدخال `google._domainkey.marketingexperts.com.sa` سيُنتِج `google._domainkey.marketingexperts.com.sa.marketingexperts.com.sa` ⇒ فشل صامت. |
| 2 | علامات اقتباس زائدة | لا تُضِف `"` يدويًّا إن كانت اللوحة تضيفها. |
| 3 | مسافات/أسطر جديدة | مفتاح DKIM طويل؛ عند النسخ من Google قد تُدرَج أسطر. **يجب أن يكون النصّ سطرًا واحدًا متّصلًا** (تقسيم السلسلة إلى أجزاء `"…" "…"` مقبول تقنيًّا إن فرضته اللوحة، لكنّ الأفضل سطر واحد). |
| 4 | Selector خاطئ | لا تضع المفتاح على `default._domainkey` أو أيّ اسم آخر إن كانت Google تستخدم `google`. |
| 5 | تكرار السجلّ | لا تُنشئ أكثر من TXT واحد بنفس الـHost. |
| 6 | Truncation | تحقّق بعد الإدخال أنّ طول القيمة المُعادة من DNS يطابق الطول الأصليّ. |

---

## 9. DNS backup

**ملفّ النسخة الاحتياطيّة (مُنشأ فعليًّا):**
`Ops/dns/marketingexperts.com.sa-DNS-BACKUP-20260731T004116Z.txt`

| البند | القيمة |
|---|---|
| وقت الالتقاط | `2026-07-31T00:41:16Z` |
| المصدر | الخادم المرجعيّ `ns1.marketingexperts.com.sa` |
| الطريقة | جرد لكلّ نوع سجلّ (AXFR مرفوض — سلوك أمنيّ صحيح) |
| الأنواع المشمولة | SOA, NS, A, AAAA, MX, TXT, CAA + سجلّات البريد الفرعيّة + سجلّات المصادقة (الأساس) |
| المحتوى الحسّاس | **لا يوجد — بيانات DNS عامّة حصرًا** |
| غرض الملفّ | مرجع تراجُع: أيّ اختلاف عن هذه اللقطة بعد التغيير = حادث يُصحَّح بالاستعادة الحرفيّة |

---

## 10. DNS changes

### 10.1 بوّابة ما قبل التعديل (المرحلة 5) — جدول التغييرات المعتمَد

| النوع | Host (كما يُدخَل في اللوحة) | القيمة الحاليّة | القيمة الجديدة (منقّحة) | TTL | الإجراء | السبب |
|---|---|---|---|---|---|---|
| `TXT` | `google._domainkey` | *(غير موجود — NXDOMAIN)* | `v=DKIM1; k=rsa; p=<PUBLIC_KEY_FROM_GOOGLE>` | `3600` | **ADD** | تفعيل توقيع DKIM — المتطلّب الجوهريّ الغائب |
| `TXT` | `_dmarc` | *(غير موجود — NXDOMAIN)* | `v=DMARC1; p=none; pct=100; adkim=r; aspf=r` | `3600` | **ADD** | بدء المراقبة الآمنة بلا أيّ أثر على التسليم |
| `TXT` | `@` (الجذر) | `v=spf1 a mx ip4:104.152.168.202 include:spf.hostwhitelabel.com include:_spf.google.com -all` | *(بلا تغيير)* | `3600` | **KEEP** | SPF سليم ويجتاز الفحص — لا دليل على خطأ |
| `TXT` | `@` (الجذر) | `google-site-verification=…` ×2 | *(بلا تغيير)* | `3600` | **NO CHANGE** | خارج نطاق المهمّة |
| `MX` | `@` | 5 سجلّات Google | *(بلا تغيير)* | `3600` | **KEEP** | البريد الوارد سليم |
| `A` | `@` | `104.152.168.202` | *(بلا تغيير)* | `3600` | **NO CHANGE** | خارج نطاق المهمّة |
| `NS` | `@` | ns1/ns2/ns3 | *(بلا تغيير)* | `3600` | **NO CHANGE** | خارج نطاق المهمّة |

**طبيعة التغيير: `ADD-ONLY` بحتة — سجلّان جديدان على اسمَي مضيف جديدَين، وصفر حذف وصفر استبدال وصفر تعديل.**

### 10.2 حالة التنفيذ

| البند | الحالة |
|---|---|
| سجلّات DNS التي كُتبت | **صفر** |
| سجلّات DNS التي حُذفت | **صفر** |
| سجلّات DNS التي عُدِّلت | **صفر** |
| السبب | لا يوجد وصول إداريّ إلى لوحة DNS لدى `hostwhitelabel.com` |
| نقطة التوقّف | **`READY FOR DNS CHANGE`** |

### 10.3 القيم النهائيّة للإدخال اليدويّ

**السجلّ الأوّل — DKIM**
```
Type  : TXT
Host  : google._domainkey
TTL   : 3600
Value : v=DKIM1; k=rsa; p=<تُنسَخ حرفيًّا من Google Admin Console>
```

**السجلّ الثاني — DMARC (القيمة المعتمَدة لغياب صندوق تقارير مؤكَّد)**
```
Type  : TXT
Host  : _dmarc
TTL   : 3600
Value : v=DMARC1; p=none; pct=100; adkim=r; aspf=r
```

**البديل — يُستخدَم فقط إذا تأكّد وجود صندوق `dmarc@marketingexperts.com.sa` فعليًّا وقادر على الاستقبال:**
```
Value : v=DMARC1; p=none; pct=100; rua=mailto:dmarc@marketingexperts.com.sa; adkim=r; aspf=r
```

### 10.4 تصميم DMARC — تبرير كلّ وسم (المرحلة 4)

| الوسم | القيمة | التبرير |
|---|---|---|
| `v` | `DMARC1` | إلزاميّ |
| `p` | **`none`** | مراقبة فقط. **ممنوع `quarantine`/`reject`** — المصدر S2 (خادم cPanel) بلا DKIM وسيفشل المحاذاة، وتشديد السياسة الآن سيحجب بريدًا شرعيًّا |
| `pct` | `100` | تغطية كاملة للعيّنة المرصودة (بلا أثر تسليميّ لأنّ `p=none`) |
| `adkim` | `r` (relaxed) | يسمح بمحاذاة النطاقات الفرعيّة — الوضع الآمن للبداية |
| `aspf` | `r` (relaxed) | نفس المنطق |
| `rua` | **مؤجَّل** | لا يمكن تأكيد وجود صندوق تقارير دون وصول إلى Workspace Admin؛ وأمر العمل يمنع استخدام صندوق غير موجود |
| `ruf` | **غير مُضاف** | يتطلّب مراجعة خصوصيّة ودعم — خارج R1 |
| `fo` | **غير مُضاف** | يتطلّب تأكيد القدرة على استقبال ومعالجة التقارير |
| `sp` | **غير مُضاف** | لا `sp=reject` إطلاقًا في R1 |

**ملاحظة تشغيليّة مهمّة:** بدون `rua` لن تصل **أيّ** تقارير aggregate، وبالتالي ستكون مراقبة الأيّام السبعة قائمة على فحص الرؤوس وشكاوى الوصول فقط. **التوصية:** إنشاء `dmarc@marketingexperts.com.sa` (صندوق أو مجموعة) ثمّ تحديث السجلّ إلى النسخة الحاملة لـ`rua` — وهو تعديل TXT واحد بلا أيّ أثر على التسليم.

### 10.5 القواعد المانعة أثناء التنفيذ اليدويّ

- ممنوع حذف أو استبدال SPF.
- ممنوع تعديل MX.
- ممنوع أكثر من سجلّ DMARC واحد.
- ممنوع أكثر من سجلّ SPF واحد.
- ممنوع TXT مكرّر بنفس الـHost.
- ممنوع نسخ مفتاح DKIM إلى Host خاطئ.
- ممنوع إلحاق اسم النطاق مرّتين.
- ممنوع حذف أيّ سجلّ دون الرجوع إلى ملفّ النسخة الاحتياطيّة.
- ممنوع تغيير أيّ سجلّ DNS غير متعلّق بالمهمّة.
- **يُسجَّل وقت التعديل الفعليّ وTTL عند التنفيذ.**

---

## 11. Propagation evidence

**الحالة: غير متاحة — لم يُنشَر أيّ سجلّ بعد.**

### 11.1 خطّة التحقّق الواجب تنفيذها بعد الإضافة (بالترتيب الإلزاميّ)

| # | الفحص | الأمر | المعيار |
|---|---|---|---|
| 1 | DKIM على **المرجعيّ أوّلًا** | `dig TXT google._domainkey.marketingexperts.com.sa @ns1.marketingexperts.com.sa` | `NOERROR` + قيمة تبدأ بـ`v=DKIM1` |
| 2 | DMARC على المرجعيّ | `dig TXT _dmarc.marketingexperts.com.sa @ns1.marketingexperts.com.sa` | `NOERROR` + `v=DMARC1; p=none` |
| 3 | تأكيد على ns2 و ns3 | نفس الاستعلامَين مقابل `ns2`/`ns3` | تطابق تامّ (تجنّبًا لعدم تزامن الخوادم) |
| 4 | Resolvers عامّة | نفس الاستعلامَين مقابل `8.8.8.8` و`1.1.1.1` و`9.9.9.9` | تطابق |
| 5 | عدد السجلّات | عدّ نتائج TXT لكلّ اسم | **DMARC = 1 بالضبط**، **DKIM = 1 بالضبط** |
| 6 | SPF لم يتأثّر | `dig TXT marketingexperts.com.sa` | **سجلّ SPF واحد فقط**، بنفس النصّ الحرفيّ في §2.4 |
| 7 | MX لم يتأثّر | `dig MX marketingexperts.com.sa` | 5 سجلّات مطابقة للنسخة الاحتياطيّة |
| 8 | Truncation | مقارنة طول قيمة DKIM بالأصل من Google | تطابق تامّ |
| 9 | صياغة | فحص عدم وجود مسافات/اقتباسات دخيلة | نظيف |

### 11.2 توقّع زمنيّ مبنيّ على دليل

**الخادم المرجعيّ يستجيب فورًا. أمّا الـResolvers العامّة فقد تستمرّ في إرجاع `NXDOMAIN` حتى `86400` ثانية = 24 ساعة** بسبب قيمة `minimum` في الـSOA (تخزين النتائج السالبة — RFC 2308).

**قاعدة إلزاميّة:** إن لم يظهر السجلّ على Resolver عامّ، **انتظر حسب TTL وأعِد الفحص — ولا تُعِد إنشاء السجلّ ولا تُغيّر قيمته عشوائيًّا بسبب تأخّر الـCache.**

---

## 12. Google authentication status

| البند | الحالة |
|---|---|
| `Start authentication` | **لم يُنفَّذ** — الشرط المسبق (انتشار سجلّ DKIM) لم يتحقّق لأنّ السجلّ لم يُنشَر أصلًا |
| حالة المصادقة في Google Admin | **غير معروفة** — لا وصول إلى Console |
| الترتيب الإلزاميّ | نشر TXT ← إثبات الانتشار على المرجعيّ ← **ثمّ** `Start authentication` |

### 12.1 عند رفض Google للسجلّ بعد اكتمال الانتشار — تسلسل التشخيص (بلا توليد مفتاح جديد)

1. تحقّق أنّ الـSelector في DNS **مطابق حرفيًّا** لما يعرضه Google.
2. تحقّق من صياغة الـHost — خصوصًا **ازدواج اسم النطاق**.
3. قارن قيمة TXT المُعادة من `dig` بالقيمة في Console **حرفًا بحرف**.
4. تأكّد من اكتمال الانتشار على **الخوادم الثلاثة** ns1/ns2/ns3 لا واحدًا فقط.
5. تحقّق من عدم وجود اقتباسات أو مسافات دخيلة أو Truncation.
6. **لا يُولَّد مفتاح جديد إلّا بعد استنفاد 1–5 وتحديد السبب.**

---

## 13. Test recipients

| البند | الحالة |
|---|---|
| رسائل الاختبار المُرسَلة | **صفر** |
| السبب | المصادقة غير مفعّلة بعد؛ إرسال اختبار الآن سيقيس الوضع القديم ولا يُثبت شيئًا عن DKIM/DMARC |
| الحدّ الأقصى المصرَّح به لاحقًا | **3 رسائل كحدّ أقصى** |
| الوجهات المخطّطة | (1) صندوق Gmail داخليّ/مملوك للشركة، (2) Outlook/Microsoft 365 إن توفّر، (3) Yahoo إن توفّر |
| ما تمّ التأكيد على منعه | إرسال جماعيّ، إرسال للعملاء، إعادة إرسال الـ85 التاريخيّة، تشغيل الـScheduler يدويًّا، استخدام قوائم الموظّفين |
| توثيق المصدر | يجب تسجيل مصدر كلّ رسالة اختبار (النظام الطبيعيّ أم إرسال إداريّ من Gmail) |

---

## 14. SPF header results

**NOT TESTED** — لم تُرسَل رسائل اختبار.

الأساس المتوقَّع من التحليل البنيويّ: `spf=pass` مع `smtp.mailfrom=marketingexperts.com.sa` ومحاذاة كاملة (§3.1). لكنّ هذا **توقّع مبنيّ على تحليل المصدر، وليس نتيجة رؤوس مرصودة**، ولا يُسجَّل كـPASS.

---

## 15. DKIM header results

**NOT TESTED** — لا يمكن أن يمرّ DKIM قبل نشر السجلّ وتفعيل المصادقة. الحالة الحاليّة المؤكَّدة: **لا توقيع DKIM على أيّ رسالة صادرة.**

---

## 16. DMARC header results

**NOT TESTED** — لا سياسة DMARC منشورة، فلا نتيجة `dmarc=` تُقيَّم لدى المستقبِل.

---

## 17. Inbox/Spam placement

**NOT MEASURED.**

الوضع الموروث من التذقيق الأمّ يبقى ساريًا حرفيًّا: **85 رسالة `Provider Accepted` وموضع الوصول غير معروف (0 مؤكَّد الوصول / 0 مؤكَّد الفشل).**

**تنبيه منهجيّ إلزاميّ:** حتى بعد نجاح `SPF=PASS` و`DKIM=PASS` و`DMARC=PASS`، فإنّ **موضع الوصول (Inbox / Promotions / Spam) يُسجَّل كنتيجة منفصلة ولا يُضمَن بمجرّد نجاح المصادقة.** المصادقة شرط ضروريّ لا كافٍ.

---

## 18. System invariants

| # | الثابت | قبل (00:37 UTC) | بعد (00:42 UTC) | الحكم |
|---|---|---|---|---|
| 1 | `MainPID` | `258585` | `258585` | مطابق |
| 2 | `NRestarts` | `0` | `0` | مطابق |
| 3 | `ActiveState` | `active` | `active` | مطابق |
| 4 | بدء العمليّة | `2026-07-29 18:54:53 UTC` | `2026-07-29 18:54:53 UTC` | مطابق |
| 5 | health داخليّ | `200` | `200` | مطابق |
| 6 | health عامّ | `200` | `200` | مطابق |
| 7 | `env` mtime | `2026-07-26 19:49:58 UTC` | `2026-07-26 19:49:58 UTC` | مطابق |
| 8 | `env` sha256 | `f42cb619…f5a61f23` | `f42cb619…f5a61f23` | مطابق |
| 9 | `EmailNotifications__Mode` | `Enabled` | `Enabled` | مطابق |
| 10 | `RecipientSafetyMode` | `Disabled` | `Disabled` | مطابق |
| 11 | `Scheduler__Enabled` | `true` | `true` | مطابق |
| 12 | `email_notifications` total | `224` | `224` | مطابق |
| 13 | `Sent` | `85` | `85` | مطابق |
| 14 | `Pending/Processing/Failed` | `0/0/0` | `0/0/0` | مطابق |
| 15 | `DryRun` | `139` | `139` | مطابق |
| 16 | `email_outbox` | `0` | `0` | مطابق |
| 17 | آخر `SentAt` | `2026-07-30 18:51:33.218301+00` | `2026-07-30 18:51:33.218301+00` | مطابق |
| 18 | عدد الهجرات | `30` | `30` | مطابق |
| 19 | رأس الهجرات | `20260724224053_…` | `20260724224053_…` | مطابق |
| 20 | **بصمة الصفوف md5** | `a593e2382d77fe9efb9b99ecf12a3136` | `a593e2382d77fe9efb9b99ecf12a3136` | **مطابق** |
| 21 | أداة الاسترداد | `0` عمليّة | `0` عمليّة | مطابق |

**21/21 مطابق.**

### 18.1 ضمانات الصفر

| الضمان | الإثبات |
|---|---|
| صفر كتابة على قاعدة البيانات | البصمة md5 للـ224 صفًّا مطابقة تمامًا قبل/بعد |
| صفر إرسال بريد | `Sent` ثابت عند 85، وآخر `SentAt` لم يتغيّر |
| صفر تشغيل Recovery | `0` عمليّة لأداة الاسترداد في القياسَين |
| صفر تشغيل Scheduler يدويّ | لم يُستدعَ أيّ مسار توليد |
| صفر إعادة تشغيل | `MainPID` و`NRestarts` و`ExecMainStartTimestamp` ثابتة |
| صفر تعديل إعداد | `env` mtime و sha256 متطابقان |
| صفر Migration | العدد `30` والرأس ثابتان |
| صفر نشر Backend/Frontend | لم يُنفَّذ أيّ `publish`/`rsync` |
| صفر تعديل كود | لم يُعدَّل أيّ ملفّ مصدر (قراءة فقط لـ`MailKitEmailSender.cs`) |
| صفر تعديل SMTP credentials | لم تُقرَأ ولم تُطبَع ولم تُغيَّر |
| **صفر تعديل DNS** | لم يُضَف/يُحذَف/يُعدَّل أيّ سجلّ؛ الجرد قرائيّ بـ`dig` حصرًا |
| صفر طباعة أسرار | لم يُستخدَم `cat` على ملفّ البيئة؛ استُخرِجت مفاتيح غير سرّية محدّدة فقط |

---

## 19. Rollback

### 19.1 الوضع الحاليّ

**لا حاجة إلى تراجُع — لم يُنفَّذ أيّ تغيير على أيّ نظام.**

### 19.2 خطّة التراجُع الجاهزة عند تنفيذ تغيير DNS لاحقًا

| السيناريو | الإجراء | الأثر |
|---|---|---|
| فشل/خطأ في DKIM | حذف سجلّ `TXT google._domainkey` | العودة إلى الوضع الحاليّ تمامًا (بلا DKIM) — بلا أيّ أثر على SPF/MX |
| مشكلة في DMARC | حذف سجلّ `TXT _dmarc` | العودة إلى الوضع الحاليّ (بلا سياسة) |
| مسّ غير مقصود بـSPF | استعادة النصّ حرفيًّا من §2.4 وملفّ النسخة الاحتياطيّة | استرجاع فوريّ |
| مسّ غير مقصود بـMX | استعادة السجلّات الخمسة من §2.3 | استرجاع فوريّ |
| مرجع التراجُع | `Ops/dns/marketingexperts.com.sa-DNS-BACKUP-20260731T004116Z.txt` | لقطة مرجعيّة موثَّقة بالوقت |

**خاصّية أمان جوهريّة:** التغيير **إضافيّ بحت (ADD-ONLY)** على اسمَي مضيف **غير موجودَين حاليًّا** ⇒ التراجُع = حذف ما أُضيف فقط، ولا يمكن بنيويًّا أن يُتلِف أيّ سجلّ قائم.

**تحذير زمنيّ:** بعد النشر ستُخزَّن السجلّات في الـCache لمدّة TTL = 3600 ثانية، فأثر التراجُع لن يكون فوريًّا على كلّ الـResolvers.

---

## 20. Monitoring plan

### 20.1 تذكرة المراقبة اللاحقة (تُفتَح بعد اكتمال التفعيل)

**`EMAIL-DOMAIN-AUTHENTICATION-R1 — 7-DAY POST-AUTHENTICATION MONITORING`**

| # | البند | التفصيل |
|---|---|---|
| 1 | المدّة | **7 أيّام** من لحظة ظهور `DKIM=PASS` في رؤوس رسالة حقيقيّة |
| 2 | تجميد السياسة | **ممنوع تشديد DMARC خلال الفترة** — `p=none` يبقى كما هو |
| 3 | مراقبة الإرسال الطبيعيّ | متابعة نوافذ المجدول (09:00 و16:00 بتوقيت الرياض) وتوثيق العدد المتوقَّع مقابل المُرسَل |
| 4 | الارتدادات | مراجعة صندوق `info@marketingexperts.com.sa` لأيّ DSN/Mail Delivery Subsystem — **بلا حذف أو نقل لأيّ رسالة** |
| 5 | الشكاوى | تسجيل كلّ بلاغ «لم تصلني الرسالة» مع الوقت والمستلِم المقنَّع لمطابقته بصفّ `email_notifications` |
| 6 | تقارير aggregate | مراجعتها **فقط إن فُعِّل `rua`** — وإلّا يُسجَّل «غير متاح» صراحةً |
| 7 | عيّنة رؤوس | فحص رؤوس رسالة حقيقيّة واحدة على الأقلّ أسبوعيًّا لتأكيد استمرار `SPF/DKIM/DMARC = PASS` |
| 8 | موضع الوصول | تسجيل Inbox/Promotions/Spam كمؤشّر منفصل عن المصادقة |
| 9 | المصادر غير الموقَّعة | رصد S2/S3 (§3.2) — أيّ إرسال شرعيّ منها يحتاج قرارًا مستقلًّا قبل أيّ تشديد |
| 10 | شرط الانتقال | **ممنوع الانتقال إلى `quarantine` أو `reject` دون تقرير مستقلّ** يُثبت أنّ كلّ المصادر الشرعيّة تجتاز المحاذاة |

### 20.2 الفجوة القائمة من التذقيق الأمّ (تبقى مفتوحة)

غياب Message-ID وردّ المزوّد ومطابقة الارتدادات في النظام يبقى **فجوة رصد بنيويّة** لا تُعالَج في هذه التذكرة (تذكرة `EMAIL-DELIVERY-OBSERVABILITY-R1` المؤجَّلة). لذلك ستظلّ المراقبة معتمدة على فحص الرؤوس والشكاوى اليدويّة.

---

## 21. Final decision

### 21.1 ملخّص تنفيذيّ

1. **البوّابة التشغيليّة PASS** — 27 فحصًا، النظام مستقرّ وصفر أخطاء SMTP وصفر عمليّات معلّقة.
2. **جرد DNS مكتمل ومؤكَّد من ثلاثة Resolvers + الخادم المرجعيّ** — لا اعتماد على Cache واحد.
3. **SPF = PASS** — سجلّ واحد صالح، 6/10 استعلامات، `-all`، ومحاذاة كاملة ⇒ `KEEP` بلا مساس.
4. **MX = PASS** — Google Workspace كامل ⇒ `KEEP`.
5. **DKIM = NOT FOUND** — 24 selector مفحوصًا، جميعها `NXDOMAIN` على الخادم المرجعيّ.
6. **DMARC = NOT FOUND** — `NXDOMAIN` مؤكَّد مرجعيًّا.
7. **مصدر الإرسال مُثبَت بالمصدر**: `From` و`Envelope From` كلاهما `marketingexperts.com.sa` عبر `smtp.gmail.com:587` ⇒ المحاذاة مضمونة بنيويًّا فور تفعيل DKIM.
8. **اكتُشِفت مصادر إرسال شرعيّة أخرى** (خادم cPanel `104.152.168.202` ونطاقات الموزِّع العريضة) بلا DKIM ⇒ **تأكيد قاطع على إلزاميّة `p=none`** ومنع أيّ تشديد.
9. **موفّر DNS مُعرَّف بدليل** (`hostwhitelabel.com`) لكنّ **الوصول الإداريّ غير متاح**، و**Google Admin Console غير متاح** ⇒ يستحيل توليد مفتاح DKIM أو كتابة أيّ سجلّ.
10. **قيمة DKIM لم تُخترَع** — سُجِّلت كـ`<PUBLIC_KEY_FROM_GOOGLE>` مع الإجراء الدقيق لاستخراجها.
11. **اكتُشِف عامل زمنيّ حرج**: negative-cache TTL = **24 ساعة** ⇒ تأخّر ظهور السجلّات على الـResolvers العامّة أمر متوقَّع ولا يستدعي إعادة إنشاء السجلّ.
12. **نسخة احتياطيّة موثَّقة لـDNS أُنشئت فعليًّا** قبل أيّ تغيير مقترح.
13. **صفر تغيير على أيّ نظام** — 21/21 ثابتًا مطابقًا وبصمة الصفوف لم تتحرّك.

### 21.2 سبب القرار `PARTIAL`

| معيار PASS | الحالة |
|---|---|
| SPF = PASS | مُحقَّق (على مستوى السجلّ) |
| DKIM = PASS | **غير مُحقَّق** — السجلّ غير منشور |
| DMARC = PASS | **غير مُحقَّق** — السجلّ غير منشور |
| Google Workspace authentication `Active` | **غير مُحقَّق** — لا وصول إلى Console |
| لا ازدواج DNS | مُحقَّق (لا شيء أُضيف) |
| لا رفض SMTP | مُحقَّق |
| رسائل اختبار محدودة | لم تُرسَل (0 من 3) |
| النظام لم يتأثّر | مُحقَّق 21/21 |

**السبب المباشر والوحيد للحالة `PARTIAL`: تعذّر الوصول الإداريّ إلى Google Admin Console وإلى لوحة DNS لدى `hostwhitelabel.com`** — وهو بالضبط شرط التوقّف المنصوص عليه في المرحلة 5 وفي تعريف `PARTIAL` في المرحلة 12.

**هذه ليست حالة `FAIL`:** لا يوجد خطأ صياغة، ولا سجلّات مكرّرة، ولم يُرفَض DKIM من Google، ولم يتأثّر SPF أو MX. لا شيء انكسر — العمل جاهز للتنفيذ فقط.

### 21.3 الخطوات المتبقّية لبلوغ `PASS`

| # | الخطوة | المسؤول | مرجع هذا التقرير |
|---|---|---|---|
| 1 | توليد DKIM 2048-bit من Google Admin واستخراج Host/Value/Selector | مالك Google Workspace (Super Admin) | §7.2 |
| 2 | إضافة سجلَّي TXT في لوحة `hostwhitelabel.com` | مالك الوصول إلى DNS (`ahm***@gmail.com` حسب SOA) | §10.3 + تحذيرات §8.1 |
| 3 | إثبات الانتشار على المرجعيّ ثمّ العامّ | — | §11.1 |
| 4 | الضغط على `Start authentication` بعد الانتشار حصرًا | مالك Google Workspace | §12 |
| 5 | إرسال ≤3 رسائل اختبار وفحص الرؤوس | — | §13 |
| 6 | تسجيل موضع الوصول منفصلًا عن المصادقة | — | §17 |
| 7 | بدء مراقبة السبعة أيّام | — | §20.1 |

### 21.4 الحالة النهائيّة

```
EMAIL-DOMAIN-AUTHENTICATION-R1
DKIM + DMARC PRODUCTION CONFIGURATION + DELIVERY VALIDATION

PREFLIGHT      : PASS — 27/27 CHECKS
DNS INVENTORY  : COMPLETE — AUTHORITATIVE + 3 PUBLIC RESOLVERS
SPF            : PASS — SINGLE VALID RECORD (6/10 LOOKUPS, -all) — KEEP
MX             : PASS — GOOGLE WORKSPACE — KEEP
DKIM           : NOT FOUND — 24 SELECTORS SCANNED, ALL NXDOMAIN
DMARC          : NOT FOUND — NXDOMAIN CONFIRMED AUTHORITATIVELY
SENDING SOURCE : PROVEN — FROM = ENVELOPE-FROM = marketingexperts.com.sa VIA smtp.gmail.com:587
OTHER SOURCES  : 2 UNSIGNED LEGITIMATE SOURCES FOUND — p=none MANDATORY
DNS PROVIDER   : IDENTIFIED (hostwhitelabel.com) — ADMIN ACCESS NOT AVAILABLE
GOOGLE ADMIN   : ACCESS NOT AVAILABLE — DKIM KEY NOT GENERATED
DNS BACKUP     : CAPTURED 2026-07-31T00:41:16Z
DNS CHANGES    : 0 ADDED / 0 MODIFIED / 0 DELETED
TEST MESSAGES  : 0 SENT (LIMIT 3, DEFERRED)
NEGATIVE TTL   : 86400s (24h) — PROPAGATION DELAY EXPECTED, DO NOT RE-CREATE
INVARIANTS     : 21/21 IDENTICAL — ROW FINGERPRINT UNCHANGED
ZERO WRITES / ZERO SENDS / ZERO RESTART / ZERO CONFIG CHANGE / ZERO DEPLOY / ZERO DNS CHANGE

READY FOR DNS CHANGE

PARTIAL — DNS/AUTHENTICATION VERIFICATION PENDING
```

### 21.5 شرط التوقّف

توقّف التنفيذ عند هذه النقطة. **لم يُبدَأ ولن يُبدَأ دون قرار مستقلّ:** أيّ `p=quarantine` أو `p=reject`، أيّ إرسال جماعيّ أو إعادة إرسال للرسائل التاريخيّة، تذكرة `EMAIL-DELIVERY-OBSERVABILITY-R1`، أيّ تعديل كود، أيّ Migration، أو أيّ تذكرة أخرى.
