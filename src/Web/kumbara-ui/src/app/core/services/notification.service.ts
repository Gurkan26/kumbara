import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { NotificationChannel, NotificationItem, NotificationSeverity } from '../models/notification.model';

const STORAGE_KEY = 'kumbara_notifications';

const SEED_NOTIFICATIONS: NotificationItem[] = [
  {
    id: 'notif-1',
    channel: 'SMS',
    recipient: '+90 555 *** 1234',
    title: 'Güvenlik Bildirimi',
    message: 'Kumbara hesabınıza yeni bir cihazdan başarılı giriş yapıldı.',
    severity: 'info',
    status: 'DELIVERED',
    sentAt: new Date(Date.now() - 1000 * 60 * 30).toISOString()
  },
  {
    id: 'notif-2',
    channel: 'EMAIL',
    recipient: 'kullanici@kumbara.local',
    title: 'Yeni Cüzdan Tanımlandı',
    message: 'TRY Para Biriminde ana cüzdanınız başarıyla aktif edilmiştir.',
    severity: 'success',
    status: 'DELIVERED',
    sentAt: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString()
  },
  {
    id: 'notif-3',
    channel: 'SMS',
    recipient: '+90 555 *** 1234',
    title: 'Simülasyon Uyarısı',
    message: 'Hatırlatma: Kumbara ortamı eğitim ve simülasyon amaçlıdır. Gerçek para veya değer taşımaz.',
    severity: 'warning',
    status: 'DELIVERED',
    sentAt: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString()
  }
];

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private http = inject(HttpClient);
  private apiUrl = '/api/notifications/recent';

  readonly notifications = signal<NotificationItem[]>(this.loadInitial());
  readonly count = computed(() => this.notifications().length);

  constructor() {
    this.fetchRecent();
  }

  private loadInitial(): NotificationItem[] {
    if (typeof window === 'undefined') return SEED_NOTIFICATIONS;
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : SEED_NOTIFICATIONS;
  }

  private save(): void {
    if (typeof window !== 'undefined') {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this.notifications()));
    }
  }

  fetchRecent(): Observable<NotificationItem[]> {
    return this.http.get<NotificationItem[]>(this.apiUrl).pipe(
      map(items => (Array.isArray(items) && items.length > 0 ? items : this.notifications())),
      tap(items => {
        this.notifications.set(items);
        this.save();
      }),
      catchError(() => of(this.notifications()))
    );
  }

  dispatchNotification(
    channel: NotificationChannel,
    recipient: string,
    title: string,
    message: string,
    severity: NotificationSeverity = 'info'
  ): void {
    const newItem: NotificationItem = {
      id: 'notif-' + Date.now(),
      channel,
      recipient,
      title,
      message,
      severity,
      status: 'DELIVERED',
      sentAt: new Date().toISOString()
    };

    this.notifications.update(list => [newItem, ...list]);
    this.save();
  }

  clearAll(): void {
    this.notifications.set([]);
    this.save();
  }
}
