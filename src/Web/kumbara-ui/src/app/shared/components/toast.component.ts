import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container" *ngIf="toastService.toasts().length > 0">
      <div
        *ngFor="let t of toastService.toasts()"
        class="toast-item"
        [ngClass]="'toast-' + t.type"
        (click)="toastService.remove(t.id)"
      >
        <div class="toast-icon">
          <span *ngIf="t.type === 'success'">✅</span>
          <span *ngIf="t.type === 'error'">❌</span>
          <span *ngIf="t.type === 'warning'">⚠️</span>
          <span *ngIf="t.type === 'info'">ℹ️</span>
        </div>
        <div class="toast-text">{{ t.message }}</div>
        <button class="toast-close" (click)="toastService.remove(t.id); $event.stopPropagation()">×</button>
      </div>
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      top: 80px;
      right: 20px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 10px;
      max-width: 400px;
      width: 100%;
      pointer-events: none;
    }
    .toast-item {
      pointer-events: auto;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 16px;
      border-radius: var(--radius-md);
      background: var(--bg-surface);
      border: 1px solid var(--border-subtle);
      box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.4);
      color: #fff;
      font-size: 0.9rem;
      cursor: pointer;
      animation: slideIn 0.25s ease-out;
    }
    .toast-success {
      border-left: 4px solid var(--status-success);
    }
    .toast-error {
      border-left: 4px solid var(--status-danger);
    }
    .toast-warning {
      border-left: 4px solid var(--status-warning);
    }
    .toast-info {
      border-left: 4px solid var(--brand-primary);
    }
    .toast-text {
      flex: 1;
      word-break: break-word;
    }
    .toast-close {
      background: none;
      border: none;
      color: var(--text-dim);
      font-size: 1.2rem;
      cursor: pointer;
      padding: 0 4px;
    }
    .toast-close:hover {
      color: #fff;
    }
    @keyframes slideIn {
      from {
        opacity: 0;
        transform: translateX(30px);
      }
      to {
        opacity: 1;
        transform: translateX(0);
      }
    }
  `]
})
export class ToastComponent {
  toastService = inject(ToastService);
}
