// تحويل الـalias إلى مساره المرجعيّ (P3-NAV-003).
//
// **لا يتجاوز أيّ حارس:** التحويل يقع قبل أيّ حراسة، والوجهة المرجعيّة هي التي تحمل
// `ProtectedRoute` بحارسها الأصليّ. فالمستخدم غير المصرَّح له يصل إلى نفس السلوك الأمنيّ
// الذي كان سيصله لو كتب المسار المرجعيّ مباشرةً — لا كشف لوجود مورد ولا التفاف.
//
// يحفظ `search` و`hash` حرفيًّا كي لا تنكسر روابط عميقة تحمل فترة/مرشّحًا، ويستعمل
// `replace` كي لا يُفسد Back/Forward بحلقة ذهاب وإياب بين الـalias والمرجعيّ.
import { Navigate, useLocation } from 'react-router-dom';

export function AliasRedirect({ to }: { to: string }) {
  const { search, hash } = useLocation();
  return <Navigate to={`${to}${search}${hash}`} replace />;
}
