import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="auth-page">
      <div class="auth-card">
        <div class="auth-header">
          <div class="brand-icon">🏺</div>
          <h2>Yeni Kumbara Hesabı</h2>
          <p class="subtitle">Fintech simülasyon ortamına katılın</p>
        </div>

        <form [formGroup]="registerForm" (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label class="form-label">Ad Soyad</label>
            <input
              type="text"
              formControlName="fullName"
              class="form-control"
              placeholder="Ahmet Yılmaz"
            />
            <small class="error-text" *ngIf="registerForm.get('fullName')?.touched && registerForm.get('fullName')?.invalid">
              Ad Soyad zorunludur.
            </small>
          </div>

          <div class="form-group">
            <label class="form-label">E-Posta Adresi</label>
            <input
              type="email"
              formControlName="email"
              class="form-control"
              placeholder="ornek@kumbara.local"
            />
            <small class="error-text" *ngIf="registerForm.get('email')?.touched && registerForm.get('email')?.invalid">
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
            <small class="error-text" *ngIf="registerForm.get('password')?.touched && registerForm.get('password')?.invalid">
              Şifre en az 6 karakter olmalıdır.
            </small>
          </div>

          <button
            type="submit"
            class="btn btn-primary btn-block"
            [disabled]="registerForm.invalid || isSubmitting()"
          >
            <span *ngIf="!isSubmitting()">Kayıt Ol</span>
            <span *ngIf="isSubmitting()">Hesap Oluşturuluyor...</span>
          </button>
        </form>

        <div class="auth-footer">
          <span>Zaten hesabınız var mı?</span>
          <a routerLink="/login">Giriş Yap</a>
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
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private router = inject(Router);

  readonly isSubmitting = signal<boolean>(false);

  registerForm = this.fb.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  onSubmit(): void {
    if (this.registerForm.invalid) return;

    this.isSubmitting.set(true);
    const { fullName, email, password } = this.registerForm.value;

    this.authService.register({
      fullName: fullName!,
      email: email!,
      password: password!
    }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.toastService.success('Kayıt başarılı! Şimdi giriş yapabilirsiniz.');
        this.router.navigate(['/login']);
      },
      error: () => {
        this.isSubmitting.set(false);
      }
    });
  }
}
