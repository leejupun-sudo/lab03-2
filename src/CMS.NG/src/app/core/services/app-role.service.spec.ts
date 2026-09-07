import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { AppRole, AppRoleRequest } from '@core/models/app-role.model';
import { AppRoleService } from './app-role.service';

const BASE_URL = `${environment.apiBaseUrl}/app-roles`;

function makeRole(overrides: Partial<AppRole> = {}): AppRole {
  return {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 3,
    userIds: ['helen', 'Jenny_Tsao', 'miles@uuu.com.tw'],
    ...overrides,
  };
}

describe('AppRoleService', () => {
  let service: AppRoleService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppRoleService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AppRoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the app-roles collection', () => {
    const expected = [makeRole()];
    let actual: AppRole[] | undefined;

    service.getAll().subscribe((roles) => (actual = roles));

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('query() POSTs the filter object to /query', () => {
    const expected = [makeRole({ roleId: 'User', roleName: 'User', permissionLevel: 100 })];
    let actual: AppRole[] | undefined;

    service
      .query({ keyword: 'user', permissionLevelFrom: 50, permissionLevelTo: 200 })
      .subscribe((roles) => (actual = roles));

    const req = httpMock.expectOne(`${BASE_URL}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      keyword: 'user',
      permissionLevelFrom: 50,
      permissionLevelTo: 200,
    });
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('getById() issues GET with the pkid in the route', () => {
    let actual: AppRole | undefined;

    service.getById(7).subscribe((role) => (actual = role));

    const req = httpMock.expectOne(`${BASE_URL}/7`);
    expect(req.request.method).toBe('GET');
    req.flush(makeRole({ pkid: 7 }));

    expect(actual?.pkid).toBe(7);
  });

  it('create() POSTs the request body to the collection', () => {
    const request: AppRoleRequest = {
      pkid: 0,
      roleId: 'Editor',
      roleName: 'Content Editor',
      permissionLevel: 50,
      description: '內容編輯',
      userIds: ['helen'],
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(makeRole({ pkid: 3, roleId: 'Editor' }));
  });

  it('update() PUTs to the collection with the pkid in the body (no route param)', () => {
    const request: AppRoleRequest = {
      pkid: 2,
      roleId: 'User',
      roleName: 'General User',
      permissionLevel: 200,
      description: null,
      userIds: [],
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(makeRole({ pkid: 2, roleId: 'User' }));
  });

  it('delete() issues DELETE with the pkid in the route', () => {
    service.delete(2).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('surfaces server errors to the caller', () => {
    let status: number | undefined;

    service.getById(999).subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne(`${BASE_URL}/999`).flush('not found', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });
});
