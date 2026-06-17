// الإعدادات — معلومات الحساب الحالي، الهوية، ودلائل النظام.
import { useAuth } from '../lib/auth';
import { useDirectoryUsers, useTeams, useDepartments } from '../lib/useDirectory';
import { Card, Badge } from '../components/ui';
import { CardsSkeleton } from '../components/states';
import { SectionTitle } from '../components/dashboard';
import { roleLabel } from '../lib/format';

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

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl border border-line bg-offwhite p-4 text-center">
      <p className="text-2xl font-bold text-navy">{value}</p>
      <p className="text-xs text-ink-2">{label}</p>
    </div>
  );
}
