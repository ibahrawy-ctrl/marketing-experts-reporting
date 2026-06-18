// الإعدادات — معلومات الحساب الحالي، الهوية، ودلائل النظام.
import { useState, type FormEvent } from 'react';
import { useAuth } from '../lib/auth';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { Card, Badge, Field, Input, Button, Alert } from '../components/ui';
import { CardsSkeleton } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { roleLabel } from '../lib/format';
import { apiErrorMessage } from '../lib/api';

export default function SettingsPage() {
  const { user } = useAuth();
  const users = useDirectoryUsers();
  const teams = useTeams();
  const departments = useDepartments();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">الإعدادات</h1>
        <p className="mt-1 text-sm text-ink-2">معلومات الحساب والنظام.</p>
      </div>

      <Card>
        <SectionTitle title="حسابي" />
        <dl className="grid gap-3 sm:grid-cols-2">
          <div>
            <dt className="text-xs text-ink-2">الاسم</dt>
            <dd className="font-semibold text-navy">{user?.fullName ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">البريد</dt>
            <dd className="font-semibold text-navy">{user?.email ?? '—'}</dd>
          </div>
          <div className="sm:col-span-2">
            <dt className="text-xs text-ink-2">الأدوار</dt>
            <dd className="mt-1 flex flex-wrap gap-1">
              {(user?.roles ?? []).map((r) => (
                <Badge key={r} tone="navy">{roleLabel[r]}</Badge>
              ))}
            </dd>
          </div>
        </dl>
      </Card>

      <ChangeEmailCard />

      <ChangePasswordCard />

      <Card>
        <SectionTitle title="ملخص النظام" />
        {users.isLoading || teams.isLoading || departments.isLoading ? (
          <CardsSkeleton count={3} />
        ) : (
          <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <Stat label="المستخدمون" value={(users.data ?? []).length} />
            <Stat label="الإدارات" value={(departments.data ?? []).length} />
            <Stat label="الفرق" value={(teams.data ?? []).length} />
          </dl>
        )}
      </Card>

      <Card>
        <SectionTitle title="الهوية" />
        <div className="flex items-center gap-4">
          <div className="rounded-xl bg-navy px-4 py-3">
            <img src="/logo-arabic.png" alt="خبراء التسويق" className="h-9" />
          </div>
          <div className="text-sm text-ink-2">
            <p className="font-semibold text-navy">نظام تقارير الأداء والتشغيل الداخلي</p>
            <p>خبراء التسويق · تسويق أوضح … نمو أقوى.</p>
          </div>
        </div>
      </Card>
    </div>
  );
}

// بطاقة تغيير بريد الدخول للحساب الحالي، مع تأكيد بكلمة المرور الحالية.
function ChangeEmailCard() {
  const { user, changeEmail } = useAuth();
  const [newEmail, setNewEmail] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [status, setStatus] = useState<'idle' | 'saving'>('idle');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(false);
    setStatus('saving');
    try {
      await changeEmail(newEmail, currentPassword);
      setSuccess(true);
      setNewEmail('');
      setCurrentPassword('');
    } catch (err) {
      setError(apiErrorMessage(err, 'تعذّر تغيير البريد الإلكتروني.'));
    } finally {
      setStatus('idle');
    }
  }

  return (
    <Card>
      <SectionTitle title="تغيير البريد الإلكتروني" />
      <form onSubmit={onSubmit} className="space-y-4">
        <Alert tone="navy">
          البريد الإلكتروني هو هوية تسجيل الدخول. سجّل الدخول بالبريد الجديد بعد التغيير.
        </Alert>

        <Field label="البريد الإلكتروني الحالي">
          <Input type="email" value={user?.email ?? ''} disabled />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="البريد الإلكتروني الجديد">
            <Input
              type="email"
              autoComplete="email"
              value={newEmail}
              onChange={(e) => setNewEmail(e.target.value)}
              required
            />
          </Field>
          <Field label="كلمة المرور الحالية (للتأكيد)">
            <Input
              type="password"
              autoComplete="current-password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
            />
          </Field>
        </div>

        {error && <Alert tone="alert">{error}</Alert>}
        {success && <Alert tone="success">تم تغيير البريد الإلكتروني بنجاح.</Alert>}

        <Button type="submit" disabled={status === 'saving'}>
          {status === 'saving' ? 'جارٍ الحفظ…' : 'تغيير البريد الإلكتروني'}
        </Button>
      </form>
    </Card>
  );
}

// بطاقة تغيير كلمة المرور للحساب الحالي، مع عرض سياسة كلمة المرور وإظهار رسالة الخادم (لمعرفة الشرط الناقص).
function ChangePasswordCard() {
  const { changePassword } = useAuth();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [status, setStatus] = useState<'idle' | 'saving'>('idle');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(false);

    if (newPassword !== confirmPassword) {
      setError('كلمة المرور الجديدة وتأكيدها غير متطابقين.');
      return;
    }

    setStatus('saving');
    try {
      await changePassword(currentPassword, newPassword);
      setSuccess(true);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err) {
      setError(apiErrorMessage(err, 'تعذّر تغيير كلمة المرور.'));
    } finally {
      setStatus('idle');
    }
  }

  return (
    <Card>
      <SectionTitle title="تغيير كلمة المرور" />
      <form onSubmit={onSubmit} className="space-y-4">
        <Alert tone="navy">
          سياسة كلمة المرور: 8 أحرف على الأقل، وتشمل حرفًا كبيرًا (A-Z) وحرفًا صغيرًا (a-z) ورقمًا (0-9).
        </Alert>

        <Field label="كلمة المرور الحالية">
          <Input
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            required
          />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="كلمة المرور الجديدة">
            <Input
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
            />
          </Field>
          <Field label="تأكيد كلمة المرور الجديدة">
            <Input
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
            />
          </Field>
        </div>

        {error && <Alert tone="alert">{error}</Alert>}
        {success && <Alert tone="success">تم تغيير كلمة المرور بنجاح. ستُنهى الجلسات الأخرى.</Alert>}

        <Button type="submit" disabled={status === 'saving'}>
          {status === 'saving' ? 'جارٍ الحفظ…' : 'تغيير كلمة المرور'}
        </Button>
      </form>
    </Card>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl border border-line bg-offwhite p-4 text-center">
      <p className="text-2xl font-bold text-navy">{value}</p>
      <p className="text-xs text-ink-2">{label}</p>
    </div>
  );
}
