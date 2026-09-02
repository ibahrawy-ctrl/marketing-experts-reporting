// VIS-02 — تبويب «التقارير» داخل مساحة عمل المشروع.
//
// السبب الجذريّ للعيب: `LinkedReportsCard` كان قائمًا ومستعمَلًا في صفحة تفاصيل المشروع
// وحدها، بينما مساحة عمل المشروع (Project 360) — وهي الوجهة التي يفتحها مدير الحساب
// وقائد الفريق فعلًا — لم تحمل تبويبًا للتقارير إطلاقًا، فبدا النظام كأنّه لا يربط
// التقارير بالمشاريع. الإصلاح سطحُ عرضٍ لا منطق جديد: النقطة نفسها والحارس نفسه.
//
// كسول كبقيّة التبويبات: لا يُطلق `GET /projects/{id}/reports` إلّا عند فتح التبويب.
import { useProjectReports } from '../../lib/useClients';
import { LoadingState } from '../states';
import { LinkedReportsCard } from '../../pages/ClientDetailPage';
import { Project360QueryError } from './shared';

export function ProjectReportsTab({ projectId }: { projectId: string }) {
  const reports = useProjectReports(projectId);

  if (reports.isLoading) return <LoadingState label="يتم تحميل تقارير المشروع…" />;
  if (reports.isError)
    return <Project360QueryError error={reports.error} onRetry={() => reports.refetch()} />;

  return (
    <LinkedReportsCard
      rows={reports.data ?? []}
      title="تقارير المشروع المرتبطة"
      projectId={projectId}
      showDecisionColumns
    />
  );
}
