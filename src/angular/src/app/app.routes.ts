import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/overview',
    pathMatch: 'full',
  },
  {
    path: 'overview',
    loadComponent: () => import('./overview/overview').then((m) => m.Overview),
  },
  {
    path: 'lobby/:id',
    loadComponent: () => import('./lobby/lobby').then((m) => m.Lobby),
  },
];
