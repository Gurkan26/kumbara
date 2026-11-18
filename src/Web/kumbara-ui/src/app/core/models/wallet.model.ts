export enum WalletStatus {
  Active = 1,
  Suspended = 2,
  Frozen = 3,
  Closed = 4
}

export interface Wallet {
  id: string;
  userId: string;
  currency: string;
  status: WalletStatus | string | number;
  dailyLimit: number;
  monthlyLimit: number;
  createdAt: string;
  updatedAt?: string;
  // UI-enriched computed / balance fields:
  balance?: number;
  blockedBalance?: number;
}

export interface CreateWalletRequest {
  userId: string;
  currency: string;
  dailyLimit?: number;
  monthlyLimit?: number;
}

export interface FreezeWalletRequest {
  reason: string;
}

export interface UpdateLimitsRequest {
  dailyLimit: number;
  monthlyLimit: number;
}
