// ======================================================================
// صفحة مساهمة التقرير في مشروع (PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1)
//
// **الادّعاء المركزيّ هنا سلبيّ**: ما يخصّ مشروعًا آخر لا يظهر لأنّه **لم يصل** أصلًا.
// لذلك لا تُموَّه الهوكات بل يُتجسَّس على `api` مباشرةً، فيُقاس **العنوان المطلوب فعلًا**:
// لو استبدل أحدهم يومًا نداء الشريحة بنداء التسليم الكامل ثمّ أخفى الزائد في العرض،
// لسقط اختبار «لا نداء إلى `/submissions/{id}`» بينما اختبار النصّ وحده كان سيمرّ.
//
// `retry: false` إلزاميّ: 404 هنا قرار أمنيّ نهائيّ لا خطأ عابر، وإعادة المحاولة كانت
// ستؤخّر الحالة النهائيّة وتُفسِد عدّاد النداءات.
// ======================================================================

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { api } from '../lib/api';
import type { ProjectReportSliceDto } from '../types/api';
import ProjectReportSlicePage from './ProjectReportSlicePage';

const PROJECT_A = '1f23cea4-682e-4dc4-a72c-ac7be39d2356';
const SUBMISSION = '1caffdb6-0a94-41db-831e-765bc025bfda';
const SLICE_URL = `/projects/${PROJECT_A}/reports/${SUBMISSION}`;

// بصمتان لا تظهران في أيّ نصّ آخر: وجود بصمة المشروع الآخر في الـDOM تسريب صريح.
const MARKER_A = '776001';
const MARKER_B = '889002';
const GENERAL_NOTE = 'ملخّص-عامّ-لا-ينتمي-لمشروع';

const CONFIG = JSON.stringify({
  projectRequired: true,
  minProjects: 1,
  maxProjects: 5,
  fields: [{ key: 'spend', label: 'الميزانية', type: 'Text', required: true }],
});

function sliceFixture(): ProjectReportSliceDto {
  return {
    submissionId: SUBMISSION,
    projectId: PROJECT_A,
    projectName: 'حملات إعلانية',
    clientId: 'c1',
    clientName: 'عيادات محمد الرافعي',
    submitterId: 'u-ahmed',
    submitterName: 'أحمد عبدالفتاح',
    templateTitle: 'التقرير الأسبوعيّ',
    periodType: 'Weekly',
    periodKey: '2026-W35',
    status: 'Draft',
    submittedAtUtc: null,
    fields: [
      {
        templateFieldId: 'f1',
        label: 'أداء المشاريع',
        configJson: CONFIG,
        order: 1,
        entries: [{ answers: { spend: MARKER_A }, workItems: [] }],
      },
    ],
  };
}

function axiosLikeError(status: number, type: string, detail: string) {
  return Object.assign(new Error(detail), {
    isAxiosError: true,
    response: { status, data: { type, detail, title: detail } },
  });
}

let getCalls: string[] = [];
let body: unknown;
let failure: unknown;
let consoleErrors: unknown[][] = [];

beforeEach(() => {
  vi.restoreAllMocks();
  getCalls = [];
  body = sliceFixture();
  failure = undefined;
  consoleErrors = [];
  vi.spyOn(console, 'error').mockImplementation((...args) => {
    consoleErrors.push(args);
  });
  vi.spyOn(api, 'get').mockImplementation((url: string) => {
    getCalls.push(url);
    return failure ? Promise.reject(failure) : Promise.resolve({ data: body } as never);
  });
});

afterEach(() => {
  // أيّ خطأ في الطرفيّة يُبطِل ادّعاء «صفر أخطاء Console» المطلوب في التذكرة.
  expect(consoleErrors).toEqual([]);
});

function renderSlice() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/app/projects/${PROJECT_A}/reports/${SUBMISSION}`]}>
        <Routes>
          <Route path="/app/projects/:projectId/reports/:reportId" element={<ProjectReportSlicePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ProjectReportSlicePage — الحالات النهائيّة', () => {
  // ===== واجهة 4: حالة تحميل معلنة لا دوّامة صامتة =====
  it('يُظهر حالة تحميل معلنة قبل وصول الشريحة', () => {
    renderSlice();
    expect(screen.getByText('يتم تحميل مساهمة التقرير في هذا المشروع…')).toBeInTheDocument();
  });

  // ===== واجهة 5: 404 حالة نهائيّة واضحة برسالة واحدة لثلاث حالات =====
  it('يُظهر حالة نهائيّة واضحة عند 404 بلا تمييز بين «غير موجود» و«خارج النطاق»', async () => {
    failure = axiosLikeError(404, 'project.not_found', 'المشروع أو التقرير غير متاح.');
    renderSlice();
    expect(await screen.findByText('التقرير غير متاح ضمن هذا المشروع')).toBeInTheDocument();
    expect(
      screen.getByText('التقرير غير موجود، أو غير مرتبط بهذا المشروع، أو خارج نطاق صلاحيّتك.'),
    ).toBeInTheDocument();
    // لا دوّامة باقية ولا صفحة بيضاء.
    expect(screen.queryByText('يتم تحميل مساهمة التقرير في هذا المشروع…')).not.toBeInTheDocument();
    // نداء واحد فقط: `retry: false` يمنع تكرار قرار أمنيّ نهائيّ.
    expect(getCalls.filter((u) => u === SLICE_URL)).toHaveLength(1);
  });

  // ===== واجهة 6: خطأ غير 404 يبقى قابلًا لإعادة المحاولة =====
  it('يُظهر خطأً قابلًا لإعادة المحاولة عند عطل خادم غير 404', async () => {
    failure = axiosLikeError(500, 'server.error', 'خطأ داخلي في الخادم.');
    renderSlice();
    expect(await screen.findByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
    expect(screen.queryByText('التقرير غير متاح ضمن هذا المشروع')).not.toBeInTheDocument();
  });

  // ===== واجهة 7: زرّ الرجوع يعود إلى نفس المشروع في كلّ حالة =====
  it('يعرض رابط الرجوع إلى نفس صفحة المشروع في حالتَي النجاح والرفض', async () => {
    const ok = renderSlice();
    expect((await screen.findByRole('link', { name: '← رجوع إلى صفحة المشروع' }))).toHaveAttribute(
      'href',
      `/app/projects/${PROJECT_A}`,
    );
    ok.unmount();

    failure = axiosLikeError(404, 'project.not_found', 'غير متاح.');
    renderSlice();
    expect(await screen.findByRole('link', { name: '← رجوع إلى صفحة المشروع' })).toHaveAttribute(
      'href',
      `/app/projects/${PROJECT_A}`,
    );
  });
});

describe('ProjectReportSlicePage — نطاق المشروع', () => {
  // ===== واجهة 8: العنوان يعلن السياق صراحةً =====
  it('يعرض عنوانًا يعلن أنّ المعروض مساهمة هذا المشروع وحده', async () => {
    renderSlice();
    expect(
      await screen.findByRole('heading', {
        name: 'مساهمة تقرير أحمد عبدالفتاح في مشروع حملات إعلانية / 2026-W35',
      }),
    ).toBeInTheDocument();
    expect(screen.getByText('عيادات محمد الرافعي')).toBeInTheDocument();
  });

  // ===== واجهة 9: لا شيء خارج سياق المشروع في الـDOM =====
  it('لا يعرض أيّ بيانات خارج سياق هذا المشروع', async () => {
    renderSlice();
    await screen.findByText(MARKER_A);
    const dom = document.body.textContent ?? '';
    expect(dom).not.toContain(MARKER_B);
    expect(dom).not.toContain(GENERAL_NOTE);
    expect(dom).not.toContain('مشروع غير معروف');
  });

  // ===== واجهة 10: التصفية خادميّة — لا تُجلَب الحمولة الكاملة أصلًا =====
  it('يطلب مسار الشريحة وحده ولا يمسّ مسار التسليم الكامل إطلاقًا', async () => {
    renderSlice();
    await screen.findByText(MARKER_A);
    expect(getCalls).toEqual([SLICE_URL]);
    expect(getCalls.some((u) => u.startsWith('/submissions/'))).toBe(false);
  });

  // ===== واجهة 11: شريحة فارغة تُصرَّح لا تُترَك بياضًا =====
  it('يُصرِّح بغياب عناصر هذا المشروع بدل عرض صفحة فارغة', async () => {
    body = { ...sliceFixture(), fields: [] };
    renderSlice();
    expect(
      await screen.findByText(/لا يحتوي هذا التقرير على عناصر مسجَّلة لهذا المشروع/),
    ).toBeInTheDocument();
  });
});
