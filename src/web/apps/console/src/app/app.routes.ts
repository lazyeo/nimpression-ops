import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { loginGuard } from './core/guards/login.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'auth/login',
  },
  {
    path: 'auth',
    children: [
      {
        path: 'login',
        canActivate: [loginGuard],
        loadComponent: () =>
          import('./features/auth/login/login.component').then((m) => m.LoginComponent),
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'login',
      },
    ],
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin', 'Dispatcher'] },
    loadComponent: () =>
      import('./layouts/admin-shell/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'dispatch',
        data: { labelKey: 'NAV.DISPATCH', icon: 'dispatch' },
        loadComponent: () =>
          import('./features/admin/dispatch/dispatch.component').then((m) => m.DispatchComponent),
      },
      {
        path: 'drivers',
        data: { labelKey: 'NAV.DRIVERS', icon: 'drivers' },
        loadComponent: () =>
          import('./features/admin/drivers/drivers.component').then((m) => m.DriversComponent),
      },
      {
        path: 'vehicles',
        data: { labelKey: 'NAV.VEHICLES', icon: 'vehicles' },
        loadComponent: () =>
          import('./features/admin/vehicles/vehicles.component').then((m) => m.VehiclesComponent),
      },
      {
        path: 'areas',
        data: { labelKey: 'NAV.AREAS', icon: 'areas' },
        loadComponent: () =>
          import('./features/admin/areas/areas.component').then((m) => m.AreasComponent),
      },
      {
        path: 'timesheets',
        data: { labelKey: 'NAV.TIMESHEETS', icon: 'timesheets' },
        loadComponent: () =>
          import('./features/admin/placeholder/placeholder.component').then(
            (m) => m.PlaceholderComponent,
          ),
      },
      {
        path: 'payroll',
        data: { labelKey: 'NAV.PAYROLL', icon: 'payroll' },
        loadComponent: () =>
          import('./features/admin/placeholder/placeholder.component').then(
            (m) => m.PlaceholderComponent,
          ),
      },
      {
        path: 'incidents',
        data: { labelKey: 'NAV.INCIDENTS', icon: 'incidents' },
        loadComponent: () =>
          import('./features/admin/placeholder/placeholder.component').then(
            (m) => m.PlaceholderComponent,
          ),
      },
      {
        path: 'fines',
        data: { labelKey: 'NAV.FINES', icon: 'fines' },
        loadComponent: () =>
          import('./features/admin/placeholder/placeholder.component').then(
            (m) => m.PlaceholderComponent,
          ),
      },
      {
        path: 'news',
        data: { labelKey: 'NAV.NEWS', icon: 'news' },
        loadComponent: () =>
          import('./features/admin/placeholder/placeholder.component').then(
            (m) => m.PlaceholderComponent,
          ),
      },
      {
        path: 'notifications',
        data: { labelKey: 'NAV.NOTIFICATIONS', icon: 'notifications' },
        loadComponent: () =>
          import('./features/admin/placeholder/placeholder.component').then(
            (m) => m.PlaceholderComponent,
          ),
      },
      {
        path: 'audit',
        data: { labelKey: 'NAV.AUDIT', icon: 'audit' },
        loadComponent: () =>
          import('./features/admin/placeholder/placeholder.component').then(
            (m) => m.PlaceholderComponent,
          ),
      },
    ],
  },
  {
    path: 'driver',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Driver'] },
    loadComponent: () =>
      import('./layouts/driver-shell/driver-shell.component').then((m) => m.DriverShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'tasks',
      },
      {
        path: 'tasks',
        loadComponent: () =>
          import('./features/driver/tasks/driver-tasks.component').then(
            (m) => m.DriverTasksComponent,
          ),
      },
      {
        path: 'shifts',
        loadComponent: () =>
          import('./features/driver/shifts/driver-shifts.component').then(
            (m) => m.DriverShiftsComponent,
          ),
      },
      {
        path: 'payslips',
        loadComponent: () =>
          import('./features/driver/payslips/driver-payslips.component').then(
            (m) => m.DriverPayslipsComponent,
          ),
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/driver/profile/driver-profile.component').then(
            (m) => m.DriverProfileComponent,
          ),
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
