// بناء سياق الملاحة من جلسة المستخدم (P3-NAV-001).
//
// **مصدر واحد لا نسختان:** كلّ سطح يعرض عناصر ملاحة (الشريط الجانبيّ، شريط الأقسام،
// البحث العامّ) يشتقّ سياقه من هنا حصرًا — فلا يمكن أن يفترق شرط الظهور في البحث عن
// شرط الظهور في القائمة. السياق **قراءة فقط** من الجلسة ولا يمنح شيئًا: الحراسة الفعليّة
// على الخادم وفي `ProtectedRoute`.
import { useMemo } from 'react';
import { useAuth } from './auth';
import type { NavCtx } from './navConfig';

export function useNavCtx(): NavCtx {
  const { user, hasAnyRole, permissions, scopeType, features, isSalesRep, isSalesB2cTeamLeader } = useAuth();
  const jobRoleCode = user?.jobRoleCode ?? null;
  return useMemo<NavCtx>(
    () => ({
      authenticated: !!user,
      hasAnyRole,
      permissions,
      features,
      scopeType,
      isSalesRep,
      isSalesB2cTeamLeader,
      jobRoleCode,
    }),
    [user, hasAnyRole, permissions, features, scopeType, isSalesRep, isSalesB2cTeamLeader, jobRoleCode],
  );
}
