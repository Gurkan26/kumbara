import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { WalletService } from '../../core/services/wallet.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <!-- Top Persistent Warning Banner -->
    <div class="simulation-banner">
      <span>⚠️</span>
      <strong>Eğitim amaçlı simülasyondur — Gerçek para veya değer taşımaz (No Real Money)</strong>
      <span>⚠️</span>
    </div>

    <!-- Navigation Header -->
    <header class="navbar-header" *ngIf="authService.isAuthenticated()">
      <div class="navbar-container">
        <!-- Brand -->
        <a routerLink="/dashboard" class="brand-link">
          <div class="brand-icon">🏺</div>
          <div class="brand-text">
            <span class="brand-title">KUMBARA</span>
            <span class="brand-badge">Fintech Sim</span>
          </div>
        </a>

        <!-- Links -->
        <nav class="nav-links">
          <a routerLink="/dashboard" routerLinkActive="active" class="nav-item">
            <span class="nav-icon">📊</span>
            <span>Dashboard</span>
          </a>
          <a routerLink="/transfer" routerLinkActive="active" class="nav-item">
            <span class="nav-icon">⚡</span>
            <span>Transfer & Saga</span>
          </a>
          <a routerLink="/history" routerLinkActive="active" class="nav-item">
            <span class="nav-icon">📜</span>
            <span>Ledger Defteri</span>
          </a>
          <a routerLink="/notifications" routerLinkActive="active" class="nav-item">
            <span class="nav-icon">🔔</span>
            <span>Bildirimler</span>
            <span class="notif-count" *ngIf="notificationService.count() > 0">
              {{ notificationService.count() }}
            </span>
          </a>
        </nav>

        <!-- User Controls -->
        <div class="user-section">
          <div class="wallet-badge" *ngIf="walletService.selectedWallet() as sw">
            <span class="currency-tag">{{ sw.currency }}</span>
            <span class="balance-val">{{ (sw.balance || 0) | number:'1.2-2' }}</span>
          </div>

          <div class="user-badge" title="{{ authService.currentUser()?.email }}">
            <div class="avatar">{{ userInitial() }}</div>
            <span class="user-name">{{ authService.currentUser()?.fullName || 'Kullanıcı' }}</span>
          </div>

          <button class="btn btn-secondary btn-sm logout-btn" (click)="logout()" title="Çıkış Yap">
            <span>Çıkış</span>
            <span>➔</span>
          </button>
        </div>
      </div>
    </header>
  `,
  styles: [`
    .navbar-header {
      background: rgba(17, 24, 39, 0.95);
      border-bottom: 1px solid var(--border-subtle);
      backdrop-filter: blur(8px);
      position: sticky;
      top: 36px;
      z-index: 999;
    }
    .navbar-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 12px 16px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
    }
    .brand-link {
      display: flex;
      align-items: center;
      gap: 10px;
      text-decoration: none;
      color: inherit;
    }
    .brand-icon {
      font-size: 1.8rem;
    }
    .brand-text {
      display: flex;
      flex-direction: column;
    }
    .brand-title {
      font-weight: 800;
      font-size: 1.25rem;
      letter-spacing: 1px;
      background: linear-gradient(135deg, #60a5fa, #818cf8);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .brand-badge {
      font-size: 0.65rem;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .nav-links {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .nav-item {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 14px;
      font-size: 0.9rem;
      font-weight: 500;
      color: var(--text-muted);
      text-decoration: none;
      border-radius: var(--radius-md);
      transition: all 0.2s ease;
      position: relative;
    }
    .nav-item:hover {
      color: var(--text-main);
      background: rgba(255, 255, 255, 0.05);
    }
    .nav-item.active {
      color: #fff;
      background: rgba(59, 130, 246, 0.15);
      border: 1px solid rgba(59, 130, 246, 0.3);
      font-weight: 600;
    }
    .notif-count {
      background: #ef4444;
      color: white;
      font-size: 0.7rem;
      padding: 1px 6px;
      border-radius: 999px;
      font-weight: 700;
    }
    .user-section {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .wallet-badge {
      display: flex;
      align-items: center;
      gap: 6px;
      background: rgba(16, 185, 129, 0.12);
      border: 1px solid rgba(16, 185, 129, 0.3);
      padding: 6px 12px;
      border-radius: var(--radius-md);
      font-size: 0.85rem;
    }
    .currency-tag {
      font-weight: 700;
      color: #10b981;
    }
    .balance-val {
      font-weight: 600;
      color: #fff;
    }
    .user-badge {
      display: flex;
      align-items: center;
      gap: 8px;
      background: var(--bg-card);
      border: 1px solid var(--border-subtle);
      padding: 4px 10px 4px 4px;
      border-radius: 999px;
      font-size: 0.85rem;
    }
    .avatar {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      background: linear-gradient(135deg, #3b82f6, #6366f1);
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      color: #fff;
      font-size: 0.8rem;
    }
    .user-name {
      max-width: 120px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      color: var(--text-main);
    }
    .logout-btn {
      padding: 6px 10px;
    }
    @media (max-width: 860px) {
      .navbar-container {
        flex-direction: column;
        align-items: stretch;
      }
      .nav-links {
        overflow-x: auto;
        padding-bottom: 4px;
      }
      .user-section {
        justify-content: space-between;
      }
    }
  `]
})
export class NavbarComponent {
  authService = inject(AuthService);
  walletService = inject(WalletService);
  notificationService = inject(NotificationService);
  router = inject(Router);

  userInitial(): string {
    const user = this.authService.currentUser();
    if (!user) return 'U';
    return (user.fullName || user.email || 'U').charAt(0).toUpperCase();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
