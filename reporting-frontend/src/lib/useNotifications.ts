import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { api, API_BASE_URL } from './api';
import { tokenStore } from './tokenStore';
import type { NotificationDto } from '../types/api';

export function useUnreadCount() {
  return useQuery({
    queryKey: ['notif-unread'],
    queryFn: async () => {
      const { data } = await api.get<{ count: number }>('/notifications/unread-count');
      return data.count;
    },
    refetchInterval: 60000,
  });
}

export function useNotifications() {
  return useQuery({
    queryKey: ['notifications'],
    queryFn: async () => {
      const { data } = await api.get<NotificationDto[]>('/notifications');
      return data;
    },
  });
}

// اتصال SignalR لحظي لتحديث جرس الإشعارات.
export function useNotificationRealtime() {
  const qc = useQueryClient();
  useEffect(() => {
    if (!tokenStore.access) return;
    const hubUrl = API_BASE_URL.replace(/\/api$/, '') + '/hubs/notifications';
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => tokenStore.access ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('notification', () => {
      void qc.invalidateQueries({ queryKey: ['notif-unread'] });
      void qc.invalidateQueries({ queryKey: ['notifications'] });
    });

    connection.start().catch(() => {
      /* الاعتماد على refetchInterval كبديل */
    });

    return () => {
      void connection.stop();
    };
  }, [qc]);
}
