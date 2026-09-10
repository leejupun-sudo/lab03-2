import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/**
 * 沒有 token 就不讓進 — 導向登入頁.
 *
 * Applied once, as `canActivateChild` on the pathless parent that wraps every app route, so a
 * new feature route is covered the moment it is added. `/login` sits outside that parent and
 * stays public.
 *
 * The requested URL rides along as `returnUrl`, so signing in lands the user where they were
 * headed instead of on the default page.
 *
 * This is navigation ergonomics, not a security boundary: the API rejects an unauthenticated
 * request whatever the browser believes.
 */
export const authGuard: CanActivateChildFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.token()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
