// P123-R4 — المنتقي يقول الحقيقة حين لا يستطيع.
//
// كان يعرض صندوق اختيار فيه سطر «اختر مستخدمًا…» وحده في كلّ الحالات سواءً: أثناء التحميل،
// وعند فشل الطلب، وحين يكون النطاق فارغًا أو ممنوعًا. المستخدم يرى بابًا مفتوحًا خلفه لا شيء،
// فلا يعرف أعليه الانتظار أم إعادة المحاولة أم مراجعة صلاحيّته — وهو بالضبط ما يمنعه DEC-05.
//
// المصدر `/directory/users` مُصفّى خادميًّا بالمُحلِّل نفسه الذي يحرس بقيّة السطوح، فالقائمة
// لا تُنشئ صلاحيّة ولا تُوسِّع نطاقًا: هي عرضٌ لمن يراهم المستخدم أصلًا.
import { useDirectoryUsers } from '../lib/useDirectory';
import { classifySurfaceState } from '../lib/surfaceState';
import { Alert, Select } from './ui';

export function UserPicker({
  value,
  onChange,
  placeholder = 'اختر مستخدمًا…',
  emptyMessage = 'لا يوجد مستخدم ضمن نطاقك.',
  required = false,
}: {
  value: string;
  onChange: (id: string) => void;
  placeholder?: string;
  /** ما يُقال حين يكون النطاق فارغًا أو ممنوعًا — كلاهما نتيجة واحدة: لا أحد لتختاره. */
  emptyMessage?: string;
  required?: boolean;
}) {
  const users = useDirectoryUsers();
  const state = classifySurfaceState({
    isLoading: users.isLoading,
    error: users.error,
    isEmpty: (users.data ?? []).length === 0,
  });

  if (state === 'Loading') return <p className="text-sm text-ink-2">جارٍ تحميل القائمة…</p>;

  if (state === 'Failed') {
    return (
      <Alert tone="alert">
        تعذّر تحميل القائمة مؤقّتًا.{' '}
        <button type="button" className="underline" onClick={() => void users.refetch()}>
          إعادة المحاولة
        </button>
      </Alert>
    );
  }

  // الفراغ والمنع يلتقيان في نتيجة واحدة صادقة، وإعادة المحاولة عليهما عبث.
  if (state !== 'Available') return <Alert tone="alert">{emptyMessage}</Alert>;

  return (
    <Select required={required} value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">{placeholder}</option>
      {(users.data ?? []).map((u) => (
        <option key={u.id} value={u.id}>
          {u.fullName} — {u.email}
        </option>
      ))}
    </Select>
  );
}
