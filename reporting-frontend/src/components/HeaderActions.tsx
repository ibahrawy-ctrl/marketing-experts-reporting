// عناصر الترويسة (UI-NAV-RESTRUCTURE-R2) — Frontend فقط: بحث عام، إجراءات سريعة، بانتظار اعتمادي،
// حسابي (تغيير كلمة المرور/البريد + خروج)، ومساعدة. كلها روابط لمسارات قائمة فقط ولا توسّع أيّ صلاحية.
import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { roleLabel } from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import { searchableTabs } from '../lib/navConfig';

// زرّ ترويسة بأيقونة موحّد الشكل.
function HeaderButton({
  label,
  onClick,
  active,
  children,
}: {
  label: string;
  onClick?: () => void;
  active?: boolean;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={onClick}
      className={`grid h-9 w-9 place-items-center rounded-lg transition hover:bg-navy-600 ${
        active ? 'bg-navy-600' : ''
      }`}
    >
      {children}
    </button>
  );
}

// حاوية قائمة منسدلة تُغلق عند النقر خارجها أو Escape.
function Popover({
  open,
  onClose,
  children,
  className = '',
}: {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  className?: string;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    function onDoc(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDoc);
      document.removeEventListener('keydown', onKey);
    };
  }, [open, onClose]);
  if (!open) return null;
  return (
    <div
      ref={ref}
      className={`absolute left-0 top-11 z-30 rounded-xl border border-line bg-white text-ink shadow-lg ${className}`}
    >
      {children}
    </div>
  );
}

// أيقونات خطية مضمّنة (خاصّة بالترويسة).
function GlyphSearch() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round">
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-3.2-3.2" />
    </svg>
  );
}
function GlyphBolt() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
      <path d="M13 3 4 14h7l-1 7 9-11h-7l1-7Z" />
    </svg>
  );
}
function GlyphInbox() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 13h4l2 3h6l2-3h4M4 13 6 5h12l2 8v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-5Z" />
    </svg>
  );
}
function GlyphHelp() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M9.5 9a2.5 2.5 0 0 1 4.8.9c0 1.7-2.3 2.1-2.3 3.6" />
      <path d="M12 17h.01" />
    </svg>
  );
}

// ===== البحث العام — تصفية التبويبات التي يراها المستخدم فقط =====
function GlobalSearch() {
  const { user, hasAnyRole, isSalesRep, isSalesB2cTeamLeader } = useAuth();
  const jobRoleCode = user?.jobRoleCode ?? null;
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState('');
  const tabs = useMemo(() => searchableTabs({ hasAnyRole, isSalesRep, isSalesB2cTeamLeader, jobRoleCode }), [hasAnyRole, isSalesRep, isSalesB2cTeamLeader, jobRoleCode]);
  const results = useMemo(() => {
    const term = q.trim();
    if (!term) return [];
    return tabs
      .filter((t) => t.label.includes(term) || t.moduleLabel.includes(term) || t.keywords.includes(term))
      .slice(0, 8);
  }, [q, tabs]);

  function go(to: string) {
    setOpen(false);
    setQ('');
    navigate(to);
  }

  return (
    <div className="relative">
      <HeaderButton label="بحث" active={open} onClick={() => setOpen((v) => !v)}>
        <GlyphSearch />
      </HeaderButton>
      <Popover open={open} onClose={() => setOpen(false)} className="w-72 p-2">
        <input
          autoFocus
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="ابحث في الشاشات…"
          className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
        />
        <div className="mt-2 max-h-72 overflow-auto">
          {q.trim() && results.length === 0 && (
            <p className="px-2 py-3 text-center text-xs text-ink-2">لا نتائج مطابقة.</p>
          )}
          {results.map((r) => (
            <button
              key={r.to}
              type="button"
              onClick={() => go(r.to)}
              className="flex w-full items-center justify-between rounded-lg px-2.5 py-2 text-right text-sm hover:bg-navy-50"
            >
              <span className="font-medium text-ink">{r.label}</span>
              <span className="text-[11px] text-ink-3">{r.moduleLabel}</span>
            </button>
          ))}
        </div>
      </Popover>
    </div>
  );
}

// ===== إجراءات سريعة — اختصارات لمسارات متاحة للمستخدم فقط =====
function QuickActions() {
  const { user, hasAnyRole, isSalesRep, isSalesB2cTeamLeader, canApprove } = useAuth();
  const jobRoleCode = user?.jobRoleCode ?? null;
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const allowed = useMemo(
    () => new Set(searchableTabs({ hasAnyRole, isSalesRep, isSalesB2cTeamLeader, jobRoleCode }).map((t) => t.to)),
    [hasAnyRole, isSalesRep, isSalesB2cTeamLeader, jobRoleCode],
  );
  const shortcuts = useMemo(() => {
    const base = [
      { to: '/app/submissions', label: 'تقرير جديد / التقارير المقدَّمة' },
      { to: '/app/leave-requests', label: 'طلب إجازة / استئذان' },
      { to: '/app/hr-requests', label: 'طلب موارد بشرية' },
      { to: '/app/my-kpi', label: 'مؤشرات أدائي' },
      { to: '/app/balances', label: 'أرصدتي' },
    ];
    const list = base.filter((s) => allowed.has(s.to));
    if (canApprove && allowed.has('/app/workflows')) list.push({ to: '/app/workflows', label: 'مسارات الاعتماد' });
    return list;
  }, [allowed, canApprove]);

  if (shortcuts.length === 0) return null;

  function go(to: string) {
    setOpen(false);
    navigate(to);
  }

  return (
    <div className="relative">
      <HeaderButton label="إجراءات سريعة" active={open} onClick={() => setOpen((v) => !v)}>
        <GlyphBolt />
      </HeaderButton>
      <Popover open={open} onClose={() => setOpen(false)} className="w-60 p-1.5">
        <p className="px-2.5 py-1.5 text-[11px] font-bold text-ink-3">إجراءات سريعة</p>
        {shortcuts.map((s) => (
          <button
            key={s.to}
            type="button"
            onClick={() => go(s.to)}
            className="block w-full rounded-lg px-2.5 py-2 text-right text-sm font-medium text-ink hover:bg-navy-50"
          >
            {s.label}
          </button>
        ))}
      </Popover>
    </div>
  );
}

// ===== بانتظار اعتمادي — يظهر لمن يملك صلاحية الاعتماد (Frontend فقط) =====
function PendingActions() {
  const { canApprove } = useAuth();
  const navigate = useNavigate();
  if (!canApprove) return null;
  return (
    <HeaderButton label="بانتظار اعتمادي" onClick={() => navigate('/app/workflows')}>
      <GlyphInbox />
    </HeaderButton>
  );
}

// ===== مساعدة =====
function HelpMenu() {
  const [open, setOpen] = useState(false);
  return (
    <div className="relative">
      <HeaderButton label="مساعدة" active={open} onClick={() => setOpen((v) => !v)}>
        <GlyphHelp />
      </HeaderButton>
      <Popover open={open} onClose={() => setOpen(false)} className="w-64 p-3">
        <p className="text-sm font-bold text-navy">بحاجة لمساعدة؟</p>
        <p className="mt-1 text-xs text-ink-2">
          دليل الاستخدام المصوّر متاح في مركز المساعدة. تواصل مع مسؤول النظام عند الحاجة.
        </p>
      </Popover>
    </div>
  );
}

// ===== حسابي — الاسم/البريد/الأدوار + تغيير كلمة المرور/البريد (self-service) + خروج =====
function ProfileMenu() {
  const { user, logout, changePassword, changeEmail } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [panel, setPanel] = useState<'menu' | 'password' | 'email'>('menu');

  async function handleLogout() {
    setOpen(false);
    await logout();
    navigate('/login');
  }

  function close() {
    setOpen(false);
    setPanel('menu');
  }

  const initials = (user?.fullName ?? '?').trim().charAt(0);

  return (
    <div className="relative">
      <button
        type="button"
        aria-label="حسابي"
        onClick={() => setOpen((v) => !v)}
        className="flex items-center gap-2 rounded-lg py-1 pl-1 pr-2 transition hover:bg-navy-600"
      >
        <span className="grid h-8 w-8 place-items-center rounded-full bg-white text-sm font-bold text-navy">
          {initials}
        </span>
        <span className="hidden text-right sm:block">
          <span className="block text-sm font-semibold leading-tight">{user?.fullName}</span>
          <span className="block text-[11px] leading-tight text-navy-100">
            {(user?.roles ?? []).map((r) => roleLabel[r]).join(' · ')}
          </span>
        </span>
      </button>
      <Popover open={open} onClose={close} className="w-72 p-3">
        {panel === 'menu' && (
          <div>
            <div className="border-b border-line pb-3">
              <p className="text-sm font-bold text-navy">{user?.fullName}</p>
              <p className="mt-0.5 text-xs text-ink-2">{user?.email}</p>
              <div className="mt-1.5 flex flex-wrap gap-1">
                {(user?.roles ?? []).map((r) => (
                  <span key={r} className="rounded-full bg-navy-50 px-2 py-0.5 text-[11px] font-semibold text-navy">
                    {roleLabel[r]}
                  </span>
                ))}
              </div>
            </div>
            <div className="mt-2 space-y-0.5">
              <button
                type="button"
                onClick={() => setPanel('password')}
                className="block w-full rounded-lg px-2.5 py-2 text-right text-sm font-medium text-ink hover:bg-navy-50"
              >
                تغيير كلمة المرور
              </button>
              <button
                type="button"
                onClick={() => setPanel('email')}
                className="block w-full rounded-lg px-2.5 py-2 text-right text-sm font-medium text-ink hover:bg-navy-50"
              >
                تغيير البريد الإلكتروني
              </button>
              <button
                type="button"
                onClick={handleLogout}
                className="block w-full rounded-lg px-2.5 py-2 text-right text-sm font-medium text-alert hover:bg-red-50"
              >
                تسجيل الخروج
              </button>
            </div>
          </div>
        )}
        {panel === 'password' && (
          <ChangePasswordForm
            onDone={() => setPanel('menu')}
            onBack={() => setPanel('menu')}
            changePassword={changePassword}
          />
        )}
        {panel === 'email' && (
          <ChangeEmailForm
            currentEmail={user?.email ?? ''}
            onDone={() => setPanel('menu')}
            onBack={() => setPanel('menu')}
            changeEmail={changeEmail}
          />
        )}
      </Popover>
    </div>
  );
}

function BackRow({ title, onBack }: { title: string; onBack: () => void }) {
  return (
    <div className="mb-2 flex items-center justify-between border-b border-line pb-2">
      <span className="text-sm font-bold text-navy">{title}</span>
      <button type="button" onClick={onBack} className="text-xs font-medium text-navy-600 hover:underline">
        رجوع
      </button>
    </div>
  );
}

function ChangePasswordForm({
  onDone,
  onBack,
  changePassword,
}: {
  onDone: () => void;
  onBack: () => void;
  changePassword: (current: string, next: string) => Promise<void>;
}) {
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (next !== confirm) {
      setError('كلمة المرور الجديدة وتأكيدها غير متطابقين.');
      return;
    }
    setSaving(true);
    try {
      await changePassword(current, next);
      onDone();
    } catch (err) {
      setError(apiErrorMessage(err, 'تعذّر تغيير كلمة المرور.'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="space-y-2">
      <BackRow title="تغيير كلمة المرور" onBack={onBack} />
      <p className="rounded-lg border border-navy-100 bg-navy-50 p-2 text-[11px] text-navy">
        8 أحرف على الأقل، وتشمل حرفًا كبيرًا وصغيرًا ورقمًا.
      </p>
      <input type="password" autoComplete="current-password" placeholder="كلمة المرور الحالية" value={current} onChange={(e) => setCurrent(e.target.value)} required className="w-full rounded-lg border border-line px-3 py-2 text-sm outline-none focus:border-navy" />
      <input type="password" autoComplete="new-password" placeholder="كلمة المرور الجديدة" value={next} onChange={(e) => setNext(e.target.value)} required className="w-full rounded-lg border border-line px-3 py-2 text-sm outline-none focus:border-navy" />
      <input type="password" autoComplete="new-password" placeholder="تأكيد كلمة المرور الجديدة" value={confirm} onChange={(e) => setConfirm(e.target.value)} required className="w-full rounded-lg border border-line px-3 py-2 text-sm outline-none focus:border-navy" />
      {error && <p className="rounded-lg border border-red-100 bg-red-50 p-2 text-[11px] text-alert">{error}</p>}
      <button type="submit" disabled={saving} className="w-full rounded-lg bg-orange px-4 py-2 text-sm font-semibold text-white transition hover:bg-orange-600 disabled:opacity-50">
        {saving ? 'جارٍ الحفظ…' : 'حفظ'}
      </button>
    </form>
  );
}

function ChangeEmailForm({
  currentEmail,
  onDone,
  onBack,
  changeEmail,
}: {
  currentEmail: string;
  onDone: () => void;
  onBack: () => void;
  changeEmail: (newEmail: string, currentPassword: string) => Promise<void>;
}) {
  const [newEmail, setNewEmail] = useState('');
  const [password, setPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      await changeEmail(newEmail, password);
      onDone();
    } catch (err) {
      setError(apiErrorMessage(err, 'تعذّر تغيير البريد الإلكتروني.'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="space-y-2">
      <BackRow title="تغيير البريد الإلكتروني" onBack={onBack} />
      <p className="rounded-lg border border-navy-100 bg-navy-50 p-2 text-[11px] text-navy">
        البريد هو هوية الدخول. سجّل الدخول بالبريد الجديد بعد التغيير.
      </p>
      <input type="email" value={currentEmail} disabled className="w-full rounded-lg border border-line bg-offwhite px-3 py-2 text-sm text-ink-2" />
      <input type="email" autoComplete="email" placeholder="البريد الإلكتروني الجديد" value={newEmail} onChange={(e) => setNewEmail(e.target.value)} required className="w-full rounded-lg border border-line px-3 py-2 text-sm outline-none focus:border-navy" />
      <input type="password" autoComplete="current-password" placeholder="كلمة المرور الحالية (للتأكيد)" value={password} onChange={(e) => setPassword(e.target.value)} required className="w-full rounded-lg border border-line px-3 py-2 text-sm outline-none focus:border-navy" />
      {error && <p className="rounded-lg border border-red-100 bg-red-50 p-2 text-[11px] text-alert">{error}</p>}
      <button type="submit" disabled={saving} className="w-full rounded-lg bg-orange px-4 py-2 text-sm font-semibold text-white transition hover:bg-orange-600 disabled:opacity-50">
        {saving ? 'جارٍ الحفظ…' : 'حفظ'}
      </button>
    </form>
  );
}

// المجموعة اليسرى من الترويسة: البحث/الإجراءات/بانتظار/المساعدة (تُخفى على الشاشات الصغيرة).
export function HeaderQuickTools() {
  return (
    <div className="hidden items-center gap-1 md:flex">
      <GlobalSearch />
      <QuickActions />
      <PendingActions />
      <HelpMenu />
    </div>
  );
}

export { ProfileMenu };
