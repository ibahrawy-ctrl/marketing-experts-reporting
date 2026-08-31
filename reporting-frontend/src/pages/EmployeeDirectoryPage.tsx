// ======================================================================
// P123-R2 — «دليل الموظّفين» المحكوم بالنطاق.
//
// كان النظام يعرض عنصر قائمة باسم «دليل الموظّفين» يقود إلى سطح تحرير بيانات الموارد البشريّة
// المحصور بخمسة أدوار، فلا يملك المدير ولا قائد الفريق ولا الموظّف طريقًا إلى أيّ قائمة موظّفين
// إطلاقًا، ولا سبيل لفتح ملفّ إلّا بكتابة GUID في شريط العنوان. القدرة المطلوبة في §1 —
// «الوصول إلى دليل موظّفين محكوم بالنطاق» و«البحث وفتح الملفّ بنقرة واحدة» — كانت غائبة عمليًّا.
//
// الصفحة لا تُنشئ صلاحيّة ولا تُوسّع نطاقًا: مصدرها الوحيد `/directory/users` الذي يُصفّي خادميًّا
// عبر IScopeResolver — وهو **المُحلِّل نفسه** الذي يحرس `/dashboard/employee-profile/{id}`.
// من هذا التطابق يأتي الادّعاء الجوهريّ: كلّ صفّ معروض هنا قابل للفتح فعلًا، ولا يُعرض صفّ
// يصفع الخادمُ صاحبَه — وهو مُثبَت خادميًّا في `DirectoryOpenableContractTests`.
// ======================================================================
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useDirectoryUsers, useDepartments, useTeams, useJobRoles } from '../lib/useDirectory';
import { Badge, Card, EmptyState, Field, Input } from '../components/ui';
import { ForbiddenState, QueryError, TableSkeleton } from '../components/states';
import { classifySurfaceState } from '../lib/surfaceState';

export default function EmployeeDirectoryPage() {
  const users = useDirectoryUsers();
  const departments = useDepartments();
  const teams = useTeams();
  const jobRoles = useJobRoles();

  const [search, setSearch] = useState('');

  const nameOf = (list: { id: string; nameAr: string }[] | undefined, id: string | null | undefined) =>
    (id ? list?.find((x) => x.id === id)?.nameAr : undefined) ?? '—';

  const rows = useMemo(() => {
    const list = users.data ?? [];
    const term = search.trim().toLocaleLowerCase('ar');
    if (!term) return list;
    return list.filter(
      (u) =>
        u.fullName.toLocaleLowerCase('ar').includes(term) || u.email.toLocaleLowerCase('ar').includes(term),
    );
  }, [users.data, search]);

  // الحالات الستّ (§8): الفراغ يُقاس على **الأصل** لا على نتيجة البحث — «لا أحد في نطاقك» حقيقة
  // عن الصلاحيّة، و«لا مطابق لبحثك» حقيقة عن نصّ كتبه المستخدم للتوّ. خلطهما يُنتج رسالة كاذبة.
  const state = classifySurfaceState({
    isLoading: users.isLoading,
    error: users.error,
    isEmpty: (users.data ?? []).length === 0,
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-navy">دليل الموظّفين</h1>
        <p className="mt-1 text-sm text-ink-2">
          يعرض الموظّفين ضمن نطاق صلاحيّتك فقط، ويفتح ملفّ أيٍّ منهم بنقرة واحدة. النطاق يحدّده الخادم
          لا هذه الشاشة.
        </p>
      </div>

      {state === 'Loading' && (
        <Card>
          <TableSkeleton rows={6} cols={5} />
        </Card>
      )}

      {state === 'Forbidden' && (
        <ForbiddenState
          title="لا يمكن عرض دليل الموظّفين"
          description="لا يتيح لك نطاق صلاحيّتك الاطّلاع على قائمة الموظّفين. راجع مديرك المباشر إن كنت تحتاج ذلك."
        />
      )}

      {state === 'Failed' && (
        <QueryError
          onRetry={() => users.refetch()}
          title="تعذّر تحميل دليل الموظّفين"
          description="حدث خطأ مؤقّت أثناء جلب القائمة. أعد المحاولة."
        />
      )}

      {state === 'Empty' && (
        <EmptyState
          title="لا يوجد موظّفون ضمن نطاقك"
          description="لا يضمّ نطاق صلاحيّتك الحاليّ أيّ موظّف لعرضه. يتغيّر ذلك بتغيّر هيكلك التنظيميّ."
        />
      )}

      {state === 'Available' && (
        <>
          <Card>
            <div className="w-full sm:w-72">
              <Field label="بحث">
                <Input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="الاسم أو البريد…"
                  aria-label="البحث في دليل الموظّفين"
                />
              </Field>
            </div>
          </Card>

          <Card className="overflow-x-auto p-0">
            <table className="w-full min-w-[720px] text-right text-sm">
              <thead className="border-b border-line bg-navy-50 text-xs text-ink-2">
                <tr>
                  <th className="px-4 py-3 font-semibold">الموظّف</th>
                  <th className="px-4 py-3 font-semibold">المسمّى الوظيفيّ</th>
                  <th className="px-4 py-3 font-semibold">الفريق</th>
                  <th className="px-4 py-3 font-semibold">الإدارة</th>
                  <th className="px-4 py-3 font-semibold">الملفّ</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-4 py-10 text-center text-ink-3">
                      لا يوجد موظّف مطابق لبحثك.
                    </td>
                  </tr>
                ) : (
                  rows.map((u) => (
                    <tr key={u.id} className="border-b border-line last:border-0">
                      <td className="px-4 py-3">
                        {/* الاسم نفسه هو الرابط: «نقرة واحدة» تعني ألّا يُطالَب المستخدم بمعرّف. */}
                        <Link to={`/app/employee/${u.id}`} className="font-medium text-navy hover:underline">
                          {u.fullName}
                        </Link>
                        <div className="text-xs text-ink-3">{u.email}</div>
                        {!u.isActive && (
                          <Badge tone="muted">
                            <span className="text-[10px]">موقوف</span>
                          </Badge>
                        )}
                      </td>
                      <td className="px-4 py-3 text-ink-2">{nameOf(jobRoles.data, u.jobRoleId)}</td>
                      <td className="px-4 py-3 text-ink-2">{nameOf(teams.data, u.teamId)}</td>
                      <td className="px-4 py-3 text-ink-2">{nameOf(departments.data, u.departmentId)}</td>
                      <td className="px-4 py-3">
                        <Link
                          to={`/app/employee/${u.id}`}
                          className="text-sm font-semibold text-orange hover:underline"
                        >
                          فتح الملفّ
                        </Link>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </Card>
        </>
      )}
    </div>
  );
}
