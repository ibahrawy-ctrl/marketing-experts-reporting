# تقرير إغلاق النشر على الإنتاج — حزمة واجهة المودريشن (MODERATION-UX-R1)

**التاريخ:** 17 يوليو 2026
**وقت النشر (UTC):** `20260717-205006`
**النوع:** Frontend فقط — لا Backend، لا Migration، لا تغيير API/Workflow/Approval/KPI/Governance.
**المصدر:** النسخة المعزولة `/private/tmp/mod-ux-rc` (فرع `release/mod-ux-r1`) من HEAD نظيف `6859ee0` + ملفّي الحزمة فقط.

---

## 1) النطاق المعزول (Isolation)
سطح التغيير = ملفّان فقط، مبنيّان على HEAD نظيف `6859ee0`:
- `reporting-frontend/src/pages/SubmissionsPage.tsx` (منطق عرض/تحرير المودريشن: moderationGroups، بطاقات KPI، Accordion/Collapse، Sticky Header، تجربة الحفظ).
- `reporting-frontend/src/pages/ProjectRepeatableGrid.test.tsx` (اختبارات، +43 سطرًا).

**إثبات العزل:** `git status --short` في النسخة المعزولة أظهر ملفّين فقط مُعدَّلين مقابل `6859ee0`، لا شيء غيرهما (لا backend، لا هجرة، لا ملفات أخرى).

## 2) البناء والاختبار
- اختبارات `ProjectRepeatableGrid.test.tsx`: **25/25 نجحت**.
- `npm run build` (tsc + vite) ناجح — التحذير الوحيد = تعليق `/*#__PURE__*/` الحميد في @microsoft/signalr (معروف، بلا أثر).
- Bundle الجديد: **`index-BSL8cj_1.js`**؛ CSS: `index-DWyJanfk.css`.
- **لا تسريب localhost** في الحزمة (`localhost:5090`=0)، و`reports.emarketingacademy.net/api` مضمّن (بُني بـ `VITE_API_BASE_URL=https://reports.emarketingacademy.net/api`).

## 3) البناء = الإنتاج (Byte-Identity)
| الملف | md5 محلي | md5 على الخادم |
|---|---|---|
| `index-BSL8cj_1.js` | `ed2744f06b995539a85ddd1eeb3414d1` | `ed2744f06b995539a85ddd1eeb3414d1` |
| `index.html` | `101cdde83afe5e59da81d7a7613f045d` | `101cdde83afe5e59da81d7a7613f045d` |

الإنتاج يقدّم **نفس البناء المُختبَر على RC حرفيًّا (byte-identical)**.

## 4) النشر
- **Backup قبل النشر:** `/opt/reporting/reporting-frontend/dist-backup-modux-r1-20260717-205006` (يحوي الحزمة السابقة `index-BbXihVZO.js`). المسار محفوظ في `/root/modux-r1-backup-path.txt`، والطابع الزمني في `/root/modux-r1-deploy-ts.txt`.
- **الأمر:** `rsync -az --delete dist/ → /opt/reporting/reporting-frontend/dist/` ثم `chown -R www-data:www-data`. **لا restart** (nginx static).

## 5) التحقق بعد النشر (Smoke)
- Health BEFORE = **200** / Health AFTER = **200** (`https://reports.emarketingacademy.net/health`).
- `index.html` المخدوم (HTTPS) يشير إلى **`index-BSL8cj_1.js`**.
- الحزمة الجديدة تُخدَم علنًا (HTTPS) = **200**؛ الحزمة القديمة `index-BbXihVZO.js` = **404** (أُزيلت بـ `--delete`).
- عدد `index-*.js` في assets = **1** (نظيف).
- علامات المودريشن (`grid-cols-6`) موجودة في الحزمة المخدومة علنًا.
- **Backend لم يُمَسّ:** الخدمة `reporting-api` = active؛ إصلاح ScopeResolver (`ResolveLedTeamAdditionalMemberIds`) ما زال حاضرًا (3)؛ لا هجرة طُبِّقت.

## 6) مستوى إثبات سيناريوهات الـUX التفاعلية
سيناريوهات UX التفاعلية (فتح تقرير مودريشن، عرض الموظف، Review/Read-Only، Accordion/Collapse، بطاقات KPI، أفضل منشور، Sticky Header، فتح التقارير القديمة/غير المودريشن بلا أخطاء) **أُثبتت بالكامل على RC/UAT** بنفس المصدر (تقرير `MODERATION-UX-R1-RC-UAT-Closure-Report.md`، البنود 3–6)، والإنتاج يقدّم **نفس البناء byte-identical**. لم يُنفَّذ اختبار تفاعلي مُصادَق على الإنتاج لعدم توفّر حسابات مستخدمين حقيقية، والتزامًا بعدم إعادة تعيين أي كلمة مرور دون موافقة جديدة. **مستوى الإثبات على الإنتاج = تطابق بناء + تحقّق حزمة مخدومة + تحميل علني 200** (مكافئ آمن).

## 7) إجراء التراجع (Rollback)
Frontend فقط، لا هجرة لعكسها:
```
BK=$(cat /root/modux-r1-backup-path.txt)   # dist-backup-modux-r1-20260717-205006
rsync -az --delete "$BK"/ /opt/reporting/reporting-frontend/dist/
chown -R www-data:www-data /opt/reporting/reporting-frontend/dist
```
لا حاجة لإعادة تشغيل. يعيد الحزمة `index-BbXihVZO.js`.

## 8) القرار
✅ **النشر مكتمل وناجح.** MODERATION-UX-R1 مُغلقة على الإنتاج. لا يُبدأ في حزمة Media Buyer إلا بعد إعلان الإغلاق الكامل.
