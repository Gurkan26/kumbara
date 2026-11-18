import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const toastService = inject(ToastService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401: Unauthorized
      if (error.status === 401) {
        // Avoid infinite loop if refreshing fails or already on login
        if (req.url.includes('/api/auth/login') || req.url.includes('/api/auth/refresh')) {
          toastService.error('Giriş oturumunuz geçersiz veya süresi dolmuş.');
          authService.logout();
          router.navigate(['/login']);
          return throwError(() => error);
        }

        // Try to refresh token
        return authService.refreshToken().pipe(
          switchMap(authRes => {
            if (authRes && authRes.accessToken) {
              const retryReq = req.clone({
                setHeaders: {
                  Authorization: `Bearer ${authRes.accessToken}`
                }
              });
              return next(retryReq);
            }
            authService.logout();
            router.navigate(['/login']);
            return throwError(() => error);
          }),
          catchError(refreshErr => {
            authService.logout();
            router.navigate(['/login']);
            return throwError(() => refreshErr);
          })
        );
      }

      // 429: Rate Limit Exceeded
      if (error.status === 429) {
        toastService.warning('⚠️ Çok fazla istek gönderildi (429 Rate Limit). Lütfen birkaç saniye bekleyin.');
      } else if (error.status === 400) {
        // 400: Risk, Balance, or Domain Validation Errors
        const errorMsg =
          error.error?.error ||
          error.error?.message ||
          (typeof error.error === 'string' ? error.error : 'İşlem gerçekleştirilemedi (Geçersiz İstek).');
        toastService.error(`İşlem Hatası: ${errorMsg}`);
      } else if (error.status === 404) {
        const errorMsg = error.error?.message || 'İstenen kaynak bulunamadı (404).';
        toastService.info(errorMsg);
      } else if (error.status >= 500) {
        toastService.error('Sunucu hatası oluştu (500). Lütfen daha sonra tekrar deneyiniz.');
      }

      return throwError(() => error);
    })
  );
};
