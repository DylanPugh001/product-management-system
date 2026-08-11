import { Routes } from '@angular/router';
import { LoginComponent } from './login/login';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'products', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  {
    path: 'products',
    canActivate: [authGuard],
    loadChildren: () => import('./products/products.routes').then((m) => m.productsRoutes),
  },
  { path: '**', redirectTo: 'products' },
];
