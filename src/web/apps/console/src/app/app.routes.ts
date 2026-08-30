import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { loginGuard } from './core/guards/login.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [loginGuard],
    children: [],
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
          import('./features/dashboard/dashboard.routes').then(
            (m) => m.DASHBOARD_ROUTES,
          ),
      },
      {
        path: 'dispatch',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'drivers',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'vehicles',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'areas',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'timesheets',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'payroll',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'incidents',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'fines',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'news',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
          ),
      },
      {
        path: 'audit',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent,
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
