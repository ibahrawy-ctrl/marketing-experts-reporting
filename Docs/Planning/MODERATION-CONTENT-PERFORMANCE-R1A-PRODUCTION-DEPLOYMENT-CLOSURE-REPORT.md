# MODERATION-CONTENT-PERFORMANCE-R1A — PRODUCTION DEPLOYMENT & OPERATIONAL CLOSURE REPORT

**التاريخ:** 2026-07-20 · **النطاق:** Frontend فقط · **الحالة:** DEPLOYED — OPERATIONAL CLOSURE COMPLETE

---

## 1. Commit
`265fa7a` — `fix(moderation): align live V5 submission display and vocabulary`

## 2. Parent / السلالة
`3eee204` (production head: Unified Status + Fatma-Direct + Archive Governance + Admin Governance + KPI Partial Unique Index)

## 3. Before bundle (الإنتاج قبل النشر)
`index-6J6SJ-kI.js` (index.html mtime 2026-07-20 02:41:35 UTC)

## 4. After bundle (بعد النشر)
`index-COB6nGUW.js` (1,301,393 bytes، مبنيّ بـ `VITE_API_BASE_URL=https://reports.emarketingacademy.net/api`)

## 5. Backup path
`/opt/reporting/reporting-frontend/dist-backup-modr1a-20260720-075312` (1.4M، يحوي الحزمة القديمة `index-6J6SJ-kI.js` كاملة). TS مخزَّن `/root/modr1a-deploy-ts.txt`.

## 6. Deployment evidence
- `rsync -az --delete` للـ dist المبنيّ ثم `chown -R www-data:www-data`. **بلا** backend publish/restart/dotnet/migration/seeder/SQL/env.
- index.html يشير إلى `index-COB6nGUW.js`؛ الحزمة الجديدة تُخدَم 200 عبر HTTPS؛ الحزمة القديمة 404 (cache-bust)؛ SPA deep-link (`/app/submissions`, `/app/kpi`) يرجع index.html؛ **0 تسريب `localhost:5090`**؛ prod base same-origin مضمّن مرة واحدة (الـ2 `localhost` المتبقيان = fallback مكتبات SSR حميد `window.location.href || http://localhost`).

## 7. Smoke tests
- HTTP: home 200، asset 200، deep links 200، anonymous `/api/submissions` 401، `/health` 200.
- Authenticated (break-glass، قراءة فقط، التوكن لم يُطبع): login OK، GET submissions 200، GET moderation V5 template `db8c764d` 200.

## 8. Business validation (تقرير مديرشن V5 حقيقي)
- **الأقسام الخمسة مثبَتة في الحزمة المُقدَّمة:** نظرة عامة / حجم العمل / الجودة والتصعيد / الحالات / السرد والتوصيات.
- **مفاتيح Vocabulary-1 مثبَتة:** project_status, incoming_messages, answered_messages, avg_response_minutes, cases_grid, escalations, complaints, converted_opportunities, recommendations.
- **المؤشرات الممنوعة (Vocabulary-3) = 0:** posts_published / comment_response_rate / best_post / tasks_completion_rate / publishing_tracking — كلها غائبة (لا Performance/Average Delay/Violations/Productivity/Quality Metrics/Analytics مُختلَقة).
- **GridDisplay hardening (SubmissionsPage.tsx:1521-1524):** الصفوف الفارغة تُخفى (`filter`)، الصفوف المشوَّهة محروسة بـ `Array.isArray`، الصفوف الصحيحة تظهر.
- **Fallback عام محفوظ:** `isModerationVocab1` بوّابة ثلاثية المفاتيح؛ غير-المديرشن يمرّ عبر `ProjectRepeatableDisplay` العام (السطر 1452+).
- **بيانات الإنتاج الحقيقية:** «تقرير المديرشن الأسبوعي» = 2 تسليم (كلاهما V5) ⇒ العرض المجمَّع؛ 92 تسليمًا عبر 21 قالبًا آخر ⇒ المسار العام. الإجمالي 94.
- **Unified Status سليم:** 5 حالات (ApprovedByDirectManager, Closed, Draft, Returned, Submitted)، بلا تغيير سكيمة/API/DTO.

## 9. Regression proof
- Backend **لم يُعَد تشغيله** (uptime `Mon 2026-07-20 06:44:10 UTC`)؛ publish dir + `Reporting.Api.dll` mtime `2026-07-20 02:40:34` (لم تُمَسّ بنشر 07:52).
- Migration head `20260716015239_KpiEvaluationPartialUniqueIndex`، count **29** — بلا تغيير. health داخلي+عام 200.
- RC (`khubara-reporting-rc.service`) active، حزمته `index-3ZsmYS16.js` بلا تغيير.
- Email__Enabled=false / Reminders__Enabled=true / Scheduler__Enabled=true، env mtime `2026-07-17 12:29:37` — بلا تغيير.
- Archive Governance / Fatma-Direct / Unified Status جميعها في الـ backend غير المُعاد تشغيله ⇒ غير متأثّرة.
- Post-deploy authenticated reads 200: dashboard/me، submissions، report-templates، kpi-templates، clients، projects، notifications.

## 10. Rollback instructions (frontend-only)
```
cp -a /opt/reporting/reporting-frontend/dist-backup-modr1a-20260720-075312/. /opt/reporting/reporting-frontend/dist/
chown -R www-data:www-data /opt/reporting/reporting-frontend/dist
```
لا حاجة لأي backend/migration rollback (النشر frontend فقط). الحزمة القديمة `index-6J6SJ-kI.js` تعود فورًا (nginx static، بلا restart).

## 11. Known limitations
- R1A يُواءم العرض مع **Vocabulary-1 / Production V5 فقط**. التوسعة المهيكلة R1B (platform / violation_type / severity / responsibility_owner / attribution + قالب V6) **مؤجّلة** — لا تبدأ قبل موافقة صريحة.
- تحذير بناء حميد وحيد: signalr `/*#__PURE__*/` (Rolldown)؛ و2 مرجع `localhost` من fallback المكتبات (ليس API base).
