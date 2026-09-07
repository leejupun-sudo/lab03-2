import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';
import { CourseGroupService } from './course-group.service';

const BASE_URL = `${environment.apiBaseUrl}/course-groups`;

function makeGroup(overrides: Partial<CourseGroup> = {}): CourseGroup {
  return {
    pkid: 1,
    description: 'Azure系列課程',
    courseCount: 48,
    partnerCourseGroupCount: 2,
    ...overrides,
  };
}

describe('CourseGroupService', () => {
  let service: CourseGroupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CourseGroupService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(CourseGroupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the course-groups collection', () => {
    const expected = [makeGroup()];
    let actual: CourseGroup[] | undefined;

    service.getAll().subscribe((groups) => (actual = groups));

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('query() POSTs the filter object to /query', () => {
    const expected = [makeGroup()];
    let actual: CourseGroup[] | undefined;

    service.query({ keyword: 'Azure' }).subscribe((groups) => (actual = groups));

    const req = httpMock.expectOne(`${BASE_URL}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'Azure' });
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('getById() issues GET with the pkid in the route', () => {
    let actual: CourseGroup | undefined;

    service.getById(7).subscribe((group) => (actual = group));

    const req = httpMock.expectOne(`${BASE_URL}/7`);
    expect(req.request.method).toBe('GET');
    req.flush(makeGroup({ pkid: 7 }));

    expect(actual?.pkid).toBe(7);
  });

  it('create() POSTs the request body to the collection', () => {
    const request: CourseGroupRequest = { pkid: 0, description: 'Kubernetes系列課程' };

    service.create(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(makeGroup({ pkid: 4, description: 'Kubernetes系列課程' }));
  });

  it('update() PUTs to the collection with the pkid in the body (no route param)', () => {
    const request: CourseGroupRequest = { pkid: 2, description: 'SharePoint進階系列' };

    service.update(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(makeGroup({ pkid: 2 }));
  });

  it('delete() issues DELETE with the pkid in the route', () => {
    service.delete(2).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('surfaces a 409 from delete to the caller (group in use)', () => {
    let status: number | undefined;

    service.delete(1).subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne(`${BASE_URL}/1`).flush('in use', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('surfaces server errors to the caller', () => {
    let status: number | undefined;

    service.getById(999).subscribe({ error: (error) => (status = error.status) });

    httpMock
      .expectOne(`${BASE_URL}/999`)
      .flush('not found', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });
});
