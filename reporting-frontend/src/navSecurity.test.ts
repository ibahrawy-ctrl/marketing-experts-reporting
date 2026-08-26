import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Role } from './types/api';
import { MODULES, PERMISSIONS, ROUTE_ALIASES, isItemVisible, type NavCtx } from './lib/navConfig';

// ===== P3-SEC-005 — بوّابة أمنيّة: القائمة لا تُعلن بابًا مُقفَلًا =====
//
// القائمة ليست تصريحًا؛ الحارس هو `ProtectedRoute` وسياسات الخادم. لكن إظهار رابط
// لسطحٍ يُصفَع صاحبه عند فتحه عطبٌ في ذاته: يوهم بصلاحيّة غير قائمة ويكشف وجود
// أسطح لا تخصّ المستخدم. لذا الثابت المفروض هنا:
//
//   { الأدوار التي ترى الرابط }  ⊆  { الأدوار التي يسمح لها حارس المسار بفتحه }
//
// وهو اتّجاه واحد عمدًا: التضييق مسموح (القائمة قد تُخفي ما يُسمح بفتحه مباشرةً)،
// والتوسيع ممنوع. ويُقاس على **أسوأ حالة**: نمنح المستخدم كلّ القدرات وكلّ المسمّيات
// الوظيفيّة، فإن ظهر الرابط عندئذٍ لدور لا يسمح به الحارس فهو تسريب عرضٍ حقيقيّ.

const ALL_ROLES: Role[] = [
  'Admin',
  'CEO',
  'GeneralManager',
  'Manager',
  'TeamLeader',
  'Employee',
  'CeoSupport',
  'HR',
  'Viewer',
  'FinanceManager',
  'Accountant',
  'AccountPortfolioReader',
];

const appSource = readFileSync(resolve(__dirname, './App.tsx'), 'utf8');

/// أسماء ثوابت الأدوار في `App.tsx` وقيمها (مع فكّ النشر `...OTHER`).
const roleConsts = new Map<string, Role[]>();
for (const m of appSource.matchAll(/const ([A-Z_0-9]+): Role\[\] = \[([^\]]*)\]/g)) {
  const values: Role[] = [];
  for (const part of m[2].split(',').map((p) => p.trim()).filter(Boolean)) {
    if (part.startsWith('...')) values.push(...(roleConsts.get(part.slice(3)) ?? []));
    else values.push(part.replace(/'/g, '') as Role);
  }
  roleConsts.set(m[1], values);
}

/// حارس كلّ مسار: `null` يعني «كلّ مصادَق عليه» (لا قيد أدوار).
const routeGuards = new Map<string, Role[] | null>();
for (const m of appSource.matchAll(/\{\s*path:\s*'([^']+)'[^}]*?\}/g)) {
  const entry = m[0];
  const rolesRef = /roles:\s*([A-Z_0-9]+)/.exec(entry);
  routeGuards.set(m[1], rolesRef ? (roleConsts.get(rolesRef[1]) ?? []) : null);
}

/// أوسع سياق ممكن لدور واحد: كلّ القدرات، كلّ النطاقات المطروقة، كلّ المسمّيات.
function widestCtx(role: Role, scopeType: NavCtx['scopeType']): NavCtx {
  return {
    authenticated: true,
    hasAnyRole: (...r: Role[]) => r.includes(role),
    permissions: new Set(Object.values(PERMISSIONS)),
    scopeType,
    isSalesRep: true,
    isSalesB2cTeamLeader: true,
    jobRoleCode: 'ACCOUNT_MGR',
  };
}

const SCOPES: NavCtx['scopeType'][] = ['own', 'team', 'department', 'company', 'governance', null];

/// هل يمكن لهذا الدور أن يرى الرابط تحت أيّ نطاق؟
function canSee(item: (typeof MODULES)[number]['items'][number], role: Role): boolean {
  return SCOPES.some((s) => isItemVisible(item, widestCtx(role, s)));
}

describe('P3-SEC-005 — القائمة لا تتجاوز حارس المسار', () => {
  it('ثوابت الأدوار وحرّاس المسارات قُرِئت فعلًا (وإلّا فالفحص أجوف)', () => {
    expect(roleConsts.size).toBeGreaterThan(10);
    expect(routeGuards.size).toBeGreaterThan(50);
    expect(roleConsts.get('CLIENT_360_ROLES')).toContain('AccountPortfolioReader');
  });

  it('كل دور يرى رابطًا يستطيع فتح مساره', () => {
    const leaks: string[] = [];
    for (const m of MODULES) {
      for (const item of m.items) {
        const guard = routeGuards.get(item.target);
        if (guard === undefined) {
          leaks.push(`${item.id}: المسار ${item.target} غير مسجَّل`);
          continue;
        }
        if (guard === null) continue; // مفتوح لكلّ مصادَق عليه
        for (const role of ALL_ROLES) {
          if (canSee(item, role) && !guard.includes(role)) {
            leaks.push(`${item.id}: الدور ${role} يرى الرابط ولا يستطيع فتح ${item.target}`);
          }
        }
      }
    }
    expect(leaks).toEqual([]);
  });

  it('لا مسار تطبيقيّ يُصيَّر خارج الغلاف المحميّ', () => {
    // الحراسة العرضيّة تُطبَّق عبر `Protected` على **كلّ** عنصر في `APP_ROUTES` دفعةً واحدة،
    // فالخطر ليس في مسار محروس خطأً بل في مسار يُكتَب حرفيًّا خارج الحلقة فينجو من الغلاف.
    // لذا الفحص على شكل المصدر: المسارات الحرفيّة الوحيدة المسموحة هي العامّة صراحةً.
    const literal = [...appSource.matchAll(/<Route\s+path="([^"]+)"/g)].map((m) => m[1]);
    expect(literal.sort()).toEqual(['/', '/login']);
  });

  it('كل alias يرث حارس وجهته المرجعيّة (لا التفاف على الحراسة)', () => {
    // الـalias يُصيَّر كتحويل خالص إلى الوجهة، والوجهة وحدها تحمل `ProtectedRoute`.
    // فيكفي إثبات أنّ الوجهة مسجَّلة بحارسها وأنّ الـalias ليس مسارًا مستقلًّا له حارس آخر.
    for (const a of ROUTE_ALIASES) {
      expect(routeGuards.has(a.to)).toBe(true);
      expect(routeGuards.has(a.from)).toBe(false);
    }
  });

  it('عناصر القدرات لا تُرى بلا قدرتها مهما كان الدور', () => {
    const gated = MODULES.flatMap((m) => m.items).filter((i) => i.permissionsAny || i.permissionsAll);
    expect(gated.length).toBeGreaterThan(0);
    for (const item of gated) {
      for (const role of ALL_ROLES) {
        for (const scope of SCOPES) {
          const ctx: NavCtx = { ...widestCtx(role, scope), permissions: new Set<string>() };
          expect(isItemVisible(item, ctx)).toBe(false);
        }
      }
    }
  });

  it('لا شيء يظهر بلا جلسة مصادَق عليها', () => {
    for (const m of MODULES) {
      for (const item of m.items) {
        expect(isItemVisible(item, { ...widestCtx('Admin', 'company'), authenticated: false })).toBe(false);
      }
    }
  });
});
