import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProductService } from '../product.service';
import { Product } from '../product.models';

@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule],
  templateUrl: './product-form.html',
  styleUrl: './product-form.css',
})
export class ProductFormComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);

  readonly isEdit = signal(false);
  readonly productId = signal<number | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly forbidden = signal(false);

  readonly form = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(200)]),
    description: new FormControl<string | null>(null),
    price: new FormControl<number>(0, [Validators.required, Validators.min(0)]),
    stock: new FormControl<number>(0, [Validators.required, Validators.min(0)]),
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEdit.set(true);
      this.productId.set(Number(idParam));
      this.loading.set(true);
      this.productService.getById(Number(idParam)).subscribe({
        next: (product: Product) => {
          this.form.patchValue({
            name: product.name,
            description: product.description,
            price: product.price,
            stock: product.stock,
          });
          this.loading.set(false);
        },
        error: (err) => {
          this.loading.set(false);
          if (err.status === 403) {
            this.forbidden.set(true);
          } else {
            this.error.set('Failed to load the product.');
          }
        },
      });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const raw = this.form.getRawValue();
    const payload = {
      name: raw.name!,
      description: raw.description,
      price: raw.price ?? 0,
      stock: raw.stock ?? 0,
    };

    const request = this.isEdit()
      ? this.productService.update(this.productId()!, payload)
      : this.productService.create(payload);

    request.subscribe({
      next: (product) => {
        this.router.navigate(['/products']);
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Failed to save the product.');
      },
    });
  }
}
