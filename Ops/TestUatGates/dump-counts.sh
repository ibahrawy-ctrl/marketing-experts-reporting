#!/usr/bin/env bash
# يستخرج عدد صفوف جدول من نسخة pg_dump النصّيّة (كتلة COPY … FROM stdin حتى \.)
# يقبل الاسم مقتبسًا أو غير مقتبس لأنّ pg_dump يقتبس أسماء Identity فقط.
set -uo pipefail
F=${DUMP:-/root/backups/20260816-recon-l/reporting_test_uat.sql}
for t in "$@"; do
  n=$(awk -v a="^COPY public\\.\"?" -v tbl="$t" '
    $0 ~ (a tbl "\"? ") {inblk=1; next}
    inblk && $0 == "\\." {inblk=0}
    inblk {c++}
    END {print c+0}' "$F")
  echo "$t=$n"
done
