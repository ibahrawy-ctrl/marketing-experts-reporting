// ===== P360-WF-R2 §11 — ربط الحوكمة بالمشروع =====
// الخادم يقبل `ProjectId` على الخطر ويعرضه، وتبويب حوكمة المشروع يقرأ `/projects/{id}/risks`
// المرشَّح بـ`ProjectId`. لكنّ نموذج المخاطر — وهو مسار الكتابة الوحيد في الواجهة — لم يكن
// يرسل الحقل قطّ، فبقي التبويب فارغًا مهما سجّلت الإدارة من مخاطر. القياس هنا على **الحمولة**.

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../lib/api';
import GovernancePage from './GovernancePage';

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: { userId: 'u1', fullName: 'مدير', roles: ['Manager'] },
    hasAnyRole: () => true,
    loading: false,
  }),
}));

vi.mock('react-router-dom', () => ({
  Link: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

const PROJECT = { id: 'proj-1', name: 'مشروع الحملة الصيفيّة' };
let posts: { url: string; body?: unknown }[] = [];

beforeEach(() => {
  vi.restoreAllMocks();
  posts = [];
  vi.spyOn(api, 'get').mockImplementation((url: string) =>
    Promise.resolve({ data: url === '/projects' ? [PROJECT] : [] } as never),
  );
  vi.spyOn(api, 'post').mockImplementation((url: string, body?: unknown) => {
    posts.push({ url, body });
    return Promise.resolve({ data: {} } as never);
  });
});

function renderPage() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const r = render(
    <QueryClientProvider client={qc}>
      <GovernancePage />
    </QueryClientProvider>,
  );
  fireEvent.click(screen.getByRole('button', { name: 'المخاطر' }));
  return r;
}

describe('نموذج المخاطر — الربط بالمشروع', () => {
  it('يرسل معرّف المشروع المختار ضمن حمولة الخطر', async () => {
    renderPage();
    const project = await screen.findByLabelText(/^المشروع/);
    fireEvent.change(screen.getByLabelText('عنوان الخطر'), {
      target: { value: 'تأخّر اعتماد العميل للمحتوى' },
    });
    fireEvent.change(project, { target: { value: PROJECT.id } });
    fireEvent.click(screen.getByRole('button', { name: 'إضافة خطر' }));

    await waitFor(() => expect(posts.filter((p) => p.url === '/risks')).toHaveLength(1));
    expect(posts[0].body).toMatchObject({
      title: 'تأخّر اعتماد العميل للمحتوى',
      projectId: PROJECT.id,
    });
  });

  it('يرسل `null` حين لا يُختار مشروع، فيبقى الخطر عامًّا كما كان', async () => {
    renderPage();
    await screen.findByLabelText(/^المشروع/);
    fireEvent.change(screen.getByLabelText('عنوان الخطر'), {
      target: { value: 'خطر تشغيليّ عامّ' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'إضافة خطر' }));

    await waitFor(() => expect(posts.filter((p) => p.url === '/risks')).toHaveLength(1));
    expect((posts[0].body as { projectId: unknown }).projectId).toBeNull();
  });
});
