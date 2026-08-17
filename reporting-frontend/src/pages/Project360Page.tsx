// ======================================================================
// Project 360 Workspace — مساحة عمل المشروع (CPW-R3 · R2-W12 · §10)
//
// **لوحة أوّلًا**: التحميل الأوّل نداء واحد `GET /projects/{id}/overview`، ومنه تُبنى
// الترويسة وبطاقة الصحّة وكلّ ملخّصات تبويب النظرة العامّة بلا نداء ثانٍ.
//
// **تبويبات كسولة**: عناصر `Tabs` تُنشئ عنصر JSX لكلّ تبويب، لكنّ المكوّن **لا يُركَّب**
// إلّا حين يصير تبويبه نشطًا ⟹ لا يُطلق استعلامه إلّا عند الفتح. فتح المساحة لا يجرّ
// وراءه استعلامات الأهداف والمؤشّرات والاستراتيجيّة والحوكمة دفعة واحدة.
//
// **التبويبات الممنوعة**: لا «مستندات» ولا «مهامّ» ولا CRM ولا «مالية» — خارج نطاق
// هذه الحزمة بقرار مالك، وإضافتها هنا كانت ستوحي بربط لا وجود له في الخادم.
//
// **الصلاحيّة عرضٌ لا حماية**: `access` يخفي أزرارًا فقط؛ الخادم يرفض بصرف النظر
// عمّا تعرضه هذه الصفحة (D-07 + FINDING-W6-04).
//
// **خريطة قدرات واحدة (P360-WF-R2 §12)**: `access` تأتي من `overview.access` الذي يبنيه
// الخادم من حرّاسه نفسها. كانت الصفحة تشتقّها محلّيًّا من الأدوار والمعرّفات — نسخة ثانية
// من قواعد التخويل تنحرف عن الأصل عند أوّل تعديل، فتَعِد بزرٍّ يردّه الخادم أو تُخفي
// إجراءً مسموحًا (وهذا ما وقع فعلًا لمالك المشروع `ProjectOwnerId` الذي لم تكن الصيغة
// المحلّيّة تعرفه أصلًا).
// ======================================================================

import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Badge, Card } from '../components/ui';
import { LoadingState } from '../components/states';
import { Tabs, type TabItem } from '../components/Tabs';
import { ProjectBriefTab } from '../components/project360/ProjectBriefTab';
import { ProjectContractDeliverablesTab } from '../components/project360/ProjectContractDeliverablesTab';
import { ProjectGovernanceTab } from '../components/project360/ProjectGovernanceTab';
import { ProjectHealthPanel } from '../components/project360/ProjectHealthPanel';
import { ProjectKpisTab } from '../components/project360/ProjectKpisTab';
import { ProjectObjectivesTab } from '../components/project360/ProjectObjectivesTab';
import { ProjectOverviewTab } from '../components/project360/ProjectOverviewTab';
import { ProjectStrategyTab } from '../components/project360/ProjectStrategyTab';
import {
  Detail,
  Project360QueryError,
  type Project360Access,
} from '../components/project360/shared';
import { formatDate, formatDateTime, projectStatusLabel, projectStatusTone, serviceTypeLabel } from '../lib/format';
import { percentOrDash, projectProgressModeLabel } from '../lib/project360Format';
import { useProjectOverview, useRecomputeProjectHealth } from '../lib/useProject360';

export default function Project360Page() {
  const { projectId } = useParams<{ projectId: string }>();
  const [tab, setTab] = useState('overview');

  const overview = useProjectOverview(projectId);
  const recompute = useRecomputeProjectHealth(projectId ?? '');

  const project = overview.data?.project;

  if (!projectId) return null;
  if (overview.isLoading) return <LoadingState label="يتم تحميل مساحة عمل المشروع…" />;
  if (overview.isError)
    return <Project360QueryError error={overview.error} onRetry={() => overview.refetch()} />;
  if (!overview.data || !project) return null;

  const data = overview.data;

  // تسمية فقط، بلا قاعدة: كلّ قرار تخويل اتُّخِذ في الخادم وهذه الأسطر تنقل نتيجته.
  const access: Project360Access = {
    canManage: data.access.canManageStructure,
    canOperate: data.access.canOperate,
    canWriteGovernance: data.access.canWriteGovernance,
  };

  const items: TabItem[] = [
    {
      id: 'overview',
      label: 'النظرة العامّة',
      content: <ProjectOverviewTab overview={data} onOpenTab={setTab} />,
    },
    { id: 'brief', label: 'الموجز والسياق', content: <ProjectBriefTab overview={data} /> },
    {
      id: 'strategy',
      label: 'الاستراتيجيّة',
      content: <ProjectStrategyTab projectId={projectId} access={access} />,
    },
    {
      id: 'objectives',
      label: 'الأهداف',
      content: <ProjectObjectivesTab projectId={projectId} access={access} />,
    },
    {
      id: 'kpis',
      label: 'المؤشّرات والقراءات',
      content: <ProjectKpisTab projectId={projectId} access={access} />,
    },
    {
      id: 'contract-deliverables',
      label: 'المخرَجات التعاقديّة',
      content: <ProjectContractDeliverablesTab projectId={projectId} access={access} />,
    },
    {
      id: 'governance',
      label: 'القرارات والحوكمة',
      content: <ProjectGovernanceTab projectId={projectId} />,
    },
  ];

  return (
    <div className="space-y-5" dir="rtl">
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-xl font-bold text-navy">{project.name}</h1>
              <Badge tone={projectStatusTone(project.status)}>
                {projectStatusLabel[project.status]}
              </Badge>
              <Badge tone="muted">{serviceTypeLabel[project.serviceType]}</Badge>
            </div>
            <p className="mt-1 text-sm text-ink-2">
              <Link to={`/app/clients/${project.clientId}`} className="hover:underline">
                {project.clientName}
              </Link>
              {' · '}
              {formatDate(project.startDate)} — {formatDate(project.endDate)}
            </p>
          </div>
          <Link
            to={`/app/projects/${project.id}`}
            className="text-sm font-medium text-orange-600 hover:underline"
          >
            العودة إلى صفحة المشروع
          </Link>
        </div>

        {/* المسنَدون بالأسماء (§12): «من المسؤول» سؤال تشغيليّ لا يُجاب بمعرّف GUID. */}
        <div className="mt-4 grid gap-3 border-t border-line pt-4 sm:grid-cols-2 lg:grid-cols-4">
          <Detail label="مالك المشروع">{project.projectOwnerName ?? '—'}</Detail>
          <Detail label="قائد الفريق">{project.teamLeaderName ?? '—'}</Detail>
          <Detail label="مدير الحساب">{project.accountManagerName ?? '—'}</Detail>
          <Detail label="الفريق المالك">{project.ownerTeamName ?? '—'}</Detail>
        </div>

        {/* شفافيّة التقدّم (§6-1): الرقم لا يُعرَض عاريًا — معه طريقة احتسابه ومتى وعلى كم مخرَج. */}
        <div className="mt-3 grid gap-3 border-t border-line pt-3 sm:grid-cols-2 lg:grid-cols-4">
          <Detail label="نسبة تقدّم المشروع">{percentOrDash(project.progressPercent)}</Detail>
          <Detail label="طريقة الاحتساب">
            {projectProgressModeLabel[project.progressMode]}
          </Detail>
          <Detail label="آخر احتساب">
            {project.progressCalculatedAtUtc ? formatDateTime(project.progressCalculatedAtUtc) : '—'}
          </Detail>
          <Detail label="مخرَجات المصدر">{project.progressSourceDeliverableCount}</Detail>
        </div>

        {project.progressMode === 'EqualWeightFallback' ? (
          <p className="mt-3 rounded-md bg-amber-50 p-3 text-xs text-amber-900">
            أوزان المخرَجات غير مضبوطة، فاحتُسِبت النسبة بأوزان متساوية — الرقم تقديريّ حتّى
            تُضبَط الأوزان في تبويب المخرَجات التعاقديّة.
          </p>
        ) : null}
        {project.progressMode === 'NoDeliverables' ? (
          <p className="mt-3 text-xs text-ink-2">
            لا يوجد مخرَج تعاقديّ نشط يُشتقّ منه تقدّم المشروع.
          </p>
        ) : null}
      </Card>

      <ProjectHealthPanel
        health={data.health}
        canRecompute={access.canOperate}
        onRecompute={() => recompute.mutate()}
        isRecomputing={recompute.isPending}
        recomputeError={recompute.error}
      />

      <Tabs items={items} value={tab} onChange={setTab} ariaLabel="أقسام مساحة عمل المشروع" />
    </div>
  );
}
