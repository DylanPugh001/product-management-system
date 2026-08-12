import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const state = auth.state();

  if (!state || new Date(state.expiresAt) <= new Date()) {
    return router.createUrlTree(['/login']);
  }

  return true;
};
