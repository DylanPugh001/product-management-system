import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApprovedProduct, Product, ProductPayload } from './product.models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);

  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>('/api/products');
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`/api/products/${id}`);
  }

  create(payload: ProductPayload): Observable<Product> {
    return this.http.post<Product>('/api/products', payload);
  }

  update(id: number, payload: ProductPayload): Observable<Product> {
    return this.http.put<Product>(`/api/products/${id}`, payload);
  }

  approve(id: number): Observable<Product> {
    return this.http.post<Product>(`/api/products/${id}/approve`, {});
  }

  reject(id: number, reason?: string): Observable<Product> {
    return this.http.post<Product>(`/api/products/${id}/reject`, { reason });
  }

  softDelete(id: number): Observable<Product> {
    return this.http.delete<Product>(`/api/products/${id}`);
  }

  getApproved(): Observable<ApprovedProduct[]> {
    return this.http.get<ApprovedProduct[]>('/api/products/approved');
  }
}
