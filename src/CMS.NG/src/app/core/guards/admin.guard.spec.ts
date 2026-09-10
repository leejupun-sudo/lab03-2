import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  GuardResult,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';

import { makeProfile, makeToken } from '@core/services/auth.service.spec';
import { AUTH_STORAGE_KEY } from '@core/services/auth.service';
import { adminGuard } from './admin.guard';

/** Runs the guard in an injection context, as the router would. */
function runGuard(url: string): GuardResult {
  return TestBed.runInInjectionContext(() =>
    adminGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  ) as GuardResult;
}

/** Seeds a session whose token carries exactly these role claims. */
function seedRoles(roles: string[]): void {
  sessionStorage.setItem(
    AUTH_STORAGE_KEY,
    JSON.stringify(makeProfile({ accessToken: makeToken({ role: roles }) })),
  );
}

describe('adminGuard', () => {
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    router = TestBed.inject(Router);
  });

  afterEach(() => sessionStorage.clear());

  it('lets an Admin through', () => {
    seedRoles(['Admin', 'User']);

    expect(runGuard('/app-users')).toBeTrue();
  });

  it('redirects a user who holds only User', () => {
    seedRoles(['User']);

    const result = runGuard('/app-users');

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/featured-promo-items');
  });

  it('redirects a token that carries no roles at all', () => {
    seedRoles([]);

    expect(runGuard('/app-roles') instanceof UrlTree).toBeTrue();
  });

  /** The role name matches case-insensitively, as `AppRole.RoleId`'s CI collation does. */
  it('accepts a differently-cased Admin claim', () => {
    seedRoles(['admin']);

    expect(runGuard('/publish-statuses')).toBeTrue();
  });

  /**
   * Signed out, the outer authGuard has already redirected to /login, so this guard never runs
   * in practice. It still must not throw or accidentally allow.
   */
  it('redirects when there is no session at all', () => {
    expect(runGuard('/app-users') instanceof UrlTree).toBeTrue();
  });

  it('sends the redirect somewhere a non-Admin can actually open', () => {
    seedRoles(['User']);

    const target = router.serializeUrl(runGuard('/app-users') as UrlTree);

    expect(target).not.toContain('app-users');
    expect(target).not.toContain('app-roles');
    expect(target).not.toContain('publish-statuses');
  });
});
