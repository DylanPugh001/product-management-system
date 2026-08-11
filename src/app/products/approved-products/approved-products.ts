import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProductService } from '../product.service';
import { ApprovedProduct } from '../product.models';

@Component({
  selector: 'app-approved-products',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './approved-products.html',
  styleUrl: './approved-products.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApprovedProductsComponent implements OnInit {
  private readonly productService = inject(ProductService);

  readonly products = signal<ApprovedProduct[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.productService.getApproved().subscribe({
      next: (products) => {
        this.products.set(products);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load approved products.');
      },
    });
  }
}
