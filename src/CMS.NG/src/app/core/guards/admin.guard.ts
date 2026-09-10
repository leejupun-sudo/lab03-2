import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/**
 * Where a non-Admin is sent instead. 上稿作業 is the 首頁 Home entry and the same page
 * `login.ts` lands on, so the redirect never lands on another guarded route.
 */
const NON_ADMIN_LANDING = '/featured-promo-items';

/**
 * 系統管理 專用路由的守衛 — 沒有 Admin 角色就導回首頁.
 *
 * Applied as `canActivateChild` on the 系統管理 branch of the route tree (使用者／角色／發布狀態),
 * so every child — list, form and detail — is covered, and a route added to that branch tomorrow
 * is covered by construction. It nests inside {@link authGuard}: signed-out users never reach
 * this check, because the outer guard has already sent them to /login.
 *
 * **This is navigation ergonomics, not the security boundary.** Roles here come from decoding the
 * stored JWT in the browser without verifying its signature, so a user who edits their own session
 * storage can walk past it. What actually stops them is `[Authorize(Roles = "Admin")]` on
 * `AppUsersController`, `AppRolesController`, `PublishStatusesController` and the two Admin-only
 * lookup actions: every one of those requests comes back `403` whatever the browser believes.
 *
 * The point of the guard is that the honest case never sees a broken page. Without it, a non-Admin
 * following an old bookmark would load the list page, fire its request, and get an error toast on
 * an empty grid — the API refusing correctly, but the UI having promised something it could not
 * deliver.
 */
export const adminGuard: CanActivateChildFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAdmin() || router.createUrlTree([NON_ADMIN_LANDING]);
};
