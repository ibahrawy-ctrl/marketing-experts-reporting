// ======================================================================
// Project 360 — تبويب الحوكمة (CPW-R3 · R2-W12 · W5)
//
// **قراءة فقط، بلا استثناء**: لا زرّ إنشاء ولا تعديل ولا تغيير حالة هنا. الحوكمة
// لها سير عملها ومساراتها القائمة، وفتح نافذة كتابة من مساحة المشروع كان سيُنشئ
// مسارًا ثانيًا يتجاوز ذلك السير.
// ======================================================================

import { Badge, Card, EmptyState } from '../ui';
import { LoadingState } from '../states';
import { Project360QueryError, SectionHeading } from './shared';
import {
  decisionStatusLabel,
  formatDate,
  managementNoteStatusLabel,
  managementNoteTypeLabel,
  riskSeverityLabel,
  riskStatusLabel,
} from '../../lib/format';
import { useProjectDecisions, useProjectNotes, useProjectRisks } from '../../lib/useProject360';

export function ProjectGovernanceTab({ projectId }: { projectId: string }) {
  const risks = useProjectRisks(projectId);
  const decisions = useProjectDecisions(projectId);
  const notes = useProjectNotes(projectId);

  if (risks.isLoading || decisions.isLoading || notes.isLoading) return <LoadingState />;
  if (risks.isError) return <Project360QueryError error={risks.error} onRetry={() => risks.refetch()} />;
  if (decisions.isError)
    return <Project360QueryError error={decisions.error} onRetry={() => decisions.refetch()} />;
  if (notes.isError) return <Project360QueryError error={notes.error} onRetry={() => notes.refetch()} />;

  return (
    <div className="space-y-5">
      <section>
        <SectionHeading title="المخاطر" />
        {(risks.data ?? []).length === 0 ? (
          <EmptyState title="لا توجد مخاطر مسجّلة على هذا المشروع" />
        ) : (
          <div className="space-y-2">
            {risks.data!.map((r) => (
              <Card key={r.id}>
                <div className="flex flex-wrap items-center gap-2">
                  <h4 className="text-sm font-semibold text-navy">{r.title}</h4>
                  <Badge tone="alert">{riskSeverityLabel[r.severity]}</Badge>
                  <Badge tone="muted">{riskStatusLabel[r.status]}</Badge>
                </div>
                {r.description ? <p className="mt-1 text-sm text-ink-2">{r.description}</p> : null}
                <p className="mt-1 text-xs text-ink-2">
                  المسؤول: {r.ownerFullName ?? '—'} · أُنشئت {formatDate(r.createdAtUtc)}
                </p>
              </Card>
            ))}
          </div>
        )}
      </section>

      <section>
        <SectionHeading title="القرارات" />
        {(decisions.data ?? []).length === 0 ? (
          <EmptyState title="لا توجد قرارات مسجّلة على هذا المشروع" />
        ) : (
          <div className="space-y-2">
            {decisions.data!.map((d) => (
              <Card key={d.id}>
                <div className="flex flex-wrap items-center gap-2">
                  <h4 className="text-sm font-semibold text-navy">{d.title}</h4>
                  <Badge tone="muted">{decisionStatusLabel[d.status]}</Badge>
                </div>
                {d.description ? <p className="mt-1 text-sm text-ink-2">{d.description}</p> : null}
                <p className="mt-1 text-xs text-ink-2">
                  اتّخذه: {d.madeByFullName ?? '—'} · بتاريخ {formatDate(d.decidedAtUtc)}
                </p>
              </Card>
            ))}
          </div>
        )}
      </section>

      <section>
        <SectionHeading title="الملاحظات الإداريّة" />
        {(notes.data ?? []).length === 0 ? (
          <EmptyState title="لا توجد ملاحظات إداريّة على هذا المشروع" />
        ) : (
          <div className="space-y-2">
            {notes.data!.map((n) => (
              <Card key={n.id}>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge tone="navy">{managementNoteTypeLabel[n.noteType]}</Badge>
                  <Badge tone="muted">{managementNoteStatusLabel[n.status]}</Badge>
                  {n.requiresAction ? <Badge tone="gold">تتطلّب إجراءً</Badge> : null}
                </div>
                <p className="mt-1 whitespace-pre-wrap text-sm text-ink">{n.body}</p>
                <p className="mt-1 text-xs text-ink-2">
                  بقلم: {n.authorFullName ?? '—'} · {formatDate(n.createdAtUtc)}
                </p>
              </Card>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
