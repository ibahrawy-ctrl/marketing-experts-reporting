import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api';
import { useUnreadCount, useNotifications } from '../lib/useNotifications';
import { formatDate } from '../lib/format';
import { normalizeNotificationLink } from '../lib/notificationLink';

export function NotificationsBell() {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const qc = useQueryClient();
  const { data: unread = 0 } = useUnreadCount();
  const { data: items = [] } = useNotifications();

  const markAll = useMutation({
    mutationFn: () => api.post('/notifications/read-all'),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['notif-unread'] });
      void qc.invalidateQueries({ queryKey: ['notifications'] });
    },
  });

  function toggle() {
    const next = !open;
    setOpen(next);
    if (next && unread > 0) markAll.mutate();
  }

  return (
    <div className="relative">
      <button
        onClick={toggle}
        aria-label="الإشعارات"
        className="relative rounded-lg p-2 text-white hover:bg-navy-600"
      >
        <span className="text-xl">🔔</span>
        {unread > 0 && (
          <span className="absolute -top-0.5 -left-0.5 grid h-5 w-5 place-items-center rounded-full bg-orange text-[10px] font-bold text-white">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute left-0 z-30 mt-2 w-80 overflow-hidden rounded-xl border border-line bg-white shadow-lg">
          <div className="border-b border-line px-4 py-3 text-sm font-semibold text-navy">
            الإشعارات
          </div>
          <div className="max-h-96 overflow-y-auto">
            {items.length === 0 ? (
              <p className="px-4 py-6 text-center text-sm text-ink-2">لا توجد إشعارات</p>
            ) : (
              items.map((n) => (
                <button
                  key={n.id}
                  onClick={() => {
                    setOpen(false);
                    if (n.link) navigate(normalizeNotificationLink(n.link));
                  }}
                  className={`block w-full border-b border-line px-4 py-3 text-right hover:bg-offwhite ${
                    n.isRead ? '' : 'bg-navy-50'
                  }`}
                >
                  <p className="text-sm font-semibold text-ink">{n.title}</p>
                  {n.body && <p className="mt-0.5 text-xs text-ink-2">{n.body}</p>}
                  <p className="mt-1 text-[11px] text-ink-3">{formatDate(n.createdAtUtc)}</p>
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
