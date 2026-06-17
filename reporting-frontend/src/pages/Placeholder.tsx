import { Card } from '../components/ui';

// شاشة مؤقتة تُستكمل في المرحلة التالية (شاشات الوحدات).
export function Placeholder({ title }: { title: string }) {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold text-navy">{title}</h1>
      <Card>
        <p className="text-ink-2">هذه الشاشة قيد الإنشاء وستُستكمل في المرحلة التالية.</p>
      </Card>
    </div>
  );
}
