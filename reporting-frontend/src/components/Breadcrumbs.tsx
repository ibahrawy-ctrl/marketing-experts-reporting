// فتات الخبز السياقيّة (P3-NAV-004) — مشتقّة من سجلّ الملاحة وحده.
//
// **لا تكشف اسم مورد**: المقاطع الديناميّة (معرّف موظّف/فريق/مشروع) تُعرَض بتسمية عامّة
// «التفاصيل»، لأنّ الاسم الحقيقيّ بيانٌ لا تملك القائمة تصريحًا بقراءته — والصفحة المصرَّح لها
// وحدها تعرضه في محتواها. الـalias يعرض دائمًا مسار عنصره المرجعيّ لا نفسه.
import { Link, useLocation } from 'react-router-dom';
import { useMemo } from 'react';
import { buildBreadcrumbs, type NavCtx } from '../lib/navConfig';

export function Breadcrumbs({ ctx }: { ctx: NavCtx }) {
  const { pathname } = useLocation();
  const crumbs = useMemo(() => buildBreadcrumbs(pathname, ctx), [pathname, ctx]);
  if (crumbs.length === 0) return null;

  return (
    <nav aria-label="مسار التنقّل" className="mb-3 text-sm text-ink-3">
      <ol className="flex flex-wrap items-center gap-1.5">
        {crumbs.map((c, i) => (
          <li key={`${c.label}:${i}`} className="flex items-center gap-1.5">
            {i > 0 && <span aria-hidden="true">›</span>}
            {c.to ? (
              <Link to={c.to} className="rounded hover:text-navy hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-orange">
                {c.label}
              </Link>
            ) : (
              <span aria-current="page" className="font-medium text-ink-2">
                {c.label}
              </span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
