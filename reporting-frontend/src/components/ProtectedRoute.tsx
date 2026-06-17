import { type ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { LoadingScreen } from './ui';
import type { Role } from '../types/api';

export function ProtectedRoute({
  children,
  roles,
}: {
  children: ReactNode;
  roles?: Role[];
}) {
  const { user, loading, hasAnyRole } = useAuth();
  if (loading) return <LoadingScreen />;
  if (!user) return <Navigate to="/login" replace />;
  if (roles && !hasAnyRole(...roles)) return <Navigate to="/app" replace />;
  return <>{children}</>;
}
