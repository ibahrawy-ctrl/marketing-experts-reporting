import { describe, it, expect } from 'vitest';
import type { Role } from '../types/api';
import {
  FEATURES,
  MODULES,
  PERMISSIONS,
  accessibleModules,
  buildBreadcrumbs,
  canonicalPath,
  isItemVisible,
  isPathVisible,
  itemTarget,
  moduleTarget,
  resolveActive,
  resolveBadge,
  searchableItems,
  visibleItems,
  type NavCtx,
  type NavItem,
  type ScopeType,
} from './navConfig';

// ===== P3-NAV-001/002 — سجلّ الملاحة: الظهور مشتقّ من قدرات الخادم لا من التخمين =====
// كلّ ما هنا **عرضٌ فقط**: تضييق الظهور لا يمنع أحدًا من الوصول (الحارس خادميّ)،
// لكن توسيعه يعرض على المستخدم بابًا يُصفَع به. لذا الاحتياطيّ الآمن: الغموض ⇒ إخفاء.

function ctx(over: Partial<NavCtx> & { roles?: Role[] } = {}): NavCtx {
  const roles = over.roles ?? [];
  return {
    authenticated: true,
    hasAnyRole: (...r: Role[]) => r.some((x) => roles.includes(x)),
    permissions: new Set<string>(),
    // الميزات مفتوحة افتراضيًّا في هذا المساعد عمدًا: بوّابة الميزة بُعد **مستقلّ** يُختبَر
    // في كتلته الخاصّة أدناه، فلا تُلوِّث نتائجُها فحوصَ الأدوار والصلاحيّات والنطاق.
    features: new Set<string>(Object.values(FEATURES)),
    scopeType: null,
    isSalesRep: false,
    isSalesB2cTeamLeader: false,
    jobRoleCode: null,
    ...over,
  };
}

const findItem = (id: string): NavItem => {
  const item = MODULES.flatMap((m) => m.items).find((i) => i.id === id);
  if (!item) throw new Error(`عنصر غير موجود في السجلّ: ${id}`);
  return item;
};

describe('البنية الثابتة للوحدات السبع', () => {
  it('سبع وحدات بالضبط بترتيب ومعرّفات ثابتة', () => {
    expect(MODULES.map((m) => m.id)).toEqual([
      'home',
      'people',
      'reports',
      'performance',
      'governance',
      'portfolio',
      'settings',
    ]);
  });

  it('الترتيب والأيقونة لا يتغيّران بتغيّر الدور', () => {
    const asAdmin = accessibleModules(ctx({ roles: ['Admin'] }));
    const asEmployee = accessibleModules(ctx({ roles: ['Employee'] }));
    for (const list of [asAdmin, asEmployee]) {
      const orders = list.map((m) => m.order);
      expect([...orders]).toEqual([...orders].sort((a, b) => a - b));
      for (const m of list) {
        expect(m.icon).toBe(MODULES.find((x) => x.id === m.id)?.icon);
      }
    }
  });

  it('أقسام كلّ وحدة مرتّبة تصاعديًّا بحقل الترتيب', () => {
    for (const m of MODULES) {
      const items = visibleItems(m, ctx({ roles: ['Admin'] }));
      const orders = items.map((i) => i.order);
      expect([...orders]).toEqual([...orders].sort((a, b) => a - b));
    }
  });
});

describe('شروط الظهور', () => {
  it('الجلسة غير المصادَق عليها لا ترى شيئًا إطلاقًا', () => {
    expect(accessibleModules(ctx({ authenticated: false, roles: ['Admin'] }))).toEqual([]);
  });

  it('وحدة بلا أيّ قسم ظاهر تختفي (ما لم تكن دائمة الظهور)', () => {
    const asEmployee = accessibleModules(ctx({ roles: ['Employee'] })).map((m) => m.id);
    expect(asEmployee).toContain('home');
    expect(asEmployee).toContain('people');
    expect(asEmployee).not.toContain('settings');
  });

  it('قدرة غائبة ⇒ إخفاء، وقدرة حاضرة ⇒ ظهور (الاحتياطيّ الآمن)', () => {
    const item = findItem('people.hr-operations');
    expect(isItemVisible(item, ctx({ roles: ['Admin'] }))).toBe(false);
    expect(
      isItemVisible(item, ctx({ roles: ['Employee'], permissions: new Set([PERMISSIONS.hrOperationsView]) })),
    ).toBe(true);
  });

  it('تعدّد الأدوار = اتّحاد ما يراه كلّ دور منفردًا', () => {
    const single = new Set(searchableItems(ctx({ roles: ['Employee'] })).map((i) => i.to));
    const admin = new Set(searchableItems(ctx({ roles: ['Admin'] })).map((i) => i.to));
    const both = new Set(searchableItems(ctx({ roles: ['Employee', 'Admin'] })).map((i) => i.to));
    for (const to of [...single, ...admin]) expect(both.has(to)).toBe(true);
  });

  it('المسمّى الوظيفيّ شرط إضافيّ لا بديل عن الدور', () => {
    const item = findItem('portfolio.account');
    // الدور وحده لا يكفي…
    expect(isItemVisible(item, ctx({ roles: ['AccountPortfolioReader'] }))).toBe(false);
    // …والمسمّى وحده لا يكفي (وإلّا ظهر رابط يمنعه حارس المسار عند فتحه)…
    expect(isItemVisible(item, ctx({ roles: ['Employee'], jobRoleCode: 'ACCOUNT_MGR' }))).toBe(false);
    // …ويلزم اجتماعهما.
    expect(isItemVisible(item, ctx({ roles: ['AccountPortfolioReader'], jobRoleCode: 'ACCOUNT_MGR' }))).toBe(true);
  });
});

// ===== P123-R1 — بوّابة توفّر الميزة =====
// السؤال الذي تجيب عنه هذه الكتلة وحدها: هل تعرض القائمةُ رابطًا يردّ عليه الخادم 404 حتمًا
// لأنّ الميزة مطفأة في هذه البيئة؟ الفارق عن كتلة «شروط الظهور» جوهريّ: تلك تسأل «هل يملك
// المستخدم السطح؟» وهذه تسأل «هل السطح مفتوح أصلًا؟» — وخلطهما هو أصل قراءة «إغلاق» كـ«خطأ».
describe('P123-R1 — توفّر الميزة شرط سابق على كلّ شرط آخر', () => {
  const NO_FEATURES = new Set<string>();
  const GATED: ReadonlyArray<[string, string]> = [
    ['people.me', FEATURES.employee360],
    ['people.attendance', FEATURES.attendance],
    ['people.hr-operations', FEATURES.hrOperations],
  ];

  it('كلّ عنصر مشروط بميزة يختفي حين تكون مطفأة — مهما اتّسع الدور والصلاحيّة', () => {
    for (const [id] of GATED) {
      const item = findItem(id);
      const widest = ctx({
        roles: ['Admin'],
        permissions: new Set(Object.values(PERMISSIONS)),
        scopeType: 'company',
        features: NO_FEATURES,
      });
      expect(isItemVisible(item, widest)).toBe(false);
    }
  });

  it('إشعال الميزة لا يمنح شيئًا: الصلاحيّة تبقى شرطًا مستقلًّا', () => {
    const item = findItem('people.hr-operations');
    // الميزة مفتوحة والصلاحيّة غائبة ⇒ إخفاء (الخادم يردّ 403 لو فُتِح المسار).
    expect(isItemVisible(item, ctx({ roles: ['Admin'], features: new Set([FEATURES.hrOperations]) }))).toBe(false);
    // الصلاحيّة حاضرة والميزة مطفأة ⇒ إخفاء أيضًا (الخادم يردّ 404).
    expect(
      isItemVisible(item, ctx({
        roles: ['Employee'],
        permissions: new Set([PERMISSIONS.hrOperationsView]),
        features: NO_FEATURES,
      })),
    ).toBe(false);
    // اجتماعهما وحده يُظهِر الرابط.
    expect(
      isItemVisible(item, ctx({
        roles: ['Employee'],
        permissions: new Set([PERMISSIONS.hrOperationsView]),
        features: new Set([FEATURES.hrOperations]),
      })),
    ).toBe(true);
  });

  it('كلّ ميزة تحكم عنصرها وحده — لا إطفاء جانبيّ', () => {
    for (const [id, key] of GATED) {
      const only = ctx({
        roles: ['Admin'],
        permissions: new Set(Object.values(PERMISSIONS)),
        features: new Set([key]),
      });
      expect(isItemVisible(findItem(id), only)).toBe(true);
      for (const [otherId] of GATED.filter(([g]) => g !== id))
        expect(isItemVisible(findItem(otherId), only)).toBe(false);
    }
  });

  it('«الموظفون» تبقى ظاهرة بكلّ الأعلام مطفأة (خدمات الموظّف بلا بوّابة ميزة)', () => {
    const modules = accessibleModules(ctx({ roles: ['Employee'], features: NO_FEATURES })).map((m) => m.id);
    expect(modules).toContain('people');
    const ids = visibleItems(MODULES.find((m) => m.id === 'people')!, ctx({ roles: ['Employee'], features: NO_FEATURES })).map((i) => i.id);
    expect(ids).not.toContain('people.me');
    expect(ids).not.toContain('people.attendance');
    expect(ids).toContain('people.leaves');
  });

  it('مسار الميزة المطفأة ليس مسارًا ظاهرًا (لا يُقترَح في البحث ولا يُعَدّ مملوكًا)', () => {
    const off = ctx({ roles: ['Admin'], permissions: new Set(Object.values(PERMISSIONS)), features: NO_FEATURES });
    expect(isPathVisible('/app/attendance', off)).toBe(false);
    expect(isPathVisible('/app/employee/me', off)).toBe(false);
    expect(searchableItems(off).map((i) => i.to)).not.toContain('/app/attendance');
  });

  it('عدّاد عنصر مشروط بميزة مطفأة لا يُعرَض إطلاقًا', () => {
    const item = findItem('people.hr-operations');
    const off = ctx({
      roles: ['Admin'],
      permissions: new Set([PERMISSIONS.hrOperationsView]),
      features: NO_FEATURES,
    });
    expect(resolveBadge(item, off, { hrOpsQueue: 7 })).toBeNull();
  });

  it('مفاتيح FEATURES تطابق ما يُسنَد فعلًا في السجلّ (لا مفتاح مخترَع)', () => {
    const known = new Set<string>(Object.values(FEATURES));
    for (const item of MODULES.flatMap((m) => m.items))
      if (item.featureKey) expect(known.has(item.featureKey)).toBe(true);
  });
});

describe('الوجهة السياقيّة (وضع الذات)', () => {
  const dashboard = findItem('performance.dashboard');

  it('نطاق الذات يقود إلى السطح الشخصيّ، وغيره إلى اللوحة الموحّدة', () => {
    expect(itemTarget(dashboard, ctx({ scopeType: 'own' }))).toBe('/app/my-kpi');
    for (const scope of ['team', 'department', 'company', 'governance'] as ScopeType[]) {
      expect(itemTarget(dashboard, ctx({ scopeType: scope }))).toBe('/app/performance');
    }
  });

  it('كلا المسارين يُبرِزان العنصر نفسه (لا يفقد المستخدم موضعه)', () => {
    expect(resolveActive('/app/my-kpi')?.item.id).toBe('performance.dashboard');
    expect(resolveActive('/app/performance')?.item.id).toBe('performance.dashboard');
    expect(resolveActive('/app/kpi')?.item.id).toBe('performance.dashboard');
  });

  it('وجهة الوحدة = أوّل قسم ظاهر فيها لا قسم محجوب', () => {
    const people = MODULES.find((m) => m.id === 'people')!;
    expect(moduleTarget(people, ctx({ roles: ['Employee'] }))).toBe('/app/employee/me');
  });
});

describe('المطابقة والتحويلات', () => {
  it('المسار الديناميّ يُبرِز عنصره المرجعيّ', () => {
    expect(resolveActive('/app/teams/abc-123')?.item.id).toBe('people.teams');
    expect(resolveActive('/app/clients/abc-123')?.item.id).toBe('portfolio.clients');
  });

  it('«الرئيسية» مطابقة تامّة فلا تبتلع كلّ المسارات', () => {
    expect(resolveActive('/app')?.module.id).toBe('home');
    expect(resolveActive('/app/teams')?.module.id).toBe('people');
  });

  it('المسار غير المعروف لا يُبرِز شيئًا بدل أن يُبرِز خطأً', () => {
    expect(resolveActive('/app/does-not-exist')).toBeNull();
    expect(canonicalPath('/app/does-not-exist')).toBe('/app/does-not-exist');
  });

  it('ظهور المسار يشمل مساراته الديناميّة وaliasاته لا وجهته المحسوبة وحدها', () => {
    const self = ctx({ roles: ['Employee'], scopeType: 'own' });
    expect(isPathVisible('/app/my-kpi', self)).toBe(true);
    expect(isPathVisible('/app/performance', self)).toBe(true);
    expect(isPathVisible('/app/kpi-templates', self)).toBe(false);
  });
});

describe('العدّادات', () => {
  const item = findItem('people.hr-operations');
  const allowed = ctx({ permissions: new Set([PERMISSIONS.hrOperationsView]) });

  it('لا عدّاد لقسم لا يراه المستخدم (لا تسريب عدّ خارج النطاق)', () => {
    expect(resolveBadge(item, ctx({ roles: ['Admin'] }), { hrOpsQueue: 9 })).toBeNull();
  });

  it('العدّاد يظهر لصاحب القدرة فقط', () => {
    expect(resolveBadge(item, allowed, { hrOpsQueue: 9 })).toBe(9);
    expect(resolveBadge(item, allowed, {})).toBeNull();
  });
});

describe('فتات الخبز', () => {
  it('الوحدة › المجموعة › القسم، والقسم الحاليّ غير قابل للنقر', () => {
    const crumbs = buildBreadcrumbs('/app/sales-aggregation', ctx({ roles: ['Admin'] }));
    expect(crumbs.map((c) => c.label)).toEqual(['التقارير', 'التجميعات والمقارنات', 'تجميع المبيعات']);
    expect(crumbs[crumbs.length - 1].to).toBeNull();
  });

  it('المقطع الديناميّ يُعرَض بتسمية عامّة ما لم تمرّره صفحة مصرَّح لها', () => {
    const generic = buildBreadcrumbs('/app/teams/abc-123', ctx({ roles: ['Admin'] }));
    expect(generic.map((c) => c.label)).toEqual(['الموظفون', 'فرق العمل', 'التفاصيل']);
    const named = buildBreadcrumbs('/app/teams/abc-123', ctx({ roles: ['Admin'] }), 'فريق التنفيذ');
    expect(named[named.length - 1].label).toBe('فريق التنفيذ');
  });

  it('الـalias يعرض مسار عنصره المرجعيّ لا نفسه', () => {
    const crumbs = buildBreadcrumbs('/app/escalations', ctx({ roles: ['Admin'] }));
    expect(crumbs.map((c) => c.label)).toEqual(['الحوكمة', 'التصعيدات']);
  });

  it('المسار غير المعروف بلا فتات خبز', () => {
    expect(buildBreadcrumbs('/app/does-not-exist', ctx({ roles: ['Admin'] }))).toEqual([]);
  });
});
