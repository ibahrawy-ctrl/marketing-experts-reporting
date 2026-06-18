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
}

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  // تغيير كلمة المرور للحساب الحالي (تُبطِل كل جلسات التجديد النشطة بالخادم).
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
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
}

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

  const value = useMemo<AuthContextValue>(
    () => ({ user, loading, login, logout, changePassword, hasAnyRole, canApprove, canViewGovernance, canManageTeams, canManageTemplates, canManageClients }),
    [user, loading, login, logout, changePassword, hasAnyRole, canApprove, canViewGovernance, canManageTeams, canManageTemplates, canManageClients],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
