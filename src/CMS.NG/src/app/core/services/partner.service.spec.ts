import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { Partner, PartnerRequest, totalPartnerUsage } from '@core/models/partner.model';
import { PartnerService } from './partner.service';

const BASE_URL = `${environment.apiBaseUrl}/partners`;

function makePartner(overrides: Partial<Partner> = {}): Partner {
  return {
    pkid: 1,
    name: 'CompTIA',
    appKey: 'CompTIA',
    nameOnPartnerMenu: 'CompTIA',
    nameOnCourseDetailPage: 'CompTIA',
    displayOrder: 3,
    imageFilename: 'CompTIA.png',
    courseCount: 42,
    certificationCount: 4,
    partnerCourseGroupCount: 2,
    promotion2Count: 7,
    seminarCount: 0,
    ...overrides,
  };
}

function makeRequest(overrides: Partial<PartnerRequest> = {}): PartnerRequest {
  return {
    pkid: 0,
    name: 'Cisco',
    appKey: 'Cisco',
    nameOnPartnerMenu: 'Cisco 網路認證課程',
    nameOnCourseDetailPage: 'Cisco',
    displayOrder: 9999,
    imageFilename: null,
    ...overrides,
  };
}

describe('PartnerService', () => {
  let service: PartnerService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PartnerService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PartnerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the partners collection', () => {
    const expected = [makePartner()];
    let actual: Partner[] | undefined;

    service.getAll().subscribe((partners) => (actual = partners));

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('query() POSTs the filter object to /query', () => {
    const expected = [makePartner()];
    let actual: Partner[] | undefined;

    service.query({ keyword: 'ISO' }).subscribe((partners) => (actual = partners));

    const req = httpMock.expectOne(`${BASE_URL}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'ISO' });
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('getById() issues GET with the pkid in the route', () => {
    let actual: Partner | undefined;

    service.getById(19).subscribe((partner) => (actual = partner));

    const req = httpMock.expectOne(`${BASE_URL}/19`);
    expect(req.request.method).toBe('GET');
    req.flush(makePartner({ pkid: 19 }));

    expect(actual?.pkid).toBe(19);
  });

  it('create() POSTs the request body to the collection', () => {
    const request = makeRequest();

    service.create(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(makePartner({ pkid: 67, name: 'Cisco', appKey: 'Cisco' }));
  });

  it('create() carries a null imageFilename through unchanged', () => {
    service.create(makeRequest({ imageFilename: null })).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect((req.request.body as PartnerRequest).imageFilename).toBeNull();
    req.flush(makePartner({ imageFilename: null }));
  });

  it('update() PUTs to the collection with the pkid in the body (no route param)', () => {
    const request = makeRequest({ pkid: 19, name: '國際標準課程', appKey: 'ISO' });

    service.update(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(makePartner({ pkid: 19 }));
  });

  it('delete() issues DELETE with the pkid in the route', () => {
    service.delete(31).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/31`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('surfaces a 409 from delete to the caller (partner in use)', () => {
    let status: number | undefined;

    service.delete(1).subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne(`${BASE_URL}/1`).flush('in use', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('surfaces a 409 from create to the caller (duplicate AppKey)', () => {
    let status: number | undefined;

    service.create(makeRequest({ appKey: 'ISO' })).subscribe({
      error: (error) => (status = error.status),
    });

    httpMock.expectOne(BASE_URL).flush('duplicate', { status: 409, statusText: 'Conflict' });

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

  it('totalPartnerUsage() sums all five reference counts', () => {
    expect(totalPartnerUsage(makePartner())).toBe(55);
    expect(
      totalPartnerUsage(
        makePartner({
          courseCount: 0,
          certificationCount: 0,
          partnerCourseGroupCount: 0,
          promotion2Count: 0,
          // Seminar has no FK constraint, but it still counts as usage.
          seminarCount: 34,
        }),
      ),
    ).toBe(34);
  });
});
