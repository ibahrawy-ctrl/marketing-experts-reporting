import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { Collapsible } from './Collapsible';

// ===== UX-PRIMITIVES Phase 0 — اختبارات مكوّن Collapsible (a11y + طيّ/فتح) =====

describe('Collapsible', () => {
  it('مطويّ افتراضيًّا: aria-expanded=false والمنطقة مخفيّة', () => {
    render(
      <Collapsible title="تفاصيل">
        <p>محتوى داخليّ</p>
      </Collapsible>,
    );
    const btn = screen.getByRole('button', { name: /تفاصيل/ });
    expect(btn).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('محتوى داخليّ')).not.toBeVisible();
  });

  it('يربط الزرّ بالمنطقة عبر aria-controls/aria-labelledby', () => {
    render(
      <Collapsible title="تفاصيل">
        <p>محتوى</p>
      </Collapsible>,
    );
    const btn = screen.getByRole('button', { name: /تفاصيل/ });
    const regionId = btn.getAttribute('aria-controls');
    const region = document.getElementById(regionId!);
    expect(region).not.toBeNull();
    expect(region).toHaveAttribute('aria-labelledby', btn.id);
    expect(region).toHaveAttribute('role', 'region');
  });

  it('النقر يفتح ويُظهر المحتوى (Enter/Space عبر زرّ أصليّ)', async () => {
    render(
      <Collapsible title="تفاصيل">
        <p>محتوى داخليّ</p>
      </Collapsible>,
    );
    const btn = screen.getByRole('button', { name: /تفاصيل/ });
    await userEvent.click(btn);
    expect(btn).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('محتوى داخليّ')).toBeVisible();
  });

  it('defaultOpen=true يبدأ مفتوحًا', () => {
    render(
      <Collapsible title="تفاصيل" defaultOpen>
        <p>محتوى</p>
      </Collapsible>,
    );
    expect(screen.getByRole('button', { name: /تفاصيل/ })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('محتوى')).toBeVisible();
  });

  it('النمط المُتحكَّم: يستدعي onOpenChange ويحترم open', async () => {
    const onOpenChange = vi.fn();
    const { rerender } = render(
      <Collapsible title="تفاصيل" open={false} onOpenChange={onOpenChange}>
        <p>محتوى</p>
      </Collapsible>,
    );
    await userEvent.click(screen.getByRole('button', { name: /تفاصيل/ }));
    expect(onOpenChange).toHaveBeenCalledWith(true);
    // مُتحكَّم ⇒ يبقى مطويًّا حتى يُحدّثه الأب.
    expect(screen.getByRole('button', { name: /تفاصيل/ })).toHaveAttribute('aria-expanded', 'false');
    rerender(
      <Collapsible title="تفاصيل" open onOpenChange={onOpenChange}>
        <p>محتوى</p>
      </Collapsible>,
    );
    expect(screen.getByRole('button', { name: /تفاصيل/ })).toHaveAttribute('aria-expanded', 'true');
  });
});
