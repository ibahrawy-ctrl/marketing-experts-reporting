// ======================================================================
// Project 360 — تبويب مؤشّرات الأداء (CPW-R3 · R2-W12 · D-02 · FINDING-W6-02)
//
// **لا إنشاء يتيمًا**: نموذج الإنشاء لا يُفتَح إلّا من داخل هدف، ولا يوجد في الواجهة
// مسار «مؤشّر على مستوى المشروع» — لأنّه غير موجود في الخادم أصلًا (يعود 405).
//
// **القراءة اليدويّة لمؤشّر يدويّ فقط**: زرّ تسجيل القراءة يظهر حين `sourceType = Manual`؛
// المؤشّر المشتقّ من المهامّ أو من تكامل خارجيّ يرفضه الخادم، فعرض الزرّ يوهم بإمكان لا وجود له.
//
// **صفر معادلة**: نسبة التحقّق والانحراف والاتّجاه تصل محسوبة من الخادم.
// ======================================================================

import { useState } from 'react';
import { Badge, Button, Card, EmptyState, Field, Input, Select } from '../ui';
import { LoadingState } from '../states';
import { ActionButton, Project360QueryError, SectionHeading, type Project360Access } from './shared';
import { apiErrorMessage } from '../../lib/api';
import { formatDate } from '../../lib/format';
import {
  percentOrDash,
  projectKpiCategoryLabel,
  projectKpiDirectionLabel,
  projectKpiFrequencyLabel,
  projectKpiSourceTypeLabel,
  projectKpiTrendGlyph,
  projectKpiTrendLabel,
  projectKpiUnitLabel,
} from '../../lib/project360Format';
import {
  useCreateKpiReading,
  useCreateProjectKpi,
  useKpiReadings,
  useProjectKpis,
  useProjectObjectives,
} from '../../lib/useProject360';
import type {
  ProjectKpiCategory,
  ProjectKpiDirection,
  ProjectKpiDto,
  ProjectKpiFrequency,
  ProjectKpiUnit,
} from '../../types/project360';

const CATEGORIES = Object.keys(projectKpiCategoryLabel) as ProjectKpiCategory[];
const UNITS = Object.keys(projectKpiUnitLabel) as ProjectKpiUnit[];
const FREQUENCIES = Object.keys(projectKpiFrequencyLabel) as ProjectKpiFrequency[];
const DIRECTIONS = Object.keys(projectKpiDirectionLabel) as ProjectKpiDirection[];

export function ProjectKpisTab({
  projectId,
  access,
}: {
  projectId: string;
  access: Project360Access;
}) {
  const objectives = useProjectObjectives(projectId);
  const kpis = useProjectKpis(projectId);
  const create = useCreateProjectKpi(projectId);

  const [createUnderObjective, setCreateUnderObjective] = useState<string | null>(null);
  const [openReadingsFor, setOpenReadingsFor] = useState<ProjectKpiDto | null>(null);

  if (objectives.isLoading || kpis.isLoading) return <LoadingState />;
  if (objectives.isError)
    return <Project360QueryError error={objectives.error} onRetry={() => objectives.refetch()} />;
  if (kpis.isError) return <Project360QueryError error={kpis.error} onRetry={() => kpis.refetch()} />;

  const objectiveList = objectives.data ?? [];
  const kpiList = kpis.data ?? [];

  if (objectiveList.length === 0) {
    return (
      <EmptyState
        title="لا توجد أهداف بعد"
        description="المؤشّر يُنشأ تحت هدف حصرًا، فسجّل هدفًا أوّلًا من تبويب الأهداف."
      />
    );
  }

  return (
    <div className="space-y-4">
      <SectionHeading title="مؤشّرات الأداء" />
      {create.isError ? <p className="text-sm text-alert">{apiErrorMessage(create.error)}</p> : null}

      {objectiveList.map((o) => {
        const own = kpiList.filter((k) => k.objectiveId === o.id);
        return (
          <Card key={o.id}>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h4 className="text-sm font-semibold text-navy">{o.name}</h4>
              {access.canManage ? (
                <Button
                  variant="ghost"
                  onClick={() => setCreateUnderObjective(createUnderObjective === o.id ? null : o.id)}
                >
                  مؤشّر جديد تحت هذا الهدف
                </Button>
              ) : null}
            </div>

            {createUnderObjective === o.id ? (
              <KpiForm
                loading={create.isPending}
                onCancel={() => setCreateUnderObjective(null)}
                onSubmit={(request) =>
                  create.mutate(
                    { objectiveId: o.id, request },
                    { onSuccess: () => setCreateUnderObjective(null) },
                  )
                }
              />
            ) : null}

            {own.length === 0 ? (
              <p className="mt-3 text-sm text-ink-2">لا توجد مؤشّرات تحت هذا الهدف.</p>
            ) : (
              <div className="mt-3 overflow-x-auto">
                <table className="w-full text-right text-sm">
                  <thead className="text-xs text-ink-2">
                    <tr>
                      <th className="p-2 font-medium">المؤشّر</th>
                      <th className="p-2 font-medium">المستهدف</th>
                      <th className="p-2 font-medium">الحالي</th>
                      <th className="p-2 font-medium">التحقّق</th>
                      <th className="p-2 font-medium">الاتّجاه</th>
                      <th className="p-2 font-medium">المصدر</th>
                      <th className="p-2 font-medium">آخر قراءة</th>
                      <th className="p-2" />
                    </tr>
                  </thead>
                  <tbody>
                    {own.map((k) => (
                      <tr key={k.id} className="border-t border-line">
                        <td className="p-2 font-medium text-ink">
                          {k.name}
                          <span className="mr-2 text-xs text-ink-2">
                            {projectKpiCategoryLabel[k.category]}
                          </span>
                        </td>
                        <td className="p-2">{k.targetValue}</td>
                        <td className="p-2">{k.currentValue ?? '—'}</td>
                        <td className="p-2">{percentOrDash(k.achievementPercent)}</td>
                        <td className="p-2" title={projectKpiTrendLabel[k.trend]}>
                          {projectKpiTrendGlyph[k.trend]}
                        </td>
                        <td className="p-2">
                          <Badge tone={k.sourceType === 'Manual' ? 'navy' : 'muted'}>
                            {projectKpiSourceTypeLabel[k.sourceType]}
                          </Badge>
                        </td>
                        <td className="p-2">{formatDate(k.lastReadingDate)}</td>
                        <td className="p-2">
                          <Button
                            variant="ghost"
                            onClick={() => setOpenReadingsFor(openReadingsFor?.id === k.id ? null : k)}
                          >
                            القراءات
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {openReadingsFor && openReadingsFor.objectiveId === o.id ? (
              <KpiReadings projectId={projectId} kpi={openReadingsFor} access={access} />
            ) : null}
          </Card>
        );
      })}
    </div>
  );
}

// ----------------------------------------------------------------------

type KpiFormValues = {
  name: string;
  targetValue: number;
  description: string | null;
  category: ProjectKpiCategory;
  unit: ProjectKpiUnit;
  direction: ProjectKpiDirection;
  frequency: ProjectKpiFrequency;
  baselineValue: number | null;
  weight: number;
};

function KpiForm({
  loading,
  onSubmit,
  onCancel,
}: {
  loading: boolean;
  onSubmit: (values: KpiFormValues) => void;
  onCancel: () => void;
}) {
  const [name, setName] = useState('');
  const [target, setTarget] = useState('0');
  const [baseline, setBaseline] = useState('');
  const [weight, setWeight] = useState('0');
  const [category, setCategory] = useState<ProjectKpiCategory>('Custom');
  const [unit, setUnit] = useState<ProjectKpiUnit>('Number');
  const [direction, setDirection] = useState<ProjectKpiDirection>('HigherIsBetter');
  const [frequency, setFrequency] = useState<ProjectKpiFrequency>('Monthly');

  return (
    <div className="mt-3 rounded-lg border border-line bg-offwhite p-4">
      <div className="grid gap-4 md:grid-cols-3">
        <Field label="الاسم">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="القيمة المستهدَفة">
          <Input type="number" step="0.01" value={target} onChange={(e) => setTarget(e.target.value)} />
        </Field>
        <Field label="خطّ الأساس" help="اختياريّ.">
          <Input
            type="number"
            step="0.01"
            value={baseline}
            onChange={(e) => setBaseline(e.target.value)}
          />
        </Field>
        <Field label="التصنيف">
          <Select value={category} onChange={(e) => setCategory(e.target.value as ProjectKpiCategory)}>
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {projectKpiCategoryLabel[c]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الوحدة">
          <Select value={unit} onChange={(e) => setUnit(e.target.value as ProjectKpiUnit)}>
            {UNITS.map((u) => (
              <option key={u} value={u}>
                {projectKpiUnitLabel[u]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الاتّجاه المرغوب">
          <Select
            value={direction}
            onChange={(e) => setDirection(e.target.value as ProjectKpiDirection)}
          >
            {DIRECTIONS.map((d) => (
              <option key={d} value={d}>
                {projectKpiDirectionLabel[d]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الدوريّة">
          <Select
            value={frequency}
            onChange={(e) => setFrequency(e.target.value as ProjectKpiFrequency)}
          >
            {FREQUENCIES.map((f) => (
              <option key={f} value={f}>
                {projectKpiFrequencyLabel[f]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="الوزن" help="خام؛ يطبّعه الخادم داخل الهدف.">
          <Input type="number" step="0.01" value={weight} onChange={(e) => setWeight(e.target.value)} />
        </Field>
      </div>
      <div className="mt-4 flex gap-2">
        <ActionButton
          busy={loading}
          onClick={() =>
            onSubmit({
              name: name.trim(),
              targetValue: Number(target) || 0,
              description: null,
              category,
              unit,
              direction,
              frequency,
              baselineValue: baseline === '' ? null : Number(baseline),
              weight: Number(weight) || 0,
            })
          }
        >
          حفظ
        </ActionButton>
        <Button variant="ghost" onClick={onCancel}>
          إلغاء
        </Button>
      </div>
    </div>
  );
}

// ----------------------------------------------------------------------

function KpiReadings({
  projectId,
  kpi,
  access,
}: {
  projectId: string;
  kpi: ProjectKpiDto;
  access: Project360Access;
}) {
  const readings = useKpiReadings(projectId, kpi.objectiveId, kpi.id);
  const addReading = useCreateKpiReading(projectId);
  const [date, setDate] = useState('');
  const [value, setValue] = useState('');

  const canRecordManually = access.canOperate && kpi.sourceType === 'Manual';

  return (
    <div className="mt-4 rounded-lg border border-line bg-offwhite p-4">
      <h5 className="mb-2 text-sm font-semibold text-navy">قراءات «{kpi.name}»</h5>

      {canRecordManually ? (
        <div className="mb-3 flex flex-wrap items-end gap-3">
          <Field label="تاريخ القراءة">
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field label="القيمة">
            <Input
              type="number"
              step="0.01"
              value={value}
              onChange={(e) => setValue(e.target.value)}
            />
          </Field>
          <ActionButton
            busy={addReading.isPending}
            disabled={!date || value === ''}
            onClick={() =>
              addReading.mutate(
                {
                  objectiveId: kpi.objectiveId,
                  kpiId: kpi.id,
                  request: { readingDate: date, value: Number(value) },
                },
                {
                  onSuccess: () => {
                    setDate('');
                    setValue('');
                  },
                },
              )
            }
          >
            تسجيل قراءة
          </ActionButton>
        </div>
      ) : null}

      {addReading.isError ? (
        <p className="mb-2 text-sm text-alert">{apiErrorMessage(addReading.error)}</p>
      ) : null}

      {readings.isLoading ? <LoadingState /> : null}
      {readings.isError ? (
        <Project360QueryError error={readings.error} onRetry={() => readings.refetch()} />
      ) : null}
      {readings.data && readings.data.length === 0 ? (
        <p className="text-sm text-ink-2">لا توجد قراءات مسجّلة.</p>
      ) : null}
      {readings.data && readings.data.length > 0 ? (
        <table className="w-full text-right text-sm">
          <thead className="text-xs text-ink-2">
            <tr>
              <th className="p-2 font-medium">التاريخ</th>
              <th className="p-2 font-medium">القيمة</th>
              <th className="p-2 font-medium">التحقّق وقتها</th>
              <th className="p-2 font-medium">سجّلها</th>
            </tr>
          </thead>
          <tbody>
            {readings.data.map((r) => (
              <tr key={r.id} className="border-t border-line">
                <td className="p-2">{formatDate(r.readingDate)}</td>
                <td className="p-2">{r.value}</td>
                <td className="p-2">{percentOrDash(r.achievementSnapshot)}</td>
                <td className="p-2">{r.recordedByFullName ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
