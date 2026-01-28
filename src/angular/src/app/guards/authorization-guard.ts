import { CanActivateFn, Router } from '@angular/router';
import { LoginHubService } from '../services/login-hub-service';
import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, take, switchMap, from, map } from 'rxjs';

export const authorizationGuard: CanActivateFn = (childRoute, state) => {
  const loginHubService = inject(LoginHubService);
  const router = inject(Router);

  return toObservable(loginHubService.connectionEstablished).pipe(
    filter((connected) => connected),
    take(1),
    switchMap(() => from(loginHubService.checkAuthorization())),
    map((isAuth) => (isAuth ? true : router.parseUrl('/login'))),
  );
};
