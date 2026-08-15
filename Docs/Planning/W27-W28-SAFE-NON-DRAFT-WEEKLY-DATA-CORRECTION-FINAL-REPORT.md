# W27/W28 SAFE NON-DRAFT WEEKLY DATA CORRECTION — FINAL REPORT

**التاريخ:** 2026-07-15 (دورة W29) — نافذة التنفيذ ~11:16→11:27 UTC
**البيئة:** الإنتاج — `reporting_prod` @ `reports.emarketingacademy.net` (VPS 187.127.72.232)
**نوع العملية:** تصحيح بيانات فقط (PeriodKey) — بلا تغيير سكيمة، بلا هجرة، بلا نشر كود.
**التغيير الوحيد المسموح:** `PeriodKey` من `2026-W27` إلى `2026-W28` لـ17 سجلًّا معتمَدًا فقط (11 تقرير أسبوعي + 6 تقييم KPI أسبوعي، كلها غير Draft وغير محذوفة).

---

## البنود الثمانية عشر

| # | البند | النتيجة |
|---|-------|---------|
| 1 | Phase 0 — Pre-Flight (قراءة فقط) | ✅ GO — رأس الهجرة `20260713171040`، count=27، تصنيف W27/W28 مطابق لتقرير الأثر (الفرق الوحيد سجل يومي جديد واحد في دورة W29 الجارية = نشاط تشغيلي طبيعي خارج النطاق) |
| 2 | Phase 1 — إعادة التحقق من 17 معرّفًا | ✅ Reports approved=11, KPI approved=6, Total=17؛ كل الهدف eligible=true، 0 صف حاجب في W28 |
| 3 | تأكيد غياب المستثناة من قوائم الهدف | ✅ `b127a8f9`,`232f5c72`,`416de42c`,`c85111db`,`10273050`,`bfa49118` خارج مجموعات الهدف |
| 4 | Phase 2 — نسخة كاملة + SHA-256 + pg_restore --list | ✅ dump 684935 bytes، SHA-256 `79b79e7b…5476f8`، pg_restore --list=344 |
| 5 | Phase 2 — نسخ مستهدفة (CSV) + Manifest | ✅ report_submissions=11، submission_field_values=127، approval_steps=15، kpi_evaluations=6، kpi_results=64، review_events=0، audit_logs_related=34 + manifest بالبصمات |
| 6 | Phase 3 — Dry-Run (بلا كتابة، رُوجِع) | ✅ 11+6 مستهدَف، 0 collision/daily/W29/excluded، Draft selected=0، Daily selected=0 → ROLLBACK |
| 7 | Phase 4 — التحديث داخل معاملة واحدة (PeriodKey فقط) | ✅ Reports updated=11، KPI updated=6 بقوائم معرّفات صريحة + شرط أمان `W27∧Weekly∧¬Deleted∧¬Draft` |
| 8 | Phase 5 — التدقيق `weekly_period_corrected` | ✅ 17 صفًّا (11 ReportSubmission + 6 KpiEvaluation)، ActorId=`49607be5…` (bhrawy)، Before=2026-W27, After=2026-W28, Reason عربي + لقطة، بلا إشعارات/بريد |
| 9 | Phase 6 — التأكيدات قبل الالتزام | ✅ كلها نجحت: reports_W28=11, W27=0؛ kpi_W28=6, W27=0؛ immutable_mismatch=0؛ audit_new=17؛ draft_changed=0؛ excl_changed=0؛ daily=29؛ dup_keys=0 |
| 10 | الالتزام (COMMIT) | ✅ COMMITTED |
| 11 | Phase 7 — 11 تقرير الآن W28 بحالاتها الأصلية | ✅ Closed×6 / Submitted×3 / ApprovedByDirectManager×1 / Returned×1، كلها غير محذوفة |
| 12 | Phase 7 — 6 KPI الآن W28 بدرجاتها الأصلية | ✅ 94.25 / 75.00 / 90.85 / 86.25 / 90.40 / 92.85 محفوظة، الحالات Approved/Submitted كما كانت |
| 13 | Phase 7 — المستثناة بلا تغيير | ✅ فاطمة تقرير W27 Submitted + مسودتها W28 Draft؛ أميرة KPI W27 UnderReview + مسودتها W28 Draft؛ محمود القوصي النصّي "من 1 الى 7 يوليو 2026" ApprovedByDirectManager؛ خالد W27 Submitted IsDeleted=true |
| 14 | Phase 7 — كل المسودات بقيت Draft | ✅ W27 KPI 7 draft، W28 KPI 1 draft، W28 report 2 draft — بلا مساس |
| 15 | Phase 7 — اليومية = 29 (before=after) | ✅ 29، updated=0 |
| 16 | Phase 7 — 0 تعارض مفتاح فريد | ✅ report_dup=0، kpi_dup=0 |
| 17 | Phase 8 — صحة النظام + API W28 | ✅ الخدمة active، NRestarts=0، health داخلي=200، عام `/health`=200، الهجرات=27 (بلا تغيير)، لا أخطاء 500/42501/deadlock؛ API: `/api/submissions?period=2026-W28`=200 يحوي الـ11 كلها، `/api/kpi-evaluations?period=2026-W28`=200 |
| 18 | Phase 9 — التنظيف + التقرير | ✅ سكربتات التصحيح المؤقتة أُزيلت (خادم+محلي)؛ النسخ الاحتياطية الأربع محفوظة؛ هذا التقرير |

---

## الحكم النهائي

**W27/W28 SAFE NON-DRAFT WEEKLY DATA CORRECTION — COMPLETED SUCCESSFULLY.**
17 سجلًّا معتمَدًا (11 تقرير + 6 KPI) نُقلت فترتها من 2026-W27 إلى 2026-W28 داخل معاملة واحدة، مع تدقيق كامل (17 صف)، دون تغيير أي محتوى أو حالة أو درجة أو تاريخ أو علاقة، ودون مساس بأي مسودة أو سجل يومي أو محذوف أو أي من السجلات المستثناة (فاطمة/أميرة/محمود القوصي/خالد مجدي). النظام سليم، لا تعارض مفاتيح فريدة، لا أخطاء.

---

## جدول التنظيف الإلزامي (Mandatory Housekeeping)

| العنصر المؤقت | أُنشئ؟ | تم حذفه؟ | سبب الاحتفاظ (إن بقي) |
|---------------|--------|----------|------------------------|
| سكربتات SQL المؤقتة على الخادم (`/tmp/w28-*.sql`, `/tmp/phase*.sql`) | نعم | ✅ نعم | — |
| سكربتات SQL المؤقتة محليًّا (`/tmp/w28-*.sql`) | نعم | ✅ نعم | — |
| توكن الأدمن (break-glass، قراءة فقط) | نعم (بالذاكرة، لم يُطبع) | ✅ نعم (أُفرِغ فور الاستخدام) | — |
| النسخة الكاملة قبل التصحيح `reporting_prod-pre-w27-w28-safe-correction-20260715-111616.dump` (+.sha256) | نعم | ❌ لا | نسخة الرجوع الوحيدة — تُحفَظ حتى انتهاء نافذة المراقبة |
| مجلد النسخ المستهدفة `w28-targeted-20260715-111616/` (CSV + manifest) | نعم | ❌ لا | مرجع الاسترجاع المستهدف — يُحفَظ حتى انتهاء نافذة المراقبة |
| علامة الطابع الزمني `/root/w28-correction-ts.txt` | نعم | ❌ لا | مرجع تشغيلي للطابع الزمني |
| نسخ احتياطية Backend/Frontend | ❌ لا | — | لم يتغيّر أي كود (تصحيح بيانات فقط) |
| مستخدمو/بيانات UAT | ❌ لا | — | لم يُنشأ أي شيء |
| خدمات/معاينات مؤقتة | ❌ لا | — | لم تُشغَّل |
| SQL تشغيلي متبقٍّ على الخادم | ❌ لا | ✅ لا يوجد | التُزِم بالحذف بعد حفظ التقرير |

---

## قائمة STOP (ما لم يُلمَس ولا يُلمَس)

- أي تقرير يومي؛ أي PeriodKey يومي `YYYY-MM-DD`؛ اليومية بقيت 29 بلا تغيير.
- أي Draft (بلا تغيير فترة/حذف/أرشفة/دمج/نسخ محتوى/تغيير حالة/تعديل حقل) — كل المسودات بقيت كما هي.
- أي IsDeleted=true (تقرير خالد مجدي `416de42c`).
- فاطمة محمد (`b127a8f9` + مسودة `232f5c72`)، أميرة محمد (`10273050` + مسودة `bfa49118`) — بلا مساس.
- محمود القوصي النصّي (`c85111db`) — **NO ACTION، قرار منفصل لاحق**.
- W29+، السجلات الصحيحة أصلًا في W28، Navigation، Fatma Direct Routing، Restore/Archive Governance، القوالب/المحتوى، التقارير التشغيلية/التنفيذية، تقرير سماح، Email/Reminders/Scheduler، الوحدات الاستراتيجية.

**التنفيذ اكتمل. التوقف الآن بانتظار أي توجيه إضافي.**
