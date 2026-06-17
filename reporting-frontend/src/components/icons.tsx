// مجموعة أيقونات خطية بسيطة (currentColor) — بهوية خبراء التسويق.
import type { SVGProps } from 'react';

export type IconName =
  | 'home'
  | 'teams'
  | 'reports'
  | 'kpi'
  | 'workflow'
  | 'governance'
  | 'analytics'
  | 'template'
  | 'kpiTemplate'
  | 'users'
  | 'departments'
  | 'settings'
  | 'audit'
  | 'calendar'
  | 'clients'
  | 'projects';

const PATHS: Record<IconName, string> = {
  home: 'M3 10.5 12 3l9 7.5M5 9.5V20a1 1 0 0 0 1 1h4v-6h4v6h4a1 1 0 0 0 1-1V9.5',
  teams: 'M16 14a4 4 0 1 0-4-4 4 4 0 0 0 4 4Zm0 0c-3 0-5 1.5-5 4v2h10v-2c0-2.5-2-4-5-4ZM8 11a3 3 0 1 0-3-3 3 3 0 0 0 3 3Zm-5 7v-1c0-2 1.5-3.5 4-3.8',
  reports: 'M6 2h8l4 4v16H6V2Zm8 0v4h4M9 12h6M9 16h6M9 8h2',
  kpi: 'M4 19V5m0 14h16M7 16l3-4 3 3 4-6',
  workflow: 'M6 4h4v4H6V4Zm8 12h4v4h-4v-4Zm-8 0h4v4H6v-4Zm2-8v4m2 2H8m8 0h-4m4-2V8',
  governance: 'M12 3 4 6v5c0 5 3.5 8 8 10 4.5-2 8-5 8-10V6l-8-3Zm-3 8 2 2 4-4',
  analytics: 'M4 4v16h16M8 16V9m4 7V5m4 11v-4',
  template: 'M4 4h16v4H4V4Zm0 6h7v10H4V10Zm9 0h7v4h-7v-4Zm0 6h7v4h-7v-4Z',
  kpiTemplate: 'M4 4h16v16H4V4Zm0 5h16M9 4v5m0 4 2 2 4-4',
  users: 'M9 11a4 4 0 1 0-4-4 4 4 0 0 0 4 4Zm0 0c-3.3 0-6 1.8-6 4.5V19h12v-3.5C15 12.8 12.3 11 9 11Zm8-7a3.5 3.5 0 0 1 0 7m2 8v-2.5c0-1.7-1-3-2.5-3.7',
  departments: 'M4 21V5a1 1 0 0 1 1-1h7a1 1 0 0 1 1 1v16M13 21V9a1 1 0 0 1 1-1h5a1 1 0 0 1 1 1v12M3 21h18M7 8h2m-2 4h2m-2 4h2m8-4h2m-2 4h2',
  settings:
    'M12 9a3 3 0 1 0 3 3 3 3 0 0 0-3-3Zm8 3a8 8 0 0 0-.1-1.3l2-1.6-2-3.4-2.4 1a8 8 0 0 0-2.2-1.3L13 1h-2l-.3 2.7a8 8 0 0 0-2.2 1.3l-2.4-1-2 3.4 2 1.6A8 8 0 0 0 4 12a8 8 0 0 0 .1 1.3l-2 1.6 2 3.4 2.4-1a8 8 0 0 0 2.2 1.3L11 23h2l.3-2.7a8 8 0 0 0 2.2-1.3l2.4 1 2-3.4-2-1.6A8 8 0 0 0 20 12Z',
  audit: 'M9 3h6l1 2h3v16H5V5h3l1-2Zm0 8 2 2 4-4M9 16h6',
  calendar: 'M4 7a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7Zm0 4h16M8 3v4m8-4v4M8 15h2m4 0h2',
  clients: 'M3 21V8l6-4 6 4v13M9 21v-5h2v5M21 21V11l-6-3M6 11h2m-2 3h2m9-1h2m-2 3h2',
  projects: 'M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7Zm6 6 2 2 4-4',
};

export function NavIcon({ name, ...props }: { name: IconName } & SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      <path d={PATHS[name]} />
    </svg>
  );
}
