import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, map, catchError, of, forkJoin } from 'rxjs';
import { ApiResponse } from '../models/auth.model';
import { CreateWalletRequest, FreezeWalletRequest, UpdateLimitsRequest, Wallet } from '../models/wallet.model';
import { AccountBalanceDto } from '../models/ledger.model';

@Injectable({
  providedIn: 'root'
})
export class WalletService {
  private http = inject(HttpClient);
  private apiUrl = '/api/wallets';
  private ledgerApiUrl = '/api/ledger';

  readonly wallets = signal<Wallet[]>([]);
  readonly selectedWallet = signal<Wallet | null>(null);
  readonly isLoading = signal<boolean>(false);

  loadWallets(userId: string): Observable<Wallet[]> {
    this.isLoading.set(true);
    return this.http.get<ApiResponse<Wallet[]>>(`${this.apiUrl}/user/${userId}`).pipe(
      map(res => res.data || []),
      tap(walletsList => {
        this.wallets.set(walletsList);
        if (walletsList.length > 0 && !this.selectedWallet()) {
          this.selectedWallet.set(walletsList[0]);
        }
        this.isLoading.set(false);
        this.enrichWithBalances(walletsList);
      }),
      catchError(err => {
        this.isLoading.set(false);
        return of([]);
      })
    );
  }

  createWallet(request: CreateWalletRequest): Observable<Wallet> {
    return this.http.post<ApiResponse<Wallet>>(this.apiUrl, request).pipe(
      map(res => {
        if (!res.success || !res.data) {
          throw new Error(res.error || res.message || 'Cüzdan oluşturulamadı.');
        }
        return res.data;
      }),
      tap(newWallet => {
        newWallet.balance = 0;
        newWallet.blockedBalance = 0;
        this.wallets.update(list => [newWallet, ...list]);
        if (!this.selectedWallet()) {
          this.selectedWallet.set(newWallet);
        }
      })
    );
  }

  freezeWallet(walletId: string, reason: string): Observable<boolean> {
    return this.http.post<ApiResponse<boolean>>(`${this.apiUrl}/${walletId}/freeze`, { reason } as FreezeWalletRequest).pipe(
      map(res => res.success),
      tap(() => {
        this.wallets.update(list =>
          list.map(w => (w.id === walletId ? { ...w, status: 3 } : w))
        );
        if (this.selectedWallet()?.id === walletId) {
          this.selectedWallet.update(w => (w ? { ...w, status: 3 } : null));
        }
      })
    );
  }

  unfreezeWallet(walletId: string): Observable<boolean> {
    return this.http.post<ApiResponse<boolean>>(`${this.apiUrl}/${walletId}/unfreeze`, {}).pipe(
      map(res => res.success),
      tap(() => {
        this.wallets.update(list =>
          list.map(w => (w.id === walletId ? { ...w, status: 1 } : w))
        );
        if (this.selectedWallet()?.id === walletId) {
          this.selectedWallet.update(w => (w ? { ...w, status: 1 } : null));
        }
      })
    );
  }

  updateLimits(walletId: string, dailyLimit: number, monthlyLimit: number): Observable<boolean> {
    const payload: UpdateLimitsRequest = { dailyLimit, monthlyLimit };
    return this.http.put<ApiResponse<boolean>>(`${this.apiUrl}/${walletId}/limits`, payload).pipe(
      map(res => res.success),
      tap(() => {
        this.wallets.update(list =>
          list.map(w => (w.id === walletId ? { ...w, dailyLimit, monthlyLimit } : w))
        );
      })
    );
  }

  selectWallet(wallet: Wallet): void {
    this.selectedWallet.set(wallet);
  }

  enrichWithBalances(walletsList: Wallet[]): void {
    if (!walletsList.length) return;

    walletsList.forEach(w => {
      this.http.get<AccountBalanceDto>(`${this.ledgerApiUrl}/accounts/${w.id}/balance`).pipe(
        catchError(() => of(null))
      ).subscribe(balance => {
        if (balance) {
          this.wallets.update(list =>
            list.map(item =>
              item.id === w.id
                ? { ...item, balance: balance.availableBalance, blockedBalance: balance.blockedBalance }
                : item
            )
          );
          if (this.selectedWallet()?.id === w.id) {
            this.selectedWallet.update(curr =>
              curr ? { ...curr, balance: balance.availableBalance, blockedBalance: balance.blockedBalance } : null
            );
          }
        }
      });
    });
  }
}
