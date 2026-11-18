import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { WalletService } from '../../core/services/wallet.service';
import { LedgerService } from '../../core/services/ledger.service';
import { ToastService } from '../../core/services/toast.service';
import { NotificationService } from '../../core/services/notification.service';
import { Wallet, WalletStatus } from '../../core/models/wallet.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="container">
      <!-- Welcome Header -->
      <div class="dashboard-header">
        <div>
          <h1>Hesap Özeti & Cüzdanlar</h1>
          <p class="text-muted">Aktif hesaplarınızı, bakiyelerinizi ve harcama limitlerinizi yönetin.</p>
        </div>
        <div class="action-buttons">
          <button class="btn btn-secondary" (click)="refreshWallets()">
            🔄 Yenile
          </button>
          <button class="btn btn-primary" (click)="openCreateModal()">
            ➕ Yeni Cüzdan Aç
          </button>
        </div>
      </div>

      <!-- Quick Metrics Overview -->
      <div class="metrics-grid">
        <div class="metric-card">
          <span class="metric-label">Toplam Cüzdan Sayısı</span>
          <span class="metric-value">{{ walletService.wallets().length }}</span>
          <span class="metric-sub">Aktif & Askıda hesaplar</span>
        </div>
        <div class="metric-card">
          <span class="metric-label">Birincil Cüzdan Bakiyesi</span>
          <span class="metric-value highlight" *ngIf="walletService.selectedWallet() as sw">
            {{ (sw.balance || 0) | number:'1.2-2' }} {{ sw.currency }}
          </span>
          <span class="metric-value highlight" *ngIf="!walletService.selectedWallet()">
            0.00 TRY
          </span>
          <span class="metric-sub" *ngIf="walletService.selectedWallet() as sw">
            Bloke Bakiye: {{ (sw.blockedBalance || 0) | number:'1.2-2' }} {{ sw.currency }}
          </span>
          <span class="metric-sub" *ngIf="!walletService.selectedWallet()">
            Cüzdan seçilmedi
          </span>
        </div>
        <div class="metric-card">
          <span class="metric-label">Simülasyon Durumu</span>
          <span class="badge badge-warning" style="align-self: flex-start; margin-top: 6px;">
            Test Ortamı (Sandbox)
          </span>
          <span class="metric-sub">Gerçek finansal işlem yapılmaz</span>
        </div>
      </div>

      <!-- Wallets Section -->
      <div class="section-title">
        <h2>Cüzdanlarım</h2>
        <span class="text-dim">Detayları görmek veya işlem yapmak için seçin</span>
      </div>

      <!-- Loading State -->
      <div class="loading-state" *ngIf="walletService.isLoading()">
        <p>Cüzdan bilgileri ve ledger bakiyeleri yükleniyor...</p>
      </div>

      <!-- Empty State -->
      <div class="empty-state card" *ngIf="!walletService.isLoading() && walletService.wallets().length === 0">
        <div class="empty-icon">👛</div>
        <h3>Henüz Kayıtlı Bir Cüzdanınız Yok</h3>
        <p class="text-muted">Kumbara platformunda işlem yapabilmek için hemen bir TRY, USD veya EUR cüzdanı oluşturun.</p>
        <button class="btn btn-primary" (click)="openCreateModal()">
          İlk Cüzdanımı Oluştur
        </button>
      </div>

      <!-- Wallets Grid -->
      <div class="wallets-grid" *ngIf="walletService.wallets().length > 0">
        <div
          *ngFor="let w of walletService.wallets()"
          class="wallet-card"
          [class.selected]="walletService.selectedWallet()?.id === w.id"
          [class.frozen]="isFrozen(w)"
          (click)="selectWallet(w)"
        >
          <div class="wallet-top">
            <div class="currency-chip">
              <span class="flag-icon">{{ getCurrencyIcon(w.currency) }}</span>
              <span class="currency-code">{{ w.currency }}</span>
            </div>
            <span class="badge" [ngClass]="getStatusBadgeClass(w)">
              {{ getStatusLabel(w) }}
            </span>
          </div>

          <div class="wallet-balance-box">
            <span class="balance-title">Kullanılabilir Bakiye</span>
            <div class="balance-amount">
              {{ (w.balance || 0) | number:'1.2-2' }}
              <span class="curr">{{ w.currency }}</span>
            </div>
            <div class="blocked-amount" *ngIf="(w.blockedBalance || 0) > 0">
              🔒 Bloke Edilen: {{ (w.blockedBalance || 0) | number:'1.2-2' }} {{ w.currency }}
            </div>
          </div>

          <div class="wallet-limits">
            <div class="limit-row">
              <span class="limit-label">Günlük Limit:</span>
              <span class="limit-val">{{ w.dailyLimit | number:'1.0-0' }} {{ w.currency }}</span>
            </div>
            <div class="limit-row">
              <span class="limit-label">Aylık Limit:</span>
              <span class="limit-val">{{ w.monthlyLimit | number:'1.0-0' }} {{ w.currency }}</span>
            </div>
          </div>

          <div class="wallet-actions" (click)="$event.stopPropagation()">
            <button
              class="btn btn-sm btn-secondary"
              (click)="openDepositModal(w)"
              [disabled]="isFrozen(w)"
              title="Test bakiyesi ekle"
            >
              💵 Bakiye Yükle
            </button>
            <a
              class="btn btn-sm btn-primary"
              routerLink="/transfer"
              [queryParams]="{ from: w.id }"
              *ngIf="!isFrozen(w)"
            >
              ⚡ Transfer
            </a>
            <button
              *ngIf="!isFrozen(w)"
              class="btn btn-sm btn-danger"
              (click)="freezeWallet(w)"
              title="Cüzdanı dondur"
            >
              ❄️ Dondur
            </button>
            <button
              *ngIf="isFrozen(w)"
              class="btn btn-sm btn-secondary"
              style="color: #10b981; border-color: rgba(16, 185, 129, 0.4);"
              (click)="unfreezeWallet(w)"
              title="Cüzdanı çöz"
            >
              🔓 Çöz
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Wallet Modal -->
    <div class="modal-overlay" *ngIf="showCreateModal()">
      <div class="modal-content">
        <div class="card-title">
          <span>Yeni Cüzdan Oluştur</span>
          <button class="btn btn-sm btn-secondary" (click)="closeCreateModal()">✕</button>
        </div>

        <form [formGroup]="createWalletForm" (ngSubmit)="onCreateWalletSubmit()">
          <div class="form-group">
            <label class="form-label">Para Birimi</label>
            <select formControlName="currency" class="form-control">
              <option value="TRY">TRY - Türk Lirası (₺)</option>
              <option value="USD">USD - Amerikan Doları ($)</option>
              <option value="EUR">EUR - Euro (€)</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">Günlük Transfer Limiti</label>
            <input
              type="number"
              formControlName="dailyLimit"
              class="form-control"
              placeholder="50000"
            />
          </div>

          <div class="form-group">
            <label class="form-label">Aylık Transfer Limiti</label>
            <input
              type="number"
              formControlName="monthlyLimit"
              class="form-control"
              placeholder="250000"
            />
          </div>

          <div style="display: flex; gap: 10px; margin-top: 20px;">
            <button type="button" class="btn btn-secondary" style="flex: 1;" (click)="closeCreateModal()">
              İptal
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              style="flex: 1;"
              [disabled]="createWalletForm.invalid || isCreating()"
            >
              <span *ngIf="!isCreating()">Cüzdanı Aç</span>
              <span *ngIf="isCreating()">Açılıyor...</span>
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Deposit Simulated Funds Modal -->
    <div class="modal-overlay" *ngIf="showDepositModal()">
      <div class="modal-content">
        <div class="card-title">
          <span>Test Bakiyesi Yükle</span>
          <button class="btn btn-sm btn-secondary" (click)="closeDepositModal()">✕</button>
        </div>

        <p class="text-muted" style="font-size: 0.85rem; margin-bottom: 16px;">
          Seçili cüzdana çift taraflı (double-entry) ledger kaydı ile anında simüle fon yatırın.
        </p>

        <form [formGroup]="depositForm" (ngSubmit)="onDepositSubmit()">
          <div class="form-group">
            <label class="form-label">Yüklenecek Tutar ({{ depositTargetWallet()?.currency }})</label>
            <input
              type="number"
              formControlName="amount"
              class="form-control"
              placeholder="1000"
              min="1"
            />
          </div>

          <div class="form-group">
            <label class="form-label">Açıklama</label>
            <input
              type="text"
              formControlName="description"
              class="form-control"
              placeholder="Simülasyon bakiye yüklemesi"
            />
          </div>

          <div style="display: flex; gap: 10px; margin-top: 20px;">
            <button type="button" class="btn btn-secondary" style="flex: 1;" (click)="closeDepositModal()">
              İptal
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              style="flex: 1;"
              [disabled]="depositForm.invalid || isDepositing()"
            >
              <span *ngIf="!isDepositing()">Yükle</span>
              <span *ngIf="isDepositing()">İşleniyor...</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
      gap: 16px;
    }
    .action-buttons {
      display: flex;
      gap: 10px;
    }
    .metrics-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 16px;
      margin-bottom: 32px;
    }
    .metric-card {
      background: var(--bg-card);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-lg);
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .metric-label {
      font-size: 0.85rem;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .metric-value {
      font-size: 1.8rem;
      font-weight: 700;
      color: #fff;
    }
    .metric-value.highlight {
      color: #38bdf8;
    }
    .metric-sub {
      font-size: 0.8rem;
      color: var(--text-dim);
    }
    .section-title {
      margin-bottom: 16px;
    }
    .loading-state {
      text-align: center;
      padding: 40px;
      color: var(--text-muted);
    }
    .empty-state {
      text-align: center;
      padding: 48px 24px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
    }
    .empty-icon {
      font-size: 3rem;
    }
    .wallets-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 20px;
    }
    .wallet-card {
      background: var(--bg-card);
      border: 2px solid var(--border-subtle);
      border-radius: var(--radius-lg);
      padding: 22px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      cursor: pointer;
      transition: all 0.2s ease;
      position: relative;
    }
    .wallet-card:hover {
      transform: translateY(-2px);
      border-color: rgba(59, 130, 246, 0.5);
    }
    .wallet-card.selected {
      border-color: #3b82f6;
      box-shadow: 0 0 20px rgba(59, 130, 246, 0.25);
    }
    .wallet-card.frozen {
      opacity: 0.75;
      border-color: rgba(239, 68, 68, 0.4);
    }
    .wallet-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .currency-chip {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .flag-icon {
      font-size: 1.4rem;
    }
    .currency-code {
      font-weight: 800;
      font-size: 1.1rem;
    }
    .wallet-balance-box {
      background: rgba(0, 0, 0, 0.2);
      border-radius: var(--radius-md);
      padding: 14px;
    }
    .balance-title {
      font-size: 0.8rem;
      color: var(--text-muted);
    }
    .balance-amount {
      font-size: 1.7rem;
      font-weight: 800;
      color: #10b981;
      margin-top: 4px;
    }
    .curr {
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-muted);
    }
    .blocked-amount {
      font-size: 0.75rem;
      color: #f59e0b;
      margin-top: 4px;
    }
    .wallet-limits {
      border-top: 1px solid var(--border-subtle);
      padding-top: 12px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .limit-row {
      display: flex;
      justify-content: space-between;
      font-size: 0.85rem;
    }
    .limit-label {
      color: var(--text-dim);
    }
    .limit-val {
      font-weight: 600;
    }
    .wallet-actions {
      display: flex;
      gap: 8px;
      margin-top: auto;
      flex-wrap: wrap;
    }
  `]
})
export class DashboardComponent implements OnInit {
  authService = inject(AuthService);
  walletService = inject(WalletService);
  ledgerService = inject(LedgerService);
  toastService = inject(ToastService);
  notificationService = inject(NotificationService);
  private fb = inject(FormBuilder);

  showCreateModal = signal<boolean>(false);
  isCreating = signal<boolean>(false);

  showDepositModal = signal<boolean>(false);
  isDepositing = signal<boolean>(false);
  depositTargetWallet = signal<Wallet | null>(null);

  createWalletForm = this.fb.group({
    currency: ['TRY', [Validators.required]],
    dailyLimit: [50000, [Validators.required, Validators.min(1)]],
    monthlyLimit: [250000, [Validators.required, Validators.min(1)]]
  });

  depositForm = this.fb.group({
    amount: [1000, [Validators.required, Validators.min(1)]],
    description: ['Demo Bakiye Yükleme', [Validators.required]]
  });

  ngOnInit(): void {
    this.refreshWallets();
  }

  refreshWallets(): void {
    const user = this.authService.currentUser();
    const userId = user?.id || '00000000-0000-0000-0000-000000000001';
    this.walletService.loadWallets(userId).subscribe();
  }

  selectWallet(w: Wallet): void {
    this.walletService.selectWallet(w);
  }

  isFrozen(w: Wallet): boolean {
    return w.status === 3 || w.status === 'Frozen' || (typeof w.status === 'string' && w.status.toLowerCase() === 'frozen');
  }

  getStatusBadgeClass(w: Wallet): string {
    if (this.isFrozen(w)) return 'badge-danger';
    return 'badge-success';
  }

  getStatusLabel(w: Wallet): string {
    if (this.isFrozen(w)) return 'DONDURULDU';
    return 'AKTİF';
  }

  getCurrencyIcon(curr: string): string {
    switch (curr?.toUpperCase()) {
      case 'TRY': return '🇹🇷';
      case 'USD': return '🇺🇸';
      case 'EUR': return '🇪🇺';
      default: return '💰';
    }
  }

  openCreateModal(): void {
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  onCreateWalletSubmit(): void {
    if (this.createWalletForm.invalid) return;

    this.isCreating.set(true);
    const user = this.authService.currentUser();
    const userId = user?.id || '00000000-0000-0000-0000-000000000001';
    const { currency, dailyLimit, monthlyLimit } = this.createWalletForm.value;

    this.walletService.createWallet({
      userId,
      currency: currency!,
      dailyLimit: Number(dailyLimit),
      monthlyLimit: Number(monthlyLimit)
    }).subscribe({
      next: (created) => {
        this.isCreating.set(false);
        this.closeCreateModal();
        this.toastService.success(`${currency} cüzdanı başarıyla oluşturuldu!`);
        this.notificationService.dispatchNotification(
          'EMAIL',
          user?.email || 'user@kumbara.local',
          'Yeni Cüzdan Oluşturuldu',
          `${currency} para biriminde yeni cüzdanınız (ID: ${created.id}) aktif edildi.`,
          'success'
        );
      },
      error: () => {
        this.isCreating.set(false);
      }
    });
  }

  freezeWallet(w: Wallet): void {
    const reason = prompt('Cüzdan dondurma sebebi giriniz:', 'Güvenlik incelemesi') || 'Güvenlik incelemesi';
    this.walletService.freezeWallet(w.id, reason).subscribe({
      next: () => {
        this.toastService.warning(`${w.currency} cüzdanı donduruldu.`);
      }
    });
  }

  unfreezeWallet(w: Wallet): void {
    this.walletService.unfreezeWallet(w.id).subscribe({
      next: () => {
        this.toastService.success(`${w.currency} cüzdanı yeniden aktif edildi.`);
      }
    });
  }

  openDepositModal(w: Wallet): void {
    this.depositTargetWallet.set(w);
    this.depositForm.reset({
      amount: 1000,
      description: 'Simülasyon Fon Yatırma'
    });
    this.showDepositModal.set(true);
  }

  closeDepositModal(): void {
    this.showDepositModal.set(false);
    this.depositTargetWallet.set(null);
  }

  onDepositSubmit(): void {
    const target = this.depositTargetWallet();
    if (!target || this.depositForm.invalid) return;

    this.isDepositing.set(true);
    const { amount, description } = this.depositForm.value;

    this.ledgerService.deposit({
      accountId: target.id,
      amount: Number(amount),
      currency: target.currency,
      description: description!
    }).subscribe({
      next: () => {
        this.isDepositing.set(false);
        this.closeDepositModal();
        this.toastService.success(`${amount} ${target.currency} başarıyla yatırıldı!`);
        this.walletService.enrichWithBalances([target]);
      },
      error: () => {
        this.isDepositing.set(false);
      }
    });
  }
}
