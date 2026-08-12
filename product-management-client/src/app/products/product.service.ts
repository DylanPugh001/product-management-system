import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApprovedProduct, Product, ProductPayload } from './product.models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);

  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>(`${environment.apiBaseUrl}/api/products`);
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`${environment.apiBaseUrl}/api/products/${id}`);
  }

  create(payload: ProductPayload): Observable<Product> {
    return this.http.post<Product>(`${environment.apiBaseUrl}/api/products`, payload);
  }

  update(id: number, payload: ProductPayload): Observable<Product> {
    return this.http.put<Product>(`${environment.apiBaseUrl}/api/products/${id}`, payload);
  }

  approve(id: number): Observable<Product> {
    return this.http.post<Product>(`${environment.apiBaseUrl}/api/products/${id}/approve`, {});
  }

  reject(id: number, reason?: string): Observable<Product> {
    return this.http.post<Product>(`${environment.apiBaseUrl}/api/products/${id}/reject`, { reason });
  }

  softDelete(id: number): Observable<Product> {
    return this.http.delete<Product>(`${environment.apiBaseUrl}/api/products/${id}`);
  }

  getApproved(): Observable<ApprovedProduct[]> {
    return this.http.get<ApprovedProduct[]>(`${environment.apiBaseUrl}/api/products/approved`);
  }
}
