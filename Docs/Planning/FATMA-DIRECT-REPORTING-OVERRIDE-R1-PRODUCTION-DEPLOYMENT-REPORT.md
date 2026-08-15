# FATMA-DIRECT-REPORTING-OVERRIDE-R1 — تقرير النشر الإنتاجيّ النهائيّ

**الحالة: `PRODUCTION DEPLOYED = DONE` — نُشر بنجاح على الإنتاج بموافقة صريحة، النطاق مقفول على 5 بنود.**

- التاريخ: 2026-07-15 (W29) — نافذة النشر `TS=20260715-182329`.
- المصدر: شجرة العمل المعزولة `/tmp/fatma-dr-worktree` (detached @ 4a3ff8c، أساس prod، الدلتا backend-only = 8 ملفات).
- الميزة: قاعدة عامّة `BypassTeamLeaderApproval` (`bool NOT NULL DEFAULT false` على `AspNetUsers`) — «التبعية المباشرة للمدير». لا شرط باسم/بريد/معرّف فاطمة. طُبِّقت على فاطمة فقط.

---

## 1) Production Pre-Flight (قراءة فقط) — PASS

| فحص | النتيجة |
|---|---|
| مالك `AspNetUsers` | `reporting_app` (دور اتصال التطبيق نفسه، login=t, superuser=f) ⇒ التطبيق مالك الجداول فيطبّق الهجرة تلقائيًّا عند الإقلاع — **prod ≠ RC** |
| صلاحية DML/DDL للتطبيق | INSERT/UPDATE=t، owner ⇒ ALTER مسموح |
| `ac360154-2ece-4f24-9457-c8794c954ed2` | Permission / **Submitted / TeamLeader**، حدث `submitted` واحد فقط (لم يُمَسّ) |
| فاطمة `TeamId` | `34b4928d-241d-479c-8422-ee6a99d2e394` (ادارة حسابات العملاء) — ثابت |
| فاطمة `ManagerId` | `7e2cb6ac-…` = إبراهيم البحراوي (نشط) — ثابت |
| قائد الفريق | `f4e25122-…` = أحمد عبدالرؤوف (نشط) — ثابت |
| بقيّة الفريق | سماح ابوالمجد + شيريهان القاضي — يبقون على أحمد |
| service/health/migrations | active، NRestarts=0، 200/200، آخر هجرة `20260713171040` (27)، العمود غائب |

**اكتشاف حاسم:** على الإنتاج التطبيق مالك الجداول ⇒ الهجرة تُطبَّق تلقائيًّا عند الإقلاع (بخلاف RC حيث المالك NOLOGIN منفصل وتطلّبت الهجرة تطبيقًا يدويًّا بدور المالك).

## 2) Backup (TS=`20260715-182329`)
- DB: `/root/db-backups/reporting_prod-prefatmadr-20260715-182329.dump` (1,452,490 bytes)
- Backend: `/opt/reporting/publish-backup-fatmadr-20260715-182329`
- Frontend: `/opt/reporting/reporting-frontend/dist-backup-fatmadr-20260715-182329`
- TS مخزَّن: `/root/fatmadr-deploy-ts.txt`

## 3) نشر Backend + الهجرة الواحدة — migrations 27 → 28
- `dotnet publish -c Release` ثم `rsync -az --delete --exclude appsettings.Development.json` → `/opt/reporting/publish` + `chown www-data` + `systemctl restart`.
- **Frontend لم يُنشَر** (الميزة backend-only، الواجهة byte-identical للأساس).
- سجلّ الإقلاع: **`Applying migration '20260715162851_AddBypassTeamLeaderApproval'`** + `INSERT INTO "__EFMigrationsHistory"` + `Hosting environment: Production` + `Now listening on: http://127.0.0.1:5090`.
- Post-deploy: health 200/200، `migrations=28`، العمود `BypassTeamLeaderApproval boolean NOT NULL default false`، active/NRestarts=0.

## 4) تفعيل الحقل لفاطمة — UPDATE محروس
- `UPDATE 1` (بشرط `Id` + `Email` + `BypassTeamLeaderApproval=false`).
- `bypass_true = 1` (فاطمة حصرًا)، `TeamId`/`ManagerId` ثابتان، COMMIT.

## 5) إصلاح `ac360154` — معاملة مستقلة محروسة
- الحارس: يُحدّث فقط إن كان الطلب `Submitted/TeamLeader` بالضبط. النتيجة `UPDATE 1` + `INSERT 0 1`.
- النهائي: `Status=TeamLeaderApproved`, `CurrentStep=Manager`, `UpdatedAtUtc` مضبوط.
- الحدث المضاف يحاكي مسار الكود حرفيًّا: `team_leader_step_skipped / Employee / Submitted→TeamLeaderApproved`، Actor=فاطمة، التعليق: «تم تخطي مراجعة قائد الفريق (تبعية مباشرة للمدير)، وتم توجيه الطلب إلى المدير المباشر.»
- Timeline النهائي: `submitted` ثم `team_leader_step_skipped`.

## 6) Smoke الوظيفيّ الحيّ على ثنائيّة prod (مستخدمون مؤقّتون `fatmadr-smoke-`، أُنشئوا ثم نُظِّفوا) — PASS
| سيناريو | العَلَم | النتيجة الفعلية |
|---|---|---|
| A (ضابط) | bypass=false | `Submitted / TeamLeader` — يمرّ بقائد الفريق (السلوك القائم محفوظ) |
| B (معالَجة) | bypass=true | `TeamLeaderApproved / Manager` + `[submitted, team_leader_step_skipped(Submitted→TeamLeaderApproved)]` |

الفرق الوحيد بين A وB = العَلَم. لم يُلمَس أيّ حساب حقيقيّ ولا حساب أدمن (المستخدم المؤقّت أُنشئ بـ hash هوية v3 صالح مولّد بـ Python، login عبر API قَبِله).

## 7) تحقّق عدم التأثّر + التنظيف
- فريق أحمد الحقيقيّ: أحمد=`f`، سماح=`f`، شيريهان=`f`، فاطمة=`t` — **بقيّة الفريق يبقون على أحمد**.
- تنظيف smoke: DELETE 3 أحداث + 2 طلبات + 3 مستخدمين + 1 فريق + 1 إدارة، **residuals=0**، السكربتات أُزيلت.
- التحقّق النهائيّ الموحّد: service active/NRestarts=0، health 200/200، migrations=28، bypass_true=1 (فاطمة)، ac360154=TeamLeaderApproved/Manager، smoke residuals=0.

## ما لم يُمَسّ (خارج النطاق المقفول)
الواجهة، التقويم، البريد، القوالب، بقيّة طابور الـHotfix، KPI/ComputeScore، ScopeResolver، CurrentApproverId، مسار KPI reviewer، TeamId/قائد فريق/ManagerId لفاطمة، وأيّ عضو آخر في الفريق، وأيّ حساب حقيقيّ أو أدمن.

## Rollback (عند الحاجة)
- **الكود**: استعادة `publish-backup-fatmadr-20260715-182329` + `systemctl restart`.
- **الهجرة**: عكسها `DropColumn BypassTeamLeaderApproval` على `AspNetUsers` (آمن، إضافيّ بحت) أو استعادة DB dump.
- **تفعيل فاطمة**: `UPDATE "AspNetUsers" SET "BypassTeamLeaderApproval"=false WHERE "Id"='03b725e4-d996-432f-8771-79d67a659871';`
- **ac360154**: طلب معتمَد فعليًّا؛ إن لزم العكس ⟵ إعادته يدويًّا إلى Submitted/TeamLeader + حذف حدث التخطّي (قرار الإدارة).

---
**النتيجة: النشر الإنتاجيّ اكتمل بالكامل ضمن النطاق المقفول على البنود الخمسة، بلا انحراف، وبلا أثر خارج النطاق.**
