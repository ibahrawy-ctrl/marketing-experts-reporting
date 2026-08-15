// ======================================================================
// Project 360 — بطاقة صحّة المشروع (CPW-R3 · R2-W12 · FINDING-W6-03)
//
// **صفر معادلة هنا**: كلّ رقم معروض يأتي جاهزًا من `ProjectHealthDto`. لا جمع ولا
// ترجيح ولا تحويل نتيجة إلى حالة — `ProjectHealthPolicy` في الخادم هو المصدر الوحيد،
// وإعادة تنفيذ المعادلة هنا كانت ستنتج «صحّتين» تختلفان عند أوّل تعديل للسياسة.
//
// **`null` ليست صفرًا**: مكوّن غائب يعني «مستبعَد وأُعيد توزيع وزنه»، فيُعرَض بشرطة
// وبوسم صريح لا برقم صفر يوحي بالفشل.
// ======================================================================

import { Badge, Card } from '../ui';
import { ActionButton, Detail } from './shared';
import { apiErrorMessage } from '../../lib/api';
import { formatDateTime } from '../../lib/format';
import {
  healthReasonLabel,
  percentOrDash,
  projectHealthStatusLabel,
  projectHealthStatusTone,
} from '../../lib/project360Format';
import type { ProjectHealthDto } from '../../types/project360';

export function ProjectHealthPanel({
  health,
  canRecompute,
  onRecompute,
  isRecomputing,
  recomputeError,
}: {
  health: ProjectHealthDto;
  canRecompute: boolean;
  onRecompute?: () => void;
  isRecomputing?: boolean;
  recomputeError?: unknown;
}) {
  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm text-ink-2">صحّة المشروع</p>
          <div className="mt-1 flex items-center gap-3">
            <span className="text-3xl font-bold text-navy" data-testid="health-score">
              {percentOrDash(health.score)}
            </span>
            {health.status ? (
              <Badge tone={projectHealthStatusTone(health.status)}>
                {projectHealthStatusLabel[health.status]}
              </Badge>
            ) : (
              <Badge tone="muted">غير محتسَبة</Badge>
            )}
          </div>
        </div>

        {/* الزرّ يظهر لمن يملك القدرة فقط — والحماية الحقيقيّة في الخادم لا هنا. */}
        {canRecompute && onRecompute ? (
          <ActionButton variant="ghost" onClick={onRecompute} busy={isRecomputing}>
            إعادة احتساب الصحّة
          </ActionButton>
        ) : null}
      </div>

      <div className="mt-4 grid gap-4 sm:grid-cols-3">
        <Detail label="نتيجة المؤشّرات">
          {health.kpiScore === null ? <ExcludedMark /> : percentOrDash(health.kpiScore)}
        </Detail>
        <Detail label="نسبة التقدّم">{percentOrDash(health.progressPercent)}</Detail>
        <Detail label="التزام الجدول الزمنيّ">
          {health.scheduleScore === null ? <ExcludedMark /> : percentOrDash(health.scheduleScore)}
        </Detail>
      </div>

      {health.reasonCodes.length > 0 ? (
        <ul className="mt-4 space-y-1.5 border-t border-line pt-3" data-testid="health-reasons">
          {health.reasonCodes.map((code) => (
            <li key={code} className="text-sm text-ink-2">
              • {healthReasonLabel(code)}
            </li>
          ))}
        </ul>
      ) : null}

      <p className="mt-3 text-xs text-ink-2">
        آخر احتساب: {formatDateTime(health.lastEvaluatedAtUtc)} · آخر حفظ:{' '}
        {health.persistedAtUtc ? formatDateTime(health.persistedAtUtc) : 'لم تُحفَظ بعد'}
      </p>

      {recomputeError ? (
        <p className="mt-2 text-sm text-alert">{apiErrorMessage(recomputeError)}</p>
      ) : null}
    </Card>
  );
}

function ExcludedMark() {
  return (
    <span className="text-ink-2" title="مكوّن مستبعَد من الاحتساب — أُعيد توزيع وزنه">
      — مستبعَد
    </span>
  );
}
