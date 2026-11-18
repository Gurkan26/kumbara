export interface AccountBalanceDto {
  accountId: string;
  currency: string;
  availableBalance: number;
  blockedBalance: number;
  totalBalance: number;
}

export interface LedgerEntryDto {
  id: string;
  transactionId: string;
  accountId: string;
  amount: number;
  type: 'Debit' | 'Credit' | string;
  currency: string;
  description: string;
  createdAt: string;
}

export interface DepositRequest {
  accountId: string;
  amount: number;
  currency: string;
  transactionId?: string;
  description?: string;
}
