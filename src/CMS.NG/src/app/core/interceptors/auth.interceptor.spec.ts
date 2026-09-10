import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { environment } from '@env';

import { AUTH_STORAGE_KEY } from '@core/services/auth.service';
import { makeProfile, seedSession } from '@core/services/auth.service.spec';
import { authInterceptor } from './auth.interceptor';

const API_URL = `${environment.apiBaseUrl}/app-roles`;
const LOGIN_URL = `${environment.apiBaseUrl}/Auth/login`;

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    sessionStorage.clear();
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: router },
        MessageService,
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // ---------- Attaching the token ----------

  it('attaches the session-storage token as an Authorization: Bearer header', () => {
    const profile = seedSession();

    http.get(API_URL).subscribe();

    const req = httpMock.expectOne(API_URL);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${profile.accessToken}`);
    req.flush([]);
  });

  it('attaches the header on writes as well as reads', () => {
    const profile = seedSession();

    http.post(API_URL, { roleId: 'Editor' }).subscribe();

    const req = httpMock.expectOne(API_URL);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${profile.accessToken}`);
    req.flush({});
  });

  it('sends no Authorization header when there is no token', () => {
    http.get(API_URL).subscribe();

    const req = httpMock.expectOne(API_URL);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });

  it('reads the token per request, so a token written after startup is still used', () => {
    http.get(API_URL).subscribe();
    httpMock.expectOne(API_URL).flush([]);

    const profile = seedSession(makeProfile({ accessToken: 'a-later-token' }));

    http.get(API_URL).subscribe();
    const second = httpMock.expectOne(API_URL);
    expect(second.request.headers.get('Authorization')).toBe(`Bearer ${profile.accessToken}`);
    second.flush([]);
  });

  it('does not attach the token to requests outside the API', () => {
    seedSession();

    http.get('https://www.uuu.com.tw/course/PLF').subscribe();

    const req = httpMock.expectOne('https://www.uuu.com.tw/course/PLF');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  // ---------- Handling a 401 ----------

  it('a 401 clears session storage and redirects to the login page', () => {
    seedSession();
    sessionStorage.setItem('course-list-filters', '{"keyword":"Azure"}');

    http.get(API_URL).subscribe({ error: () => {} });
    httpMock.expectOne(API_URL).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.length).toBe(0);
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('a late 401 for an old token leaves a session signed in since then alone', () => {
    // The orphaned-request race: a call goes out under one token, the user is signed out and
    // signs back in, and only then does the first call fail. Clearing here would delete the
    // token the user just obtained and bounce them off the page they just reached.
    const stale = makeProfile({ accessToken: 'stale-token' });
    seedSession(stale);

    http.get(API_URL).subscribe({ error: () => {} });
    const inFlight = httpMock.expectOne(API_URL);
    expect(inFlight.request.headers.get('Authorization')).toBe('Bearer stale-token');

    const fresh = makeProfile({ accessToken: 'fresh-token' });
    seedSession(fresh);

    inFlight.flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBe(JSON.stringify(fresh));
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('a 401 still surfaces the error to the caller', () => {
    seedSession();
    let status: number | undefined;

    http.get(API_URL).subscribe({ error: (error: { status: number }) => (status = error.status) });
    httpMock.expectOne(API_URL).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
  });

  it('leaves the session alone for other error statuses', () => {
    seedSession();

    http.get(API_URL).subscribe({ error: () => {} });
    httpMock.expectOne(API_URL).flush(null, { status: 409, statusText: 'Conflict' });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('does not redirect on the login endpoint 401 — that is a wrong password, not a dead session', () => {
    http.post(LOGIN_URL, { userId: 'miles@uuu.com.tw', password: 'wrong' }).subscribe({ error: () => {} });
    httpMock.expectOne(LOGIN_URL).flush(
      { title: '登入失敗', detail: '帳號或密碼錯誤。' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(router.navigate).not.toHaveBeenCalled();
  });

  // ---------- Handling a 403 ----------
  //
  // 403 means the token and the account are both fine — this caller may just not do this. That is
  // a different situation from 401 and must be handled differently: clearing the session would
  // tell the user to sign in again for a problem signing in again cannot fix.

  it('a 403 keeps the session and does not redirect', () => {
    seedSession();

    http.get(API_URL).subscribe({ error: () => {} });
    httpMock.expectOne(API_URL).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('a 403 shows the 權限不足 toast', () => {
    seedSession();
    const messageService = TestBed.inject(MessageService);
    const add = spyOn(messageService, 'add');

    http.get(API_URL).subscribe({ error: () => {} });
    httpMock.expectOne(API_URL).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(add).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({ severity: 'warn', summary: '權限不足' }),
    );
  });

  /**
   * Found by the ship-workflow coverage audit. A 403 from somewhere that is not this API — the
   * public site the QR codes point at, say — is not this app's permission problem, and the token
   * was never attached to that request in the first place. No toast, no redirect.
   */
  it('leaves a 403 from outside the API alone', () => {
    seedSession();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    http.get('https://public.example.com/thing').subscribe({ error: () => {} });
    httpMock
      .expectOne('https://public.example.com/thing')
      .flush(null, { status: 403, statusText: 'Forbidden' });

    expect(add).not.toHaveBeenCalled();
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  /**
   * And a 401 from outside the API must not end the session either — a dead session is something
   * only this API can pronounce on.
   */
  it('leaves a 401 from outside the API alone', () => {
    seedSession();

    http.get('https://public.example.com/thing').subscribe({ error: () => {} });
    httpMock
      .expectOne('https://public.example.com/thing')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('a 403 still surfaces the error to the caller', () => {
    seedSession();
    let status: number | undefined;

    http.get(API_URL).subscribe({ error: (error: { status: number }) => (status = error.status) });
    httpMock.expectOne(API_URL).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
  });
});
