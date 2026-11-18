import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, map, catchError, of } from 'rxjs';
import { AccountBalanceDto, DepositRequest, LedgerEntryDto } from '../models/ledger.model';

@Injectable({
  providedIn: 'root'
})
export class LedgerService {
  private http = inject(HttpClient);
  private apiUrl = '/api/ledger';

  readonly currentBalance = signal<AccountBalanceDto | null>(null);
  readonly entries = signal<LedgerEntryDto[]>([]);
  readonly isLoading = signal<boolean>(false);

  getBalance(accountId: string): Observable<AccountBalanceDto | null> {
    return this.http.get<AccountBalanceDto>(`${this.apiUrl}/accounts/${accountId}/balance`).pipe(
      tap(balance => this.currentBalance.set(balance)),
      catchError(() => {
        this.currentBalance.set(null);
        return of(null);
      })
    );
  }

  getEntries(accountId: string): Observable<LedgerEntryDto[]> {
    this.isLoading.set(true);
    return this.http.get<LedgerEntryDto[]>(`${this.apiUrl}/accounts/${accountId}/entries`).pipe(
      map(list => (Array.isArray(list) ? list : [])),
      tap(list => {
        // Sort descending by date
        const sorted = [...list].sort(
          (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        this.entries.set(sorted);
        this.isLoading.set(false);
      }),
      catchError(() => {
        this.entries.set([]);
        this.isLoading.set(false);
        return of([]);
      })
    );
  }

  deposit(request: DepositRequest): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/deposit`, request).pipe(
      tap(() => {
        // Refresh balance and entries
        this.getBalance(request.accountId).subscribe();
        this.getEntries(request.accountId).subscribe();
      })
    );
  }
}
