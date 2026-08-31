#!/usr/bin/env python3
"""R22B — يولّد إعداد قسم المشاريع الهدف (schemaVersion=2) لكل قالب INCLUDE.

قراءة فقط: يقرأ اللقطات المصدرية المصدَّرة من قواعد البيانات ويكتب حمولات
الهدف تحت Ops/R22B/payloads/target/. لا يتصل بأي قاعدة بيانات ولا ينشر شيئًا.

عقد بنود العمل مأخوذ حرفيًّا من قالب كاتب المحتوى v9 المنشور في الإنتاج (R22A).
"""

import hashlib
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "payloads" / "source"
DST = ROOT / "payloads" / "target"

# عقد بنود العمل — منسوخ حرفيًّا من كاتب المحتوى v9 (الإنتاج)، بلا أي تعديل.
WORK_ITEMS_CONTRACT = {
    "key": "workItems",
    "label": "بنود العمل",
    "addLabel": "+ إضافة بند عمل",
    "itemLabel": "بند عمل",
    "minItems": 1,
    "maxItems": 0,
    "uniqueBy": [],
}

# القوالب المشمولة، وطريقة اشتقاق حقول بند العمل لكل واحد.
#   "all"  = كل حقول المشروع تنتقل كما هي إلى بند العمل، وتبقى fields فارغة.
#   "grid" = الحقل المذكور (شبكة) يتحوّل إلى حقول بند العمل، وباقي الحقول تبقى حقول مشروع.
INCLUDED = {
    "تقرير فريق الفيديو": {"mode": "all"},
    "تقرير فريق التصميم": {"mode": "all"},
    "تقرير المديرشن الأسبوعي": {"mode": "all"},
    "تقرير متابعة مقالات SEO الأسبوعي": {"mode": "grid", "gridKey": "articles_grid"},
}

# خريطة أعمدة articles_grid → حقول بند العمل.
# التسميات والترتيب محفوظة حرفيًّا كما في الأعمدة الحالية؛ المفاتيح مُشتقّة من الدلالة.
# الأنواع أمينة لشكل خلايا الشبكة اليوم (نص حر) ⟹ لا ترقية دلالية ولا اختراع خيارات.
SEO_ARTICLE_ITEM_FIELDS = [
    ("article_title", "عنوان المقال", "ShortText"),
    ("target_keyword", "الكلمة المفتاحية", "ShortText"),
    ("article_status", "الحالة", "ShortText"),
    ("reviewer", "المراجع", "ShortText"),
    ("delivery_date", "تاريخ التسليم", "ShortText"),
    ("notes", "ملاحظات", "LongText"),
]


def canon(obj) -> str:
    return json.dumps(obj, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def md5(obj) -> str:
    return hashlib.md5(canon(obj).encode("utf-8")).hexdigest()


def build_target(config: dict, spec: dict) -> dict:
    src_fields = config.get("fields") or []
    out = {k: v for k, v in config.items() if k not in ("fields", "workItems", "schemaVersion")}

    if spec["mode"] == "all":
        project_fields = []
        item_fields = [dict(f) for f in src_fields]
    else:
        grid_key = spec["gridKey"]
        grid = next((f for f in src_fields if f.get("key") == grid_key), None)
        if grid is None:
            raise SystemExit(f"الشبكة {grid_key} غير موجودة في الإعداد المصدر")
        cols = grid.get("columns") or []
        expected = [lbl for _, lbl, _ in SEO_ARTICLE_ITEM_FIELDS]
        if cols != expected:
            raise SystemExit(f"أعمدة الشبكة تغيّرت عن المقيس: {cols!r} != {expected!r}")
        project_fields = [dict(f) for f in src_fields if f.get("key") != grid_key]
        item_fields = [
            {"key": k, "type": t, "label": lbl, "columns": None,
             "options": None, "required": False}
            for k, lbl, t in SEO_ARTICLE_ITEM_FIELDS
        ]

    out["fields"] = project_fields
    out["schemaVersion"] = 2
    out["workItems"] = {**WORK_ITEMS_CONTRACT, "fields": item_fields}
    return out


def main() -> None:
    DST.mkdir(parents=True, exist_ok=True)

    # الإعداد المرجعيّ للإنتاج — يُستعمل لبذر بيئة بائتة لا تحمل نفس البنية.
    prod_rows = json.loads((SRC / "effective-reporting_prod.json").read_text(encoding="utf-8"))
    prod_by_title = {r["title"]: r["config"] for r in prod_rows}

    summary = []
    for src_path in sorted(SRC.glob("effective-*.json")):
        db = src_path.stem.replace("effective-", "")
        rows = json.loads(src_path.read_text(encoding="utf-8"))
        for row in rows:
            spec = INCLUDED.get(row["title"])
            if spec is None:
                continue
            before = row["config"]
            seeded = False
            if spec["mode"] == "grid":
                keys = {f.get("key") for f in (before.get("fields") or [])}
                if spec["gridKey"] not in keys:
                    # بيئة بائتة: تُبذَر من إعداد الإنتاج لتكون مرآة تمثيليّة للقبول.
                    before = prod_by_title[row["title"]]
                    seeded = True
            after = build_target(before, spec)
            rec = {
                "db": db,
                "title": row["title"],
                "templateId": row["template_id"],
                "fromVersion": row["version"],
                "fromVersionId": row["version_id"],
                "projectSectionFieldKey": row["field_key"],
                "projectSectionFieldLabel": row["field_label"],
                "seededFromProduction": seeded,
                "actualStaleConfig": row["config"] if seeded else None,
                "before": before,
                "after": after,
                "beforeMd5": md5(before),
                "afterMd5": md5(after),
                "projectFieldsBefore": [f["key"] for f in (before.get("fields") or [])],
                "projectFieldsAfter": [f["key"] for f in after["fields"]],
                "workItemFieldsAfter": [f["key"] for f in after["workItems"]["fields"]],
            }
            out = DST / f"{db}--{row['template_id']}.json"
            out.write_text(json.dumps(rec, ensure_ascii=False, indent=2), encoding="utf-8")
            summary.append(rec)

    for r in summary:
        print(f"{r['db']:20s} v{r['fromVersion']} {r['title']}")
        print(f"    مشروع: {r['projectFieldsBefore']} -> {r['projectFieldsAfter']}")
        print(f"    بنود : {r['workItemFieldsAfter']}")
        print(f"    md5  : {r['beforeMd5'][:12]} -> {r['afterMd5'][:12]}")


if __name__ == "__main__":
    main()
