import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: string;
  type: 'info' | 'success' | 'warning' | 'error';
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  readonly toasts = signal<ToastMessage[]>([]);

  show(message: string, type: 'info' | 'success' | 'warning' | 'error' = 'info', durationMs = 4500): void {
    const id = 'toast-' + Math.random().toString(36).substring(2, 9);
    const toast: ToastMessage = { id, type, message };

    this.toasts.update(list => [...list, toast]);

    setTimeout(() => {
      this.remove(id);
    }, durationMs);
  }

  success(msg: string): void {
    this.show(msg, 'success');
  }

  info(msg: string): void {
    this.show(msg, 'info');
  }

  warning(msg: string): void {
    this.show(msg, 'warning');
  }

  error(msg: string): void {
    this.show(msg, 'error', 6000);
  }

  remove(id: string): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}
