import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { useState } from 'react';
// مصدر SubmissionsPage كنصّ خام (ميزة Vite ?raw) — لفحص كتلة الحذف الإداريّ مصدريًّا في الاختبار 11.
import submissionsSource from './pages/SubmissionsPage.tsx?raw';
import {
  ToastProvider,
  useToast,
  POST_SUCCESS_NAV_DELAY_MS,
} from './components/ActionResultToast';
import { Button } from './components/ui';
import { approvalErrorMessage } from './lib/api';

// APPROVAL ACTION UX R1 — مواصفة سلوك طبقة تغذية الاعتماد الموحّدة (10 اختبارات).

// خطأ Axios مُصطنَع: يحمل isAxiosError=true حتى يتعرّف عليه axios.isAxiosError،
// مع response.status وData.type (كود الخطأ يوضع في «type» لا «code»).
function axiosErr(status: number, type?: string, detail?: string) {
  return { isAxiosError: true, response: { status, data: { type, detail } }, message: 'err' };
}

// مكوّن يطلق toast.success عند الضغط.
function SuccessTrigger({ message }: { message: string }) {
  const toast = useToast();
  return <button onClick={() => toast.success(message)}>trigger</button>;
}

// نمط القرار النهائي: invalidate ⟶ toast ⟶ setTimeout(onBack, 700).
function TerminalAction({ onBack, invalidate }: { onBack: () => void; invalidate: () => Promise<void> }) {
  const toast = useToast();
  const run = async () => {
    await invalidate();
    toast.success('✅ تم اعتماد التقرير بنجاح');
    setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS);
  };
  return <button onClick={run}>approve</button>;
}

// نمط حفظ المسودة: toast فقط، بلا رجوع.
function SaveDraft({ onBack }: { onBack: () => void }) {
  const toast = useToast();
  const run = async () => {
    // حفظ المسودة يبقى في الصفحة: Toast فقط، بلا setTimeout(onBack) — عمدًا.
    void onBack;
    toast.success('✅ تم حفظ البيانات بنجاح');
  };
  return <button onClick={run}>save</button>;
}

afterEach(() => {
  vi.useRealTimers();
});

describe('APPROVAL ACTION UX R1', () => {
  it('1. يعرض Toast نجاح عند نداء toast.success', () => {
    render(
      <ToastProvider>
        <SuccessTrigger message="✅ تم الاعتماد" />
      </ToastProvider>,
    );
    fireEvent.click(screen.getByText('trigger'));
    expect(screen.getByText('✅ تم الاعتماد')).toBeInTheDocument();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('2. يُخفي Toast تلقائيًّا بعد 3500ms', () => {
    vi.useFakeTimers();
    render(
      <ToastProvider>
        <SuccessTrigger message="سيختفي" />
      </ToastProvider>,
    );
    act(() => {
      fireEvent.click(screen.getByText('trigger'));
    });
    expect(screen.getByText('سيختفي')).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(3500);
    });
    expect(screen.queryByText('سيختفي')).not.toBeInTheDocument();
  });

  it('3. زر التحميل يعرض Spinner ويُعطَّل', () => {
    const { container } = render(<Button loading>اعتماد</Button>);
    const btn = screen.getByRole('button');
    expect(btn).toBeDisabled();
    expect(container.querySelector('.animate-spin')).toBeInTheDocument();
  });

  it('4. يمنع النقر المزدوج (loading ⟶ disabled ⟶ لا onClick)', () => {
    const onClick = vi.fn();
    render(
      <Button loading onClick={onClick}>
        اعتماد
      </Button>,
    );
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
  });

  it('5. رسالة نوعية لـ 403 (auth.forbidden = اعتمده مستخدم آخر)', () => {
    const msg = approvalErrorMessage(axiosErr(403, 'auth.forbidden'));
    expect(msg).toContain('بواسطة مستخدم آخر');
  });

  it('6. رسالة نوعية لـ 409 (الطلب لم يعد متاحًا)', () => {
    const msg = approvalErrorMessage(axiosErr(409, 'submission.not_actionable.conflict'));
    expect(msg).toContain('لم يعد متاحًا');
  });

  it('7. مهلة الرجوع = 700ms وتُؤجَّل الملاحة حتى انقضائها', () => {
    expect(POST_SUCCESS_NAV_DELAY_MS).toBe(700);
    vi.useFakeTimers();
    const onBack = vi.fn();
    setTimeout(onBack, POST_SUCCESS_NAV_DELAY_MS);
    vi.advanceTimersByTime(699);
    expect(onBack).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('8. حفظ المسودة: Toast بلا رجوع للقائمة', async () => {
    vi.useFakeTimers();
    const onBack = vi.fn();
    render(
      <ToastProvider>
        <SaveDraft onBack={onBack} />
      </ToastProvider>,
    );
    await act(async () => {
      fireEvent.click(screen.getByText('save'));
    });
    expect(screen.getByText('✅ تم حفظ البيانات بنجاح')).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(onBack).not.toHaveBeenCalled();
  });

  it('9. القرار النهائي: Toast ثم رجوع بعد 700ms', async () => {
    vi.useFakeTimers();
    const onBack = vi.fn();
    const invalidate = vi.fn().mockResolvedValue(undefined);
    render(
      <ToastProvider>
        <TerminalAction onBack={onBack} invalidate={invalidate} />
      </ToastProvider>,
    );
    await act(async () => {
      fireEvent.click(screen.getByText('approve'));
    });
    expect(invalidate).toHaveBeenCalledTimes(1);
    expect(screen.getByText('✅ تم اعتماد التقرير بنجاح')).toBeInTheDocument();
    expect(onBack).not.toHaveBeenCalled();
    act(() => {
      vi.advanceTimersByTime(POST_SUCCESS_NAV_DELAY_MS);
    });
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('10. يبقى Toast ظاهرًا بعد تغيّر المسار (تفكيك الطفل)', () => {
    function Wrapper() {
      const [showChild, setShowChild] = useState(true);
      return (
        <ToastProvider>
          {showChild ? <SuccessTrigger message="يبقى ظاهرًا" /> : <div>route B</div>}
          <button onClick={() => setShowChild(false)}>navigate</button>
        </ToastProvider>
      );
    }
    render(<Wrapper />);
    fireEvent.click(screen.getByText('trigger'));
    expect(screen.getByText('يبقى ظاهرًا')).toBeInTheDocument();
    // تغيير المسار يُفكّك مكوّن الطفل لكن الـToast مرفوع في المزوّد الأعلى فيبقى.
    fireEvent.click(screen.getByText('navigate'));
    expect(screen.getByText('route B')).toBeInTheDocument();
    expect(screen.getByText('يبقى ظاهرًا')).toBeInTheDocument();
  });

  // 11. حارس النطاق: «الحذف الإداريّ» (ADMIN-GOVERNANCE-R1) خارج نطاق Approval UX R1.
  // نقرأ مصدر SubmissionsPage ونعزل كتلة زر الحذف الإداريّ + طفرته، ونتحقّق أنّها لم تكتسب
  // Toast ولا Spinner (loading) ولا حارس isPending جديدًا — بل تبقى setErr + apiErrorMessage (سلوك الأصل).
  it('11. زر الحذف الإداريّ لم يكتسب Toast/Spinner (يطابق سلوك الأصل)', () => {
    const src = submissionsSource;
    // طفرة الحذف الإداريّ ما زالت تستعمل setErr + apiErrorMessage (لا Toast).
    expect(src).toContain('const adminDelete = useMutation({');
    const mutStart = src.indexOf('const adminDelete = useMutation({');
    const mutBlock = src.slice(mutStart, mutStart + 260);
    expect(mutBlock).toContain('onError: (e) => setErr(apiErrorMessage(e)),');
    expect(mutBlock).not.toContain('toast.');
    // زرّ الحذف الإداريّ: disabled فقط (لا loading=)، وonClick يستعمل setErr(null) لا حارس isPending.
    const btnAnchor = src.indexOf('حذف إداريّ للتقرير');
    const btnBlock = src.slice(btnAnchor - 400, btnAnchor);
    expect(btnBlock).toContain('disabled={adminDelete.isPending}');
    expect(btnBlock).not.toContain('loading={adminDelete');
    expect(btnBlock).not.toContain('toast.');
    expect(btnBlock).toContain('setErr(null);');
  });
});
