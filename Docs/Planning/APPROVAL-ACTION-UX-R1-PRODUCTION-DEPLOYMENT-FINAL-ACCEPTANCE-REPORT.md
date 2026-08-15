# APPROVAL ACTION UX R1 — Frontend-Only Production Deployment — Final Acceptance Report

**التاريخ:** 2026-07-16 · **البيئة:** Production (`reports.emarketingacademy.net`) · **نوع النشر:** Frontend-only (بلا Backend / بلا Migration / بلا كتابة بيانات)

## الهوية (Candidate Identity)
- **Source Commit:** `92b8c01341522e64a1ebfe7328da6dc842ee7688`
- **Parent:** `50145fdfb2fee59a09c128a8f20aafabfb3261ec`
- **Branch:** `approval-action-ux-r1`
- **Frontend delta vs Parent:** 10 ملفات UX فقط (ActionResultToast.tsx, ui.tsx, lib/api.ts, main.tsx, HrRequestsPage, KpiPage, LeaveRequestsPage, SubmissionsPage + اختبارات) — 580+ / 112-.
- **Backend delta vs Parent:** فارغ (frontend-only مؤكَّد).
- **Build:** `tsc -b = 0` · `vite build = PASS` · **Vitest = 161/161 PASS** (21 ملف اختبار).
- **الحزمة:** `/api` base · لا source maps · لا localhost/127.0.0.1/prod-host/RC · secret scan = 0.
- **العلامات:** Toast نجاح/403/409 · تأخير 700ms · Spinner(animate-spin) · isPending · me_nav_collapsed_v2 · my-cycles · my-days · admin-delete · review-events — كلها حاضرة.

## الحزمة الجديدة (Deployed)
| ملف | الاسم | SHA-256 |
|---|---|---|
| index.html | — | `eb3cd88792bd16267ee9db35c449f6bb66d84dd9846a300c09892927bf0e7644` |
| JS | `assets/index-Ce0oKQzH.js` | `033803f0f5c01ab23965aa456eaf22722187d4554d899e245c49430df9e4dff8` |
| CSS | `assets/index-CS8WqKYP.css` | `44f4b706c23df674065d564a273513c1d3c8cac12bafadac0c266ab424fa2a9d` |

## الحزمة السابقة (Previous)
| ملف | الاسم | SHA-256 |
|---|---|---|
| index.html | — | `f4035e9850d02a4e59e05d60ae404e888e7cfd1da889236fd822ac5e92acbc38` |
| JS | `assets/index-B-y7LHB8.js` | `9363e56dc9f3c14f1abe5227029260e30d82d06d08e2b39cce11ab8255541841` |
| CSS | `assets/index-C8CkvKW-.css` | `c412b1f58202d2b18f9f2ebaaf2057b2244b902a4fdef9ef06128c0ee0127bca` |

## النسخة الاحتياطية و Rollback
- **Backup:** `/opt/reporting/reporting-frontend/dist-backup-approval-ux-r1-20260716-003222` (7/7 ملفات، www-data، SHAs مطابقة للسابقة).
- **Audit Timestamp:** `20260716-003222` (`/root/approval-ux-r1-deploy-ts.txt`).
- **Rollback command:**
```bash
rm -rf /opt/reporting/reporting-frontend/dist
cp -a /opt/reporting/reporting-frontend/dist-backup-approval-ux-r1-20260716-003222 \
      /opt/reporting/reporting-frontend/dist
chown -R www-data:www-data /opt/reporting/reporting-frontend/dist
```
(لا يلزم Restart للـBackend.)

## إثبات Frontend-Only (Phase 4)
- **Backend DLL hashes قبل/بعد = متطابقة:** Api `7ac6bde4…` · Application `3541a74d…` · Infrastructure `039c3c9b…` · Domain `c2d6a585…`.
- **Migrations:** count=28 / head=`20260715162851_AddBypassTeamLeaderApproval` (ثابت قبل/بعد).
- **NRestarts=0** (لم يتغيّر) · **ExecMainStart** `Wed 2026-07-15 18:26:03 UTC` (لم يتغيّر) · Service **active**.
- Health داخلي=200 · عام=200.
- index.html يشير للحزمة الجديدة · الحزمة الجديدة HTTPS=200 · served JS SHA = المرشح تمامًا · الحزمة القديمة `index-B-y7LHB8.js` = **404** · لا host leakage · لا source maps.
- **الحكم:** `FRONTEND-ONLY DEPLOYMENT PROOF = PASS`.

## Zero-Impact Smoke (Phase 5)
- (أ) مسارات SPA (`/`, submissions, kpi, leave-requests, hr-requests, report-calendar, governance, dashboard) = 200.
- (ب) بنية الحزمة: Toast · Loading/animate-spin · isPending×3 · تأخير 700ms · رسائل 403/409 — حاضرة.
- (ج) عقود Backend تتطلب Auth (401، ليست 404): submissions approve/return/escalate · kpi approve/submit · leave team-leader/manager/hr approve + return · employee-service-requests start-review/complete/reject. (ملاحظة: لا يوجد مسار `/leave-requests/{id}/decide` — القرار مقسّم لمسارات فعلية.)
- (د) Admin Delete: بنيويًا لم يكتسب Toast/Spinner، لا ملفات admin-delete في الدلتا.

## المراقبة (Phase 6)
5 جولات: داخلي=200 · عام=200 · newJS=200 · service active · NRestarts=0 · email_outbox=0 · لا 500/42501/Exception/crash في السجل.

## Regression (Phase 8)
علامات: me_nav_collapsed_v2 · my-cycles · my-days · review-events · admin-delete · الحوكمة · تصعيد — حاضرة. Backend hashes ثابتة · migrations ثابتة · Email__Enabled=false · email_outbox=0 · health=200.

## Production Visual Smoke — قيد (Phase 7)
- قِشرة SPA العامة تُقدَّم RTL عربي (`dir="rtl"` lang="ar") بالحزمة الجديدة عبر HTTPS.
- **Visual smoke المصادَق (طفرة حيّة) لم يُنفَّذ على الإنتاج** حفاظًا على البيانات الحقيقية — لا fixture اختبار مخصّص، وممنوع تغيير كلمات مرور حسابات حقيقية أو استخدام بيانات موظفين. الاعتماد على **RC Live UAT الكامل (Visual Toast Evidence = PASS)** + **تطابق الحزمة byte-for-byte** (served SHA `033803f0` = المرشح `92b8c01` = artifact الـRC).

## التأكيدات النهائية
- Admin Delete = **unchanged**.
- Backend / Migrations = **صفر تغييرات**.
- `email_outbox = 0`.
- Email / Reminders / Scheduler / BackgroundJobs = **false**.

---

## الحكم
```text
APPROVAL ACTION UX R1 — PRODUCTION DEPLOYMENT SUCCESSFUL
```
