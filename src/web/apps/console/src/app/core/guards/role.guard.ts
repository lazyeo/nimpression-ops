import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { UserRole } from '../models/auth.models';

export const roleGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return router.createUrlTree(['/auth/login']);
  }

  const expectedRoles = (route.data?.['roles'] as UserRole[] | undefined) || [];
  const userRole = authService.userRole();

  if (!userRole) {
    return router.createUrlTree(['/auth/login']);
  }

  if (expectedRoles.length === 0 || expectedRoles.includes(userRole)) {
    return true;
  }

  // Driver accessing admin routes -> Redirect cleanly to driver shell (W6 requirement: Driver 访问 admin 路由要跳转而非报错)
  if (userRole === 'Driver') {
    return router.createUrlTree(['/driver']);
  }

  // Admin/Dispatcher accessing driver routes -> Redirect cleanly to admin shell
  return router.createUrlTree(['/admin']);
};
