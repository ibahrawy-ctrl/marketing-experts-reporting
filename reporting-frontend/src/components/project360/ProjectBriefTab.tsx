// ======================================================================
// Project 360 — تبويب الموجز والسياق (CPW-R3 · R2-W12)
//
// **صفر نداء إضافيّ للمشروع**: كلّ بيانات المشروع من `ProjectOverviewDto` المحمَّل
// مسبقًا. النداء الوحيد هنا هو دليل المستخدمين لترجمة المعرّفات إلى أسماء، وهو
// **تجميليّ**: إن رُفض أو فشل يبقى التبويب صالحًا بشرطة مكان الاسم بدل شاشة خطأ.
// ======================================================================

import { Card } from '../ui';
import { Detail, SectionHeading } from './shared';
import { useDirectoryUsers } from '../../lib/useDirectory';
import { formatDate, projectStatusLabel, serviceTypeLabel } from '../../lib/format';
import { percentOrDash } from '../../lib/project360Format';
import type { ProjectOverviewDto } from '../../types/project360';

export function ProjectBriefTab({ overview }: { overview: ProjectOverviewDto }) {
  const users = useDirectoryUsers();
  const p = overview.project;

  const nameOf = (id: string | null) =>
    id ? (users.data ?? []).find((u) => u.id === id)?.fullName ?? '—' : '—';

  return (
    <div className="space-y-5">
      <Card>
        <SectionHeading title="هويّة المشروع" />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Detail label="العميل">{p.clientName}</Detail>
          <Detail label="نوع الخدمة">{serviceTypeLabel[p.serviceType]}</Detail>
          <Detail label="الحالة">{projectStatusLabel[p.status]}</Detail>
          <Detail label="نسبة التقدّم">{percentOrDash(p.progressPercent)}</Detail>
          <Detail label="تاريخ البدء">{formatDate(p.startDate)}</Detail>
          <Detail label="تاريخ الانتهاء">{formatDate(p.endDate)}</Detail>
        </div>
      </Card>

      <Card>
        <SectionHeading title="المسؤوليّات" />
        <div className="grid gap-4 sm:grid-cols-3">
          <Detail label="مالك المشروع">{nameOf(p.projectOwnerId)}</Detail>
          <Detail label="مدير الحساب">{nameOf(p.accountManagerId)}</Detail>
          <Detail label="قائد الفريق">{nameOf(p.teamLeaderId)}</Detail>
        </div>
      </Card>

      <Card>
        <SectionHeading title="الموجز" />
        <p className="whitespace-pre-wrap text-sm text-ink">{p.summary || '—'}</p>
      </Card>
    </div>
  );
}
