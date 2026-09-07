#!/usr/bin/env python3
# RPT-APPROVER-INTEGRITY-01 / المرحلة 6 — تعقيم مصنوعات الأدلّة النصّيّة:
# استبدال أسماء الأشخاص/البُرُد/الاعتمادات الحرفيّة بعناصر نائبة، بلا طباعة أيّ قيمة حسّاسة.
import os, re, glob, unicodedata

ROOT = '/private/tmp/prod-verify'
OUT = os.path.join(ROOT, 'evidence-sanitized')
os.makedirs(OUT, exist_ok=True)

DIAC = re.compile(r'[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640]')
AR = r'\u0600-\u06FF'
STOP = {'admin', 'test', 'user', 'system', 'ceo', 'gm', 'hr', 'مدير', 'المدير', 'النظام', 'نظام',
        'العام', 'عام', 'الحساب', 'حساب', 'مستخدم', 'المستخدم', 'قائد', 'فريق', 'الفريق',
        'موظف', 'الموظف', 'تقرير', 'التقرير', 'دعم'}

names, toks = set(), set()
for line in open('/tmp/pii-matchlist.txt', encoding='utf-8'):
    v = DIAC.sub('', unicodedata.normalize('NFKC', line)).strip()
    if len(v) < 3:
        continue
    if ' ' in v:
        names.add(v)
    for p in re.split(r'[@._\- ]', v):
        if len(p) >= 3 and p.lower() not in STOP:
            toks.add(p)

KEY = re.compile(r'(?i)\b([A-Za-z_]*(?:password|passwd|pwd|secret)[A-Za-z_]*)\s*(=|:)\s*([\'"`])(?:(?!\3).)*\3')
EMAIL = re.compile(r'[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}')

def sanitize(txt):
    n = 0
    txt, k = KEY.subn(lambda m: '%s%s process.env.RPT_PROD_SECRET' % (m.group(1), m.group(2)), txt)
    n += k
    txt, k = EMAIL.subn('[REDACTED_EMAIL]', txt)
    n += k
    for nm in sorted(names, key=len, reverse=True):
        txt, k = re.subn(re.escape(nm), '[REDACTED_NAME]', txt)
        n += k
    for t in sorted(toks, key=len, reverse=True):
        # السوابق العربيّة (و/ف/ب/ل/ك/ال/لل) تلتصق بالاسم ⇒ لا حدّ يساريّ للأسماء ≥4 حروف
        left = '' if len(t) >= 4 else r'(?<![%s\w])' % AR
        pat = re.compile(r'%s%s(?![%s\w])' % (left, re.escape(t), AR))
        txt, k = pat.subn('[REDACTED_NAME]', txt)
        n += k
    return txt, n

total, touched = 0, []
for src in sorted(glob.glob(os.path.join(ROOT, '*.mjs'))
                  + glob.glob(os.path.join(ROOT, '*.json'))
                  + glob.glob(os.path.join(ROOT, '*.txt'))):
    txt = open(src, encoding='utf-8', errors='replace').read()
    out, n = sanitize(txt)
    open(os.path.join(OUT, os.path.basename(src)), 'w', encoding='utf-8').write(out)
    total += n
    if n:
        touched.append((os.path.basename(src), n))

print('SANITIZED_ARTIFACTS=%d' % len(glob.glob(os.path.join(OUT, '*'))))
print('REPLACEMENTS=%d FILES_TOUCHED=%d' % (total, len(touched)))
for n, c in touched:
    print('  %s replacements=%d' % (n, c))
