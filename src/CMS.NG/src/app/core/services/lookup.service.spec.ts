import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { AppRoleLookup } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user.model';
import { LookupService } from './lookup.service';

describe('LookupService', () => {
  let service: LookupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [LookupService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(LookupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAppUsers() issues GET to /lookups/app-users', () => {
    const expected: AppUserLookup[] = [
      { userId: 'helen', userName: 'helen', isActive: true, label: 'helen (helen)' },
    ];
    let actual: AppUserLookup[] | undefined;

    service.getAppUsers().subscribe((users) => (actual = users));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/app-users`);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('getAppRoles() issues GET to /lookups/app-roles', () => {
    const expected: AppRoleLookup[] = [
      { pkid: 1, roleId: 'Admin', roleName: 'Administrator', label: 'Administrator (Admin)' },
    ];
    let actual: AppRoleLookup[] | undefined;

    service.getAppRoles().subscribe((roles) => (actual = roles));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/app-roles`);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('getCertifications() issues GET to /lookups/certifications', () => {
    service.getCertifications().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/certifications`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('getJobCategories() issues GET to /lookups/job-categories', () => {
    service.getJobCategories().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/job-categories`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('getCourses() issues GET to /lookups/courses', () => {
    service.getCourses().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/courses`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });
});
