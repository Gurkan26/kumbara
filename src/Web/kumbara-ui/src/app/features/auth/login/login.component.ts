import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="auth-page">
      <div class="auth-card">
        <div class="auth-header">
          <div class="brand-icon">🏺</div>
          <h2>Kumbara Giriş</h2>
          <p class="subtitle">Fintech mikroservis simülasyon platformu</p>
        </div>

        <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label class="form-label">E-Posta Adresi</label>
            <input
              type="email"
              formControlName="email"
              class="form-control"
              placeholder="ornek@kumbara.local"
            />
            <small class="error-text" *ngIf="loginForm.get('email')?.touched && loginForm.get('email')?.invalid">
              Geçerli bir e-posta adresi giriniz.
            </small>
          </div>

          <div class="form-group">
            <label class="form-label">Şifre</label>
            <input
              type="password"
              formControlName="password"
              class="form-control"
              placeholder="••••••••"
            />
            <small class="error-text" *ngIf="loginForm.get('password')?.touched && loginForm.get('password')?.invalid">
              Şifre en az 6 karakter olmalıdır.
            </small>
          </div>

          <button
            type="submit"
            class="btn btn-primary btn-block"
            [disabled]="loginForm.invalid || isSubmitting()"
          >
            <span *ngIf="!isSubmitting()">Giriş Yap</span>
            <span *ngIf="isSubmitting()">Doğrulanıyor...</span>
          </button>
        </form>

        <div class="demo-box">
          <div class="demo-title">Hızlı Test Hesabı:</div>
          <button type="button" class="btn btn-secondary btn-sm btn-block" (click)="fillDemoUser()">
            ⚡ Demo Kullanıcı Bilgilerini Doldur
          </button>
        </div>

        <div class="auth-footer">
          <span>Hesabınız yok mu?</span>
          <a routerLink="/register">Yeni Hesap Oluştur</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .auth-page {
      min-height: calc(100vh - 40px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px 16px;
    }
    .auth-card {
      background: var(--bg-card);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-xl);
      max-width: 440px;
      width: 100%;
      padding: 36px 32px;
      box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.4);
    }
    .auth-header {
      text-align: center;
      margin-bottom: 28px;
    }
    .brand-icon {
      font-size: 2.5rem;
      margin-bottom: 8px;
    }
    .subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-top: 4px;
    }
    .btn-block {
      width: 100%;
      margin-top: 8px;
    }
    .error-text {
      color: var(--status-danger);
      font-size: 0.8rem;
    }
    .demo-box {
      margin-top: 24px;
      padding-top: 16px;
      border-top: 1px dashed var(--border-subtle);
    }
    .demo-title {
      font-size: 0.75rem;
      color: var(--text-dim);
      text-transform: uppercase;
      margin-bottom: 8px;
      letter-spacing: 0.5px;
    }
    .auth-footer {
      margin-top: 24px;
      text-align: center;
      font-size: 0.875rem;
      color: var(--text-muted);
      display: flex;
      justify-content: center;
      gap: 6px;
    }
    .auth-footer a {
      color: var(--brand-primary);
      text-decoration: none;
      font-weight: 600;
    }
    .auth-footer a:hover {
      text-decoration: underline;
    }
  `]
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private router = inject(Router);

  readonly isSubmitting = signal<boolean>(false);

  loginForm = this.fb.group({
    email: ['demo@kumbara.local', [Validators.required, Validators.email]],
    password: ['Password123!', [Validators.required, Validators.minLength(6)]]
  });

  fillDemoUser(): void {
    this.loginForm.setValue({
      email: 'demo@kumbara.local',
      password: 'Password123!'
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isSubmitting.set(true);
    const { email, password } = this.loginForm.value;

    this.authService.login({ email: email!, password: password! }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.toastService.success('Başarıyla giriş yapıldı!');
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        // Error toast is already handled by error.interceptor or we can display specific message
      }
    });
  }
}
