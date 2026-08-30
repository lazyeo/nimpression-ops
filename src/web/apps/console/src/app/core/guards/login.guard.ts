import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export const loginGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    const role = authService.userRole();
    if (role === 'Driver') {
      return router.createUrlTree(['/driver']);
    }
    return router.createUrlTree(['/admin']);
  }

  return true;
};
