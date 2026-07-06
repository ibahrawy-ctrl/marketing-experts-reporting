// اختبارات smoke لشاشات الهيكل التنظيمي (ORG-STRUCTURE-ADMIN-R1):
// الفرق / الإدارات / تفاصيل الفريق — تغطّي العناصر المضافة: مؤشّر «بلا مدير»،
// تحذير عدم تطابق إدارة الأعضاء، ملخّص أثر نقل الفريق، والعدّادات.
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import TeamsPage from './TeamsPage';
import { DepartmentsPage } from './DepartmentsPage';
import TeamDetailsPage from './TeamDetailsPage';

const DEPT_A = 'dept-sales';
const DEPT_B = 'dept-mktg';
const TEAM_1 = 'team-1';
const LEAD = 'user-lead';
const M1 = 'user-m1';

// بيانات قابلة للتهيئة حسب السيناريو (إدارة العضو ومدير الإدارة الثانية).
function makeGetData(opts: { memberDeptId?: string; deptBManagerId?: string | null } = {}): Record<string, unknown> {
  const memberDeptId = opts.memberDeptId ?? DEPT_A;
  const deptBManagerId = opts.deptBManagerId ?? null;
  return {
    '/auth/me': {
      userId: LEAD,
      fullName: 'قائد المبيعات',
      email: 'lead@test.local',
      isActive: true,
      roles: ['Admin'],
      expectedReportCadence: 'Weekly',
    },
    '/directory/users': [
      { id: LEAD, fullName: 'قائد المبيعات', email: 'lead@test.local', isActive: true, roles: ['TeamLeader'], departmentId: DEPT_A, teamId: TEAM_1, managerId: null, jobRoleId: null },
      { id: M1, fullName: 'عضو أول', email: 'm1@test.local', isActive: true, roles: ['Employee'], departmentId: memberDeptId, teamId: TEAM_1, managerId: LEAD, jobRoleId: null },
    ],
    '/directory/teams': [
      { id: TEAM_1, nameAr: 'فريق المبيعات', nameEn: 'Sales Team', departmentId: DEPT_A, teamLeaderId: LEAD, isActive: true },
    ],
    '/directory/departments': [
      { id: DEPT_A, nameAr: 'إدارة المبيعات', nameEn: null, code: 'SALES', managerId: LEAD, isActive: true },
      { id: DEPT_B, nameAr: 'إدارة التسويق', nameEn: null, code: 'MKTG', managerId: deptBManagerId, isActive: true },
    ],
    '/submissions': [],
    '/reports/kpi-summary': { periodKey: null, evaluated: 0, averageScore: null, belowTarget: 0, rows: [] },
    '/escalations': [],
    '/decisions': [],
    '/training-needs': [],
    '/improvement-plans': [],
    '/management-notes': [],
    [`/directory/teams/${TEAM_1}/move-impact`]: {
      teamId: TEAM_1,
      teamName: 'فريق المبيعات',
      currentDepartmentId: DEPT_A,
      currentDepartmentName: 'إدارة المبيعات',
      targetDepartmentId: DEPT_B,
      targetDepartmentName: 'إدارة التسويق',
      isDepartmentChange: true,
      teamLeaderId: LEAD,
      teamLeaderName: 'قائد المبيعات',
      memberCount: 2,
      projectsCount: 0,
      activeProjectsCount: 0,
      submissionsCount: 0,
      memberDepartmentMismatchCount: 2,
      willSyncMembers: true,
      warnings: ['سيُحدَّث انتماء الأعضاء عند الحفظ مع المزامنة.'],
    },
  };
}

let currentData: Record<string, unknown> = makeGetData();

function lookup(url: string) {
  const path = url.split('?')[0];
  return currentData[path] ?? [];
}

beforeEach(() => {
  tokenStore.clear();
  currentData = makeGetData();
  vi.restoreAllMocks();
  vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: lookup(url) } as never),
  );
});

function renderPage(path: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/app/teams" element={<TeamsPage />} />
            <Route path="/app/departments" element={<DepartmentsPage />} />
            <Route path="/app/teams/:teamId" element={<TeamDetailsPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe('ORG-STRUCTURE smoke — TeamsPage', () => {
  it('يعرض العنوان وبطاقة الفريق باسمه وإدارته', async () => {
    renderPage('/app/teams');
    expect(await screen.findByText('فرق العمل')).toBeInTheDocument();
    expect(await screen.findByText('فريق المبيعات')).toBeInTheDocument();
    // اسم الإدارة يظهر ضمن بطاقة الفريق («إدارة المبيعات · القائد: …»).
    expect(await screen.findByText(/إدارة المبيعات · القائد:/)).toBeInTheDocument();
  });

  it('يعرض عدّادات الفرق والأعضاء', async () => {
    renderPage('/app/teams');
    expect(await screen.findByText('عدد الفرق')).toBeInTheDocument();
    expect(await screen.findByText('إجمالي الأعضاء')).toBeInTheDocument();
  });
});

describe('ORG-STRUCTURE smoke — DepartmentsPage', () => {
  it('يعرض العنوان والعدّادات ومؤشّر «بلا مدير» للإدارة بلا مدير', async () => {
    renderPage('/app/departments');
    expect(await screen.findByText('إدارة الإدارات')).toBeInTheDocument();
    expect(await screen.findByText('إجمالي الإدارات')).toBeInTheDocument();
    // إدارة التسويق بلا مدير ⇒ شارة «بلا مدير».
    expect(await screen.findByText('بلا مدير')).toBeInTheDocument();
  });

  it('يعرض اسم المدير للإدارة التي لها مدير', async () => {
    renderPage('/app/departments');
    // إدارة المبيعات managerId=قائد المبيعات ⇒ يظهر اسمه في عمود المدير.
    expect(await screen.findByText('قائد المبيعات')).toBeInTheDocument();
  });
});

describe('ORG-TEAM-CREATE-UX smoke — زر/نموذج إنشاء فريق', () => {
  it('يُظهر زر «إنشاء فريق جديد» للأدمن في صفحة الفرق', async () => {
    tokenStore.set('access', 'refresh'); // /auth/me = Admin (من makeGetData)
    renderPage('/app/teams');
    expect(await screen.findByText('+ إنشاء فريق جديد')).toBeInTheDocument();
  });

  it('لا يُظهر زر إنشاء الفريق لغير المصرّح (Manager)', async () => {
    currentData = {
      ...makeGetData(),
      '/auth/me': {
        userId: M1,
        fullName: 'مدير',
        email: 'mgr@test.local',
        isActive: true,
        roles: ['Manager'],
        expectedReportCadence: 'Weekly',
      },
    };
    tokenStore.set('access', 'refresh');
    renderPage('/app/teams');
    // الصفحة تُحمَّل (العنوان يظهر) لكن زر الإنشاء غائب لغير الأدمن.
    expect(await screen.findByText('فرق العمل')).toBeInTheDocument();
    expect(screen.queryByText('+ إنشاء فريق جديد')).not.toBeInTheDocument();
  });

  it('يفتح نموذج إنشاء الفريق عند الضغط على الزر (صفحة الفرق)', async () => {
    tokenStore.set('access', 'refresh');
    renderPage('/app/teams');
    fireEvent.click(await screen.findByText('+ إنشاء فريق جديد'));
    expect(await screen.findByText('فريق جديد')).toBeInTheDocument();
    expect(await screen.findByText('إنشاء الفريق')).toBeInTheDocument();
    // منتقي الإدارة متاح للاختيار داخل النموذج.
    expect(await screen.findByText('— اختر الإدارة —')).toBeInTheDocument();
  });

  it('يُظهر زر «+ فريق» داخل صف الإدارة ويفتح نموذجًا بإدارة مثبّتة (صفحة الإدارات)', async () => {
    tokenStore.set('access', 'refresh');
    renderPage('/app/departments');
    const addButtons = await screen.findAllByText('+ فريق');
    expect(addButtons.length).toBeGreaterThan(0);
    fireEvent.click(addButtons[0]);
    expect(await screen.findByText('فريق جديد')).toBeInTheDocument();
    // الإدارة مثبّتة مسبقًا ⇒ يظهر اسمها كنص لا كمنتقي («— اختر الإدارة —» غائب).
    expect(screen.queryByText('— اختر الإدارة —')).not.toBeInTheDocument();
  });
});

describe('ORG-STRUCTURE smoke — TeamDetailsPage', () => {
  it('يعرض ترويسة الفريق وإدارته وقائده وعضوه', async () => {
    renderPage(`/app/teams/${TEAM_1}`);
    expect(await screen.findByRole('heading', { name: /فريق المبيعات/ })).toBeInTheDocument();
    // سطر التعريف: «إدارة المبيعات · القائد: قائد المبيعات · 2 عضو».
    expect(await screen.findByText(/إدارة المبيعات · القائد: قائد المبيعات/)).toBeInTheDocument();
    expect(await screen.findByText('عضو أول')).toBeInTheDocument();
  });

  it('يظهر تحذير عدم تطابق إدارة الأعضاء للأدمن عند اختلاف إدارة عضو', async () => {
    currentData = makeGetData({ memberDeptId: DEPT_B });
    tokenStore.set('access', 'refresh');
    renderPage(`/app/teams/${TEAM_1}`);
    expect(await screen.findByText(/إدارتهم تختلف عن إدارة الفريق/)).toBeInTheDocument();
  });

  it('يعرض ملخّص أثر نقل الفريق عند اختيار إدارة مختلفة (للأدمن)', async () => {
    tokenStore.set('access', 'refresh');
    renderPage(`/app/teams/${TEAM_1}`);
    // لوحة إدارة الفريق تظهر للأدمن (canManageTeams) بعد جلب /auth/me.
    expect(await screen.findByText('إدارة الفريق')).toBeInTheDocument();
    // منتقي الإدارة الحالي = «إدارة المبيعات»؛ نغيّره إلى «إدارة التسويق» لتفعيل ملخّص الأثر.
    const deptSelect = screen.getByDisplayValue('إدارة المبيعات') as HTMLSelectElement;
    fireEvent.change(deptSelect, { target: { value: DEPT_B } });
    expect(await screen.findByText('ملخّص أثر نقل الفريق')).toBeInTheDocument();
    expect(
      await screen.findByText(/نقل «فريق المبيعات» من «إدارة المبيعات» إلى «إدارة التسويق»/),
    ).toBeInTheDocument();
  });
});
