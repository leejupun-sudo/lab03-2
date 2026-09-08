import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import { Course, CourseRequest, totalCourseUsage } from '@core/models/course.model';
import { CourseService } from './course.service';

const BASE_URL = `${environment.apiBaseUrl}/courses`;

export function makeCourse(overrides: Partial<Course> = {}): Course {
  return {
    pkid: 35,
    title: 'Oracle資料庫之PL／SQL基礎',
    officialTitle: null,
    courseId: 'PLF',
    prodCourseId: 'PLF',
    friendlyUrl: 'Oracle資料庫-PL-SQL基礎',
    displayOrder: 600,
    partnerPkid: 2,
    partnerName: 'Oracle',
    courseGroupPkid: 18,
    courseGroupDescription: 'Oracle DB/My SQL資料庫系列課程',
    publishStatusPkid: 3,
    publishStatusDescription: '已下架',
    scheduleOn: '2015-11-10',
    scheduleOff: '2021-11-01',
    hour: 12,
    listPrice: 49000,
    learningCredit: 14,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: false,
    faqCount: 0,
    relatedLinkCount: 0,
    hotCourseCount: 0,
    recommCount: 0,
    certificationPkids: [],
    jobCategoryPkids: [],
    ...overrides,
  };
}

export function makeRequest(overrides: Partial<CourseRequest> = {}): CourseRequest {
  return {
    pkid: 0,
    title: 'Azure基礎',
    officialTitle: null,
    courseId: 'AZ-900',
    prodCourseId: 'AZ-900',
    friendlyUrl: 'Azure-Fundamentals',
    displayOrder: 0,
    partnerPkid: 1,
    courseGroupPkid: 3,
    publishStatusPkid: 1,
    scheduleOn: '2026-01-16',
    scheduleOff: '2036-01-16',
    hour: 7,
    listPrice: 9000,
    learningCredit: 3,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: false,
    certificationPkids: [],
    jobCategoryPkids: [],
    ...overrides,
  };
}

describe('CourseService', () => {
  let service: CourseService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CourseService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(CourseService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the courses collection', () => {
    const expected = [makeCourse()];
    let actual: Course[] | undefined;

    service.getAll().subscribe((courses) => (actual = courses));

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('query() POSTs the filter object, dates as ISO strings, to /query', () => {
    const query = { keyword: 'PLF', partnerPkid: 2, scheduleOnFrom: '2015-01-01', canRepeat: null };

    service.query(query).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([]);
  });

  it('getById() issues GET with the pkid in the route', () => {
    let actual: Course | undefined;

    service.getById(35).subscribe((course) => (actual = course));

    const req = httpMock.expectOne(`${BASE_URL}/35`);
    expect(req.request.method).toBe('GET');
    req.flush(makeCourse({ certificationPkids: [34, 36] }));

    expect(actual?.certificationPkids).toEqual([34, 36]);
  });

  it('create() POSTs the request body, junction ids included', () => {
    const request = makeRequest({ certificationPkids: [40], jobCategoryPkids: [16, 23] });

    service.create(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(makeCourse({ pkid: 3341, courseId: 'AZ-900' }));
  });

  it('update() PUTs to the collection with the pkid in the body (no route param)', () => {
    const request = makeRequest({ pkid: 35, courseId: 'PLF' });

    service.update(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(makeCourse());
  });

  it('delete() issues DELETE with the pkid in the route', () => {
    service.delete(35).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/35`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('copy() POSTs the new courseId to /{pkid}/copy', () => {
    let actual: Course | undefined;

    service.copy(35, { newCourseId: 'PLF-2' }).subscribe((course) => (actual = course));

    const req = httpMock.expectOne(`${BASE_URL}/35/copy`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ newCourseId: 'PLF-2' });
    req.flush(makeCourse({ pkid: 3341, courseId: 'PLF-2' }));

    expect(actual?.courseId).toBe('PLF-2');
  });

  it('surfaces a 409 from create to the caller (duplicate courseId)', () => {
    let status: number | undefined;

    service.create(makeRequest({ courseId: 'PLF' })).subscribe({
      error: (error) => (status = error.status),
    });

    httpMock.expectOne(BASE_URL).flush('duplicate', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('surfaces a 409 from delete to the caller (course in use)', () => {
    let status: number | undefined;

    service.delete(41).subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne(`${BASE_URL}/41`).flush('in use', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('totalCourseUsage() sums all four reference counts', () => {
    expect(
      totalCourseUsage(
        makeCourse({ faqCount: 2, relatedLinkCount: 3, hotCourseCount: 1, recommCount: 0 }),
      ),
    ).toBe(6);
    // CourseRecomm has no FK constraint, but it still counts as usage.
    expect(totalCourseUsage(makeCourse({ recommCount: 5 }))).toBe(5);
  });
});
