import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { ProductService } from '../product.service';
import { Product, ProductStatus, STATUS_LABELS } from '../product.models';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './product-list.html',
  styleUrl: './product-list.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductListComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly products = signal<Product[]>([]);
  readonly statusFilter = signal<ProductStatus | 'all'>('all');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly expanded = signal<number | null>(null);

  readonly pendingRejectProduct = signal<Product | null>(null);
  readonly rejectReason = signal('');
  readonly pendingDeleteProduct = signal<Product | null>(null);

  readonly statusLabels = STATUS_LABELS;
  readonly statuses = [
    ProductStatus.Draft,
    ProductStatus.PendingApproval,
    ProductStatus.Approved,
    ProductStatus.SoftDeleted,
  ];

  get isManager(): boolean {
    return this.auth.isManager;
  }

  get visibleProducts(): Product[] {
    const filter = this.statusFilter();
    return filter === 'all' ? this.products() : this.products().filter((p) => p.status === filter);
  }

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading.set(true);
    this.error.set(null);
    this.productService.getAll().subscribe({
      next: (products) => {
        this.products.set(products);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load products.');
      },
    });
  }

  canEdit(product: Product): boolean {
    return (
      this.auth.state()?.roles.includes('Capturer') === true &&
      product.createdBy === this.auth.state()?.userId &&
      product.status !== ProductStatus.SoftDeleted
    );
  }

  canApprove(product: Product): boolean {
    return (
      this.auth.isManager &&
      product.status === ProductStatus.PendingApproval &&
      !product.pendingDelete
    );
  }

  canReject(product: Product): boolean {
    return this.auth.isManager && product.status === ProductStatus.PendingApproval;
  }

  canDelete(product: Product): boolean {
    return (
      this.auth.isManager && product.status !== ProductStatus.SoftDeleted && !product.pendingDelete
    );
  }

  canApproveDelete(product: Product): boolean {
    return (
      this.auth.isManager &&
      product.status === ProductStatus.PendingApproval &&
      product.pendingDelete
    );
  }

  approve(product: Product): void {
    this.productService.approve(product.id).subscribe({
      next: (updated) => {
        this.message.set(`Approved "${product.name}".`);
        this.products.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      },
      error: () => this.error.set('Failed to approve the product.'),
    });
  }

  promptReject(product: Product): void {
    this.pendingRejectProduct.set(product);
    this.rejectReason.set('');
  }

  confirmReject(): void {
    const product = this.pendingRejectProduct();
    if (!product) return;
    this.pendingRejectProduct.set(null);
    this.productService.reject(product.id, this.rejectReason() || undefined).subscribe({
      next: (updated) => {
        this.message.set(`Rejected "${product.name}".`);
        this.products.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      },
      error: () => this.error.set('Failed to reject the product.'),
    });
  }

  cancelReject(): void {
    this.pendingRejectProduct.set(null);
  }

  promptDelete(product: Product): void {
    this.pendingDeleteProduct.set(product);
  }

  confirmDelete(): void {
    const product = this.pendingDeleteProduct();
    if (!product) return;
    this.pendingDeleteProduct.set(null);
    this.productService.softDelete(product.id).subscribe({
      next: (updated) => {
        this.message.set(`Soft-delete request for "${product.name}" submitted for approval.`);
        this.products.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      },
      error: () => this.error.set('Failed to request the soft delete.'),
    });
  }

  cancelDelete(): void {
    this.pendingDeleteProduct.set(null);
  }

  toggleHistory(productId: number): void {
    this.expanded.set(this.expanded() === productId ? null : productId);
  }

  goToApproved(): void {
    this.router.navigate(['/products/approved']);
  }

  logout(): void {
    this.auth.logout();
  }

  authState() {
    return this.auth.state();
  }
}
