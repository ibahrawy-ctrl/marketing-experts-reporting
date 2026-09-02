#!/usr/bin/env node
// ======================================================================
// R22B-MULTILINE-RESTORE — حارس الحزمة المبنيّة (Artifact Gate)
//
// لماذا يوجد هذا الملفّ:
// إصلاح REPORT-APPROVAL-COMMENTS-MULTILINE-HOTFIX-R1 (00b5f3a) سقط صامتًا
// في 23 أغسطس 2026 لأنّ الحارس الوحيد كان اختبارًا مكوِّنيًّا على المصدر،
// والمصدر الحاكم لم يكن يحمل الإصلاح أصلًا. الاختبار الأخضر على المصدر
// لا يُثبت شيئًا عن الحزمة التي يحمّلها المتصفّح.
//
// هذا الحارس يفحص `dist/assets/*.js` نفسها — لا المصدر — ويفشل بخروج ≠ 0.
// يُشغَّل بعد `npm run build` وقبل أيّ `rsync` إلى أيّ بيئة.
//
// الاستعمال: node scripts/verify-multiline-bundle.mjs [distDir]
// ======================================================================

import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const distDir = process.argv[2] ?? 'dist';
const assetsDir = join(distDir, 'assets');

if (!existsSync(assetsDir)) {
  console.error(`FAIL: لا يوجد مجلّد الحزم: ${assetsDir}`);
  process.exit(1);
}

const jsFiles = readdirSync(assetsDir).filter((f) => f.endsWith('.js'));
if (jsFiles.length === 0) {
  console.error(`FAIL: لا توجد حزم JS داخل ${assetsDir}`);
  process.exit(1);
}

const source = jsFiles.map((f) => readFileSync(join(assetsDir, f), 'utf8')).join('\n');
const count = (needle) => source.split(needle).length - 1;

// حقل «ملاحظة / سبب» هو المرساة: يجب أن يوجد، ويجب ألّا يكون <input>.
const PLACEHOLDER = 'اكتب سبب القرار…';

// جوار كلّ ظهور للمرساة داخل الحزمة المصغَّرة. الفحوص الموضعيّة تجري على هذا الجوار
// وحده — لا على الحزمة كلّها — وإلّا التقطت معالجات مشروعة لا علاقة لها بالتعليقات
// (تنقّل التبويبات بلوحة المفاتيح، وحقل بحث يطبّق فلترًا عند Enter).
const NEIGHBOURHOOD_RADIUS = 400;
function neighbourhoods() {
  const out = [];
  let i = source.indexOf(PLACEHOLDER);
  while (i !== -1) {
    out.push(source.slice(Math.max(0, i - NEIGHBOURHOOD_RADIUS), i + NEIGHBOURHOOD_RADIUS));
    i = source.indexOf(PLACEHOLDER, i + 1);
  }
  return out;
}
// الظهور الأوّل لبطاقة «إجراء الاعتماد» هو حقل تعليق التقرير (الآخر لقرار الإجازات، خارج النطاق).
const REPORT_FIELD = neighbourhoods().filter((n) => n.includes('إجراء الاعتماد'));

const checks = [
  {
    id: 'MULTILINE_ELEMENT',
    label: 'حقل تعليق التقرير عنصر متعدّد الأسطر (textarea) لا <input type="text">',
    ok: () =>
      REPORT_FIELD.length === 1 &&
      /textarea/.test(REPORT_FIELD[0]) &&
      /rows\s*:\s*4/.test(REPORT_FIELD[0]),
    detail: () =>
      `anchors=${REPORT_FIELD.length} · textarea=${REPORT_FIELD.some((n) => /textarea/.test(n))} · rows:4=${REPORT_FIELD.some((n) => /rows\s*:\s*4/.test(n))}`,
  },
  {
    id: 'RESIZE_Y',
    label: 'الصنف resize-y حاضر (تمدّد رأسيّ للحقل)',
    ok: () => count('resize-y') >= 1,
    detail: () => `occurrences=${count('resize-y')}`,
  },
  {
    id: 'WHITESPACE_PRE_WRAP',
    label: 'whitespace-pre-wrap حاضر (حفظ الأسطر عند العرض)',
    ok: () => count('whitespace-pre-wrap') >= 2,
    detail: () => `occurrences=${count('whitespace-pre-wrap')}`,
  },
  {
    id: 'BREAK_WORDS',
    label: 'break-words حاضر مقترنًا بـpre-wrap (منع التجاوز الأفقيّ)',
    ok: () => count('whitespace-pre-wrap break-words') >= 2,
    detail: () => `pre-wrap+break-words=${count('whitespace-pre-wrap break-words')}`,
  },
  {
    id: 'COMMENT_FIELD_PRESENT',
    label: 'حقل «ملاحظة / سبب» ما زال موجودًا في الحزمة',
    ok: () => count(PLACEHOLDER) >= 1,
    detail: () => `occurrences=${count(PLACEHOLDER)}`,
  },
  {
    id: 'NO_ENTER_BLOCKER',
    label: 'لا معالج لوحة مفاتيح ولا preventDefault في جوار حقل تعليق التقرير',
    ok: () =>
      REPORT_FIELD.length === 1 &&
      !/onKeyDown|onKeyPress|onKeyUp|preventDefault/.test(REPORT_FIELD[0]),
    detail: () => {
      const hit = REPORT_FIELD[0]?.match(/onKeyDown|onKeyPress|onKeyUp|preventDefault/)?.[0];
      return `anchors=${REPORT_FIELD.length} · blocker=${hit ?? 'none'}`;
    },
  },
];

// فحوص النشر العامّة (مطلوبة في بوّابة ما قبل TEST): عنوان الـAPI والأسرار.
const EXPECTED_API_BASE = process.env.EXPECTED_API_BASE ?? null;
if (EXPECTED_API_BASE) {
  checks.push({
    id: 'API_BASE_URL',
    label: `عنوان الـAPI في الحزمة = ${EXPECTED_API_BASE} وبلا سقوط إلى localhost`,
    ok: () => source.includes(EXPECTED_API_BASE) && !source.includes('localhost:5090'),
    detail: () =>
      `expected=${source.includes(EXPECTED_API_BASE)} · localhost:5090=${count('localhost:5090')}`,
  });
}
checks.push({
  id: 'NO_SECRETS',
  label: 'لا مفاتيح خاصّة ولا توكنات JWT مضمَّنة في الحزمة',
  ok: () => !/BEGIN [A-Z ]*PRIVATE KEY|eyJ[A-Za-z0-9_-]{25,}\.[A-Za-z0-9_-]{10,}/.test(source),
  detail: () => {
    const hit = source.match(/BEGIN [A-Z ]*PRIVATE KEY|eyJ[A-Za-z0-9_-]{25,}\.[A-Za-z0-9_-]{10,}/);
    return hit ? 'FOUND' : 'none';
  },
});

let failed = 0;
console.log(`R22B-MULTILINE-RESTORE · فحص الحزمة: ${assetsDir} (${jsFiles.length} ملفّ JS)`);
for (const c of checks) {
  const pass = c.ok();
  if (!pass) failed++;
  console.log(`  [${pass ? 'PASS' : 'FAIL'}] ${c.id} — ${c.label} · ${c.detail()}`);
}

console.log(failed === 0 ? 'BUNDLE_MULTILINE_GATE=PASS' : `BUNDLE_MULTILINE_GATE=FAIL (${failed})`);
process.exit(failed === 0 ? 0 : 1);
