// اختبارات شاشة الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1 — Phase 16).
// تعزل الشاشة عن الشبكة بتمويه هوكات useArchive، وتتحقّق من: تسميات النوع/الاحتفاظ، شارة قابلية
// الاسترجاع، حالة الفراغ، حساب الصفحات (تعطيل السابق/التالي)، عرض التفاصيل + استراتيجية الاسترجاع،
// وحدود التحقّق من سبب الاسترجاع (10–500 محرفًا يُفعّل الزرّ).
import { render, screen, fireEvent, within } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import type {
  ArchiveDetailsDto,
  ArchiveItemDto,
  ArchivePagedResult,
  ArchiveItemType,
} from '../types/api';

// حالة قابلة للحقن تُقرأ داخل الموك المرفوع.
const state = vi.hoisted(() => ({
  list: undefined as ArchivePagedResult | undefined,
  listLoading: false,
  listError: undefined as unknown,
  details: undefined as ArchiveDetailsDto | undefined,
  restorePending: false,
  restoreMock: undefined as unknown,
}));

vi.mock('../lib/useArchive', () => ({
  useArchiveList: () => ({
    data: state.list,
    isLoading: state.listLoading,
    isError: !!state.listError,
    error: state.listError,
  }),
  useArchiveDetails: (_t: ArchiveItemType | null, _id: string | null) => ({
    data: state.details,
    isLoading: false,
    isError: false,
    error: undefined,
  }),
  useRestoreArchiveItem: () => ({
    mutateAsync: state.restoreMock,
    isPending: state.restorePending,
  }),
}));

import AdminArchivePage from './AdminArchivePage';

function item(over: Partial<ArchiveItemDto>): ArchiveItemDto {
  return {
    archiveItemId: 'a1',
    itemType: 'Report',
    employeeId: 'e1',
    employeeName: 'أحمد سالم',
    templateName: 'التقرير الأسبوعي',
    periodKey: '2026-W27',
    status: 'Submitted',
    deletedAtUtc: '2026-06-30T10:00:00Z',
    deletedByUserId: 'admin-1',
    deletedByName: 'مدير النظام',
    deletionReason: 'حذف تجريبيّ',
    canRestore: true,
    restoreBlockedCode: null,
    restoreBlockedReason: null,
    daysSinceDeletion: 3,
    retentionStatus: 'Fresh',
    ...over,
  };
}

function details(over: Partial<ArchiveDetailsDto>): ArchiveDetailsDto {
  return {
    ...item({}),
    currentApproverId: null,
    currentApproverName: null,
    workflowSteps: [],
    fieldValuesCount: 5,
    kpiResultsCount: 0,
    reviewEventsCount: 0,
    auditTrail: [],
    historicalApproverId: null,
    historicalApproverName: null,
    historicalApproverIsActive: null,
    restoreStrategy: 'NotApplicable',
    restoreWarning: null,
    ...over,
  };
}

beforeEach(() => {
  state.list = undefined;
  state.listLoading = false;
  state.listError = undefined;
  state.details = undefined;
  state.restorePending = false;
  state.restoreMock = vi.fn().mockResolvedValue({});
});

describe('AdminArchivePage — القائمة', () => {
  it('1) العنوان الرئيسيّ يظهر دائمًا', () => {
    render(<AdminArchivePage />);
    expect(screen.getByText('الأرشيف الإداريّ')).toBeInTheDocument();
  });

  it('2) قائمة فارغة ⟶ حالة الفراغ', () => {
    state.list = { items: [], totalCount: 0, page: 1, pageSize: 20 };
    render(<AdminArchivePage />);
    expect(screen.getByText('لا عناصر مؤرشفة')).toBeInTheDocument();
  });

  it('3) تسمية النوع: Report=تقرير و KpiEvaluation=تقييم KPI', () => {
    state.list = {
      items: [
        item({ archiveItemId: 'a1', itemType: 'Report' }),
        item({ archiveItemId: 'a2', itemType: 'KpiEvaluation', employeeName: 'سارة' }),
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
    };
    render(<AdminArchivePage />);
    const table = screen.getByRole('table');
    expect(within(table).getByText('تقرير')).toBeInTheDocument();
    expect(within(table).getByText('تقييم KPI')).toBeInTheDocument();
  });

  it('4) تسميات الاحتفاظ الثلاث (حديث/يستحقّ المراجعة/محفوظ طويل الأمد)', () => {
    state.list = {
      items: [
        item({ archiveItemId: 'a1', retentionStatus: 'Fresh', daysSinceDeletion: 2 }),
        item({ archiveItemId: 'a2', retentionStatus: 'ReviewDue', daysSinceDeletion: 40 }),
        item({ archiveItemId: 'a3', retentionStatus: 'LongTerm', daysSinceDeletion: 120 }),
      ],
      totalCount: 3,
      page: 1,
      pageSize: 20,
    };
    render(<AdminArchivePage />);
    expect(screen.getByText(/حديث/)).toBeInTheDocument();
    expect(screen.getByText(/يستحقّ المراجعة/)).toBeInTheDocument();
    expect(screen.getByText(/محفوظ طويل الأمد/)).toBeInTheDocument();
  });

  it('5) شارة قابلية الاسترجاع: قابل مقابل محجوب', () => {
    state.list = {
      items: [
        item({ archiveItemId: 'a1', canRestore: true }),
        item({ archiveItemId: 'a2', canRestore: false }),
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
    };
    render(<AdminArchivePage />);
    expect(screen.getByText('قابل للاسترجاع')).toBeInTheDocument();
    expect(screen.getByText('محجوب')).toBeInTheDocument();
  });
});

describe('AdminArchivePage — ترقيم الصفحات', () => {
  it('6) صفحة واحدة ⟶ زرّا السابق والتالي معطّلان', () => {
    state.list = { items: [item({})], totalCount: 5, page: 1, pageSize: 20 };
    render(<AdminArchivePage />);
    expect(screen.getByRole('button', { name: 'السابق' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'التالي' })).toBeDisabled();
  });

  it('7) عدّة صفحات في الصفحة الأولى ⟶ السابق معطّل والتالي مُفعّل', () => {
    state.list = { items: [item({})], totalCount: 45, page: 1, pageSize: 20 };
    render(<AdminArchivePage />);
    expect(screen.getByRole('button', { name: 'السابق' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'التالي' })).toBeEnabled();
    // إجمالي 45 / 20 ⟶ 3 صفحات.
    expect(screen.getByText(/صفحة 1 من 3/)).toBeInTheDocument();
  });
});

describe('AdminArchivePage — نافذة التفاصيل والاسترجاع', () => {
  function openDetails() {
    state.list = { items: [item({ archiveItemId: 'a1' })], totalCount: 1, page: 1, pageSize: 20 };
    render(<AdminArchivePage />);
    fireEvent.click(screen.getByRole('button', { name: 'التفاصيل' }));
  }

  it('8) استراتيجية HistoricalApproverRestored تعرض اسم المعتمِد التاريخيّ', () => {
    state.details = details({
      restoreStrategy: 'HistoricalApproverRestored',
      historicalApproverName: 'أميرة محمد',
    });
    openDetails();
    expect(screen.getByText(/استرجاع المعتمِد التاريخيّ/)).toBeInTheDocument();
    expect(screen.getByText(/أميرة محمد/)).toBeInTheDocument();
  });

  it('9) استراتيجية NoActiveApprover تعرض نصّ «دون معتمِد نشط»', () => {
    state.details = details({ restoreStrategy: 'NoActiveApprover' });
    openDetails();
    expect(screen.getByText(/استرجاع دون معتمِد نشط/)).toBeInTheDocument();
  });

  it('10) KPI بلا معتمِد ⟶ «استرجاع مباشر للتقييم»', () => {
    state.details = details({ itemType: 'KpiEvaluation', restoreStrategy: 'NotApplicable' });
    state.list = { items: [item({ archiveItemId: 'a1', itemType: 'KpiEvaluation' })], totalCount: 1, page: 1, pageSize: 20 };
    render(<AdminArchivePage />);
    fireEvent.click(screen.getByRole('button', { name: 'التفاصيل' }));
    expect(screen.getByText('استرجاع مباشر للتقييم.')).toBeInTheDocument();
  });

  it('11) زرّ الاسترجاع معطّل حتى يبلغ السبب 10 محارف على الأقل', () => {
    state.details = details({ canRestore: true });
    openDetails();
    const btn = screen.getByRole('button', { name: 'استرجاع العنصر' });
    expect(btn).toBeDisabled();
    fireEvent.change(screen.getByPlaceholderText('اذكر سبب الاسترجاع للأثر التدقيقيّ…'), {
      target: { value: 'قصير' },
    });
    expect(btn).toBeDisabled();
    fireEvent.change(screen.getByPlaceholderText('اذكر سبب الاسترجاع للأثر التدقيقيّ…'), {
      target: { value: 'سبب استرجاع كافٍ وواضح' },
    });
    expect(btn).toBeEnabled();
  });

  it('12) الضغط على الاسترجاع بسبب صالح يستدعي mutateAsync بالجسم الصحيح', () => {
    const mock = vi.fn().mockResolvedValue({});
    state.restoreMock = mock;
    state.details = details({ canRestore: true });
    openDetails();
    fireEvent.change(screen.getByPlaceholderText('اذكر سبب الاسترجاع للأثر التدقيقيّ…'), {
      target: { value: 'سبب استرجاع كافٍ وواضح' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'استرجاع العنصر' }));
    expect(mock).toHaveBeenCalledWith({
      itemType: 'Report',
      id: 'a1',
      request: { reason: 'سبب استرجاع كافٍ وواضح' },
    });
  });

  it('13) عنصر غير قابل للاسترجاع ⟶ يُعرض السبب المانع وتنبيه عدم القابلية', () => {
    state.details = details({
      canRestore: false,
      restoreBlockedReason: 'يوجد تقرير نشط بنفس الفترة.',
    });
    openDetails();
    expect(screen.getByText('يوجد تقرير نشط بنفس الفترة.')).toBeInTheDocument();
    expect(
      screen.getByText('هذا العنصر غير قابل للاسترجاع حاليًّا (انظر السبب أعلاه).'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'استرجاع العنصر' })).not.toBeInTheDocument();
  });
});
