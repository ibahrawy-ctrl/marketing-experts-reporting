# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 10: تضييق استعادات المرحلة E وإثبات حياديّتها

**التاريخ:** 16 أغسطس 2026 · **المرجع:** §10 من التذكرة · **الحكم: الاستعادات لا تغيّر سلوك الإنتاج الحيّ**

## 1. الحجم النهائيّ بعد التضييق

| الملفّ | الصافي | الطبيعة |
|---|---|---|
| `Reporting.Application/Common/RoleCapabilities.cs` | **+1** | سطر تعريف قدرة `positions.manage` (AdminOnly) |
| `Reporting.Infrastructure/Services/ScopeResolver.cs` | **+60** | استعادة `ResolvePositionScopeAsync` واتّحادها فوق نطاق الدور |
| `tests/…/RoleMatrixCapabilitiesTests.cs` | **+3 −1** | إعادة التوقّع المقابل للسطر أعلاه |

**الإجماليّ: 63 إضافة و1 حذف في 3 ملفّات.** لا تعديل على أيّ خدمة أو متحكّم أو هجرة.

## 2. لماذا هذه الاستعادات ليست «إعادة ميزة حذفها الإنتاج عمدًا»

الحذف وقع في التزام الإنتاج `83d7f8f`، ونصّ رسالته يحسم النيّة:

> `Remove FlexiblePositionsPhase1A entirely (never built into Prod/RC)` …
> `Restore ScopeResolver to pre-Positions form (functionally identical to deployed).`

⟹ الحذف كان **خطوة إعادة بناء ثنائيّة** لتثبيت مصدر يطابق ما هو منشور فعلًا، لا **قرار منتج**.
وقاعدة التفرّع `6fd2253` **كانت تحتوي** `ResolvePositionScopeAsync` — فالاستعادة تُرجِع سلفًا مشتركًا،
لا تضيف جديدًا.

## 3. إثبات الحياد على الإنتاج الحيّ (Fail-Closed بالبناء)

`ResolvePositionScopeAsync` يبدأ باستعلام `UserPositions ⋈ Positions ⋈ PositionScopes`، ثمّ:

```csharp
if (scopes.Count == 0) return (new List<Guid>(), false);
```

وفي المستدعي:

```csharp
if (positionSeesAll) seesAll = true;          // false ⟹ بلا أثر
if (positionIds.Count > 0)                    // 0 ⟹ بلا أثر
    ids = ids.Concat(positionIds).Distinct().ToList();
```

على الإنتاج الحيّ **لا توجد أيّ صفوف مناصب** (الميزة لم تُبنَ ولم تُبذَر قطّ) ⟹ الفرع يُرجِع
`(∅, false)` ⟹ `ScopeContext` النهائيّ **مطابق حرفيًّا** لناتج نسخة الإنتاج الحاليّة.

قيود إضافيّة تحصر الأثر حتّى عند وجود بيانات:
- يُحتسَب فقط للمناصب `IsActive` الحاملة `reports.view` أو `dashboard.view`.
- **توسيع رؤية فقط**: لا يمسّ `CurrentApproverId` ولا أيّ مسار اعتماد.
- `positions.manage` مقصورة على `AdminOnly`.

## 4. الجرد المضادّ: هل فات المرشّح شيء ممّا حذفه الإنتاج؟

فُحصت الملفّات الـ34 التي حذفها `83d7f8f`: **0 منها مفقود** من المرشّح.
وأثناء الفحص اكتُشف **متبقٍّ ثالث** من صنف «الحذف الصامت النقيّ» —
`RoleMatrixCapabilitiesTests.cs` — واستُعيد من `10c26f7` (السطر أعلاه). لا متبقّيات أخرى.

## 5. الحكم

**Production Live Feature Regression = 0** بالنسبة لهاتين الاستعادتين، بدليل بنيويّ (فرع خروج مبكر)
لا إحصائيّ. الاستعادتان تُبقيان ميزة develop حيّة دون أن تمسّا الإنتاج المنشور.
