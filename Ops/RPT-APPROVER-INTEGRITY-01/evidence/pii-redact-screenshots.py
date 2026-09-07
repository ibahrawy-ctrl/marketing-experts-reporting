#!/usr/bin/env python3
# RPT-APPROVER-INTEGRITY-01 / المرحلة 6 — تمويه PII في لقطات الأدلّة (على مستوى الكلمة).
# المصدر: Vision OCR (/tmp/ocr-words.tsv) + قائمة المطابقة من الإنتاج (/tmp/pii-matchlist.txt).
import os, re, sys, unicodedata
from PIL import Image, ImageDraw

ROOT = '/private/tmp/prod-verify'
DIAC = re.compile(r'[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640]')

def norm(s):
    s = unicodedata.normalize('NFKC', s)
    s = DIAC.sub('', s)
    s = (s.replace('أ', 'ا').replace('إ', 'ا').replace('آ', 'ا')
           .replace('ة', 'ه').replace('ى', 'ي').replace('ؤ', 'و').replace('ئ', 'ي'))
    s = re.sub(r'[^0-9a-zA-Z\u0600-\u06FF@._-]+', ' ', s)
    return re.sub(r'\s+', ' ', s).strip().lower()

# كلمات وظيفيّة/تسميات أدوار — ليست هويّة شخصيّة ولا تُموَّه
STOP = {norm(w) for w in ['admin', 'test', 'user', 'system', 'ceo', 'gm', 'hr', 'مدير', 'المدير',
                          'النظام', 'نظام', 'العام', 'عام', 'الحساب', 'حساب', 'مستخدم', 'المستخدم',
                          'قائد', 'فريق', 'الفريق', 'موظف', 'الموظف', 'تقرير', 'التقرير', 'دعم']}

full, tokens = set(), set()
for line in open('/tmp/pii-matchlist.txt', encoding='utf-8'):
    v = norm(line)
    if len(v) < 3:
        continue
    full.add(v)
    parts = re.split(r'[@._\- ]', v)
    for p in parts:
        if len(p) >= 3 and p not in STOP:
            tokens.add(p)

LONG = {t for t in tokens if len(t) >= 5}

def is_pii(word):
    n = norm(word)
    if len(n) < 3 or n in STOP:
        return False
    if n in tokens or n in full:
        return True
    if any(t in n for t in LONG):
        return True
    if len(n) >= 4 and any(n in t for t in LONG):
        return True
    return False

boxes = {}
for line in open('/tmp/ocr-words.tsv', encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 6:
        continue
    boxes.setdefault(p[0], []).append((int(p[1]), int(p[2]), int(p[3]), int(p[4]), '\t'.join(p[5:])))

files = [l.strip() for l in open('/tmp/pii-filelist.txt', encoding='utf-8') if l.strip()]
mode = sys.argv[1] if len(sys.argv) > 1 else 'plan'
prefix = sys.argv[2] if len(sys.argv) > 2 else ''

total, flagged, per_file = 0, set(), {}
for rel in files:
    src = os.path.join(ROOT, prefix + rel)
    masks = []
    for (x, y, w, h, t) in boxes.get(rel, []):
        if is_pii(t):
            masks.append((x, y, w, h))
            flagged.add(t.strip())
    per_file[rel] = len(masks)
    total += len(masks)
    if mode != 'apply':
        continue
    d0 = os.path.dirname(rel)
    dst = os.path.join(ROOT, (d0 + '-redacted') if d0 else 'login-redacted', os.path.basename(rel))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im = Image.open(src).convert('RGB')
    dr = ImageDraw.Draw(im)
    W, H = im.size
    for (x, y, w, h) in masks:
        pad = 3
        dr.rectangle([max(0, x - pad), max(0, y - pad), min(W, x + w + pad), min(H, y + h + pad)],
                     fill=(17, 17, 17))
    im.save(dst)

print('FILES=%d MASKED_WORDS=%d FILES_WITH_MASKS=%d' % (
    len(files), total, sum(1 for v in per_file.values() if v)))
print('ZERO_MASK_FILES=' + ','.join(k for k, v in per_file.items() if v == 0))
print('DISTINCT_FLAGGED_WORDS=%d' % len(flagged))
print(' | '.join(sorted(flagged)))
