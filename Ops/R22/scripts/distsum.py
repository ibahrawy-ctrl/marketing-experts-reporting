#!/usr/bin/env python3
"""بصمة بناء الواجهة — أسماء وأحجام وmd5 فقط، بلا أيّ محتوى (احترامًا لقاعدة حجب المولَّدات).

الاستدعاء: python3 /tmp/p360r2/distsum.py <root>
"""
import hashlib
import os
import sys

root = sys.argv[1]
rows = []
for dirpath, _, names in os.walk(root):
    for n in sorted(names):
        p = os.path.join(dirpath, n)
        h = hashlib.md5(open(p, "rb").read()).hexdigest()
        rows.append((os.path.relpath(p, root), os.path.getsize(p), h))

rows.sort()
agg = hashlib.md5()
for rel, size, h in rows:
    agg.update(f"{rel}\t{size}\t{h}\n".encode())
    print(f"{h}  {size:>9}  {rel}")
print(f"\nFILES={len(rows)}  AGGREGATE_MD5={agg.hexdigest()}")
