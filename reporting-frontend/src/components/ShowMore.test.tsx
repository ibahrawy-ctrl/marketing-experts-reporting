import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect } from 'vitest';
import { PreviewList, ShowMoreButton, useShowMore } from './ShowMore';
import { renderHook, act } from '@testing-library/react';

// ===== UX-PRIMITIVES Phase 0 — اختبارات ShowMore/PreviewList (معاينة + عرض الكل) =====

const many = Array.from({ length: 15 }, (_, i) => `عنصر ${i + 1}`);

describe('PreviewList', () => {
  it('يعرض عدد المعاينة فقط ابتداءً ويخفي البقية', () => {
    render(<PreviewList items={many} previewCount={10} renderItem={(x) => <div key={x}>{x}</div>} />);
    expect(screen.getByText('عنصر 10')).toBeInTheDocument();
    expect(screen.queryByText('عنصر 11')).toBeNull();
  });

  it('زرّ «عرض الكل» يوضّح عدد المخفيّ ويحمل aria-expanded=false', () => {
    render(<PreviewList items={many} previewCount={10} renderItem={(x) => <div key={x}>{x}</div>} />);
    const btn = screen.getByRole('button', { name: /عرض الكل \(5 إضافية\)/ });
    expect(btn).toHaveAttribute('aria-expanded', 'false');
  });

  it('التوسيع يُظهر كل العناصر ويقلب الزرّ إلى «عرض أقل»', async () => {
    render(<PreviewList items={many} previewCount={10} renderItem={(x) => <div key={x}>{x}</div>} />);
    await userEvent.click(screen.getByRole('button', { name: /عرض الكل/ }));
    expect(screen.getByText('عنصر 15')).toBeInTheDocument();
    const btn = screen.getByRole('button', { name: 'عرض أقل' });
    expect(btn).toHaveAttribute('aria-expanded', 'true');
  });

  it('لا يظهر زرّ حين العناصر ≤ عدد المعاينة', () => {
    render(<PreviewList items={['أ', 'ب']} previewCount={10} renderItem={(x) => <div key={x}>{x}</div>} />);
    expect(screen.queryByRole('button')).toBeNull();
  });
});

describe('ShowMoreButton', () => {
  it('يختفي حين لا يوجد مخفيّ', () => {
    render(<ShowMoreButton expanded={false} onToggle={() => {}} hiddenCount={0} />);
    expect(screen.queryByRole('button')).toBeNull();
  });
});

describe('useShowMore', () => {
  it('يقطّع ويوسّع الصفوف (مناسب للجداول)', () => {
    const { result } = renderHook(() => useShowMore(many, 10));
    expect(result.current.visible).toHaveLength(10);
    expect(result.current.hiddenCount).toBe(5);
    act(() => result.current.toggle());
    expect(result.current.visible).toHaveLength(15);
    expect(result.current.expanded).toBe(true);
  });
});
