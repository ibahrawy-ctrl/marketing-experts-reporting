import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { MODULES, ROUTE_ALIASES, canonicalPath, resolveActive } from './lib/navConfig';

// RECONCILE-PROD-DEVELOP-LINEAGE — حارس انحدار على جدول المسارات.
// دمج نَسَبَي الإنتاج وdevelop أسقط مسارَين (/app/governance-workspace و/app/positions)
// بينما بقيت صفحاتهما وروابط التنقّل إليهما، فصار الرابط الحيّ يؤدّي إلى لا شيء.
// البناء لا يكشف ذلك لأنّ الصفحة والرابط كليهما صالحان نحويًّا؛ لذا يُفحَص التطابق نصّيًّا.
//
// P3-NAV-003 — وُسِّع إلى **بيان حفظ المسارات**: يُقرأ `App.tsx` نصّيًّا (لا استيرادًا، كي لا
// يُحمَّل كلّ الصفحات) ويُقارَن بسجلّ الملاحة نفسه: كلّ وجهة عنصر مسجَّلة، وكلّ alias يحلّ إلى
// مسار مسجَّل، ولا alias يحجب مسارًا حقيقيًّا.

const read = (rel: string) => readFileSync(resolve(__dirname, rel), 'utf8');

const appSource = read('./App.tsx');
const routePaths = new Set([...appSource.matchAll(/path:\s*'([^']+)'/g)].map((m) => m[1]));

const navTargets = MODULES.flatMap((m) => m.items.map((i) => i.target));

describe('سجلّ المسارات', () => {
  it('كل وجهة تنقّل يقابلها مسار مسجَّل في App.tsx', () => {
    expect(navTargets.length).toBeGreaterThan(0);
    expect(navTargets.filter((t) => !routePaths.has(t))).toEqual([]);
  });

  it('المسارات الحسّاسة المستعادة بعد توحيد النَسَب مسجَّلة', () => {
    for (const p of ['/app/governance-workspace', '/app/positions']) {
      expect(routePaths.has(p)).toBe(true);
    }
  });

  it('كل alias يحلّ إلى مسار مسجَّل فعلًا', () => {
    expect(ROUTE_ALIASES.length).toBeGreaterThan(0);
    expect(ROUTE_ALIASES.filter((a) => !routePaths.has(a.to))).toEqual([]);
  });

  it('لا alias يحجب مسارًا حقيقيًّا مسجَّلًا في App.tsx', () => {
    expect(ROUTE_ALIASES.filter((a) => routePaths.has(a.from)).map((a) => a.from)).toEqual([]);
  });

  it('لا تكرار في مصادر الـaliases', () => {
    const froms = ROUTE_ALIASES.map((a) => a.from);
    expect(froms.length).toBe(new Set(froms).size);
  });

  it('كل alias يُحَلّ إلى وجهته المرجعيّة ويُبرِز عنصرها', () => {
    for (const a of ROUTE_ALIASES) {
      expect(canonicalPath(a.from)).toBe(a.to);
      expect(resolveActive(a.from)?.item.id).toBe(a.itemId);
    }
  });

  it('معرّفات عناصر التنقّل فريدة عبر الوحدات كلّها', () => {
    const ids = MODULES.flatMap((m) => m.items.map((i) => i.id));
    expect(ids.length).toBe(new Set(ids).size);
  });

  it('لا وجهة مكرّرة بين عنصرين (لا رابطان لنفس الشاشة)', () => {
    expect(navTargets.length).toBe(new Set(navTargets).size);
  });
});
