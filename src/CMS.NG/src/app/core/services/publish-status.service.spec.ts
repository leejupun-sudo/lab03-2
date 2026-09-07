import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';
import { PublishStatusService } from './publish-status.service';

const BASE_URL = `${environment.apiBaseUrl}/publish-statuses`;

function makeStatus(overrides: Partial<PublishStatus> = {}): PublishStatus {
  return {
    pkid: 2,
    description: '上架中',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false,
    courseCount: 12,
    promotion2Count: 3,
    ...overrides,
  };
}

describe('PublishStatusService', () => {
  let service: PublishStatusService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PublishStatusService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PublishStatusService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the publish-statuses collection', () => {
    const expected = [makeStatus()];
    let actual: PublishStatus[] | undefined;

    service.getAll().subscribe((statuses) => (actual = statuses));

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('query() POSTs the filter object to /query', () => {
    const expected = [makeStatus()];
    let actual: PublishStatus[] | undefined;

    service
      .query({ keyword: '上架', isDraft: false, isPublished: true, isDiscontinued: null })
      .subscribe((statuses) => (actual = statuses));

    const req = httpMock.expectOne(`${BASE_URL}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      keyword: '上架',
      isDraft: false,
      isPublished: true,
      isDiscontinued: null,
    });
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('getById() issues GET with the pkid in the route', () => {
    let actual: PublishStatus | undefined;

    service.getById(3).subscribe((status) => (actual = status));

    const req = httpMock.expectOne(`${BASE_URL}/3`);
    expect(req.request.method).toBe('GET');
    req.flush(makeStatus({ pkid: 3, description: '已下架' }));

    expect(actual?.pkid).toBe(3);
  });

  it('create() POSTs the request body — including the client-supplied pkid', () => {
    const request: PublishStatusRequest = {
      pkid: 4,
      description: '審核中',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect(req.request.body.pkid).toBe(4);
    req.flush(makeStatus({ pkid: 4, description: '審核中' }));
  });

  it('update() PUTs to the collection with the pkid in the body (no route param)', () => {
    const request: PublishStatusRequest = {
      pkid: 3,
      description: '已封存',
      isDraft: false,
      isPublished: false,
      isDiscontinued: true,
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(makeStatus({ pkid: 3 }));
  });

  it('delete() issues DELETE with the pkid in the route', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('surfaces a 409 from delete to the caller (status in use)', () => {
    let status: number | undefined;

    service.delete(2).subscribe({ error: (error) => (status = error.status) });

    httpMock
      .expectOne(`${BASE_URL}/2`)
      .flush('in use', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('surfaces server errors to the caller', () => {
    let status: number | undefined;

    service.getById(99).subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne(`${BASE_URL}/99`).flush('not found', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });
});
