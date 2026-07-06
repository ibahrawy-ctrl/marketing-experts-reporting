# Release Notes

> ملاحظة: التوثيق الداخلي التفصيلي موجود في `Docs/` (مُستبعد من Git بسياسة المشروع). هذا الملف هو سجلّ الإصدارات الرسمي المتتبَّع في Git.

---

## RC-2 — Release Candidate 2

| الحقل | القيمة |
|---|---|
| **Status** | Approved on TEST |
| **Environment** | test.emarketingacademy.net |
| **Date** | 2026-07-06 |
| **Baseline** | ✅ Official Baseline — أي Feature جديدة بعد هذا الـ Tag تُعتبر ضمن **RC-3 Development**. لا يُعدَّل RC-2 بعد الآن إلا في حالة Bug حرج. |
| **Production** | لم يُمَسّ (لا نشر إنتاجي في هذا الإصدار). |

### Sales
- B2C New Leads / Old CRM — قالب تقرير B2C بجدولين مستقلّين (كلاهما يتوقّف عند Revenue، بلا Lost/Lost Reason).
- Course Catalog — كتالوج الدورات.
- Course Management — إدارة الدورات.
- Course Selector — منتقي الدورة (Select من الكتالوج).
- Course Aggregation — تجميع حسب الدورة.
- Drill Down — تفاصيل الموظفين أسفل كل دورة (Total / New Leads / Old CRM Data، الفارغ يظهر «—»).
- Employee Aggregation — تجميع على مستوى الموظف.
- New / Old Filters — فلاتر New/Old في عرض التفاصيل حسب الموظف (كل بطاقات KPI والرسوم والجداول تتغيّر).

### Dashboards
- Executive Aggregation APIs — نقاط تجميع لوحة القيادة التنفيذية.
- B2B Dashboard — لوحة B2B.
- B2C Dashboard — لوحة B2C.

### Security
- Basic Auth — مفعّل على بيئة TEST (الجذر بلا مصادقة ⇒ 401).
- Email Disabled — `Email__Enabled=false`.
- Dry Run — `EmailNotifications__Mode=DryRun`، `Reminders__Enabled=false`، `email_outbox`=0.
- JWT — كل نقاط `/api` تتطلّب توكن (بلا توكن ⇒ 401).
- noindex — `X-Robots-Tag: noindex, nofollow` حاضر (منع الأرشفة).

### Infrastructure
- Smoke Tests Passed — كل فحوصات ما بعد النشر ناجحة.
- UAT Passed — اجتياز الاختبارات اليدوية على بيئة TEST.
- Rollback Documented — إجراءات الرجوع موثّقة (backend/frontend/nginx).
- Backups Created — نسخ احتياطية للـ publish/dist ولإعداد nginx قبل كل تغيير.

### مراجع التوثيق الداخلي (Docs/ — غير متتبَّع في Git)
- `Docs/Phase-7.1-Sales-Reporting-Fix-Pack-TEST-Deployment-Report.md`
- `Docs/RC-Test-Deployment-test.emarketingacademy.net-Report.md`
