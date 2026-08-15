# UAT TEST — Cutover Acceptance Checklist (Phase 3 — تخطيط ومراجعة فقط)

> **حالة الوثيقة:** قائمة قبول تنفيذيّة مرجعيّة. **لا تُنفَّذ الآن.** كل بند يُملأ لحظة التنفيذ الفعليّ بعد موافقة صريحة منفصلة.
> **النطاق:** خدمة TEST فقط · `Development`/`reporting_test_rc` → `Staging`/`reporting_test_uat`.
> **آخر تثبيت حالة (قراءة فقط):** 2026-07-12.

**بيانات التنفيذ (تُملأ لحظة العمل):** المنفّذ: __________ · المالك المعتمِد: __________ · تاريخ/وقت النافذة (UTC): __________ · Release ID: `UAT-CUTOVER-__________`

---

## أ. الشروط المسبقة (Preconditions) — كلها ✅ قبل البدء

| # | البند | متوقّع | فعليّ | ✅/❌ | ملاحظة |
|---|---|---|---|---|---|
| P1 | Hostname | `srv1747233` | | ☐ | |
| P2 | خدمة TEST active، NRestarts=0 | active/0 | | ☐ | |
| P3 | env يشير لـ `reporting_test_rc` | مطابق | | ☐ | |
| P4 | قاعدة `reporting_test_uat` + 30 هجرة | موجودة/30 | | ☐ | |
| P5 | عدّادات UAT (users6/dept2/teams2/clients2/projects3/ws1/deliv2/legacy12/PF1/archived6/outbox0/notif0) | مطابقة | | ☐ | |
| P6 | env جديد **كامل** (لا Delta وحده) يمرّ 06 | جاهز | | ☐ | |
| P7 | مواءمة `Seed__AdminEmail` (R2) محسومة موثّقة | محسوم | | ☐ | |
| P8 | Production + RC active ومنفصلتان | active | | ☐ | |
| P9 | قرار تدوير `Jwt__Key` (R3) موثّق | محسوم | | ☐ | |

---

## ب. Backup النهائي (G1) — موافقة A1

| # | البند | ✅/❌ | ملاحظة |
|---|---|---|---|
| B1 | تشغيل `01-backup-test.sh --apply` بـ Release ID جديد | ☐ | |
| B2 | `db-reporting_test_rc.dump` موجود وحجمه معقول | ☐ | |
| B3 | `backend-publish.tgz` + `frontend-dist.tgz` موجودان | ☐ | |
| B4 | `env-file.bak` (600) + `nginx.conf.bak` | ☐ | |
| B5 | `migration-history.txt` (30 صفًّا) | ☐ | |
| B6 | `backend-assemblies.sha256` + `frontend-bundle.sha256` | ☐ | |
| B7 | `MANIFEST.txt` كامل + تحقّق sha256 | ☐ | |

**Backup verified = GO؟** ☐ نعم ☐ لا — توقيع المالك (A1): __________

---

## ج. تثبيت الحالة قبل التركيب (G0/G2/G3)

| # | البند | متوقّع | فعليّ | ✅/❌ |
|---|---|---|---|---|
| C1 | env-file hash الحالي | `7d412075…f59a90` | | ☐ |
| C2 | runtime hash (Reporting.Api.dll) | `32d2df74…68088e` | | ☐ |
| C3 | frontend bundle hash | `85b58e92…9955ff` | | ☐ |
| C4 | `06 plan` — كل بوّابات Preflight تمرّ | كلها PASS | | ☐ |
| C5 | env الجديد يحوي كل المفاتيح الحالية غير المتغيّرة | مؤكَّد | | ☐ |

---

## د. تنفيذ Cutover (G4) — موافقة A2

| # | البند | ✅/❌ | ملاحظة |
|---|---|---|---|
| D1 | حفظ env السابق `.pre-uat-<STAMP>` (600) موجود | ☐ | STAMP: ______ |
| D2 | تركيب env الجديد ذرّيًّا (`install -m600`) تمّ | ☐ | |
| D3 | `systemctl restart khubara-reporting-test` صدر | ☐ | |

**موافقة تنفيذ Cutover (A2):** توقيع المالك: __________ · وقت: __________

---

## هـ. صحّة الخدمة والتحقق (G5/G6/G7/G8)

| # | البند | متوقّع | فعليّ | ✅/❌ |
|---|---|---|---|---|
| E1 | الخدمة active خلال ≤45s | active | | ☐ |
| E2 | `/health` = ok خلال ≤60s | ok | | ☐ |
| E3 | login Admin | 200 + token | | ☐ |
| E4 | Environment | Staging | | ☐ |
| E5 | Database | reporting_test_uat | | ☐ |
| E6 | migrations | 30 | | ☐ |
| E7 | `GET /api/report-templates` | 200 | | ☐ |
| E8 | SignalR negotiate | 200 | | ☐ |
| E9 | Project-First aggregation (W28) | rowCount=1 | | ☐ |
| E10 | Legacy archived templates | 6 | | ☐ |
| E11 | Rollup (pods) | 200 | | ☐ |
| E12 | `07-health-validation.sh` rc | 0 | | ☐ |

**عند rc=2 (وظيفيّ غير حرج):** قرار المالك (A3): ☐ استمرار ☐ Rollback — توقيع: __________

---

## و. أمان القنوات (G9)

| # | البند | متوقّع | فعليّ | ✅/❌ |
|---|---|---|---|---|
| F1 | `Email__Enabled` | false | | ☐ |
| F2 | `Reminders__Enabled` | false | | ☐ |
| F3 | `EmailNotifications__Mode` | DryRun | | ☐ |
| F4 | `email_outbox` count | 0 | | ☐ |
| F5 | لا نشاط SMTP في logs | لا شيء | | ☐ |

أي نشاط Email/SMTP ⇒ **Rollback فوري**.

---

## ز. عزل Production / RC

| # | البند | ✅/❌ |
|---|---|---|
| Z1 | `reporting-api` (Prod) active دون تغيير | ☐ |
| Z2 | `khubara-reporting-rc` active دون تغيير | ☐ |
| Z3 | لم تُلمس قاعدتا `reporting_prod` / `reporting_rc` | ☐ |
| Z4 | لم يُغيَّر Nginx/DNS/SSL | ☐ |

---

## ح. القبول النهائي (G10) — موافقة A4

- ☐ كل بنود أ–ز = ✅
- ☐ Downtime الفعليّ ضمن 5 دقائق: ______ ثانية
- ☐ لا Blocker مفتوح

**القرار النهائي:** ☐ **قبول (البقاء على UAT)** ☐ **Rollback**
**توقيع المالك (A4):** __________ · وقت: __________

---

## ط. Rollback (إن نُفِّذ)

| # | البند | متوقّع | فعليّ | ✅/❌ |
|---|---|---|---|---|
| T1 | `08-rollback-test.sh --apply` بـ ENV_PREV | تمّ | | ☐ |
| T2 | Environment بعد الرجوع | Development | | ☐ |
| T3 | Database بعد الرجوع | reporting_test_rc | | ☐ |
| T4 | runtime hash | `32d2df74…68088e` | | ☐ |
| T5 | bundle hash | `85b58e92…9955ff` | | ☐ |
| T6 | `/health` = ok + login admin 200 | مطابق | | ☐ |
| T7 | `reporting_test_uat` + `reporting_test_rc` لم تُحذفا/تُعدَّلا | مؤكَّد | | ☐ |

**سبب الرجوع (إن وُجد):** __________________________

---

## ي. المراقبة بعد القبول

| نافذة | البند | ✅/❌ |
|---|---|---|
| 15 دقيقة | service/logs/500/auth/outbox0/notifications | ☐ |
| ساعة | استقرار/استجابة/DB/تسليمات/استثناءات | ☐ |
| يوم UAT | ملاحظات المستخدمين/outbox0/أخطاء متكرّرة | ☐ |

---

## ك. تحديث Phase 3A — حزمة env جاهزة + Backup ما قبل Cutover (2026-07-12، تجهيز فقط)

> قرارات المالك أُغلِقت: env كامل (لا Delta) · Admin موحّد `admin@marketingexperts.local` (ممنوع `admin@test.local`) · عدم تدوير `Jwt__Key` في أول Cutover.

| # | البند | متوقّع | فعليّ | ✅/❌ |
|---|---|---|---|---|
| K1 | حزمة env المستهدفة | `/root/uat-prep-runtime/khubara-reporting-test.uat.env` (600، root:root، خارج Git) | مطابق | ✅ |
| K2 | عدد المفاتيح / المتغيّر | 22 / تغيّر 4 فقط، 18 دون مساس | مطابق | ✅ |
| K3 | سطر `Jwt__Key` | مطابق بايتيًّا (لم يُدوَّر) | sha256 `379cb770…c828d1ad` قبل=بعد | ✅ |
| K4 | مقارنة آمنة (§3) | added0/removed0/changed4/unchanged18 | مطابق | ✅ |
| K5 | بوّابات الإقلاع الساكنة | 11/11 PASS | مطابق | ✅ |
| K6 | اختبار runtime مؤقت (5099، Staging، UAT DB) | health200 + login admin200 + لا admin@test.local + users7 + الحيّ سليم | مطابق | ✅ |
| K7 | Backup النهائي | `UAT-TEST-FINAL-PRECUTOVER-20260712-100755` | موجود | ✅ |
| K8 | سلامة Backup | dump 374 كائن (pg_restore --list OK) · أرشيفات مقروءة · MANIFEST 11/11 · 600 | مطابق | ✅ |
| K9 | Backup السابق محفوظ | `UAT-TEST-PREP-RC4-20260712-074118` لم يُحذَف | مطابق | ✅ |
| K10 | ثبات TEST (Before==After) | كل الـhashes/العدّادات ثابتة + Prod/RC active + UAT DB لم تُمَسّ | مطابق | ✅ |

**Safe to execute cutover now:** ☑ **NO-GO** — بانتظار موافقة مستقلة نهائية.

---

> **تذكير:** هذه القائمة مرجعيّة. **لا تُنفَّذ ولا يُملأ أي بند فعليّ قبل موافقة صريحة منفصلة لكل خطوة كتابة.**
