#!/usr/bin/env python3
# بوّابة التحقّق: صفر تطابق PII في النسخ المموّهة (OCR ثانٍ مستقلّ).
import sys
sys.path.insert(0, '/tmp')
import importlib.util
spec = importlib.util.spec_from_file_location('r', '/tmp/redact.py')
# نعيد تعريف الدوال بدل استيراد السكربت (لتفادي تنفيذ المعالجة)
import re, unicodedata
DIAC = re.compile(r'[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640]')

def norm(s):
    s = unicodedata.normalize('NFKC', s)
    s = DIAC.sub('', s)
    s = (s.replace('أ', 'ا').replace('إ', 'ا').replace('آ', 'ا')
           .replace('ة', 'ه').replace('ى', 'ي').replace('ؤ', 'و').replace('ئ', 'ي'))
    s = re.sub(r'[^0-9a-zA-Z\u0600-\u06FF@._-]+', ' ', s)
    return re.sub(r'\s+', ' ', s).strip().lower()

STOP = {norm(w) for w in ['admin', 'test', 'user', 'system', 'ceo', 'gm', 'hr', 'مدير', 'المدير',
                          'النظام', 'نظام', 'العام', 'عام', 'الحساب', 'حساب', 'مستخدم', 'المستخدم',
                          'قائد', 'فريق', 'الفريق', 'موظف', 'الموظف', 'تقرير', 'التقرير', 'دعم']}
full, tokens = set(), set()
for line in open('/tmp/pii-matchlist.txt', encoding='utf-8'):
    v = norm(line)
    if len(v) < 3:
        continue
    full.add(v)
    for p in re.split(r'[@._\- ]', v):
        if len(p) >= 3 and p not in STOP:
            tokens.add(p)
LONG = {t for t in tokens if len(t) >= 5}

def is_pii(word):
    n = norm(word)
    if len(n) < 3 or n in STOP:
        return False
    return n in tokens or n in full or any(t in n for t in LONG) or (len(n) >= 4 and any(n in t for t in LONG))

resid = {}
files = set()
for line in open('/tmp/ocr-red.tsv', encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 6:
        continue
    files.add(p[0])
    if is_pii(p[5]):
        resid.setdefault(p[0], []).append(p[5])
print('REDACTED_FILES_OCRED=%d' % len(files))
print('RESIDUAL_PII_FILES=%d RESIDUAL_PII_WORDS=%d' % (len(resid), sum(len(v) for v in resid.values())))
for k, v in resid.items():
    print('  %s -> %s' % (k, ' | '.join(v)))
