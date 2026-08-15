// ======================================================================
// Project 360 — تبويب الاستراتيجيّة (CPW-R3 · R2-W12 · D-04 · DEC-W4-02)
//
// **مبنيّ من المخطَّط لا من الكود**: الأقسام والحقول تأتي من `GET /strategy/schema`.
// لا ذكر لـ«SEO» أو «Social» أو «Ads» في هذا الملفّ — قابليّة التطبيق تُحسَم في
// الخادم بالبيانات، وأيّ توسعة للكتالوج تظهر هنا تلقائيًّا بلا نشر واجهة.
//
// **ربط النواة**: رمز حقل النواة snake_case بينما مفتاح الـDTO camelCase، فالتحويل
// يتمّ آليًّا بدل جدول من 11 سطرًا يتقادم عند أوّل حقل جديد.
// ======================================================================

import { useMemo, useState } from 'react';
import { Button, Card, Field } from '../ui';
import { LoadingState } from '../states';
import { ActionButton, Project360QueryError, SectionHeading, type Project360Access } from './shared';
import { apiErrorMessage } from '../../lib/api';
import {
  useProjectStrategy,
  useProjectStrategySchema,
  useUpsertProjectStrategy,
} from '../../lib/useProject360';
import type {
  ProjectStrategyDto,
  ProjectStrategySchemaDto,
  UpsertProjectStrategyRequest,
} from '../../types/project360';

/** `strategy_summary` ⟵ `strategySummary`. تحويل عامّ يغني عن جدول مقابلات يدويّ. */
function toCamel(code: string): string {
  return code.replace(/_([a-z])/g, (_, c: string) => c.toUpperCase());
}

export function ProjectStrategyTab({
  projectId,
  access,
}: {
  projectId: string;
  access: Project360Access;
}) {
  const strategy = useProjectStrategy(projectId);
  const schema = useProjectStrategySchema(projectId);
  const upsert = useUpsertProjectStrategy(projectId);

  const [editing, setEditing] = useState(false);
  const [core, setCore] = useState<Record<string, string>>({});
  const [attrs, setAttrs] = useState<Record<string, string>>({});

  const data = strategy.data ?? null;
  const schemaData = schema.data ?? null;

  // قيم الخادم مشتقّة لا منسوخة إلى حالة: العرض يقرأ منها مباشرة، والمسوّدة المحلّيّة
  // لا توجد إلّا أثناء التحرير وتُملأ عند فتحه. مزامنة الحالة بـ`useEffect` كانت
  // ستُنشئ مصدرًا موازيًا يتأخّر عن الخادم بدورة تصيير كاملة.
  const serverCore = useMemo(
    () => (schemaData ? readCore(data, schemaData) : {}),
    [data, schemaData],
  );
  const serverAttrs = useMemo(() => readAttributes(data), [data]);

  const dynamicBySection = useMemo(() => {
    if (!schemaData) return [];
    return groupBySection(schemaData);
  }, [schemaData]);

  if (strategy.isLoading || schema.isLoading) return <LoadingState />;
  if (schema.isError) return <Project360QueryError error={schema.error} onRetry={() => schema.refetch()} />;
  if (strategy.isError)
    return <Project360QueryError error={strategy.error} onRetry={() => strategy.refetch()} />;
  if (!schemaData) return null;

  function startEdit() {
    setCore(serverCore);
    setAttrs(serverAttrs);
    setEditing(true);
  }

  function submit() {
    const request: UpsertProjectStrategyRequest = {
      ...Object.fromEntries(
        schemaData!.coreFields.map((f) => [toCamel(f.fieldCode), core[f.fieldCode] || null]),
      ),
      attributes: schemaData!.dynamicFields
        .filter((f) => (attrs[f.fieldCode] ?? '').trim().length > 0)
        .map((f) => ({
          fieldCode: f.fieldCode,
          sectionCode: f.sectionCode,
          valueText: attrs[f.fieldCode],
          sortOrder: f.sortOrder,
        })),
    };
    upsert.mutate(request, { onSuccess: () => setEditing(false) });
  }

  return (
    <div className="space-y-5">
      <SectionHeading
        title="استراتيجيّة المشروع"
        action={
          access.canManage ? (
            editing ? (
              <div className="flex gap-2">
                <ActionButton onClick={submit} busy={upsert.isPending}>
                  حفظ
                </ActionButton>
                <Button variant="ghost" onClick={() => setEditing(false)}>
                  إلغاء
                </Button>
              </div>
            ) : (
              <Button variant="ghost" onClick={startEdit}>
                {data ? 'تعديل' : 'تسجيل الاستراتيجيّة'}
              </Button>
            )
          ) : undefined
        }
      />

      {upsert.isError ? <p className="text-sm text-alert">{apiErrorMessage(upsert.error)}</p> : null}

      {!data && !editing ? (
        <Card>
          <p className="text-sm text-ink-2">لم تُسجَّل استراتيجيّة لهذا المشروع بعد.</p>
        </Card>
      ) : null}

      {data || editing ? (
        <Card>
          <h4 className="mb-3 text-sm font-semibold text-navy">النواة الثابتة</h4>
          <div className="grid gap-4 md:grid-cols-2">
            {schemaData.coreFields.map((f) => (
              <Field key={f.fieldCode} label={f.nameAr}>
                {editing ? (
                  <textarea
                    className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
                    rows={2}
                    value={core[f.fieldCode] ?? ''}
                    onChange={(e) => setCore((s) => ({ ...s, [f.fieldCode]: e.target.value }))}
                  />
                ) : (
                  <p className="whitespace-pre-wrap text-sm text-ink">
                    {serverCore[f.fieldCode] || '—'}
                  </p>
                )}
              </Field>
            ))}
          </div>
        </Card>
      ) : null}

      {(data || editing) &&
        dynamicBySection.map((section) => (
          <Card key={section.code}>
            <h4 className="mb-3 text-sm font-semibold text-navy">{section.nameAr}</h4>
            <div className="grid gap-4 md:grid-cols-2">
              {section.fields.map((f) => (
                <Field key={f.fieldCode} label={f.nameAr}>
                  {editing ? (
                    <textarea
                      className="w-full rounded-lg border border-line bg-white px-3 py-2 text-sm outline-none focus:border-navy"
                      rows={2}
                      value={attrs[f.fieldCode] ?? ''}
                      onChange={(e) => setAttrs((s) => ({ ...s, [f.fieldCode]: e.target.value }))}
                    />
                  ) : (
                    <p className="whitespace-pre-wrap text-sm text-ink">
                      {serverAttrs[f.fieldCode] || '—'}
                    </p>
                  )}
                </Field>
              ))}
            </div>
          </Card>
        ))}
    </div>
  );
}

function readCore(
  data: ProjectStrategyDto | null,
  schema: ProjectStrategySchemaDto,
): Record<string, string> {
  const source = data as unknown as Record<string, string | null> | null;
  const out: Record<string, string> = {};
  for (const f of schema.coreFields) out[f.fieldCode] = source?.[toCamel(f.fieldCode)] ?? '';
  return out;
}

function readAttributes(data: ProjectStrategyDto | null): Record<string, string> {
  const out: Record<string, string> = {};
  for (const a of data?.attributes ?? []) out[a.fieldCode] = a.valueText ?? '';
  return out;
}

/**
 * الحقول الديناميكيّة مجمّعة بأقسامها بترتيب الكتالوج. الحقل بلا قسم معروف يُجمَع
 * تحت قسم «أخرى» بدل إسقاطه — إسقاطه كان سيُخفي بيانات أرسلها الخادم.
 */
function groupBySection(schema: ProjectStrategySchemaDto) {
  const sections = [...schema.sections].sort((a, b) => a.sortOrder - b.sortOrder);
  const groups = sections.map((s) => ({
    code: s.code,
    nameAr: s.nameAr,
    fields: schema.dynamicFields
      .filter((f) => f.sectionCode === s.code)
      .sort((a, b) => a.sortOrder - b.sortOrder),
  }));
  const orphans = schema.dynamicFields.filter(
    (f) => !f.sectionCode || !sections.some((s) => s.code === f.sectionCode),
  );
  if (orphans.length > 0) groups.push({ code: '__other', nameAr: 'أخرى', fields: orphans });
  return groups.filter((g) => g.fields.length > 0);
}
