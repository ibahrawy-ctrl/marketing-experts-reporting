// ======================================================================
// P123-R1 — «الميزة مغلقة» ليست عطلًا، و«خارج نطاقك» ليس عطلًا كذلك.
//
// العيب الذي تُغلقه هذه الاختبارات كان يُقاس PASS تقنيًّا وFAIL عمليًّا: الأعلام مطفأة على
// الإنتاج ⇒ الخادم يردّ 404 بنيّة الإخفاء ⇒ اللوحة تعرض «حدث خطأ مؤقّت. أعد المحاولة» على
// حالة **دائمة**. المستخدم يعيد المحاولة بلا نهاية ويقرأ قرارًا متعمَّدًا عطلًا في النظام.
//
// الادّعاء هنا ثلاثيّ عمدًا، وكلّ طرف منه ضروريّ:
//   1) النصّ لا يَعِد بإعادة محاولة، ولا زرّ إعادة محاولة أصلًا.
//   2) **لا نداء شبكة إطلاقًا** — إذ بعد إرسال الطلب يعود 404 واحد لحالتين مختلفتين
//      (ميزة مغلقة / خارج النطاق) فيتعذّر التمييز؛ التمييز ممكن فقط قبل الإرسال.
//   3) 403/404 مع ميزة **مفتوحة** يعطي «ممنوع» دائمًا كذلك، لا «خطأ مؤقّت».
// ======================================================================

import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { api } from '../lib/api';
import { FEATURES } from '../lib/navConfig';
import { Employee360Panel } from './Employee360Panel';
import { EmployeeChecklistPanel } from './EmployeeChecklistPanel';

let enabledFeatures = new Set<string>();

vi.mock('../lib/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/auth')>()),
  useAuth: () => ({ features: enabledFeatures }),
}));

function httpError(status: number): AxiosError {
  const err = new AxiosError('denied');
  err.response = { status, statusText: '', data: {}, headers: {}, config: { headers: new AxiosHeaders() } };
  return err;
}

function renderWith(node: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>{node}</MemoryRouter>
    </QueryClientProvider>,
  );
}

const SUBJECT = '44444444-4444-4444-4444-444444444444';

beforeEach(() => {
  vi.restoreAllMocks();
  enabledFeatures = new Set<string>();
});

describe('P123-R1 — الميزة المغلقة تُعرَض إغلاقًا لا عطلًا', () => {
  it('الملفّ الشامل: نصّ إغلاق صريح، بلا زرّ إعادة محاولة، وبلا أيّ نداء شبكة', async () => {
    const get = vi.spyOn(api, 'get');
    renderWith(<Employee360Panel subject={SUBJECT} />);

    expect(await screen.findByText('هذه الخدمة غير مفعّلة حاليًّا')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة المحاولة' })).toBeNull();
    expect(screen.queryByText(/أعد المحاولة/)).toBeNull();
    // لا طلب أصلًا: هذا ما يجعل التمييز عن «خارج النطاق» ممكنًا من الأساس.
    await waitFor(() => expect(get).not.toHaveBeenCalled());
  });

  it('قائمة الالتزام: مفتاح مستقلّ — تُغلَق وحدها ولو كان الملفّ الشامل مفتوحًا', async () => {
    enabledFeatures = new Set<string>([FEATURES.employee360]);
    const get = vi.spyOn(api, 'get');
    renderWith(<EmployeeChecklistPanel subject={SUBJECT} />);

    expect(await screen.findByText('قائمة الالتزام غير مفعّلة حاليًّا')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة المحاولة' })).toBeNull();
    await waitFor(() => expect(get).not.toHaveBeenCalled());
  });
});

describe('P123-R1 — المنع الدائم يُفصَل عن العطل المؤقّت', () => {
  it('403 مع ميزة مفتوحة: «لا تملك صلاحية» بلا إعادة محاولة', async () => {
    enabledFeatures = new Set<string>(Object.values(FEATURES));
    vi.spyOn(api, 'get').mockRejectedValue(httpError(403));
    renderWith(<Employee360Panel subject={SUBJECT} />);

    expect(await screen.findByText('لا يمكن عرض الملفّ الشامل لهذا الموظّف')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة المحاولة' })).toBeNull();
  });

  it('404 خارج النطاق يُقرأ منعًا لا عطلًا', async () => {
    enabledFeatures = new Set<string>(Object.values(FEATURES));
    vi.spyOn(api, 'get').mockRejectedValue(httpError(404));
    renderWith(<Employee360Panel subject={SUBJECT} />);

    expect(await screen.findByText('لا يمكن عرض الملفّ الشامل لهذا الموظّف')).toBeInTheDocument();
    expect(screen.queryByText('تعذّر تحميل الملفّ الشامل')).toBeNull();
  });

  it('500 وحده يبقى عطلًا قابلًا لإعادة المحاولة', async () => {
    enabledFeatures = new Set<string>(Object.values(FEATURES));
    vi.spyOn(api, 'get').mockRejectedValue(httpError(500));
    renderWith(<Employee360Panel subject={SUBJECT} />);

    expect(await screen.findByText('تعذّر تحميل الملفّ الشامل')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
  });
});
