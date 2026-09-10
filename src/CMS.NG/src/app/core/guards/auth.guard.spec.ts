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

import { seedSession } from '@core/services/auth.service.spec';
import { authGuard } from './auth.guard';

/** Runs the guard in an injection context, as the router would. */
function runGuard(url: string): GuardResult {
  return TestBed.runInInjectionContext(() =>
    authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  ) as GuardResult;
}

describe('authGuard', () => {
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    router = TestBed.inject(Router);
  });

  afterEach(() => sessionStorage.clear());

  it('lets a signed-in user through', () => {
    seedSession();

    expect(runGuard('/courses')).toBeTrue();
  });

  it('redirects to /login when there is no token in session storage', () => {
    const result = runGuard('/courses');

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toContain('/login');
  });

  it('carries the requested URL through as returnUrl', () => {
    const result = runGuard('/courses/41');

    expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Fcourses%2F41');
  });

  it('blocks again once the session has been cleared', () => {
    seedSession();
    expect(runGuard('/app-users')).toBeTrue();

    sessionStorage.clear();

    expect(runGuard('/app-users') instanceof UrlTree).toBeTrue();
  });

  it('blocks a stored profile that carries no token', () => {
    sessionStorage.setItem('cms-auth', JSON.stringify({ userId: 'miles', userName: 'Miles Sun' }));

    expect(runGuard('/courses') instanceof UrlTree).toBeTrue();
  });
});
