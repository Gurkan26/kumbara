export type NotificationChannel = 'SMS' | 'EMAIL';
export type NotificationSeverity = 'info' | 'success' | 'warning' | 'error';

export interface NotificationItem {
  id: string;
  channel: NotificationChannel;
  recipient: string;
  title: string;
  message: string;
  severity: NotificationSeverity;
  status: 'DELIVERED' | 'SENT' | 'QUEUED';
  sentAt: string;
}
