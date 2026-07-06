import { render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { ReactElement } from 'react';
import { AuthProvider } from '../lib/auth';
import { tokenStore } from '../lib/tokenStore';
import { api } from '../lib/api';
import EmailNotificationsPage from './EmailNotificationsPage';

// أنواع أحداث تذكيرات التقارير التسعة (EMAIL-REPORT-REMINDERS-R1) وتسمياتها العربية.
// الهدف: التأكّد أن الشاشة تعرض تسمية عربية مفهومة لكل نوع بدل المفتاح الخام.
const reportReminderEvents: { eventType: string; label: string }[] = [
  { eventType: 'report-weekly-due', label: 'تذكير التقرير الأسبوعي' },
  { eventType: 'report-daily-due', label: 'تذكير التقرير اليومي' },
  { eventType: 'report-overdue', label: 'تأخر تقرير' },
  { eventType: 'report-team-overdue-summary', label: 'ملخص تأخر الفريق' },
  { eventType: 'report-department-overdue-summary', label: 'ملخص تأخر الإدارة' },
  { eventType: 'report-executive-overdue-summary', label: 'ملخص تنفيذي للتقارير' },
  { eventType: 'report-review-overdue-teamleader', label: 'تأخر مراجعة قائد الفريق' },
  { eventType: 'report-review-overdue-manager', label: 'تأخر مراجعة المدير' },
  { eventType: 'report-review-pending-executive', label: 'مراجعات تنفيذية معلقة' },
];

const rows = reportReminderEvents.map((e, i) => ({
  id: `n${i}`,
  createdAtUtc: '2026-06-30T10:00:00Z',
  eventType: e.eventType,
  status: 'DryRun',
  mode: 'DryRun',
  recipientUserId: `u${i}`,
  recipientName: `مستلم ${i}`,
  recipientEmail: `user${i}@test.local`,
  subject: `موضوع ${i}`,
  bodyPreview: 'معاينة',
  correlationKey: `${e.eventType}:2026-W23:u${i}`,
  sourceEntityType: 'ReportReminder',
  sourceEntityId: `u${i}`,
  errorMessage: null,
}));

const logPage = {
  items: rows,
  page: 1,
  pageSize: 25,
  totalCount: rows.length,
  summary: {
    total: rows.length,
    dryRun: rows.length,
    skipped: 0,
    failed: 0,
    sent: 0,
    pending: 0,
    cancelled: 0,
    lastCreatedAtUtc: '2026-06-30T10:00:00Z',
  },
};

beforeEach(() => {
  tokenStore.clear();
  vi.restoreAllMocks();
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    if (url.split('?')[0] === '/email-notifications/log') {
      return Promise.resolve({ data: logPage } as never);
    }
    return Promise.resolve({ data: [] } as never);
  });
});

function renderPage(el: ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <MemoryRouter>{el}</MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe('EmailNotificationsPage report-reminder labels', () => {
  it('renders the page heading and the DryRun notice', async () => {
    renderPage(<EmailNotificationsPage />);
    expect(await screen.findByText('سجلّ إشعارات البريد')).toBeInTheDocument();
    expect(
      await screen.findByText(
        'هذه الشاشة تعرض إشعارات البريد المسجّلة في وضع DryRun. لا يتم إرسال أي بريد فعلي حاليًا.',
      ),
    ).toBeInTheDocument();
  });

  it('shows the Arabic label for each of the 9 report-reminder event types in the table', async () => {
    renderPage(<EmailNotificationsPage />);
    // ننتظر ظهور أول صفّ ثم نتحقّق داخل جدول السجلّ (لا داخل قائمة الفلتر المنسدلة).
    await screen.findByText('موضوع 0');
    const table = screen.getByRole('table');
    for (const { eventType, label } of reportReminderEvents) {
      // التسمية تظهر داخل خلية الجدول — والمفتاح الخام يجب ألّا يظهر كنصّ.
      expect(within(table).getByText(label)).toBeInTheDocument();
      expect(within(table).queryByText(eventType)).not.toBeInTheDocument();
    }
  });
});
