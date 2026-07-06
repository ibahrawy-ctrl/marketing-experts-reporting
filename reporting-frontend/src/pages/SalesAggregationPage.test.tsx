import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';

// عزل الصفحة عن الشبكة: هوكات التجميع مُموّهة (لا استدعاء API) — نختبر منتقيات الفترة فقط.
vi.mock('../lib/useSalesAggregation', () => ({
  useB2cAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cCourseGrouped: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2cNewOld: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  useB2bAggregation: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
}));

import SalesAggregationPage from './SalesAggregationPage';

// ===== SALES-AGG-FIX — الواجهة لا تعرض صندوق «مفتاح الفترة» النصّي، بل منتقيات حسب النوع =====

it('لا يعرض صندوق مفتاح الفترة النصّي؛ يعرض منتقي تاريخ في الوضع الأسبوعي الافتراضي', () => {
  const { container } = render(<SalesAggregationPage />);

  // لا يوجد أي حقل بعنوان «مفتاح الفترة» (استُبدل بالمنتقيات).
  expect(screen.queryByText('مفتاح الفترة')).toBeNull();

  // الافتراضي أسبوعي ⇒ منتقي تاريخ (لا إدخال نصّي حرّ لمفتاح الفترة).
  expect(screen.getByText('نوع الفترة')).toBeInTheDocument();
  expect(screen.getByText('الأسبوع')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();

  // لا يوجد أي حقل نصّي حرّ (textbox) في الفلاتر بالوضع الأسبوعي.
  expect(screen.queryByRole('textbox')).toBeNull();
});

it('تبديل النوع يُظهر المنتقي المطابق (يومي/شهري/ربع سنوي) دون أي صندوق نصّي لمفتاح الفترة', () => {
  const { container } = render(<SalesAggregationPage />);
  const typeSelect = screen.getByRole('combobox'); // منتقي «نوع الفترة» (الوحيد في الوضع الأسبوعي)

  // يومي ⇒ منتقي تاريخ.
  fireEvent.change(typeSelect, { target: { value: 'Daily' } });
  expect(screen.getByText('اليوم')).toBeInTheDocument();
  expect(container.querySelector('input[type="date"]')).not.toBeNull();

  // شهري ⇒ منتقي شهر.
  fireEvent.change(typeSelect, { target: { value: 'Monthly' } });
  expect(screen.getByText('الشهر')).toBeInTheDocument();
  expect(container.querySelector('input[type="month"]')).not.toBeNull();

  // ربع سنوي ⇒ سنة (رقم) + الربع (قائمة).
  fireEvent.change(typeSelect, { target: { value: 'Quarterly' } });
  expect(screen.getByText('السنة')).toBeInTheDocument();
  expect(screen.getByText('الربع')).toBeInTheDocument();
  expect(container.querySelector('input[type="number"]')).not.toBeNull();

  // في كل الأوضاع لا يظهر «مفتاح الفترة».
  expect(screen.queryByText('مفتاح الفترة')).toBeNull();
});
