import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../../core/services/notification.service';
import { NotificationItem } from '../../core/models/notification.model';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container">
      <div class="page-header">
        <div>
          <h1>Sistem Bildirimleri</h1>
          <p class="text-muted">
            Kumbara asenkron bildirim servisinin (RabbitMQ Event Consumer) SMS ve E-Posta gönderim günlükleri.
          </p>
        </div>

        <div class="actions">
          <button class="btn btn-secondary btn-sm" (click)="notificationService.fetchRecent()">
            🔄 Güncelle
          </button>
          <button class="btn btn-secondary btn-sm" (click)="notificationService.clearAll()">
            🗑️ Temizle
          </button>
        </div>
      </div>

      <!-- Simulation Info Banner -->
      <div class="card sim-info-card">
        <div class="sim-icon">📱</div>
        <div class="sim-text">
          <strong>Asenkron Bildirim Hattı (Simulation Event Bus)</strong>
          <p>
            Tüm transfer başlama, risk değerlendirme ve para yatırma işlemleri asenkron olarak RabbitMQ üzerinden bildirim kuyruklarına fırlatılır ve SMS / E-posta olarak simüle edilir.
          </p>
        </div>
      </div>

      <!-- Notifications List -->
      <div class="notifications-list">
        <!-- Empty State -->
        <div class="empty-notif card" *ngIf="notificationService.notifications().length === 0">
          <div class="empty-icon">🔕</div>
          <h3>Kayıtlı Bildirim Yok</h3>
          <p class="text-muted">Herhangi bir transfer veya cüzdan işlemi yaptığınızda burada anlık olarak görünecektir.</p>
        </div>

        <!-- Notification Item Card -->
        <div
          *ngFor="let n of notificationService.notifications()"
          class="card notif-card"
          [ngClass]="'severity-' + n.severity"
        >
          <div class="notif-left">
            <div class="channel-badge" [class.sms]="n.channel === 'SMS'" [class.email]="n.channel === 'EMAIL'">
              <span *ngIf="n.channel === 'SMS'">📱 SMS</span>
              <span *ngIf="n.channel === 'EMAIL'">✉️ E-POSTA</span>
            </div>
            <span class="notif-time">{{ n.sentAt | date:'dd.MM.yyyy HH:mm:ss' }}</span>
          </div>

          <div class="notif-body">
            <div class="notif-header">
              <span class="notif-title">{{ n.title }}</span>
              <span class="recipient-tag">Alıcı: {{ n.recipient }}</span>
            </div>
            <p class="notif-msg">{{ n.message }}</p>
          </div>

          <div class="notif-right">
            <span class="badge badge-success">{{ n.status }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
      gap: 16px;
      flex-wrap: wrap;
    }
    .actions {
      display: flex;
      gap: 10px;
    }
    .sim-info-card {
      display: flex;
      gap: 16px;
      align-items: center;
      background: rgba(30, 41, 59, 0.6);
      border: 1px dashed var(--border-subtle);
      margin-bottom: 24px;
      padding: 16px 20px;
    }
    .sim-icon {
      font-size: 2rem;
    }
    .sim-text strong {
      color: #38bdf8;
      display: block;
      margin-bottom: 4px;
    }
    .sim-text p {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin: 0;
    }
    .notifications-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .empty-notif {
      text-align: center;
      padding: 48px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
    }
    .empty-icon {
      font-size: 3rem;
    }
    .notif-card {
      display: flex;
      align-items: center;
      gap: 20px;
      padding: 16px 20px;
      transition: all 0.2s;
    }
    .notif-card:hover {
      border-color: rgba(59, 130, 246, 0.4);
    }
    .notif-left {
      display: flex;
      flex-direction: column;
      gap: 6px;
      min-width: 140px;
    }
    .channel-badge {
      font-size: 0.75rem;
      font-weight: 700;
      padding: 4px 8px;
      border-radius: var(--radius-sm);
      display: inline-block;
      width: fit-content;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .channel-badge.sms {
      background: rgba(16, 185, 129, 0.15);
      color: #10b981;
      border: 1px solid rgba(16, 185, 129, 0.3);
    }
    .channel-badge.email {
      background: rgba(99, 102, 241, 0.15);
      color: #818cf8;
      border: 1px solid rgba(99, 102, 241, 0.3);
    }
    .notif-time {
      font-size: 0.75rem;
      color: var(--text-dim);
    }
    .notif-body {
      flex: 1;
    }
    .notif-header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 4px;
    }
    .notif-title {
      font-weight: 700;
      color: #fff;
      font-size: 0.95rem;
    }
    .recipient-tag {
      font-size: 0.75rem;
      color: var(--text-dim);
      background: rgba(0, 0, 0, 0.2);
      padding: 2px 6px;
      border-radius: 4px;
    }
    .notif-msg {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin: 0;
    }
    .notif-right {
      margin-left: auto;
    }
    @media (max-width: 768px) {
      .notif-card {
        flex-direction: column;
        align-items: flex-start;
        gap: 10px;
      }
      .notif-left {
        flex-direction: row;
        justify-content: space-between;
        width: 100%;
        align-items: center;
      }
      .notif-right {
        margin-left: 0;
      }
    }
  `]
})
export class NotificationsComponent implements OnInit {
  notificationService = inject(NotificationService);

  ngOnInit(): void {
    this.notificationService.fetchRecent().subscribe();
  }
}
