import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { Alert, Badge, Card } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { permissionUnitLabel } from '../lib/format';
import type { MyBalancesDto, BalanceSummaryDto } from '../types/api';

// أرصدتي (خدمات الموظف، V1.1). الرصيد مشتقّ من الحركات؛ الخادم يقصره على المستخدم نفسه.
// الرصيد السالب مسموح به مع تحذير واضح (المنع الصارم مؤجّل).
export default function MyBalancesPage() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['my-balances'],
    queryFn: async () => (await api.get<MyBalancesDto>('/me/balances')).data,
  });

  if (isLoading) return <LoadingState label="يتم تحميل أرصدتك…" />;
  if (isError || !data)
    return <QueryError onRetry={() => refetch()} description="حدث خطأ أثناء جلب أرصدتك. أعد المحاولة." />;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">أرصدتي</h1>
        <p className="mt-1 text-sm text-ink-2">
          رصيد إجازاتك وأذوناتك لسنة {data.year}. يُخصم آليًّا من الرصيد عند الاعتماد النهائي للطلب،
          ويُعاد عند إبطال طلب معتمَد. الرصيد محسوب من حركات معتمَدة لا يمكن حذفها.
        </p>
      </div>

      {(data.annualLeave.isNegative || data.permission.isNegative) && (
        <Alert tone="gold">
          أحد أرصدتك سالب — أي أنّ ما اعتُمد لك يتجاوز رصيدك المتاح. راجع الموارد البشرية لتسوية الرصيد.
        </Alert>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        <BalanceCard
          title="رصيد الإجازات (بالأيام)"
          summary={data.annualLeave}
          unitLabel="يوم"
        />
        <BalanceCard
          title="رصيد الأذونات"
          summary={data.permission}
          unitLabel={data.permissionUnit === 'Hours' ? 'ساعة' : 'إذن'}
        />
      </div>

      <Card>
        <h2 className="mb-3 font-semibold text-navy">معلومات إضافية</h2>
        <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <Info label="طلبات قيد الاعتماد" value={`${data.pendingLeaveRequests}`} />
          <Info label="وحدة الأذونات" value={permissionUnitLabel[data.permissionUnit]} />
          {data.permissionMonthlyLimit != null && (
            <Info label="حدّ الأذونات الشهري" value={`${data.permissionMonthlyLimit}`} />
          )}
          {data.permissionUsedThisMonth != null && (
            <Info label="المستخدَم هذا الشهر" value={`${data.permissionUsedThisMonth}`} />
          )}
          {data.permissionRemainingThisMonth != null && (
            <Info label="المتبقّي هذا الشهر" value={`${data.permissionRemainingThisMonth}`} />
          )}
          {data.permissionAnnualLimit != null && (
            <Info label="حدّ الأذونات السنوي" value={`${data.permissionAnnualLimit}`} />
          )}
          <Info label="السماح بالرصيد السالب" value={data.allowNegativeBalance ? 'نعم' : 'لا'} />
          <Info label="سياسة معرّفة لهذه السنة" value={data.hasPolicy ? 'نعم' : 'لا (قيم افتراضية)'} />
        </div>
        {!data.hasPolicy && (
          <p className="mt-3 text-xs text-ink-2">
            لا توجد سياسة رصيد معرّفة لسنتك بعد، لذا تُعرض القيم الافتراضية. تتولّى الموارد البشرية ضبط
            الأرصدة الافتتاحية والسياسات.
          </p>
        )}
      </Card>
    </div>
  );
}

function BalanceCard({
  title,
  summary,
  unitLabel,
}: {
  title: string;
  summary: BalanceSummaryDto;
  unitLabel: string;
}) {
  return (
    <Card>
      <div className="flex items-center justify-between">
        <h2 className="font-semibold text-navy">{title}</h2>
        {summary.isNegative ? <Badge tone="alert">رصيد سالب</Badge> : <Badge tone="success">متاح</Badge>}
      </div>
      <p className={`mt-3 text-3xl font-bold ${summary.isNegative ? 'text-red-600' : 'text-navy'}`}>
        {summary.remaining}
        <span className="mr-1 text-base font-medium text-ink-2"> {unitLabel}</span>
      </p>
      <div className="mt-3 grid grid-cols-2 gap-3 text-sm">
        <Info label="إجمالي المُضاف" value={`${summary.credited}`} />
        <Info label="إجمالي المخصوم" value={`${summary.debited}`} />
      </div>
    </Card>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-ink-2">{label}</p>
      <p className="font-medium text-ink">{value}</p>
    </div>
  );
}
