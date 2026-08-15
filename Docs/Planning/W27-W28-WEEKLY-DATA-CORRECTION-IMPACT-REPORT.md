# W27/W28 WEEKLY DATA CORRECTION — IMPACT & CONSTRAINT REPORT (Phase 0–3, NON-DRAFT ONLY)

> **الحالة:** قراءة فقط — لم تُنفَّذ أي كتابة. **الحكم: `W27/W28 WEEKLY DATA CORRECTION STOPPED — DECISION REQUIRED`.**
> **البيئة:** الإنتاج `reporting_prod` @ VPS 187.127.72.232. التاريخ المرجعي 2026-07-15.
> **الهدف:** توحيد السجلات الأسبوعية **غير المسودّة** تحت `2026-W28` (السبت 04-07 → الجمعة 10-07). المسودّات لا تُلمَس نهائيًّا؛ اليومي مُستبعَد كليًّا.
> **القاعدة الحاسمة الجديدة:** أي `Status=Draft` لا يُلمَس ولا يُعتبر تعارضًا، لكن إن منع قيد التفرّد نقل السجل الفعليّ بسبب وجود مسودّة ⇒ `STOP — DRAFT UNIQUE CONSTRAINT COLLISION`.

---

## Phase 0 — Pre-Flight (قراءة فقط) = GO
الأساس (فحص جديد): report_submissions=57، submission_field_values=654، approval_steps=56، kpi_evaluations=17، kpi_results=100، kpi_review_events=1، audit_logs=593، notifications=101، email_outbox=0، migrations=27 (رأس `20260713171040`). الأعلام false. **`WEEK DATA CORRECTION PRE-FLIGHT = GO`.**

---

## قيود التفرّد الفعلية في Production (مقروءة من pg_indexes)
- **التقارير:** `UNIQUE (ReportTemplateVersionId, SubmitterId, PeriodKey) WHERE IsDeleted=false` — **جزئيّ** (صفّان غير محذوفين بنفس المفتاح يصطدمان).
- **KPI:** `UNIQUE (KpiTemplateVersionId, SubjectUserId, PeriodKey)` — **كامل بلا فلتر** (يشمل حتى المحذوف والمسودّات).

---

## Phase 1 — تقرير الأثر (قراءة فقط، تصنيف Draft/غير-Draft)

### أ) التقارير الأسبوعية (IsDeleted=false)
| PeriodKey | غير-Draft | Draft |
|---|---|---|
| 2026-W27 | 12 | 0 |
| 2026-W28 | 8 | 2 |
+ محذوف ناعمًا W27: 1 (`416de42c` خالد مجدي — Case E).

### ب) تقييمات KPI الأسبوعية (IsDeleted=false)
| PeriodKey | غير-Draft | Draft |
|---|---|---|
| 2026-W27 | 7 | 7 |
| 2026-W28 | 2 | 1 |

### ج) إثبات استبعاد اليومي
`Daily records before correction = 28`. كلها `PeriodType=Daily` أو مفتاح `YYYY-MM-DD` ⇒ **خارج النطاق كليًّا، لا تدخل أي قائمة تحديث**.

### د) الحالات الفعلية المقروءة من DB (لا افتراض)
تقارير: {Closed, Submitted, ApprovedByDirectManager, Returned, Draft}. KPI: {Approved, Submitted, UnderReview, Draft}. لا وجود فعليّ لـ InProgress/NeedsRevision/Reopened/Rejected.

---

## Phase 2 — التصنيف النهائي

### ✅ Case A — نقل آمن غير-مسودّة (بلا صفّ مانع في W28)

**التقارير (11):**
| # | Id | الموظف | الحالة |
|---|---|---|---|
| 1 | `eb327416` | أحمد عاطف | Closed |
| 2 | `f5c61b7e` | أحمد عبدالفتاح | Closed |
| 3 | `1267bcbe` | أحمد نصار | Closed |
| 4 | `4f4d1de1` | أمير عادل | ApprovedByDirectManager |
| 5 | `f116a7ef` | إسراء حفصي | Closed |
| 6 | `100ae0b9` | خالد مجدي | Submitted |
| 7 | `991462c7` | شيريهان القاضي | Submitted |
| 8 | `7223ce57` | محمد عبدالقوي | Submitted |
| 9 | `4c5921c6` | ندى | Closed |
| 10 | `d6022f26` | نور الدين رجب | Returned |
| 11 | `7eec07f2` | يوسف عبدالله | Closed |

**KPI (6):**
| # | Id | الموظف | الحالة | الدرجة |
|---|---|---|---|---|
| 1 | `ab689bba` | أحمد عاطف | Submitted | 90.85 |
| 2 | `bf01ae11` | إسراء حفصي | Submitted | 90.40 |
| 3 | `7cbdcae9` | جهاد صلاح | Approved | 75.00 |
| 4 | `5e7a620f` | فاطمة محمد | Approved | 94.25 |
| 5 | `f14657a8` | ندى | Submitted | 92.85 |
| 6 | `b0e84767` | نور الدين رجب | Approved | 86.25 |

**إجمالي Case A الآمن = 11 تقريرًا + 6 KPI = 17 سجلًّا.**

### 🛑 Case B/Collision — اصطدام قيد تفرّد بسبب مسودّة (لا نقل، لا لمس مسودّة)
1. **تقرير فاطمة محمد** `b127a8f9` (W27 Submitted، 24 حقلًا، أُرسِل 07-09) — الصفّ المانع W28: مسودّة `232f5c72` (IsDeleted=false، نفس النسخة `1934c2ad`). الفهرس الجزئيّ يمنع صفّين غير محذوفين ⇒ **`DRAFT UNIQUE CONSTRAINT COLLISION`**.
2. **KPI أميرة محمد** `10273050` (W27 UnderReview، 92.90، 14 نتيجة، 1 review event، مراجِع إبراهيم) — الصفّ المانع W28: مسودّة `bfa49118` (نفس النسخة `31e20d54`). الفهرس الكامل يمنع أي صفّين ⇒ **`DRAFT UNIQUE CONSTRAINT COLLISION`**.

### Case C — مسودّات محميّة (NO ACTION): 7 KPI W27 + 1 KPI W28 (`bfa49118`) + 2 تقرير W28 (`232f5c72` فاطمة، `77edfa73` محمد عبدالله).
### Case D — صحيحة أصلًا على W28 (NO ACTION): 8 تقارير + 2 KPI غير-مسودّة.
### Case E — محذوف (NO ACTION): تقرير `416de42c` خالد مجدي.
### Case F — نصّي/غامض (NO ACTION — قرار منفصل): تقرير محمود القوصي `c85111db` مفتاح «من 1 الى 7 يوليو 2026».

---

## Phase 3 — تقرير التعارض والقيود

1. **Safe Moves:** 11 تقريرًا غير-مسودّة + 6 KPI غير-مسودّة = 17.
2. **Draft-Protected:** كل المسودّات أعلاه — **تُقرأ فقط، لا تُلمَس** (`ALL DRAFT RECORDS = READ ONLY / NO ACTION`).
3. **Real Non-Draft Conflicts:** **لا يوجد** (لا سجلّ W27 غير-مسودّة يقابله W28 غير-مسودّة لنفس الموظف/النسخة).
4. **Unique Constraint Collisions (بسبب مسودّة):** حالتان (فاطمة تقرير، أميرة KPI).
   - **البدائل الفنية دون لمس المسودّة:** الخيار الوحيد المحافظ على المسودّة = **عدم النقل** (يبقى السجلّ الفعليّ على W27) ورفعه لقرار منتَج/إداري مستقل. لا يمكن وضع صفّين غير محذوفين بنفس مفتاح W28، وتحرير المفتاح لا يتمّ إلا بلمس المسودّة (ممنوع). كل بديل آخر (حذف/أرشفة/نقل/حذف ناعم للمسودّة) **مرفوض بالقاعدة**.

---

## 🛑 Mandatory Stop Gate
وُجد اصطدام Unique Constraint بسبب مسودّة (حالتان) ⇒

### `W27/W28 WEEKLY DATA CORRECTION STOPPED — DECISION REQUIRED`

**جاهز للتنفيذ فور موافقة صريحة على القائمة المحدَّثة (Case A = 17 سجلًّا فقط):** Backup ثم Dry-run ثم معاملة واحدة + تدقيق `weekly_period_corrected` + Assertions.

**قرارات مطلوبة مستقلّة:** (1) تعارض فاطمة (تقرير)، (2) تعارض أميرة (KPI)، (3) محمود القوصي النصّيّ (Case F). لن يُلمَس أيّ منها ولا أيّ مسودّة إلا بقرار صريح منفصل.
