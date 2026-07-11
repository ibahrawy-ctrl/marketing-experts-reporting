import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { StickyBar } from './StickyBar';

// ===== UX-PRIMITIVES Phase 0 — اختبارات StickyBar (تصيير + region + sticky) =====

describe('StickyBar', () => {
  it('يصيّر المحتوى ضمن region مُعنون', () => {
    render(
      <StickyBar ariaLabel="الفلاتر">
        <button>تطبيق</button>
      </StickyBar>,
    );
    const region = screen.getByRole('region', { name: 'الفلاتر' });
    expect(region).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تطبيق' })).toBeInTheDocument();
  });

  it('يطبّق sticky top افتراضيًّا و bottom عند الطلب', () => {
    const { rerender } = render(<StickyBar ariaLabel="ش">x</StickyBar>);
    const region = screen.getByRole('region', { name: 'ش' });
    expect(region.className).toContain('sticky');
    expect(region.className).toContain('top-0');
    rerender(
      <StickyBar ariaLabel="ش" position="bottom">
        x
      </StickyBar>,
    );
    expect(screen.getByRole('region', { name: 'ش' }).className).toContain('bottom-0');
  });
});
