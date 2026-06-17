// رسوم بيانية خفيفة بدون أي مكتبة خارجية — بألوان الهوية (navy/orange) فقط.

const NAVY = '#243763';
const ORANGE = '#FF6E31';

export function LineTrend({
  points,
  height = 160,
}: {
  points: { label: string; value: number }[];
  height?: number;
}) {
  if (points.length === 0)
    return <p className="py-8 text-center text-sm text-ink-2">لا توجد بيانات كافية.</p>;

  const w = 560;
  const h = height;
  const pad = 28;
  const max = Math.max(100, ...points.map((p) => p.value));
  const min = Math.min(0, ...points.map((p) => p.value));
  const span = max - min || 1;
  const stepX = points.length > 1 ? (w - pad * 2) / (points.length - 1) : 0;
  const xy = points.map((p, i) => {
    const x = pad + i * stepX;
    const y = pad + (h - pad * 2) * (1 - (p.value - min) / span);
    return { x, y, ...p };
  });
  const path = xy.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');

  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="w-full" role="img" aria-label="منحنى الاتجاه">
      {[0, 0.5, 1].map((t) => (
        <line key={t} x1={pad} x2={w - pad} y1={pad + (h - pad * 2) * t} y2={pad + (h - pad * 2) * t} stroke="#E6E9F0" strokeWidth={1} />
      ))}
      <path d={path} fill="none" stroke={ORANGE} strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" />
      {xy.map((p) => (
        <g key={p.label}>
          <circle cx={p.x} cy={p.y} r={3.5} fill={NAVY} />
          <text x={p.x} y={h - 8} textAnchor="middle" fontSize={9} fill="#8A91A3">{p.label}</text>
        </g>
      ))}
    </svg>
  );
}

export function Donut({
  slices,
  size = 160,
}: {
  slices: { label: string; value: number }[];
  size?: number;
}) {
  const total = slices.reduce((s, x) => s + x.value, 0);
  if (total === 0)
    return <p className="py-8 text-center text-sm text-ink-2">لا توجد بيانات.</p>;

  const palette = [NAVY, ORANGE, '#2F4880', '#FFB088', '#1E9E6A', '#E0A82E', '#E04141', '#8A91A3'];
  const r = size / 2;
  const inner = r * 0.6;
  // إزاحة تراكمية مُحسوبة مسبقًا (تجنّب إعادة تعيين متغير أثناء العرض).
  const offsets = slices.reduce<number[]>((acc, s) => {
    acc.push((acc[acc.length - 1] ?? 0) + s.value / total);
    return acc;
  }, []);
  const arcs = slices.map((s, i) => {
    const frac = s.value / total;
    const a0 = (offsets[i] - frac) * 2 * Math.PI;
    const a1 = offsets[i] * 2 * Math.PI;
    const large = frac > 0.5 ? 1 : 0;
    const x0 = r + r * Math.sin(a0);
    const y0 = r - r * Math.cos(a0);
    const x1 = r + r * Math.sin(a1);
    const y1 = r - r * Math.cos(a1);
    const xi1 = r + inner * Math.sin(a1);
    const yi1 = r - inner * Math.cos(a1);
    const xi0 = r + inner * Math.sin(a0);
    const yi0 = r - inner * Math.cos(a0);
    const d = `M ${x0} ${y0} A ${r} ${r} 0 ${large} 1 ${x1} ${y1} L ${xi1} ${yi1} A ${inner} ${inner} 0 ${large} 0 ${xi0} ${yi0} Z`;
    return { d, color: palette[i % palette.length], label: s.label, value: s.value };
  });

  return (
    <div className="flex items-center gap-4">
      <svg viewBox={`0 0 ${size} ${size}`} width={size} height={size} role="img" aria-label="توزيع">
        {arcs.map((a) => (
          <path key={a.label} d={a.d} fill={a.color} />
        ))}
        <text x={r} y={r - 2} textAnchor="middle" fontSize={22} fontWeight={700} fill={NAVY}>{total}</text>
        <text x={r} y={r + 16} textAnchor="middle" fontSize={9} fill="#8A91A3">الإجمالي</text>
      </svg>
      <ul className="space-y-1.5 text-sm">
        {arcs.map((a) => (
          <li key={a.label} className="flex items-center gap-2">
            <span className="inline-block h-3 w-3 rounded-sm" style={{ background: a.color }} />
            <span className="text-ink-2">{a.label}</span>
            <span className="font-semibold text-navy">{a.value}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
