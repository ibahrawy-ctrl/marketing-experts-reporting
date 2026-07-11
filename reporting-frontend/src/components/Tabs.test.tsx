import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { Tabs, type TabItem } from './Tabs';

// ===== UX-PRIMITIVES Phase 0 — اختبارات مكوّن Tabs (a11y + RTL + تبديل) =====

const items: TabItem[] = [
  { id: 'a', label: 'الأول', content: <p>محتوى الأول</p> },
  { id: 'b', label: 'الثاني', content: <p>محتوى الثاني</p> },
  { id: 'c', label: 'الثالث', content: <p>محتوى الثالث</p>, disabled: true },
];

describe('Tabs', () => {
  it('يصيّر أدوار ARIA الصحيحة ويبدأ بأول تبويب نشطًا', () => {
    render(<Tabs items={items} ariaLabel="أقسام" />);
    expect(screen.getByRole('tablist', { name: 'أقسام' })).toBeInTheDocument();
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(3);
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel')).toHaveTextContent('محتوى الأول');
  });

  it('يربط التبويب باللوحة عبر aria-controls/aria-labelledby', () => {
    render(<Tabs items={items} />);
    const activeTab = screen.getByRole('tab', { name: 'الأول' });
    const panel = screen.getByRole('tabpanel');
    expect(activeTab.getAttribute('aria-controls')).toBe(panel.id);
    expect(panel.getAttribute('aria-labelledby')).toBe(activeTab.id);
  });

  it('ينقل التركيز roving: النشط tabIndex=0 والبقية -1', () => {
    render(<Tabs items={items} />);
    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveAttribute('tabindex', '0');
    expect(tabs[1]).toHaveAttribute('tabindex', '-1');
  });

  it('النقر يبدّل اللوحة النشطة', async () => {
    render(<Tabs items={items} />);
    await userEvent.click(screen.getByRole('tab', { name: 'الثاني' }));
    expect(screen.getByRole('tab', { name: 'الثاني' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel')).toHaveTextContent('محتوى الثاني');
  });

  it('RTL: ArrowLeft يتقدّم و ArrowRight يتراجع', () => {
    render(<Tabs items={items} dir="rtl" />);
    const first = screen.getByRole('tab', { name: 'الأول' });
    // ArrowLeft = للأمام في RTL ⇒ ينتقل للثاني.
    fireEvent.keyDown(first, { key: 'ArrowLeft' });
    expect(screen.getByRole('tab', { name: 'الثاني' })).toHaveAttribute('aria-selected', 'true');
    // ArrowRight = للخلف في RTL ⇒ يعود للأول.
    fireEvent.keyDown(screen.getByRole('tab', { name: 'الثاني' }), { key: 'ArrowRight' });
    expect(screen.getByRole('tab', { name: 'الأول' })).toHaveAttribute('aria-selected', 'true');
  });

  it('Home/End ينتقلان لأول/آخر تبويب مُفعّل (يتخطّى المعطّل)', () => {
    render(<Tabs items={items} dir="rtl" />);
    const first = screen.getByRole('tab', { name: 'الأول' });
    // End ⇒ آخر مُفعّل = «الثاني» (الثالث معطّل).
    fireEvent.keyDown(first, { key: 'End' });
    expect(screen.getByRole('tab', { name: 'الثاني' })).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(screen.getByRole('tab', { name: 'الثاني' }), { key: 'Home' });
    expect(screen.getByRole('tab', { name: 'الأول' })).toHaveAttribute('aria-selected', 'true');
  });

  it('التبويب المعطّل غير قابل للاختيار', async () => {
    render(<Tabs items={items} />);
    const disabled = screen.getByRole('tab', { name: 'الثالث' });
    expect(disabled).toBeDisabled();
    await userEvent.click(disabled);
    expect(screen.getByRole('tab', { name: 'الأول' })).toHaveAttribute('aria-selected', 'true');
  });

  it('النمط المُتحكَّم: يستدعي onChange ويحترم value', async () => {
    const onChange = vi.fn();
    const { rerender } = render(<Tabs items={items} value="a" onChange={onChange} />);
    await userEvent.click(screen.getByRole('tab', { name: 'الثاني' }));
    expect(onChange).toHaveBeenCalledWith('b');
    // القيمة مُتحكَّمة خارجيًّا ⇒ تبقى «a» حتى يُحدّثها الأب.
    expect(screen.getByRole('tab', { name: 'الأول' })).toHaveAttribute('aria-selected', 'true');
    rerender(<Tabs items={items} value="b" onChange={onChange} />);
    expect(screen.getByRole('tab', { name: 'الثاني' })).toHaveAttribute('aria-selected', 'true');
  });
});
