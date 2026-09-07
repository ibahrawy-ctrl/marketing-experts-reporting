#!/usr/bin/env python3
# إخفاء PII في الموضع داخل النسخ المموّهة، اعتمادًا على جولة OCR جديدة (تكرار حتّى الصفر).
import sys, re, unicodedata
from PIL import Image, ImageDraw

TSV = sys.argv[1]
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

def is_pii(w):
    n = norm(w)
    if len(n) < 3 or n in STOP:
        return False
    return n in tokens or n in full or any(t in n for t in LONG) or (len(n) >= 4 and any(n in t for t in LONG))

hits = {}
for line in open(TSV, encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 6 or not is_pii(p[5]):
        continue
    hits.setdefault(p[0], []).append(tuple(map(int, p[1:5])))

n = 0
for path, boxes in hits.items():
    im = Image.open(path).convert('RGB')
    dr = ImageDraw.Draw(im)
    W, H = im.size
    for (x, y, w, h) in boxes:
        pad = 6
        dr.rectangle([max(0, x - pad), max(0, y - pad), min(W, x + w + pad), min(H, y + h + pad)],
                     fill=(17, 17, 17))
        n += 1
    im.save(path)
print('INPLACE_FILES=%d INPLACE_BOXES=%d' % (len(hits), n))
