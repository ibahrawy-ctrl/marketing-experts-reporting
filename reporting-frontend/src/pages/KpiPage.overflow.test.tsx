// DEF-P123-008 — جدول تقييمات KPI كان يدفع الصفحة كلّها إلى تمرير أفقيّ عند 390px:
// سبعة أعمدة عرضها الأدنى 363px داخل بطاقة تتيح 353px فقط. الحارس هنا بنيويّ لا تجميليّ —
// jsdom لا يقيس التخطيط، فالمقيس هو وجود الحاوية القياسيّة التي تحصر التمرير داخل الجدول.
// حارس لا-فراغيّة إلزاميّ: لو صُيِّر الجدول فارغًا لَما أثبت الاختبار شيئًا.
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider } from '../lib/auth';
import { api } from '../lib/api';
import KpiPage from './KpiPage';

const LONG_TEMPLATE = 'مؤشرات أداء المودريشن والتفاعل المجتمعيّ الشهريّة';
const LONG_SUBJECT = 'UAT-P123 موظّف مكتمل البيانات للاختبار';

const DATA: Record<string, unknown> = {
  '/auth/me': {
    userId: 'u-emp', fullName: 'موظّف', email: 'emp@test.local',
    isActive: true, roles: ['Employee'], expectedReportCadence: 'Weekly',
  },
  '/kpi-evaluations': [
    {
      id: 'kpi-1', templateTitle: LONG_TEMPLATE, subjectUserId: 'u-emp', subjectName: LONG_SUBJECT,
      periodKey: '2026-08', totalScore: 88, trend: 'Up', status: 'Approved',
    },
  ],
};

beforeEach(() => {
  vi.restoreAllMocks();
  vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: DATA[url.split('?')[0]] ?? [] } as never),
  );
});

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <MemoryRouter initialEntries={['/app/kpi']}>
          <KpiPage />
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe('KpiPage — لا تمرير أفقيّ على مستوى الصفحة (DEF-P123-008)', () => {
  it('جدول التقييمات داخل حاوية تمرير أفقيّ، والصفوف غير فارغة', async () => {
    renderPage();

    // حارس لا-فراغيّة: البيانات الطويلة هي بالضبط ما يُفجّر العيب.
    await waitFor(() => expect(screen.getByText(LONG_TEMPLATE)).toBeInTheDocument());
    expect(screen.getByText(LONG_SUBJECT)).toBeInTheDocument();

    const table = screen.getByText('القالب').closest('table');
    expect(table).not.toBeNull();
    expect(table!.parentElement?.className).toContain('overflow-x-auto');
  });
});
