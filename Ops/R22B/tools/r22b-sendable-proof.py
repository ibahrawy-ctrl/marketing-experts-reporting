#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""إثبات قابليّة الإرسال البنيويّة للمسودّة الإنتاجيّة — قراءة فقط، بلا أيّ كتابة.
(1) إعادة تشغيل منطق حارس تكرار المشروع نفسه (SubmissionService.ValidateRepeatableSectionsAsync)
(2) تكافؤ بنيويّ مع حمولة TEST التي أُرسِلت بنجاح فعلًا."""
import json, hashlib, sys

blocks, cur = {}, None
for line in open('/tmp/r22b-payloads.txt', encoding='utf-8'):
    line = line.rstrip('\n')
    if line.startswith('###'):
        cur = line[3:]; blocks[cur] = []
    elif cur:
        blocks[cur].append(line)
G = lambda k: '\n'.join(blocks.get(k, [])).strip()

prod = json.loads(G('PROD_PAYLOAD'));  pcfg = json.loads(G('PROD_CONFIG'))
test = json.loads(G('TEST_PAYLOAD'));  tcfg = json.loads(G('TEST_CONFIG'))
out = {'testSubmissionStatus': G('TEST_STATUS')}

def ci(d, k):  # مفاتيح الإعدادات قد تختلف في حالة الأحرف
    for kk in d:
        if kk.lower() == k.lower(): return d[kk]
    return None

def replay_duplicate_guard(entries, cfg):
    """نفس منطق SubmissionService.cs:1613-1637 — HashSet على projectId مع required."""
    required = bool(ci(cfg, 'projectRequired') or False)
    seen, errors = set(), []
    for i, e in enumerate(entries):
        pid = e.get('projectId')
        if required or pid is not None:
            if not pid:
                errors.append(f'صف {i+1}: مشروع مطلوب'); continue
            if pid in seen:
                errors.append(f'صف {i+1}: لا يمكن تكرار نفس المشروع أكثر من مرة في التقرير الواحد.'); continue
            seen.add(pid)
    return errors

def replay_workitem_guard(entries, cfg):
    """نفس منطق SubmissionService.cs:1675-1738 حرفيًّا:
       MinItems؛ MaxItems يُطبَّق فقط إذا كان > 0؛ الحقول المطلوبة؛ الحقول الرقميّة؛ UniqueBy."""
    wi = ci(cfg, 'workItems')
    errors = []
    if wi is None:
        return ['القالب بلا تعريف workItems'], None
    mn = ci(wi, 'minItems') or 0
    mx = ci(wi, 'maxItems') or 0
    fields = ci(wi, 'fields') or []
    reqkeys = [ci(f, 'key') for f in fields if ci(f, 'required')]
    numfields = [f for f in fields if str(ci(f, 'fieldType') or ci(f, 'type') or '').lower() == 'number']
    uniqueby = ci(wi, 'uniqueBy') or []
    for i, e in enumerate(entries):
        items = e.get('workItems') or []
        if len(items) < mn:
            errors.append(f'المشروع {i+1}: يجب إضافة {mn} بند عمل على الأقل.'); continue
        if mx > 0 and len(items) > mx:
            errors.append(f'المشروع {i+1}: الحدّ الأقصى {mx} بند عمل.'); continue
        seen = set()
        for j, it in enumerate(items):
            a = it.get('answers') or {}
            for k in reqkeys:
                v = a.get(k)
                if v is None or (isinstance(v, str) and not v.strip()):
                    errors.append(f'المشروع {i+1} / بند عمل {j+1}: الحقل «{k}» مطلوب.')
            for nf in numfields:
                k = ci(nf, 'key'); v = a.get(k)
                if v is None or (isinstance(v, str) and not v.strip()): continue
                try: num = float(v)
                except (TypeError, ValueError):
                    errors.append(f'المشروع {i+1} / بند عمل {j+1} الحقل «{k}»: قيمة رقميّة غير صالحة.'); continue
                mnv, mxv = ci(nf, 'min'), ci(nf, 'max')
                if ci(nf, 'integerOnly') and num != int(num):
                    errors.append(f'المشروع {i+1} / بند عمل {j+1} الحقل «{k}»: يجب إدخال عدد صحيح.')
                if mnv is not None and num < mnv:
                    errors.append(f'المشروع {i+1} / بند عمل {j+1} الحقل «{k}»: أقل من الحدّ الأدنى.')
                if mxv is not None and num > mxv:
                    errors.append(f'المشروع {i+1} / بند عمل {j+1} الحقل «{k}»: أكبر من الحدّ الأقصى.')
            if not uniqueby: continue
            sig = '\u001f'.join(str(a.get(k, '')) for k in uniqueby)
            if sig in seen:
                errors.append(f'المشروع {i+1}: تكرّر بند عمل بنفس ({"، ".join(uniqueby)}).')
            seen.add(sig)
    return errors, {'minItems': mn, 'maxItemsRaw': mx, 'maxItemsEnforced': mx > 0,
                    'requiredItemKeys': reqkeys, 'numericItemKeys': [ci(f, 'key') for f in numfields],
                    'uniqueBy': uniqueby}

def skeleton(entries):
    """الهيكل البنيويّ المجرَّد: المفاتيح وأنواعها في كلّ مستوى، بلا قيم."""
    lvl_entry, lvl_item = set(), set()
    for e in entries:
        for k, v in e.items(): lvl_entry.add((k, type(v).__name__))
        for it in (e.get('workItems') or []):
            for k, v in it.items(): lvl_item.add((k, type(v).__name__))
    return sorted(lvl_entry), sorted(lvl_item)

out['prod'] = {
    'projectEntryCount': len(prod),
    'uniqueProjectIdCount': len({e.get('projectId') for e in prod}),
    'workItemsPerProject': [len(e.get('workItems') or []) for e in prod],
    'totalWorkItemCount': sum(len(e.get('workItems') or []) for e in prod),
    'cardLevelAnswerKeys': sorted({k for e in prod for k in (e.get('answers') or {})}),
}
out['test'] = {
    'projectEntryCount': len(test),
    'uniqueProjectIdCount': len({e.get('projectId') for e in test}),
    'workItemsPerProject': [len(e.get('workItems') or []) for e in test],
    'totalWorkItemCount': sum(len(e.get('workItems') or []) for e in test),
}

dup_prod = replay_duplicate_guard(prod, pcfg)
dup_test = replay_duplicate_guard(test, tcfg)
wi_prod, wi_meta_p = replay_workitem_guard(prod, pcfg)
wi_test, wi_meta_t = replay_workitem_guard(test, tcfg)

out['duplicateGuardReplay'] = {
    'prodErrors': dup_prod, 'prodResult': 'PASS' if not dup_prod else 'FAIL',
    'testErrors': dup_test, 'testResult': 'PASS' if not dup_test else 'FAIL',
}
out['workItemGuardReplay'] = {
    'prodConfig': wi_meta_p, 'prodErrors': wi_prod, 'prodResult': 'PASS' if not wi_prod else 'FAIL',
    'testConfig': wi_meta_t, 'testErrors': wi_test, 'testResult': 'PASS' if not wi_test else 'FAIL',
}

sp_e, sp_i = skeleton(prod)
st_e, st_i = skeleton(test)
out['structuralEquivalence'] = {
    'prodEntryShape': [f'{k}:{t}' for k, t in sp_e],
    'testEntryShape': [f'{k}:{t}' for k, t in st_e],
    'entryShapeIdentical': sp_e == st_e,
    'prodItemShape': [f'{k}:{t}' for k, t in sp_i],
    'testItemShape': [f'{k}:{t}' for k, t in st_i],
    'itemShapeIdentical': sp_i == st_i,
    'prodSchemaVersion': ci(pcfg, 'schemaVersion'),
    'testSchemaVersion': ci(tcfg, 'schemaVersion'),
}

ok = (out['duplicateGuardReplay']['prodResult'] == 'PASS'
      and out['workItemGuardReplay']['prodResult'] == 'PASS'
      and out['structuralEquivalence']['entryShapeIdentical']
      and out['structuralEquivalence']['itemShapeIdentical'])
out['DUPLICATE_PROJECT_VALIDATION'] = out['duplicateGuardReplay']['prodResult']
out['SUBMISSION_STRUCTURALLY_SENDABLE'] = 'YES' if ok else 'NO'
print(json.dumps(out, ensure_ascii=False, indent=2))
