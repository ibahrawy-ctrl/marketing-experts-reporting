import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect } from 'vitest';
import { MAX_VISIBLE_TABS, NavOverflow, type OverflowTab } from './NavOverflow';

// ===== P3-NAV-004 — شريط الأقسام مع فائض «المزيد ⋯» =====
// الحدّ سبعة عناصر مرئيّة؛ ما زاد يُطوى في قائمة بدل أن يُقصّ أفقيًّا أو يختفي بلا إشارة.

function tabs(n: number): OverflowTab[] {
  return Array.from({ length: n }, (_, i) => ({
    id: `t${i + 1}`,
    to: `/app/t${i + 1}`,
    label: `قسم ${i + 1}`,
    groupLabel: i >= MAX_VISIBLE_TABS ? 'مجموعة' : null,
  }));
}

function renderTabs(list: OverflowTab[], activeId: string | null = null) {
  return render(
    <MemoryRouter>
      <NavOverflow tabs={list} activeId={activeId} />
    </MemoryRouter>,
  );
}

describe('NavOverflow', () => {
  it('لا يظهر الشريط أصلًا لقسم واحد أو صفر (لا ضجيج بصريّ بلا خيار)', () => {
    renderTabs(tabs(1));
    expect(screen.queryByRole('tablist')).toBeNull();
  });

  it('حتّى سبعة أقسام تُعرَض كلّها بلا زرّ فائض', () => {
    renderTabs(tabs(MAX_VISIBLE_TABS));
    expect(screen.getAllByRole('tab')).toHaveLength(MAX_VISIBLE_TABS);
    expect(screen.queryByRole('button', { name: 'المزيد ⋯' })).toBeNull();
  });

  it('ما زاد عن سبعة يُطوى في «المزيد ⋯» ولا يُقصّ', () => {
    renderTabs(tabs(11));
    expect(screen.getAllByRole('tab')).toHaveLength(MAX_VISIBLE_TABS);
    expect(screen.queryByText('قسم 11')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'المزيد ⋯' }));
    const menu = within(screen.getByRole('menu'));
    expect(menu.getAllByRole('menuitem')).toHaveLength(11 - MAX_VISIBLE_TABS);
    expect(menu.getByText('قسم 11')).toBeInTheDocument();
    expect(menu.getByText('مجموعة')).toBeInTheDocument();
  });

  it('القسم النشط داخل الفائض يُبرِز زرّ «المزيد ⋯» نفسه كي لا يفقد المستخدم موضعه', () => {
    renderTabs(tabs(11), 't10');
    expect(screen.getByRole('button', { name: 'المزيد ⋯' })).toHaveAttribute('aria-current', 'page');
  });

  it('القسم النشط المرئيّ يُعلَّم وحده، والزرّ لا يُعلَّم', () => {
    renderTabs(tabs(11), 't2');
    expect(screen.getByRole('tab', { name: 'قسم 2' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: 'المزيد ⋯' })).not.toHaveAttribute('aria-current');
  });

  it('Escape يُغلق قائمة الفائض', () => {
    renderTabs(tabs(11));
    fireEvent.click(screen.getByRole('button', { name: 'المزيد ⋯' }));
    expect(screen.getByRole('menu')).toBeInTheDocument();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('النقر خارج القائمة يُغلقها', () => {
    renderTabs(tabs(11));
    fireEvent.click(screen.getByRole('button', { name: 'المزيد ⋯' }));
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('اختيار قسم من الفائض يُغلق القائمة', () => {
    renderTabs(tabs(11));
    fireEvent.click(screen.getByRole('button', { name: 'المزيد ⋯' }));
    fireEvent.click(within(screen.getByRole('menu')).getByText('قسم 9'));
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('العدّاد يظهر بقيمة موجبة ويُخفى عند الصفر (العدّاد إشعار بعمل مطلوب)', () => {
    const list = tabs(3);
    list[0].badge = 4;
    list[1].badge = 0;
    list[2].badge = null;
    renderTabs(list);
    expect(within(screen.getByRole('tab', { name: /قسم 1/ })).getByText('4')).toBeInTheDocument();
    expect(screen.queryByText('0')).toBeNull();
  });

  it('لا تمرير أفقيًّا: الشريط يلتفّ ولا يُقصّ', () => {
    renderTabs(tabs(11));
    const bar = screen.getByRole('tablist');
    expect(bar.className).toContain('flex-wrap');
    expect(bar.className).not.toContain('overflow-x');
  });
});
