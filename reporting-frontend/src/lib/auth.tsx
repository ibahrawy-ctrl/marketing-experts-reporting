import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api } from './api';
import { tokenStore } from './tokenStore';
import type { AuthResponse, MeResponse, PeriodType, Role } from '../types/api';

interface AuthUser {
  userId: string;
  fullName: string;
  email: string;
  roles: Role[];
  // الدورية المتوقَّعة لتقارير المستخدم (يومي لمندوبي المبيعات، أسبوعي لغيرهم).
  expectedReportCadence: PeriodType;
  // رمز المسمّى الوظيفي (مثل SALES_B2C) — لتحديد لوحات المبيعات وعناصر التنقّل (null إن لم يُسنَد).
  jobRoleCode: string | null;
}

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  // تغيير كلمة المرور للحساب الحالي (تُبطِل كل جلسات التجديد النشطة بالخادم).
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  // تغيير بريد الدخول للحساب الحالي (يتطلب كلمة المرور الحالية للتأكيد).
  changeEmail: (newEmail: string, currentPassword: string) => Promise<void>;
  hasAnyRole: (...roles: Role[]) => boolean;
  // صلاحية اعتماد التقارير (تظهر تبويب «بانتظار اعتمادي» وأزرار الاعتماد).
  canApprove: boolean;
  // صلاحية رؤية الحوكمة (المخاطر/القرارات) — تطابق RoleAccess.ViewGovernance بالخادم.
  canViewGovernance: boolean;
  // صلاحية إدارة عضوية الفرق (تعديل الاسم/القائد، إضافة/إزالة عضو) — تطابق سياسة TeamManagement بالخادم.
  canManageTeams: boolean;
  // صلاحية حوكمة القوالب وKPI (إنشاء/تعديل قوالب التقارير وKPI، الإصدارات، الأوزان، الربط) — تطابق سياسة TemplateGovernance بالخادم.
  canManageTemplates: boolean;
  // صلاحية إدارة العملاء والمشاريع (إنشاء/تعديل/أرشفة) — تطابق سياسة Policies.ManagementOnly بالخادم.
  canManageClients: boolean;
  // صلاحية إدارة بيانات العميل الأساسية (Client 360 — إنشاء/تعديل/أرشفة/تفعيل/حذف) — تطابق سياسة
  // Policies.ClientCoreManagement بالخادم (Admin/CEO/GM/Manager؛ تستثني قائد الفريق).
  canEditClientCore: boolean;
  // هل المستخدم مندوب مبيعات فردي (SALES_B2C/SALES_B2B) ⇒ يرى لوحة «مبيعاتي».
  isSalesRep: boolean;
  // متغيّر لوحة المندوب حسب مسمّاه ('B2C' | 'B2B' | null لغير المندوب).
  salesRepType: 'B2C' | 'B2B' | null;
  // هل المستخدم قائد فريق مبيعات B2C (دور TeamLeader + مسمّى SALES_B2C_TL) ⇒ يرى «لوحة مبيعات الفريق».
  isSalesB2cTeamLeader: boolean;
}

// رموز مسمّيات مندوبي المبيعات الأفراد (تطابق OrgSeeder + منطق SalesContext بالخادم).
const SALES_REP_CODES: Record<string, 'B2C' | 'B2B'> = {
  SALES_B2C: 'B2C',
  SALES_B2B: 'B2B',
};

// رمز مسمّى قائد فريق مبيعات B2C — يميّز قائد فريق المبيعات عن بقية قادة الفرق.
const SALES_B2C_TL_CODE = 'SALES_B2C_TL';

// أدوار الإدارة المخوّلة بالاعتماد (مطابقة لمنطق سلسلة الاعتماد بالخادم).
const APPROVER_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader'];
// أدوار الحوكمة (مطابقة لـ RoleAccess.CanViewGovernance بالخادم: Admin/CeoSupport/Ceo/GM).
const GOVERNANCE_ROLES: Role[] = ['Admin', 'CeoSupport', 'CEO', 'GeneralManager'];
// أدوار إدارة الفرق (مطابقة لسياسة Policies.TeamManagement بالخادم: المستوى الإداري الأعلى فقط).
// TODO: تُضاف أدوار HR / المساعد الإداري عند تعريفها في النظام (غير موجودة حاليًا).
const TEAM_MANAGEMENT_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager'];
// أدوار حوكمة القوالب وKPI (مطابقة لسياسة Policies.TemplateGovernance بالخادم: المستوى الإداري الأعلى فقط).
// TODO: تُضاف أدوار HR / المساعد الإداري عند تعريفها في النظام (غير موجودة حاليًا).
const TEMPLATE_MANAGEMENT_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager'];
// أدوار إدارة العملاء والمشاريع (مطابقة لسياسة Policies.ManagementOnly بالخادم).
const CLIENT_MANAGEMENT_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager', 'TeamLeader'];
// أدوار إدارة بيانات العميل الأساسية Client 360 (مطابقة لسياسة Policies.ClientCoreManagement بالخادم:
// Admin/CEO/GM/Manager؛ تستثني قائد الفريق TeamLeader). القرار #2 في CPW-R1B.
const CLIENT_CORE_MANAGEMENT_ROLES: Role[] = ['Admin', 'CEO', 'GeneralManager', 'Manager'];

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  // استعادة الجلسة عند الإقلاع إن وُجد رمز.
  useEffect(() => {
    let active = true;
    async function bootstrap() {
      if (!tokenStore.access) {
        setLoading(false);
        return;
      }
      try {
        const { data } = await api.get<MeResponse>('/auth/me');
        if (active) {
          setUser({
            userId: data.userId,
            fullName: data.fullName,
            email: data.email,
            roles: data.roles,
            expectedReportCadence: data.expectedReportCadence,
            jobRoleCode: data.jobRoleCode ?? null,
          });
        }
      } catch {
        tokenStore.clear();
      } finally {
        if (active) setLoading(false);
      }
    }
    void bootstrap();
    return () => {
      active = false;
    };
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await api.post<AuthResponse>('/auth/login', { email, password });
    tokenStore.set(data.accessToken, data.refreshToken);
    setUser({
      userId: data.userId,
      fullName: data.fullName,
      email: data.email,
      roles: data.roles,
      expectedReportCadence: data.expectedReportCadence,
      jobRoleCode: data.jobRoleCode ?? null,
    });
  }, []);

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout', { refreshToken: tokenStore.refresh });
    } catch {
      /* تجاهل أخطاء الخروج */
    }
    tokenStore.clear();
    setUser(null);
  }, []);

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await api.post('/auth/change-password', { currentPassword, newPassword });
  }, []);

  const changeEmail = useCallback(async (newEmail: string, currentPassword: string) => {
    await api.post('/auth/change-email', { newEmail, currentPassword });
    setUser((prev) => (prev ? { ...prev, email: newEmail.trim() } : prev));
  }, []);

  const hasAnyRole = useCallback(
    (...roles: Role[]) => !!user && user.roles.some((r) => roles.includes(r)),
    [user],
  );

  const canApprove = useMemo(
    () => !!user && user.roles.some((r) => APPROVER_ROLES.includes(r)),
    [user],
  );
  const canViewGovernance = useMemo(
    () => !!user && user.roles.some((r) => GOVERNANCE_ROLES.includes(r)),
    [user],
  );
  const canManageTeams = useMemo(
    () => !!user && user.roles.some((r) => TEAM_MANAGEMENT_ROLES.includes(r)),
    [user],
  );
  const canManageTemplates = useMemo(
    () => !!user && user.roles.some((r) => TEMPLATE_MANAGEMENT_ROLES.includes(r)),
    [user],
  );
  const canManageClients = useMemo(
    () => !!user && user.roles.some((r) => CLIENT_MANAGEMENT_ROLES.includes(r)),
    [user],
  );
  const canEditClientCore = useMemo(
    () => !!user && user.roles.some((r) => CLIENT_CORE_MANAGEMENT_ROLES.includes(r)),
    [user],
  );
  // متغيّر لوحة المندوب حسب مسمّاه ('B2C' | 'B2B' | null لغير المندوب) — يُشتَقّ من JobRoleCode.
  const salesRepType = useMemo<'B2C' | 'B2B' | null>(
    () => (user?.jobRoleCode ? SALES_REP_CODES[user.jobRoleCode] ?? null : null),
    [user],
  );
  // هل المستخدم مندوب مبيعات فردي ⇒ يرى «لوحة مبيعاتي».
  const isSalesRep = useMemo(() => salesRepType !== null, [salesRepType]);
  // هل المستخدم قائد فريق مبيعات B2C (دور TeamLeader + مسمّى SALES_B2C_TL) ⇒ يرى «لوحة مبيعات الفريق».
  const isSalesB2cTeamLeader = useMemo(
    () => !!user && user.roles.includes('TeamLeader') && user.jobRoleCode === SALES_B2C_TL_CODE,
    [user],
  );

  const value = useMemo<AuthContextValue>(
    () => ({ user, loading, login, logout, changePassword, changeEmail, hasAnyRole, canApprove, canViewGovernance, canManageTeams, canManageTemplates, canManageClients, canEditClientCore, isSalesRep, salesRepType, isSalesB2cTeamLeader }),
    [user, loading, login, logout, changePassword, changeEmail, hasAnyRole, canApprove, canViewGovernance, canManageTeams, canManageTemplates, canManageClients, canEditClientCore, isSalesRep, salesRepType, isSalesB2cTeamLeader],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
