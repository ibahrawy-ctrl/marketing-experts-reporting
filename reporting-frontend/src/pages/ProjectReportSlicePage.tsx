// ======================================================================
// مساهمة تقرير واحد في مشروع واحد (PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1)
//
// **هذه الصفحة ليست عارض تقرير**: الخادم يُرسِل شريحة المشروع مبنيّةً عنده، فما تعرضه
// الصفحة هو **كلّ** ما وصلها. لا تصفية محلّيّة ولا إخفاء — ما يخصّ مشروعًا آخر لا يصل
// المتصفّح أصلًا. أيّ تعديل لاحق يجلب الحمولة الكاملة ثمّ يُخفي منها يُبطِل الضمانة كلّها.
// ======================================================================

import { useMemo } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Card, Badge } from '../components/ui';
import { LoadingState, QueryError } from '../components/states';
import { useProjectReportSlice } from '../lib/useClients';
import { submissionStatusLabel, periodTypeLabel, formatDate } from '../lib/format';
import { apiErrorMessage } from '../lib/api';
import { parseRepeatableConfig, ProjectRepeatableDisplay } from './SubmissionsPage';
import type { ProjectNameRef, ProjectRepeatableEntry } from '../types/api';
import axios from 'axios';

export default function ProjectReportSlicePage() {
  const { projectId, reportId } = useParams<{ projectId: string; reportId: string }>();
  const slice = useProjectReportSlice(projectId, reportId);
  const data = slice.data;

  // المشروع الوحيد المسموح ذكره في هذه الصفحة — مصدره الشريحة نفسها لا استعلام ثانٍ.
  const projects = useMemo<ProjectNameRef[]>(
    () =>
      data
        ? [{ id: data.projectId, name: data.projectName, clientId: data.clientId, clientName: data.clientName }]
        : [],
    [data],
  );

  const backLink = (
    <Link to={`/app/projects/${projectId}`} className="text-sm font-semibold text-orange-600 hover:underline">
      ← رجوع إلى صفحة المشروع
    </Link>
  );

  if (slice.isLoading) return <LoadingState label="يتم تحميل مساهمة التقرير في هذا المشروع…" />;

  if (slice.isError) {
    const status = axios.isAxiosError(slice.error) ? slice.error.response?.status : undefined;
    return (
      <div dir="rtl" className="space-y-4">
        {backLink}
        {status === 404 ? (
          // رسالة واحدة لثلاث حالات (غير موجود / غير مرتبط / خارج النطاق) — التمييز بينها تعداد.
          <QueryError
            title="التقرير غير متاح ضمن هذا المشروع"
            description="التقرير غير موجود، أو غير مرتبط بهذا المشروع، أو خارج نطاق صلاحيّتك."
          />
        ) : (
          <QueryError onRetry={() => slice.refetch()} description={apiErrorMessage(slice.error)} />
        )}
      </div>
    );
  }

  if (!data) {
    return (
      <div dir="rtl" className="space-y-4">
        {backLink}
        <QueryError
          title="تعذّر عرض مساهمة التقرير"
          description="لم تصل بيانات صالحة من الخادم. أعد المحاولة، وإن تكرّر الأمر أبلغ الدعم."
          onRetry={() => slice.refetch()}
        />
      </div>
    );
  }

  return (
    <div dir="rtl" className="space-y-4">
      {backLink}

      <Card>
        <h1 className="text-lg font-bold text-navy">
          مساهمة تقرير {data.submitterName ?? 'غير معروف'} في مشروع {data.projectName} / {data.periodKey}
        </h1>
        <dl className="mt-4 grid gap-x-6 gap-y-2 text-sm md:grid-cols-3">
          <Meta label="مُقدِّم التقرير" value={data.submitterName ?? '—'} />
          <Meta label="العميل" value={data.clientName} />
          <Meta label="القالب" value={data.templateTitle ?? '—'} />
          <Meta label="الدورية" value={periodTypeLabel[data.periodType]} />
          <Meta label="الفترة" value={data.periodKey} />
          <Meta label="تاريخ الإرسال" value={formatDate(data.submittedAtUtc)} />
          <div className="min-w-0">
            <dt className="text-xs text-ink-2">الحالة</dt>
            <dd className="mt-0.5">
              <Badge tone={data.status === 'Closed' ? 'success' : data.status === 'Returned' ? 'gold' : 'navy'}>
                {submissionStatusLabel[data.status]}
              </Badge>
            </dd>
          </div>
        </dl>
      </Card>

      {data.fields.length === 0 ? (
        <Card>
          <p className="text-sm text-ink-2">
            لا يحتوي هذا التقرير على عناصر مسجَّلة لهذا المشروع. بقيّة محتوى التقرير — إن وُجد — يخصّ
            مشروعات أخرى ولا يُعرَض هنا.
          </p>
        </Card>
      ) : (
        data.fields.map((f) => (
          <Card key={f.templateFieldId}>
            <h2 className="mb-3 text-base font-semibold text-navy">{f.label}</h2>
            <ProjectRepeatableDisplay
              config={parseRepeatableConfig(f.configJson)}
              entries={toEntries(f.entries, data.projectId)}
              projects={projects}
              templateTitle={data.templateTitle ?? ''}
            />
          </Card>
        ))
      )}
    </div>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-ink-2">{label}</dt>
      <dd className="mt-0.5 font-medium text-ink">{value}</dd>
    </div>
  );
}

/**
 * الشريحة تصل بلا `projectId` داخل العنصر (كلّها لهذا المشروع أصلًا)، فيُعاد بناؤه هنا من
 * معرّف المشروع المطلوب — لا من أيّ قيمة قادمة من الحمولة — كي يبقى العرض متّسقًا مع نطاق الصفحة.
 * القيم `null` تصبح نصًّا فارغًا لأنّ عارض القسم المتكرّر يتعامل مع «فارغ» بـ«—».
 */
function toEntries(entries: Record<string, string | null>[], projectId: string): ProjectRepeatableEntry[] {
  return entries.map((answers) => ({
    projectId,
    answers: Object.fromEntries(Object.entries(answers).map(([k, v]) => [k, v ?? ''])),
  }));
}
