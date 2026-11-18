import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { WalletService } from '../../core/services/wallet.service';
import { TransferService } from '../../core/services/transfer.service';
import { ToastService } from '../../core/services/toast.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { Wallet } from '../../core/models/wallet.model';

function generateUUID(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

@Component({
  selector: 'app-transfer',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="container">
      <div class="page-header">
        <div>
          <h1>Para Transferi & Canlı Saga Takibi</h1>
          <p class="text-muted">
            Dağıtık MassTransit Saga Orchestration (Risk Analizi → Bakiye Blokesi → Double-Entry Commit).
          </p>
        </div>
      </div>

      <div class="transfer-layout">
        <!-- Transfer Form Card -->
        <div class="card form-card">
          <div class="card-title">
            <span>⚡ Transfer Başlat</span>
            <span class="badge badge-info">Idempotent</span>
          </div>

          <form [formGroup]="transferForm" (ngSubmit)="onSubmit()">
            <!-- Source Wallet Dropdown -->
            <div class="form-group">
              <label class="form-label">Kaynak Cüzdan</label>
              <select formControlName="sourceAccountId" class="form-control" (change)="onSourceChanged()">
                <option value="" disabled>Cüzdan Seçiniz</option>
                <option *ngFor="let w of availableWallets()" [value]="w.id">
                  {{ w.currency }} — Bakiye: {{ (w.balance || 0) | number:'1.2-2' }} (Günlük Limit: {{ w.dailyLimit | number:'1.0-0' }})
                </option>
              </select>
              <div class="source-info" *ngIf="selectedSourceWallet() as sw">
                <span>Kullanılabilir: <strong>{{ (sw.balance || 0) | number:'1.2-2' }} {{ sw.currency }}</strong></span>
                <span>Günlük Limit: <strong>{{ sw.dailyLimit | number:'1.0-0' }} {{ sw.currency }}</strong></span>
              </div>
            </div>

            <!-- Destination Account ID -->
            <div class="form-group">
              <label class="form-label">Hedef Hesap ID (GUID)</label>
              <div class="input-with-button">
                <input
                  type="text"
                  formControlName="destinationAccountId"
                  class="form-control"
                  placeholder="00000000-0000-0000-0000-000000000000"
                />
                <button type="button" class="btn btn-secondary btn-sm" (click)="fillSampleReceiver()">
                  Örnek Alıcı
                </button>
              </div>
              <small class="error-text" *ngIf="isSameAccount()">
                Kaynak ve hedef hesap aynı olamaz.
              </small>
            </div>

            <!-- Transfer Amount -->
            <div class="form-group">
              <label class="form-label">Transfer Tutarı</label>
              <input
                type="number"
                formControlName="amount"
                class="form-control"
                placeholder="100.00"
                step="0.01"
                min="0.01"
              />
              <div class="validation-warnings">
                <small class="error-text" *ngIf="isExceedingBalance()">
                  ⚠️ Tutar kaynak cüzdan bakiyesini aşamaz! (Bakiye: {{ (selectedSourceWallet()?.balance || 0) | number:'1.2-2' }})
                </small>
                <small class="error-text" *ngIf="isExceedingDailyLimit()">
                  ⚠️ Tutar günlük limiti aşamaz! (Günlük limit: {{ selectedSourceWallet()?.dailyLimit | number:'1.0-0' }})
                </small>
              </div>
            </div>

            <!-- Description -->
            <div class="form-group">
              <label class="form-label">Açıklama (Opsiyonel)</label>
              <input
                type="text"
                formControlName="description"
                class="form-control"
                placeholder="Örn: Kira ödemesi / simülasyon transferi"
              />
            </div>

            <!-- Idempotency Key Info -->
            <div class="idempotency-box">
              <div class="idempotency-header">
                <span class="idempotency-title">🔑 Idempotency-Key (UUID)</span>
                <button type="button" class="btn-text" (click)="regenerateKey()">Yenile</button>
              </div>
              <code class="idempotency-code">{{ idempotencyKey() }}</code>
              <small class="idempotency-help">
                Aynı anahtarla tekrar gönderilse bile işlem sadece 1 kez çalışır.
              </small>
            </div>

            <!-- Submit Button -->
            <button
              type="submit"
              class="btn btn-primary btn-block submit-btn"
              [disabled]="transferForm.invalid || isExceedingBalance() || isExceedingDailyLimit() || isSameAccount() || transferService.isTransferring()"
            >
              <span *ngIf="!transferService.isTransferring()">🚀 Transferi Başlat (Saga)</span>
              <span *ngIf="transferService.isTransferring()">İşleniyor (Saga State Machine)...</span>
            </button>
          </form>
        </div>

        <!-- Saga Live Stepper Card -->
        <div class="card saga-card">
          <div class="card-title">
            <span>🔄 Dağıtık Saga Durumu</span>
            <button class="btn btn-secondary btn-sm" (click)="transferService.resetSaga()" *ngIf="transferService.currentTransfer()">
              Sıfırla
            </button>
          </div>

          <!-- Active Transfer Meta -->
          <div class="transfer-meta" *ngIf="transferService.currentTransfer() as ct">
            <div class="meta-row">
              <span class="meta-label">Transfer ID:</span>
              <code class="meta-val">{{ ct.transferId }}</code>
            </div>
            <div class="meta-row">
              <span class="meta-label">Tutar:</span>
              <span class="meta-val highlight">{{ ct.amount }} {{ ct.currency }}</span>
            </div>
            <div class="meta-row">
              <span class="meta-label">Mevcut Durum:</span>
              <span class="badge" [ngClass]="getTransferStateClass(ct.state)">
                {{ ct.state }}
              </span>
            </div>
          </div>

          <!-- Stepper Visualizer -->
          <div class="stepper-container">
            <div
              *ngFor="let step of transferService.sagaSteps(); let last = last"
              class="stepper-item"
              [class.active]="step.status === 'in_progress'"
              [class.completed]="step.status === 'completed'"
              [class.failed]="step.status === 'failed'"
            >
              <div class="stepper-left">
                <div class="step-circle">
                  <span *ngIf="step.status === 'completed'">✓</span>
                  <span *ngIf="step.status === 'failed'">✕</span>
                  <span *ngIf="step.status === 'in_progress'" class="spinner-ring"></span>
                  <span *ngIf="step.status === 'idle'">{{ step.stepNumber }}</span>
                </div>
                <div class="step-line" *ngIf="!last"></div>
              </div>

              <div class="stepper-content">
                <div class="step-header">
                  <span class="step-title">{{ step.title }}</span>
                  <span class="step-status-tag" [ngClass]="'status-' + step.status">
                    {{ getStepStatusLabel(step.status) }}
                  </span>
                </div>
                <p class="step-desc">{{ step.description }}</p>
                <div class="step-detail" *ngIf="step.detail">{{ step.detail }}</div>
              </div>
            </div>
          </div>

          <!-- Error Alert if failed -->
          <div class="saga-error-alert" *ngIf="transferService.lastError()">
            <div class="error-icon">⚠️</div>
            <div>
              <strong>Transfer Reddedildi / İptal Edildi:</strong>
              <p>{{ transferService.lastError() }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header {
      margin-bottom: 24px;
    }
    .transfer-layout {
      display: grid;
      grid-template-columns: 1fr 1.1fr;
      gap: 24px;
    }
    .form-card, .saga-card {
      min-height: 520px;
    }
    .input-with-button {
      display: flex;
      gap: 8px;
    }
    .source-info {
      display: flex;
      justify-content: space-between;
      font-size: 0.8rem;
      color: var(--text-muted);
      margin-top: 4px;
    }
    .validation-warnings {
      margin-top: 4px;
    }
    .error-text {
      color: var(--status-danger);
      font-size: 0.8rem;
      display: block;
    }
    .idempotency-box {
      background: rgba(0, 0, 0, 0.25);
      border: 1px dashed var(--border-subtle);
      border-radius: var(--radius-md);
      padding: 12px;
      margin-bottom: 18px;
    }
    .idempotency-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 4px;
    }
    .idempotency-title {
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--text-muted);
    }
    .btn-text {
      background: none;
      border: none;
      color: var(--brand-primary);
      cursor: pointer;
      font-size: 0.75rem;
      text-decoration: underline;
    }
    .idempotency-code {
      display: block;
      font-size: 0.75rem;
      color: #38bdf8;
      font-family: monospace;
      word-break: break-all;
    }
    .idempotency-help {
      font-size: 0.7rem;
      color: var(--text-dim);
      margin-top: 4px;
      display: block;
    }
    .btn-block {
      width: 100%;
    }
    .submit-btn {
      padding: 14px;
      font-size: 1rem;
    }
    .transfer-meta {
      background: var(--bg-surface);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-md);
      padding: 12px 16px;
      margin-bottom: 20px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .meta-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.85rem;
    }
    .meta-label {
      color: var(--text-muted);
    }
    .meta-val {
      font-weight: 600;
    }
    .meta-val.highlight {
      color: #10b981;
    }
    .stepper-container {
      display: flex;
      flex-direction: column;
    }
    .stepper-item {
      display: flex;
      gap: 16px;
      min-height: 80px;
    }
    .stepper-left {
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .step-circle {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: var(--bg-surface);
      border: 2px solid var(--border-subtle);
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 0.9rem;
      color: var(--text-dim);
      transition: all 0.3s;
      position: relative;
    }
    .stepper-item.completed .step-circle {
      background: rgba(16, 185, 129, 0.2);
      border-color: var(--status-success);
      color: var(--status-success);
    }
    .stepper-item.active .step-circle {
      background: rgba(59, 130, 246, 0.2);
      border-color: var(--brand-primary);
      color: #fff;
      box-shadow: 0 0 12px rgba(59, 130, 246, 0.5);
    }
    .stepper-item.failed .step-circle {
      background: rgba(239, 68, 68, 0.2);
      border-color: var(--status-danger);
      color: var(--status-danger);
    }
    .step-line {
      width: 2px;
      flex: 1;
      background: var(--border-subtle);
      margin: 4px 0;
    }
    .stepper-item.completed .step-line {
      background: var(--status-success);
    }
    .stepper-content {
      flex: 1;
      padding-bottom: 20px;
    }
    .step-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2px;
    }
    .step-title {
      font-weight: 600;
      font-size: 0.95rem;
    }
    .step-status-tag {
      font-size: 0.7rem;
      text-transform: uppercase;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 999px;
    }
    .status-idle {
      background: var(--bg-surface);
      color: var(--text-dim);
    }
    .status-in_progress {
      background: rgba(59, 130, 246, 0.2);
      color: #60a5fa;
      animation: pulse 1.5s infinite;
    }
    .status-completed {
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
    }
    .status-failed {
      background: rgba(239, 68, 68, 0.2);
      color: #f87171;
    }
    .step-desc {
      font-size: 0.8rem;
      color: var(--text-muted);
      margin-top: 2px;
    }
    .step-detail {
      font-size: 0.75rem;
      color: #38bdf8;
      margin-top: 4px;
      font-style: italic;
    }
    .saga-error-alert {
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      border-radius: var(--radius-md);
      padding: 12px 16px;
      margin-top: 16px;
      display: flex;
      gap: 12px;
      align-items: center;
      color: #fca5a5;
      font-size: 0.85rem;
    }
    .spinner-ring {
      width: 14px;
      height: 14px;
      border: 2px solid transparent;
      border-top-color: #3b82f6;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.6; }
    }
    @media (max-width: 900px) {
      .transfer-layout {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class TransferComponent implements OnInit {
  walletService = inject(WalletService);
  transferService = inject(TransferService);
  toastService = inject(ToastService);
  notificationService = inject(NotificationService);
  authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);

  idempotencyKey = signal<string>(generateUUID());
  selectedSourceWallet = signal<Wallet | null>(null);

  transferForm = this.fb.group({
    sourceAccountId: ['', [Validators.required]],
    destinationAccountId: ['', [Validators.required, Validators.pattern(/^[0-9a-fA-F-]{36}$/)]],
    amount: [100, [Validators.required, Validators.min(0.01)]],
    description: ['Kumbara Para Transferi']
  });

  ngOnInit(): void {
    const user = this.authService.currentUser();
    const userId = user?.id || '00000000-0000-0000-0000-000000000001';

    this.walletService.loadWallets(userId).subscribe(wallets => {
      // Check query param
      const queryFrom = this.route.snapshot.queryParams['from'];
      if (queryFrom && wallets.some(w => w.id === queryFrom)) {
        this.transferForm.patchValue({ sourceAccountId: queryFrom });
        this.onSourceChanged();
      } else if (wallets.length > 0) {
        this.transferForm.patchValue({ sourceAccountId: wallets[0].id });
        this.onSourceChanged();
      }
    });
  }

  availableWallets(): Wallet[] {
    return this.walletService.wallets().filter(w => w.status === 1 || w.status === 'Active' || !w.status);
  }

  onSourceChanged(): void {
    const srcId = this.transferForm.value.sourceAccountId;
    const wallet = this.walletService.wallets().find(w => w.id === srcId) || null;
    this.selectedSourceWallet.set(wallet);
  }

  fillSampleReceiver(): void {
    // Generate or use a realistic receiver account GUID
    const sampleReceiver = 'a1b2c3d4-e5f6-47a8-b9c0-123456789abc';
    this.transferForm.patchValue({ destinationAccountId: sampleReceiver });
  }

  regenerateKey(): void {
    this.idempotencyKey.set(generateUUID());
  }

  isExceedingBalance(): boolean {
    const sw = this.selectedSourceWallet();
    const amt = Number(this.transferForm.value.amount || 0);
    if (!sw) return false;
    return amt > (sw.balance || 0);
  }

  isExceedingDailyLimit(): boolean {
    const sw = this.selectedSourceWallet();
    const amt = Number(this.transferForm.value.amount || 0);
    if (!sw || !sw.dailyLimit) return false;
    return amt > sw.dailyLimit;
  }

  isSameAccount(): boolean {
    const src = this.transferForm.value.sourceAccountId;
    const dst = this.transferForm.value.destinationAccountId;
    return !!src && !!dst && src.trim().toLowerCase() === dst.trim().toLowerCase();
  }

  onSubmit(): void {
    if (this.transferForm.invalid || this.isExceedingBalance() || this.isExceedingDailyLimit() || this.isSameAccount()) {
      return;
    }

    const { sourceAccountId, destinationAccountId, amount, description } = this.transferForm.value;
    const sw = this.selectedSourceWallet();
    const currency = sw?.currency || 'TRY';

    this.transferService.initiateTransfer({
      sourceAccountId: sourceAccountId!,
      destinationAccountId: destinationAccountId!,
      amount: Number(amount),
      currency,
      description: description || undefined
    }, this.idempotencyKey()).subscribe({
      next: () => {
        this.toastService.info('Saga transfer işlemi başlatıldı.');
        // Regenerate next idempotency key
        this.idempotencyKey.set(generateUUID());

        // Dispatch simulated SMS alert
        this.notificationService.dispatchNotification(
          'SMS',
          '+90 555 *** 1234',
          'Transfer Başlatıldı',
          `${amount} ${currency} tutarında para transferi talebiniz işlenmektedir.`,
          'info'
        );
      },
      error: () => {
        // Handled in service/interceptor
      }
    });
  }

  getTransferStateClass(state: string): string {
    switch (state) {
      case 'Completed': return 'badge-success';
      case 'Failed': return 'badge-danger';
      default: return 'badge-warning';
    }
  }

  getStepStatusLabel(status: string): string {
    switch (status) {
      case 'in_progress': return 'İşleniyor';
      case 'completed': return 'Tamamlandı';
      case 'failed': return 'Hata';
      default: return 'Bekliyor';
    }
  }
}
