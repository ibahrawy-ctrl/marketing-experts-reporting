// ======================================================================
// Project 360 — تبويب النظرة العامّة (CPW-R3 · R2-W12 · §10)
//
// **صفر نداء إضافيّ**: كلّ ما يُعرَض هنا مأخوذ من `ProjectOverviewDto` الذي حمّلته
// الصفحة مرّة واحدة. أيّ `useQuery` في هذا الملفّ يعني نداءين لنفس البيانات.
// ======================================================================

import { Card, StatCard } from '../ui';
import { SectionHeading } from './shared';
import { percentOrDash } from '../../lib/project360Format';
import type { ProjectOverviewDto } from '../../types/project360';

export function ProjectOverviewTab({
  overview,
  onOpenTab,
}: {
  overview: ProjectOverviewDto;
  onOpenTab: (tabId: string) => void;
}) {
  const { objectives, kpis, deliverables, governance, strategy } = overview;

  return (
    <div className="space-y-5">
      <section>
        <SectionHeading
          title="الأهداف"
          action={<TabLink label="فتح تبويب الأهداف" onClick={() => onOpenTab('objectives')} />}
        />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
          <StatCard label="الإجمالي" value={objectives.total} />
          <StatCard label="قيد التنفيذ" value={objectives.inProgress} />
          <StatCard label="معرّضة للخطر" value={objectives.atRisk} tone="alert" />
          <StatCard label="مكتملة" value={objectives.completed} />
          <StatCard
            label="متوسّط التحقّق"
            value={percentOrDash(objectives.averageAchievementPercent)}
          />
        </div>
      </section>

      <section>
        <SectionHeading
          title="مؤشّرات الأداء"
          action={<TabLink label="فتح تبويب المؤشّرات" onClick={() => onOpenTab('kpis')} />}
        />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
          <StatCard label="الإجمالي" value={kpis.total} />
          <StatCard label="على المسار" value={kpis.onTrack} />
          <StatCard label="معرّضة للخطر" value={kpis.atRisk} />
          <StatCard label="خارج المسار" value={kpis.offTrack} tone="alert" />
          <StatCard label="بلا قراءات" value={kpis.withoutReadings} />
        </div>
      </section>

      <section>
        <SectionHeading
          title="المخرَجات التعاقديّة"
          action={
            <TabLink
              label="فتح تبويب المخرَجات التعاقديّة"
              onClick={() => onOpenTab('contract-deliverables')}
            />
          }
        />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <StatCard label="الإجمالي" value={deliverables.total} />
          <StatCard label="مُسلَّمة" value={deliverables.completed} />
          <StatCard label="قيد العمل" value={deliverables.pending} />
          <StatCard label="متأخّرة" value={deliverables.delayed} tone="alert" />
        </div>
      </section>

      <div className="grid gap-5 lg:grid-cols-2">
        <Card>
          <SectionHeading
            title="الحوكمة"
            action={<TabLink label="فتح تبويب الحوكمة" onClick={() => onOpenTab('governance')} />}
          />
          <dl className="grid grid-cols-2 gap-3 text-sm">
            <SummaryRow label="المخاطر" value={`${governance.risksOpen} / ${governance.risksTotal}`} />
            <SummaryRow
              label="القرارات"
              value={`${governance.decisionsPending} / ${governance.decisionsTotal}`}
            />
            <SummaryRow label="الملاحظات" value={`${governance.notesOpen} / ${governance.notesTotal}`} />
          </dl>
          <p className="mt-2 text-xs text-ink-2">القيمة الأولى: المفتوح/المعلَّق من الإجمالي.</p>
        </Card>

        <Card>
          <SectionHeading
            title="الاستراتيجيّة"
            action={<TabLink label="فتح تبويب الاستراتيجيّة" onClick={() => onOpenTab('strategy')} />}
          />
          {strategy.exists ? (
            <dl className="grid grid-cols-2 gap-3 text-sm">
              <SummaryRow
                label="حقول النواة المعبّأة"
                value={`${strategy.filledCoreFields} / ${strategy.totalCoreFields}`}
              />
              <SummaryRow label="السمات الإضافيّة" value={strategy.attributesCount} />
            </dl>
          ) : (
            <p className="text-sm text-ink-2">لم تُسجَّل استراتيجيّة لهذا المشروع بعد.</p>
          )}
        </Card>
      </div>
    </div>
  );
}

function SummaryRow({ label, value }: { label: string; value: string | number }) {
  return (
    <>
      <dt className="text-ink-2">{label}</dt>
      <dd className="font-semibold text-navy">{value}</dd>
    </>
  );
}

function TabLink({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className="text-sm font-medium text-orange-600 hover:underline">
      {label}
    </button>
  );
}
