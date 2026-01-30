import { Routes } from '@angular/router';
import { authorizationGuard } from './guards/authorization-guard';
import { notAuthorizedGuard } from './guards/not-authorized-guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/overview',
    pathMatch: 'full',
  },
  {
    path: 'login',
    loadComponent: () => import('./components/login/login').then((m) => m.Login),
    canActivate: [notAuthorizedGuard],
  },
  {
    path: 'overview',
    loadComponent: () => import('./components/overview/overview').then((m) => m.Overview),
    canActivate: [authorizationGuard],
  },
  {
    path: 'lobby/:id',
    loadComponent: () => import('./components/hub/hub').then((m) => m.Hub),
    canActivate: [authorizationGuard],
  },
];
