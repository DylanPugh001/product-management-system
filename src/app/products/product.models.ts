export const enum ProductStatus {
  Draft = 0,
  PendingApproval = 1,
  Approved = 2,
  SoftDeleted = 3,
}

export interface HistoryEntry {
  id: number;
  action: string;
  actorName: string;
  timestamp: string;
  note: string | null;
}

export interface Product {
  id: number;
  name: string;
  description: string | null;
  price: number;
  stock: number;
  status: ProductStatus;
  createdBy: string;
  updatedBy: string;
  createdAt: string;
  updatedAt: string;
  pendingDelete: boolean;
  history: HistoryEntry[];
}

export interface ApprovedProduct {
  id: number;
  productId: number;
  name: string;
  description: string | null;
  price: number;
  stock: number;
  approvedAt: string;
  approvedBy: string;
}

export interface ProductPayload {
  name: string;
  description: string | null;
  price: number;
  stock: number;
}

export const STATUS_LABELS: Record<ProductStatus, string> = {
  [ProductStatus.Draft]: 'Draft',
  [ProductStatus.PendingApproval]: 'Pending Approval',
  [ProductStatus.Approved]: 'Approved',
  [ProductStatus.SoftDeleted]: 'Soft Deleted',
};
