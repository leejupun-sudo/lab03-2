import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { AppUserService } from './app-user.service';

const BASE_URL = `${environment.apiBaseUrl}/app-users`;

function makeUser(overrides: Partial<AppUser> = {}): AppUser {
  return {
    pkid: 1,
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    isActive: true,
    passwordUpdatedTime: null,
    roleCount: 2,
    roleIds: ['Admin', 'User'],
    ...overrides,
  };
}

describe('AppUserService', () => {
  let service: AppUserService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppUserService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AppUserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the app-users collection', () => {
    const expected = [makeUser()];
    let actual: AppUser[] | undefined;

    service.getAll().subscribe((users) => (actual = users));

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('query() POSTs the filter object to /query in wire shape', () => {
    const expected = [makeUser({ userId: 'helen', isActive: false })];
    let actual: AppUser[] | undefined;

    service
      .query({
        keyword: 'helen',
        isActive: false,
        roleId: 'User',
        passwordUpdatedFrom: '2026-03-01',
        passwordUpdatedTo: '2026-03-31',
      })
      .subscribe((users) => (actual = users));

    const req = httpMock.expectOne(`${BASE_URL}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      keyword: 'helen',
      isActive: false,
      roleId: 'User',
      passwordUpdatedFrom: '2026-03-01',
      passwordUpdatedTo: '2026-03-31',
    });
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('getById() issues GET with the pkid in the route', () => {
    let actual: AppUser | undefined;

    service.getById(7).subscribe((user) => (actual = user));

    const req = httpMock.expectOne(`${BASE_URL}/7`);
    expect(req.request.method).toBe('GET');
    req.flush(makeUser({ pkid: 7 }));

    expect(actual?.pkid).toBe(7);
  });

  it('create() POSTs the request body to the collection, with no password field', () => {
    const request: AppUserRequest = {
      pkid: 0,
      userId: 'bob',
      userName: 'Bob Chen',
      isActive: true,
      roleIds: ['User'],
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect(Object.keys(req.request.body as object)).not.toContain('passwordHash');
    req.flush(makeUser({ pkid: 3, userId: 'bob' }));
  });

  it('update() PUTs to the collection with the pkid in the body (no route param)', () => {
    const request: AppUserRequest = {
      pkid: 2,
      userId: 'helen',
      userName: 'Helen',
      isActive: false,
      roleIds: [],
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(makeUser({ pkid: 2, userId: 'helen' }));
  });

  it('delete() issues DELETE with the pkid in the route', () => {
    service.delete(2).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('resetPassword() POSTs to /{pkid}/reset-password with no body', () => {
    let actual: AppUser | undefined;

    service.resetPassword(2).subscribe((user) => (actual = user));

    const req = httpMock.expectOne(`${BASE_URL}/2/reset-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(makeUser({ pkid: 2, passwordUpdatedTime: null }));

    expect(actual?.pkid).toBe(2);
  });

  it('surfaces server errors to the caller', () => {
    let status: number | undefined;

    service.getById(999).subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne(`${BASE_URL}/999`).flush('not found', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });
});
