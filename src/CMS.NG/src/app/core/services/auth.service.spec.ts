import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { AuthProfile } from '@core/models/auth.model';
import { AUTH_STORAGE_KEY, AuthService } from './auth.service';

const LOGIN_URL = `${environment.apiBaseUrl}/Auth/login`;
const PROFILE_URL = `${environment.apiBaseUrl}/Auth/profile`;

/**
 * Builds a JWS whose payload carries the given claims.
 *
 * The signature is a placeholder — nothing in the browser verifies it, and nothing should: the
 * app reads claims for the menu only, the API checks the signature for everything that matters.
 */
export function makeToken(claims: Record<string, unknown> = {}): string {
  const encode = (value: unknown) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

  return [
    encode({ alg: 'HS256', typ: 'JWT' }),
    encode({ sub: 'miles@uuu.com.tw', userId: 'miles@uuu.com.tw', userName: 'Miles Sun', ...claims }),
    'not-a-real-signature',
  ].join('.');
}

/** A stored profile, ready to drop into session storage. */
export function makeProfile(overrides: Partial<AuthProfile> = {}): AuthProfile {
  return {
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    accessToken: makeToken({ role: ['Admin', 'User'] }),
    ...overrides,
  };
}

/** Signs a profile into session storage, as a completed login would. */
export function seedSession(profile: AuthProfile = makeProfile()): AuthProfile {
  sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(profile));
  return profile;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('starts signed out when session storage is empty', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token()).toBeNull();
    expect(service.roles()).toEqual([]);
  });

  it('login() POSTs { userId, password } to /Auth/login', () => {
    service.login({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' }).subscribe();

    const req = httpMock.expectOne(LOGIN_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' });
    req.flush(makeProfile());
  });

  it('login() stores the profile in SESSION storage, not local storage', () => {
    const profile = makeProfile();

    service.login({ userId: profile.userId, password: 'CMS4fun#' }).subscribe();
    httpMock.expectOne(LOGIN_URL).flush(profile);

    expect(JSON.parse(sessionStorage.getItem(AUTH_STORAGE_KEY) ?? 'null')).toEqual(profile);
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(localStorage.length).toBe(0);
  });

  it('exposes the signed-in profile after login', () => {
    const profile = makeProfile();

    service.login({ userId: profile.userId, password: 'CMS4fun#' }).subscribe();
    httpMock.expectOne(LOGIN_URL).flush(profile);

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.userId()).toBe(profile.userId);
    expect(service.userName()).toBe('Miles Sun');
    expect(service.token()).toBe(profile.accessToken);
  });

  it('a failed login stores nothing', () => {
    service.login({ userId: 'miles@uuu.com.tw', password: 'wrong' }).subscribe({ error: () => {} });
    httpMock.expectOne(LOGIN_URL).flush(
      { title: '登入失敗', detail: '帳號或密碼錯誤。' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  // ---------- Roles come out of the token, with no extra request ----------

  it('reads the role claims from the stored token', () => {
    seedSession(makeProfile({ accessToken: makeToken({ role: ['Admin', 'User'] }) }));

    expect(service.roles()).toEqual(['Admin', 'User']);
    expect(service.isAdmin()).toBeTrue();
    httpMock.expectNone(() => true);
  });

  it('accepts a single role claim written as a bare string', () => {
    seedSession(makeProfile({ accessToken: makeToken({ role: 'Admin' }) }));

    expect(service.roles()).toEqual(['Admin']);
    expect(service.isAdmin()).toBeTrue();
  });

  it('a token with no role claim yields no roles', () => {
    seedSession(makeProfile({ accessToken: makeToken({}) }));

    expect(service.roles()).toEqual([]);
    expect(service.isAdmin()).toBeFalse();
  });

  it('a non-Admin token is not an Admin', () => {
    seedSession(makeProfile({ accessToken: makeToken({ role: ['User'] }) }));

    expect(service.isAdmin()).toBeFalse();
  });

  it('matches role names case-insensitively, as the CI collation does', () => {
    seedSession(makeProfile({ accessToken: makeToken({ role: ['admin'] }) }));

    expect(service.isAdmin()).toBeTrue();
  });

  it('decodes a UTF-8 userName out of the token payload', () => {
    seedSession(makeProfile({ accessToken: makeToken({ userName: '孫小明', role: [] }) }));

    expect(service.token()).toBeTruthy();
    expect(service.roles()).toEqual([]);
  });

  it('treats a malformed token as signed in with no roles rather than throwing', () => {
    seedSession(makeProfile({ accessToken: 'not-a-jwt' }));

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.roles()).toEqual([]);
  });

  it('treats unparsable stored JSON as signed out', () => {
    sessionStorage.setItem(AUTH_STORAGE_KEY, '{ not json');

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token()).toBeNull();
  });

  it('treats a stored profile with no accessToken as signed out', () => {
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ userId: 'x', userName: 'X' }));

    expect(service.isAuthenticated()).toBeFalse();
  });

  // ---------- updateProfile ----------

  it('updateProfile() PUTs only { userName } to /Auth/profile', () => {
    seedSession();

    service.updateProfile({ userName: '孫小明' }).subscribe();

    const request = httpMock.expectOne(PROFILE_URL);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ userName: '孫小明' });
    request.flush({ userId: 'miles@uuu.com.tw', userName: '孫小明' });
  });

  it('updateProfile() replaces the stored userName, keeping the account and the token', () => {
    const seeded = seedSession();

    service.updateProfile({ userName: '孫小明' }).subscribe();
    httpMock.expectOne(PROFILE_URL).flush({ userId: seeded.userId, userName: '孫小明' });

    expect(service.userName()).toBe('孫小明');
    expect(service.userId()).toBe(seeded.userId);
    // Renaming does not re-issue the token — its userName claim goes stale and nothing reads it.
    expect(service.token()).toBe(seeded.accessToken);
    expect(service.roles()).toEqual(['Admin', 'User']);
  });

  it('updateProfile() stores nothing in local storage', () => {
    seedSession();

    service.updateProfile({ userName: '孫小明' }).subscribe();
    httpMock.expectOne(PROFILE_URL).flush({ userId: 'miles@uuu.com.tw', userName: '孫小明' });

    expect(localStorage.length).toBe(0);
  });

  it('a failed updateProfile() leaves the stored profile untouched', () => {
    seedSession(makeProfile({ userName: 'Miles Sun' }));

    service.updateProfile({ userName: '孫小明' }).subscribe({ error: () => undefined });
    httpMock.expectOne(PROFILE_URL).flush(null, { status: 500, statusText: 'Server Error' });

    expect(service.userName()).toBe('Miles Sun');
  });

  it('updateProfile() stores nothing when nobody is signed in', () => {
    service.updateProfile({ userName: '孫小明' }).subscribe();
    httpMock.expectOne(PROFILE_URL).flush({ userId: 'miles@uuu.com.tw', userName: '孫小明' });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  // ---------- Logout ----------

  it('logout() clears the whole of session storage', () => {
    seedSession();
    sessionStorage.setItem('course-list-filters', '{"keyword":"Azure"}');

    service.logout();

    expect(sessionStorage.length).toBe(0);
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token()).toBeNull();
    expect(service.roles()).toEqual([]);
  });

  it('clearSession() drops the signed-in state', () => {
    seedSession();
    expect(service.isAuthenticated()).toBeTrue();

    service.clearSession();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.userName()).toBe('');
  });
});
