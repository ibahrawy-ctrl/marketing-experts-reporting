import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// RECONCILE-PROD-DEVELOP-LINEAGE — حارس انحدار على جدول المسارات.
// دمج نَسَبَي الإنتاج وdevelop أسقط مسارَين (/app/governance-workspace و/app/positions)
// بينما بقيت صفحاتهما وروابط التنقّل إليهما، فصار الرابط الحيّ يؤدّي إلى لا شيء.
// البناء لا يكشف ذلك لأنّ الصفحة والرابط كليهما صالحان نحويًّا؛ لذا يُفحَص التطابق نصّيًّا.

const read = (rel: string) => readFileSync(resolve(__dirname, rel), 'utf8');

const routePaths = new Set(
  [...read('./App.tsx').matchAll(/path:\s*'([^']+)'/g)].map((m) => m[1]),
);
const navTargets = [...read('./lib/navConfig.ts').matchAll(/to:\s*'(\/app[^']*)'/g)].map(
  (m) => m[1],
);

describe('سجلّ المسارات', () => {
  it('كل رابط تنقّل يقابله مسار مسجَّل في App.tsx', () => {
    expect(navTargets.length).toBeGreaterThan(0);
    expect(navTargets.filter((t) => !routePaths.has(t))).toEqual([]);
  });

  it('المسارات الحسّاسة المستعادة بعد توحيد النَسَب مسجَّلة', () => {
    for (const p of ['/app/governance-workspace', '/app/positions']) {
      expect(routePaths.has(p)).toBe(true);
    }
  });
});
