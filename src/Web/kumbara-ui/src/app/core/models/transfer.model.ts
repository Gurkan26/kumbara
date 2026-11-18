export interface InitiateTransferRequest {
  sourceAccountId: string;
  destinationAccountId: string;
  amount: number;
  currency: string;
  description?: string;
}

export interface TransferDto {
  transferId: string;
  state: string;
  sourceAccountId: string;
  destinationAccountId: string;
  amount: number;
  currency: string;
  failureReason?: string;
  createdAt: string;
  completedAt?: string;
}

export type SagaStepStatus = 'idle' | 'in_progress' | 'completed' | 'failed';

export interface SagaStep {
  stepNumber: number;
  key: 'risk' | 'hold' | 'commit' | 'completed';
  title: string;
  description: string;
  status: SagaStepStatus;
  detail?: string;
}
