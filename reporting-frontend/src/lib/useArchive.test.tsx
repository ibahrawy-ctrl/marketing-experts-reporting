// اختبارات هوكات الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1 — Phase 16).
// تثبت أنّ useArchiveList تبني معاملات الاستعلام الاختيارية بشكلٍ صحيح (تحذف الفارغ، تضبط page/pageSize
// الافتراضيّين)، وأنّ مسارات التفاصيل/الاسترجاع تشتقّ القطعة الصحيحة (report/kpi) حسب نوع العنصر.
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { ReactNode } from 'react';
import { api } from './api';
import { useArchiveList, useArchiveDetails, useRestoreArchiveItem } from './useArchive';
import type { ArchiveListFilter } from '../types/api';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('useArchiveList — بناء معاملات الاستعلام', () => {
  it('1) بلا مرشّحات: يرسل page=1 و pageSize=20 فقط (لا itemType/periodKey/employeeId)', async () => {
    const get = vi
      .spyOn(api, 'get')
      .mockResolvedValue({ data: { items: [], totalCount: 0, page: 1, pageSize: 20 } } as never);
    const filter: ArchiveListFilter = {};
    renderHook(() => useArchiveList(filter), { wrapper: wrapper() });
    await waitFor(() => expect(get).toHaveBeenCalled());
    const [url, config] = get.mock.calls[0];
    expect(url).toBe('/admin/archive');
    expect(config).toEqual({ params: { page: 1, pageSize: 20 } });
  });

  it('2) مع كل المرشّحات: يمرّرها جميعًا مع page/pageSize الصريحين', async () => {
    const get = vi
      .spyOn(api, 'get')
      .mockResolvedValue({ data: { items: [], totalCount: 0, page: 2, pageSize: 50 } } as never);
    const filter: ArchiveListFilter = {
      itemType: 'KpiEvaluation',
      periodKey: '2026-W27',
      employeeId: 'emp-1',
      page: 2,
      pageSize: 50,
    };
    renderHook(() => useArchiveList(filter), { wrapper: wrapper() });
    await waitFor(() => expect(get).toHaveBeenCalled());
    const [, config] = get.mock.calls[0];
    expect(config).toEqual({
      params: { itemType: 'KpiEvaluation', periodKey: '2026-W27', employeeId: 'emp-1', page: 2, pageSize: 50 },
    });
  });

  it('3) مرشّحات جزئية: يحذف الفارغ ويُبقي المُعطى (itemType فقط)', async () => {
    const get = vi
      .spyOn(api, 'get')
      .mockResolvedValue({ data: { items: [], totalCount: 0, page: 1, pageSize: 20 } } as never);
    const filter: ArchiveListFilter = { itemType: 'Report' };
    renderHook(() => useArchiveList(filter), { wrapper: wrapper() });
    await waitFor(() => expect(get).toHaveBeenCalled());
    const [, config] = get.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(config.params.itemType).toBe('Report');
    expect(config.params).not.toHaveProperty('periodKey');
    expect(config.params).not.toHaveProperty('employeeId');
    expect(config.params.page).toBe(1);
    expect(config.params.pageSize).toBe(20);
  });
});

describe('useArchiveDetails — اشتقاق قطعة المسار', () => {
  it('4) Report ⟶ /admin/archive/report/{id}', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: {} } as never);
    renderHook(() => useArchiveDetails('Report', 'r-1'), { wrapper: wrapper() });
    await waitFor(() => expect(get).toHaveBeenCalled());
    expect(get.mock.calls[0][0]).toBe('/admin/archive/report/r-1');
  });

  it('5) KpiEvaluation ⟶ /admin/archive/kpi/{id}', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: {} } as never);
    renderHook(() => useArchiveDetails('KpiEvaluation', 'k-1'), { wrapper: wrapper() });
    await waitFor(() => expect(get).toHaveBeenCalled());
    expect(get.mock.calls[0][0]).toBe('/admin/archive/kpi/k-1');
  });

  it('6) مُعطّل حتى يتوفّر itemType و id معًا (لا استدعاء شبكة)', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: {} } as never);
    renderHook(() => useArchiveDetails(null, null), { wrapper: wrapper() });
    // مهلة قصيرة للتأكّد أنّ الاستعلام لم يُطلَق (enabled=false).
    await new Promise((r) => setTimeout(r, 20));
    expect(get).not.toHaveBeenCalled();
  });
});

describe('useRestoreArchiveItem — مسار الاسترجاع', () => {
  it('7) Report ⟶ POST /admin/archive/report/{id}/restore بالجسم', async () => {
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never);
    const { result } = renderHook(() => useRestoreArchiveItem(), { wrapper: wrapper() });
    await result.current.mutateAsync({ itemType: 'Report', id: 'r-9', request: { reason: 'سبب كافٍ للاسترجاع' } });
    expect(post).toHaveBeenCalledWith('/admin/archive/report/r-9/restore', { reason: 'سبب كافٍ للاسترجاع' });
  });

  it('8) KpiEvaluation ⟶ POST /admin/archive/kpi/{id}/restore', async () => {
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never);
    const { result } = renderHook(() => useRestoreArchiveItem(), { wrapper: wrapper() });
    await result.current.mutateAsync({ itemType: 'KpiEvaluation', id: 'k-9', request: { reason: 'سبب آخر مقبول' } });
    expect(post).toHaveBeenCalledWith('/admin/archive/kpi/k-9/restore', { reason: 'سبب آخر مقبول' });
  });
});
