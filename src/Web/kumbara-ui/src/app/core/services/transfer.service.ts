import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, tap, catchError, switchMap, timer, takeWhile, of, map } from 'rxjs';
import { InitiateTransferRequest, SagaStep, TransferDto } from '../models/transfer.model';

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

const INITIAL_SAGA_STEPS: SagaStep[] = [
  {
    stepNumber: 1,
    key: 'risk',
    title: '1. Risk Analizi',
    description: 'Redis velocity ve limit kontrolü yapılıyor...',
    status: 'idle'
  },
  {
    stepNumber: 2,
    key: 'hold',
    title: '2. Bakiye Blokesi',
    description: 'Ledger üzerinde kaynak hesap tutarı bloke ediliyor...',
    status: 'idle'
  },
  {
    stepNumber: 3,
    key: 'commit',
    title: '3. Çift Taraflı Kayıt',
    description: 'Debit ve Credit kayıtları işleniyor...',
    status: 'idle'
  },
  {
    stepNumber: 4,
    key: 'completed',
    title: '4. Sonuçlandırma',
    description: 'Transfer başarıyla onaylandı ve bitti.',
    status: 'idle'
  }
];

@Injectable({
  providedIn: 'root'
})
export class TransferService {
  private http = inject(HttpClient);
  private apiUrl = '/api/transfers';

  readonly currentTransfer = signal<TransferDto | null>(null);
  readonly sagaSteps = signal<SagaStep[]>(JSON.parse(JSON.stringify(INITIAL_SAGA_STEPS)));
  readonly isTransferring = signal<boolean>(false);
  readonly lastError = signal<string | null>(null);

  resetSaga(): void {
    this.currentTransfer.set(null);
    this.sagaSteps.set(JSON.parse(JSON.stringify(INITIAL_SAGA_STEPS)));
    this.isTransferring.set(false);
    this.lastError.set(null);
  }

  initiateTransfer(req: InitiateTransferRequest, customIdempotencyKey?: string): Observable<any> {
    this.resetSaga();
    this.isTransferring.set(true);

    const idempotencyKey = customIdempotencyKey || generateUUID();
    const headers = new HttpHeaders({
      'Idempotency-Key': idempotencyKey,
      'Content-Type': 'application/json'
    });

    // Mark step 1 in progress
    this.updateStepStatus('risk', 'in_progress', 'Risk kuralları kontrol ediliyor...');

    return this.http.post<any>(this.apiUrl, req, { headers }).pipe(
      tap(res => {
        const transferId = res?.transferId;
        if (transferId) {
          this.currentTransfer.set({
            transferId,
            state: res.status || 'Initiated',
            sourceAccountId: req.sourceAccountId,
            destinationAccountId: req.destinationAccountId,
            amount: req.amount,
            currency: req.currency,
            createdAt: new Date().toISOString()
          });
          this.pollTransferStatus(transferId);
        }
      }),
      catchError(err => {
        this.isTransferring.set(false);
        const errMsg = err?.error?.message || err?.message || 'Transfer başlatılamadı.';
        this.lastError.set(errMsg);
        this.updateStepStatus('risk', 'failed', errMsg);
        throw err;
      })
    );
  }

  getTransferById(id: string): Observable<TransferDto> {
    return this.http.get<TransferDto>(`${this.apiUrl}/${id}`);
  }

  pollTransferStatus(transferId: string): void {
    timer(500, 1000).pipe(
      switchMap(() => this.getTransferById(transferId).pipe(catchError(() => of(null)))),
      takeWhile(dto => {
        if (!dto) return true;
        this.applySagaState(dto);
        const isDone = dto.state === 'Completed' || dto.state === 'Failed';
        return !isDone;
      }, true)
    ).subscribe({
      next: dto => {
        if (dto) {
          this.currentTransfer.set(dto);
          this.applySagaState(dto);
          if (dto.state === 'Completed' || dto.state === 'Failed') {
            this.isTransferring.set(false);
          }
        }
      },
      error: () => {
        this.isTransferring.set(false);
      }
    });
  }

  private applySagaState(dto: TransferDto): void {
    const s = dto.state;

    if (s === 'Initiated' || s === 'AwaitingRisk') {
      this.updateStepStatus('risk', 'in_progress', 'Risk değerlendirmesi yapılıyor...');
    } else if (s === 'AwaitingHold') {
      this.updateStepStatus('risk', 'completed', 'Risk onaylandı.');
      this.updateStepStatus('hold', 'in_progress', 'Ledger blokesi oluşturuluyor...');
    } else if (s === 'AwaitingCommit') {
      this.updateStepStatus('risk', 'completed', 'Risk onaylandı.');
      this.updateStepStatus('hold', 'completed', 'Tutar rezerve edildi.');
      this.updateStepStatus('commit', 'in_progress', 'Ledger transferi commit ediliyor...');
    } else if (s === 'Completed') {
      this.updateStepStatus('risk', 'completed', 'Risk onaylandı.');
      this.updateStepStatus('hold', 'completed', 'Tutar rezerve edildi.');
      this.updateStepStatus('commit', 'completed', 'Ledger çift-taraflı yazıldı.');
      this.updateStepStatus('completed', 'completed', 'Transfer başarıyla tamamlandı!');
    } else if (s === 'Failed') {
      const reason = dto.failureReason || 'İşlem kurallara uymadığı için iptal edildi.';
      this.lastError.set(reason);
      // Determine which step failed
      const current = this.sagaSteps();
      const inProg = current.find(x => x.status === 'in_progress');
      if (inProg) {
        this.updateStepStatus(inProg.key, 'failed', reason);
      } else {
        this.updateStepStatus('risk', 'failed', reason);
      }
    }
  }

  private updateStepStatus(key: 'risk' | 'hold' | 'commit' | 'completed', status: 'idle' | 'in_progress' | 'completed' | 'failed', detail?: string): void {
    this.sagaSteps.update(steps =>
      steps.map(s => {
        if (s.key === key) {
          return { ...s, status, detail: detail || s.detail };
        }
        return s;
      })
    );
  }
}
