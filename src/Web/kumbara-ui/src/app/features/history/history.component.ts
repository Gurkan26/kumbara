import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { WalletService } from '../../core/services/wallet.service';
import { LedgerService } from '../../core/services/ledger.service';
import { AuthService } from '../../core/services/auth.service';
import { Wallet } from '../../core/models/wallet.model';
import { LedgerEntryDto } from '../../core/models/ledger.model';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container">
      <div class="page-header">
        <div>
          <h1>Ledger Defteri & Hareket Geçmişi</h1>
          <p class="text-muted">
            Append-only (yalnızca eklenen), çift taraflı (double-entry) muhasebe kayıtları.
          </p>
        </div>

        <!-- Account Selector -->
        <div class="account-picker" *ngIf="walletService.wallets().length > 0">
          <label class="picker-label">İncelenen Cüzdan:</label>
          <select [ngModel]="selectedWalletId()" (ngModelChange)="onWalletChange($event)" class="form-control picker-select">
            <option *ngFor="let w of walletService.wallets()" [value]="w.id">
              {{ w.currency }} — {{ w.id.substring(0, 8) }}...
            </option>
          </select>
        </div>
      </div>

      <!-- Ledger Summary Cards -->
      <div class="ledger-stats-grid" *ngIf="ledgerService.currentBalance() as bal">
        <div class="stat-box">
          <span class="stat-label">Kullanılabilir Bakiye</span>
          <span class="stat-val credit">{{ bal.availableBalance | number:'1.2-2' }} {{ bal.currency }}</span>
        </div>
        <div class="stat-box">
          <span class="stat-label">Bloke Bakiye (Holds)</span>
          <span class="stat-val warning">{{ bal.blockedBalance | number:'1.2-2' }} {{ bal.currency }}</span>
        </div>
        <div class="stat-box">
          <span class="stat-label">Toplam Ledger Bakiyesi</span>
          <span class="stat-val">{{ bal.totalBalance | number:'1.2-2' }} {{ bal.currency }}</span>
        </div>
      </div>

      <!-- Ledger Entries Table Card -->
      <div class="card entries-card">
        <div class="card-title">
          <span>Hareket Listesi ({{ ledgerService.entries().length }} Kayıt)</span>
          <button class="btn btn-secondary btn-sm" (click)="refreshEntries()">
            🔄 Yenile
          </button>
        </div>

        <!-- Loading State -->
        <div class="table-loading" *ngIf="ledgerService.isLoading()">
          Yükleniyor...
        </div>

        <!-- Empty State -->
        <div class="empty-entries" *ngIf="!ledgerService.isLoading() && ledgerService.entries().length === 0">
          <div class="empty-icon">📭</div>
          <p>Bu cüzdana ait henüz bir ledger hareketi (Debit/Credit) bulunamadı.</p>
          <div class="empty-actions">
            <a routerLink="/dashboard" class="btn btn-secondary btn-sm">Bakiye Yükle</a>
            <a routerLink="/transfer" class="btn btn-primary btn-sm">Transfer Yap</a>
          </div>
        </div>

        <!-- Table -->
        <div class="table-responsive" *ngIf="!ledgerService.isLoading() && ledgerService.entries().length > 0">
          <table class="ledger-table">
            <thead>
              <tr>
                <th>Tarih / Saat</th>
                <th>İşlem ID (Transaction)</th>
                <th>Tür</th>
                <th>Açıklama</th>
                <th style="text-align: right;">Tutar</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let entry of ledgerService.entries()">
                <td class="date-cell">
                  {{ entry.createdAt | date:'dd.MM.yyyy HH:mm:ss' }}
                </td>
                <td class="id-cell">
                  <code title="{{ entry.transactionId }}">{{ entry.transactionId.substring(0, 8) }}...</code>
                </td>
                <td>
                  <span
                    class="badge"
                    [ngClass]="isCredit(entry) ? 'badge-success' : 'badge-danger'"
                  >
                    {{ entry.type }}
                  </span>
                </td>
                <td class="desc-cell">{{ entry.description || 'İşlem' }}</td>
                <td class="amount-cell" [ngClass]="isCredit(entry) ? 'credit' : 'debit'">
                  <span>{{ isCredit(entry) ? '+' : '-' }}</span>
                  <span>{{ entry.amount | number:'1.2-2' }}</span>
                  <span class="curr">{{ entry.currency }}</span>
                </td>
              </tr>
            </tbody>
          </table>
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
    .account-picker {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .picker-label {
      font-size: 0.85rem;
      color: var(--text-muted);
      white-space: nowrap;
    }
    .picker-select {
      min-width: 220px;
    }
    .ledger-stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }
    .stat-box {
      background: var(--bg-card);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-md);
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .stat-label {
      font-size: 0.75rem;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .stat-val {
      font-size: 1.5rem;
      font-weight: 700;
      color: #fff;
    }
    .stat-val.credit {
      color: #10b981;
    }
    .stat-val.warning {
      color: #f59e0b;
    }
    .entries-card {
      min-height: 400px;
    }
    .table-loading {
      text-align: center;
      padding: 40px;
      color: var(--text-muted);
    }
    .empty-entries {
      text-align: center;
      padding: 48px;
      color: var(--text-muted);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
    }
    .empty-icon {
      font-size: 2.5rem;
    }
    .empty-actions {
      display: flex;
      gap: 10px;
    }
    .table-responsive {
      overflow-x: auto;
    }
    .ledger-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }
    .ledger-table th {
      text-align: left;
      padding: 12px 14px;
      background: rgba(0, 0, 0, 0.2);
      color: var(--text-muted);
      font-weight: 600;
      border-bottom: 1px solid var(--border-subtle);
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .ledger-table td {
      padding: 14px;
      border-bottom: 1px solid var(--border-subtle);
    }
    .ledger-table tr:hover td {
      background: rgba(255, 255, 255, 0.02);
    }
    .date-cell {
      white-space: nowrap;
      color: var(--text-muted);
      font-size: 0.85rem;
    }
    .id-cell code {
      background: var(--bg-surface);
      padding: 2px 6px;
      border-radius: var(--radius-sm);
      font-size: 0.8rem;
      color: #38bdf8;
    }
    .desc-cell {
      color: var(--text-main);
    }
    .amount-cell {
      text-align: right;
      font-weight: 700;
      font-size: 1rem;
      white-space: nowrap;
    }
    .amount-cell.credit {
      color: #10b981;
    }
    .amount-cell.debit {
      color: #ef4444;
    }
    .curr {
      font-size: 0.8rem;
      color: var(--text-dim);
      margin-left: 4px;
    }
  `]
})
export class HistoryComponent implements OnInit {
  walletService = inject(WalletService);
  ledgerService = inject(LedgerService);
  authService = inject(AuthService);

  selectedWalletId = signal<string>('');

  ngOnInit(): void {
    const user = this.authService.currentUser();
    const userId = user?.id || '00000000-0000-0000-0000-000000000001';

    this.walletService.loadWallets(userId).subscribe(wallets => {
      const selected = this.walletService.selectedWallet();
      if (selected) {
        this.selectedWalletId.set(selected.id);
        this.fetchLedger(selected.id);
      } else if (wallets.length > 0) {
        this.selectedWalletId.set(wallets[0].id);
        this.fetchLedger(wallets[0].id);
      }
    });
  }

  onWalletChange(walletId: string): void {
    this.selectedWalletId.set(walletId);
    const target = this.walletService.wallets().find(w => w.id === walletId);
    if (target) {
      this.walletService.selectWallet(target);
    }
    this.fetchLedger(walletId);
  }

  fetchLedger(accountId: string): void {
    if (!accountId) return;
    this.ledgerService.getBalance(accountId).subscribe();
    this.ledgerService.getEntries(accountId).subscribe();
  }

  refreshEntries(): void {
    const id = this.selectedWalletId();
    if (id) {
      this.fetchLedger(id);
    }
  }

  isCredit(entry: LedgerEntryDto): boolean {
    const t = entry.type?.toLowerCase();
    return t === 'credit' || t === 'alacak';
  }
}
