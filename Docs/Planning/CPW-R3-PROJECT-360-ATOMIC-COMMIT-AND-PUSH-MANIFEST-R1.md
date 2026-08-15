# CPW-R3 · Project 360 — بيان الالتزامات الذرّيّة والدفع (R1)

**التذكرة:** CPW-R3 — Project 360 Completion Candidate
**المرحلتان:** I (التزامات ذرّيّة) · J (دفع فرع الميزة)
**التاريخ:** 15 أغسطس 2026
**الفرع:** `feature/cpw-r3-project-360-candidate-r1`
**الأساس:** `c157829` — `feat(governance): close ADMIN-GOVERNANCE-R1 as the official develop EF baseline`

---

## 1) قاعدة التجهيز المُسمّى

**كلّ** ملفّ دخل التجهيز بمساره الصريح. `git add -A` و`git add .` **لم يُستعملا ولا مرّة واحدة** — وهذا ليس شكليّة: شجرة العمل الأصليّة تحوي 35 ملفًّا معدَّلًا و48 ملفًّا غير متعقَّب، أغلبها يخصّ تذاكر أخرى غير منشورة. تجهيز شامل واحد كان سيبتلع تذاكر البريد والإجازات والاعتمادات في التزام يحمل عنوان Project 360.

**الضمانة البنيويّة الإضافيّة:** الالتزامات لم تُنشأ في شجرة العمل الأصليّة أصلًا، بل في **شجرة عمل معزولة** (`git worktree`) لا تحوي إلّا ملفّات CPW-R3 المعتمَدة. أي أنّ الملفّات خارج النطاق **لم تكن موجودة فيزيائيًّا** ليُخطئ أحد بتجهيزها.

---

## 2) الالتزامات الإحدى عشرة

| # | البصمة | العنوان | ملفّات | أسطر |
| --- | --- | --- | --- | --- |
| 1 | `6236234` | `feat(projects360): add Project 360 domain entities, enums and health value objects (CPW-R3 W1)` | 14 | +902 |
| 2 | `8e66c12` | `feat(projects360): map Project 360 schema and add additive migration #33 (CPW-R3 W3)` | 7 | +5,898 |
| 3 | `0d0305a` | `feat(projects360): add application contracts, DTOs and ProjectHealthPolicy (CPW-R3 W4)` | 5 | +1,385 |
| 4 | `5e6da71` | `feat(projects360): implement services, persisted health and anti-enumeration guards (CPW-R3 W4/W6)` | 10 | +1,977 |
| 5 | `56c706e` | `feat(projects360): seed the 38-value Project 360 catalog as data, not code (CPW-R3 W5 · DEC-W4-01)` | 2 | +72 −1 |
| 6 | `1c7ab16` | `feat(projects360): expose the 34-route Project 360 API surface (CPW-R3 W5/W6)` | 7 | +343 |
| 7 | `74d6ca2` | `test(projects360): cover health policy, API surface and anti-enumeration (CPW-R3 W6-IG)` | 4 | +2,466 |
| 8 | `7bd73da` | `feat(projects360): add typed Project 360 client models, hooks and formatters (CPW-R3 R2-W12)` | 3 | +1,234 |
| 9 | `0494c2b` | `feat(projects360): build the Project 360 workspace with lazy tabs and server-owned health (CPW-R3 R2-W12)` | 12 | +1,767 |
| 10 | `fbf7983` | `test(projects360): assert single-call overview, lazy tabs and zero client-side health math (CPW-R3 R2-W12)` | 2 | +813 |
| 11 | `5dca2a3` | `docs(projects360): record the CPW-R3 design, gates, findings and candidate lineage (W0–W6-IG · R2-W12)` | 14 | +5,440 |

**الإجمالي:** 66 ملفًّا · **+16,857 / −1**

الحذف الوحيد (سطر واحد في الالتزام 5) هو تحديث تعليق في `ExecutionTaxonomyService.KnownDomains` عند إضافة مجالات الكتالوج الثلاثة — لا حذف كود.

---

## 3) الانحرافان عن التقسيم المقترَح في §16 — والتبرير

التقسيم المقترَح كان 10 التزامات. الفعليّ 11، مع فرقين مقصودين:

### 3.1 «تخزين الصحّة» لم يُفصَل عن خدمات الطفرة (الالتزام 4)

§16 اقترح التزامًا مستقلًّا لـ«تخزين الصحّة ومحاذاة مكافحة التعداد».

**لماذا لم يُفصَل:** تخزين الصحّة **ليس** طبقة فوق الخدمات بل **داخل وحدة عملها**: الخدمات المعنيّة لا تستدعي `SaveChangesAsync` مطلقًا، ومسار الحفظ الوحيد فيها هو `SaveWithHealthAsync`. فصلهما كان سيُنتج التزامًا وسيطًا **لا يُبنى** (خدمات تشير إلى عقد لم يُضَف بعد) أو **يُبنى ولا يعمل** (خدمات تحفظ بلا صحّة). الالتزام الذرّيّ هو ما يبنى ويعمل وحده، لا ما يطابق قائمة مقترَحة.

نفس المنطق ينطبق على مكافحة التعداد: `Project360Authorization` هي البوّابة التي تستدعيها كلّ خدمة، فوجودها شرط بناء لا إضافة لاحقة.

### 3.2 بذر الكتالوج فُصِل في التزام مستقلّ (الالتزام 5)

§16 لم يقترحه منفصلًا.

**لماذا فُصِل:** هذان الملفّان (`ExecutionTaxonomySeeder` · `ExecutionTaxonomyService`) هما **بالضبط** الملفّان اللذان صُنّفا خطأً خارج النطاق في المسودّة الأولى للبيان — وكان إسقاطهما سيُنتج مرشَّحًا يُبنى ويُشغَّل وهو **فارغ وظيفيًّا** (لا مخطَّط استراتيجيّة، ولا أنواع مخرَجات). عزلهما في التزام يحمل الرقم 38 في عنوانه يجعل السقوط نفسه مستحيل التكرار بصمت: أيّ إسقاط لاحق يظهر كالتزام مفقود لا كسطرين ضائعين داخل التزام كبير.

---

## 4) الفصل المُثبَت — ما لم يدخل ولا التزام واحد

هذه الملفّات موجودة في شجرة العمل الأصليّة كتعديلات غير منشورة تخصّ تذاكر أخرى، و**لم يدخل منها شيء**:

| الملفّ | التذكرة المالكة |
| --- | --- |
| `reporting-frontend/src/components/ui.tsx` | APPROVAL ACTION UX R1 (`Button.loading`) |
| `reporting-frontend/src/lib/api.ts` | APPROVAL ACTION UX R1 (`apiErrorCode` · `approvalErrorMessage`) |
| `reporting-frontend/src/lib/format.ts` | تذكرة أخرى |
| `reporting-frontend/src/main.tsx` · `pages/HrRequestsPage.tsx` · `KpiPage.tsx` · `LeaveRequestsPage.tsx` · `SubmissionsPage.tsx` · `types/api.ts` | تذاكر أخرى |
| `Reporting.Application/Notifications/*` · `Services/Email*` · `Services/Report*` | تذاكر البريد والتذكير |
| `Services/ReportReminderSchedulerService.cs` + خيارها | تذكرة جدولة التذكير |
| `tests/.../EmployeeProfileScopeTests.cs` · `ReportCalendarTests.cs` · `ReportRemindersTests.cs` | تذاكر أخرى |
| `Ops/` · `tools/LegacyExecutionFixture/` · `components/ActionResultToast.tsx` | خارج النطاق |
| ~110 وثيقة في `Docs/Planning/` و`Docs/Architecture/` لتذاكر أخرى | خارج النطاق |

### 4.1 الملفّ المختلط الوحيد — `DependencyInjection.cs`

الملفّ الوحيد الذي حمل تعديلات من تذكرتين معًا. فُصل على مستوى الـhunk:

| الـhunk | التصنيف | القرار |
| --- | --- | --- |
| `@@ -58,0 +59` `services.Configure<ReportReminderSchedulerOptions>(…)` | تذكرة أخرى | **مستبعَد** |
| `@@ -75,0 +77` `services.AddHostedService<ReportReminderSchedulerService>()` | تذكرة أخرى | **مستبعَد** |
| `@@ -104,0 +107,16` كتلة Project 360 | CPW-R3 | **مُدرَج وحده** |

**إثبات الاستبعاد:** `grep -c "ReportReminderScheduler"` في المرشَّح = **0**.

---

## 5) طريقة التدقيق التي أنتجت الفصل

بدل الحكم بالاسم، عُدَّت علامات CPW-R3 في **الأسطر المضافة** لكلّ ملفّ معدَّل:

```bash
for f in $(git diff --name-only); do
  n=$(git diff -U0 -- "$f" | grep -E "^\+" \
      | grep -icE "project360|projects360|CPW-R3|Project 360|strategy_section|strategy_field|contract_deliverable|HealthStatus|HealthPercent|HealthComputedAtUtc|ProjectOwnerId|TeamLeaderId")
  echo "$n  $f"
done | sort -rn
```

**النتيجة:** 12 ملفًّا بعلامة واحدة فأكثر · 23 ملفًّا بصفر علامات. **لا منطقة رماديّة** — الفصل حادّ لا اجتهاديّ.

هذه الطريقة هي التي كشفت خطأ تصنيف ملفَّي بذر الكتالوج (§3.2): بالاسم بديا خارج النطاق، وبمحتوى الـhunk ظهرا CPW-R3 بنسبة 100%.

---

## 6) المرحلة J — الدفع والتحقّق

| البند | القيمة |
| --- | --- |
| المستودع البعيد | `git@github.com:ibahrawy-ctrl/marketing-experts-reporting.git` |
| الفرع | `feature/cpw-r3-project-360-candidate-r1` |
| حالة الإنشاء | `* [new branch]` — لم يكن موجودًا (تحقّق `git ls-remote` قبل الدفع) |
| **بصمة الرأس البعيدة** | `5dca2a303a3d10f1f721629b212d0936b6717fad` |
| **بصمة الرأس المحلّيّة** | `5dca2a303a3d10f1f721629b212d0936b6717fad` |
| التطابق | ✅ **متطابقتان** |
| نوع الدفع | دفع عاديّ (`-u`) — **لا `--force`** ولا `--force-with-lease` |
| إعادة كتابة تاريخ | **صفر** — لا rebase ولا amend لالتزام مدفوع |

### 6.1 الفروع المشتركة لم تُمَسّ

| الفرع البعيد | البصمة بعد الدفع |
| --- | --- |
| `refs/heads/develop` | `6859ee0d51bef574a4dc4623c015817af325e78c` — **بلا تغيير** |
| `refs/heads/main` | `508509ad8474b321c80cbdd48eb84ecb54bee212` — **بلا تغيير** |

### 6.2 ما لم يُنفَّذ عمدًا

**لا Merge · لا Pull Request · لا Tag · لا نشر على TEST أو UAT أو RC أو Production · لا مساس بأيّ قاعدة حيّة.**

الدفع إلى فرع ميزة مخصَّص هو **أقصى** ما تصرّح به التعليمات، والتوقّف بعده إلزاميّ.

---

## 7) البصمات الكاملة للتحقّق المستقلّ

```
6236234cc27812d8be520e379e4524b55db08b03
8e66c12132bb7478a16f1eba5a51e9203eef3faf
0d0305a48b1584db92820c407282bb4e5462c63b
5e6da71c133483c8e03cb768b6d0fdd928b93e34
56c706e829038014d0b2925ca209dbfc55b825f8
1c7ab16b77f118e95a7985a84300b8e95384c55e
74d6ca269c4c2b487d498130f0932a97580cd624
7bd73da7dc08f0c3f1d39279f81a72f777bc4bb3
0494c2b1838e99e603c24d31c1562ebc164677e5
fbf7983da5e8dcb49621656ad136e0969a2bcbd4
5dca2a303a3d10f1f721629b212d0936b6717fad   ← HEAD (بعيد ومحلّيّ)
```

> **ملاحظة:** إن أُضيف هذا البيان نفسه والتقرير النهائيّ في التزام لاحق، فبصمة الرأس تتقدّم بالتزام واحد يحمل تغييرات وثائقيّة حصرًا، ويُدفَع بالطريقة نفسها بلا إعادة كتابة تاريخ. البصمات العشر الأولى تبقى ثابتة.
