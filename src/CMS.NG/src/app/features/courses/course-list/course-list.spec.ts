import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import { Course, CourseQuery } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { makeCourse } from '@core/services/course.service.spec';
import { CourseList } from './course-list';

// Server-ordered: CourseId ASC. pkid 41 is referenced only by CourseRecomm (no FK).
const COURSES: Course[] = [
  makeCourse({
    pkid: 2063,
    courseId: '14064GLV',
    title: 'ISO 14064溫室氣體主導查證師／確證師訓練課程',
    partnerPkid: 19,
    partnerName: 'ESG溫室氣體系列',
    courseGroupPkid: null,
    courseGroupDescription: null,
    publishStatusPkid: 2,
    publishStatusDescription: '上架中',
    learningCredit: 12.5,
    canRepeat: true,
  }),
  makeCourse({
    pkid: 41,
    courseId: 'IINS',
    title: 'CCNA Security認證-建置Cisco網路安全',
    partnerPkid: 4,
    partnerName: 'Cisco',
    courseGroupPkid: 14,
    courseGroupDescription: 'Cisco系列課程',
    recommCount: 5,
  }),
  makeCourse({ pkid: 35, faqCount: 2, relatedLinkCount: 3 }),
];

interface ListInternals {
  filters: {
    keyword: string | null;
    partnerPkid: number | null;
    scheduleOnFrom: Date | null;
    canRepeat: boolean | null;
  };
  sortState: { sortField: string; sortOrder: number };
  newCourseId: string;
  applyFilters(): void;
  clearFilters(): void;
  onSort(event: { field?: string; order?: number }): void;
  onPage(event: { first?: number; rows?: number }): void;
  confirmDelete(course: Course): void;
  openCopy(course: Course): void;
  copy(): void;
}

describe('CourseList', () => {
  let fixture: ComponentFixture<CourseList>;
  let component: ListInternals;
  let service: jasmine.SpyObj<CourseService>;
  let lookupService: jasmine.SpyObj<LookupService>;
  let confirmationService: ConfirmationService;
  let router: Router;

  function setup(
    queryResult: Observable<Course[]> = of(COURSES),
    queryParams: Record<string, string> = {},
  ): void {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['query', 'delete', 'copy']);
    service.query.and.returnValue(queryResult);
    service.delete.and.returnValue(of(void 0));
    service.copy.and.returnValue(of(makeCourse({ pkid: 3341, courseId: 'PLF-2' })));

    lookupService = jasmine.createSpyObj<LookupService>('LookupService', [
      'getPartners',
      'getCourseGroups',
      'getPublishStatuses',
    ]);
    lookupService.getPartners.and.returnValue(
      of([{ pkid: 2, name: 'Oracle', appKey: 'Oracle', label: 'Oracle (Oracle)' }]),
    );
    lookupService.getCourseGroups.and.returnValue(
      of([{ pkid: 18, description: 'Oracle DB', label: 'Oracle DB' }]),
    );
    lookupService.getPublishStatuses.and.returnValue(
      of([{ pkid: 2, description: '上架中', label: '上架中' }]),
    );

    TestBed.configureTestingModule({
      imports: [CourseList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookupService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams), paramMap: convertToParamMap({}) } },
        },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(CourseList);
    component = fixture.componentInstance as unknown as ListInternals;
    fixture.detectChanges();
  }

  function cellsOf(testId: string): string[] {
    return fixture.debugElement
      .queryAll(By.css(`[data-testid="${testId}"]`))
      .map((cell) => ((cell.nativeElement as HTMLElement).textContent ?? '').trim());
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('creates, loads the three drawer lookups, and queries courses on init', () => {
    setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
    expect(lookupService.getPartners).toHaveBeenCalledTimes(1);
    expect(lookupService.getCourseGroups).toHaveBeenCalledTimes(1);
    expect(lookupService.getPublishStatuses).toHaveBeenCalledTimes(1);
  });

  it('renders one row per course in server order', () => {
    setup();

    expect(cellsOf('cell-course-id')).toEqual(['14064GLV', 'IINS', 'PLF']);
  });

  it('renders the JOINed FK labels from the row, not from the lookups', () => {
    setup();

    expect(cellsOf('cell-partner')).toEqual(['ESG溫室氣體系列', 'Cisco', 'Oracle']);
    expect(cellsOf('cell-publish-status')).toEqual(['上架中', '已下架', '已下架']);
  });

  it('links each FK label to the parent detail page', () => {
    setup();

    const partnerLink = fixture.debugElement.query(By.css('[data-testid="cell-partner"]'));
    expect((partnerLink.nativeElement as HTMLAnchorElement).getAttribute('href')).toBe('/partners/19');
  });

  it('renders an em dash for a null course group and a link otherwise', () => {
    setup();

    const cells = cellsOf('cell-course-group');
    expect(cells[0]).toBe('—');
    expect(cells[1]).toBe('Cisco系列課程');
  });

  it('renders 允許重聽 as 是/否', () => {
    setup();

    expect(cellsOf('cell-can-repeat')).toEqual(['是', '否', '否']);
  });

  it('defaults the sort to courseId ascending', () => {
    setup();

    expect(component.sortState).toEqual({ sortField: 'courseId', sortOrder: 1 });
  });

  it('sends the drawer filters to the query endpoint with dates as ISO strings, and persists them', () => {
    setup();

    component.filters.keyword = 'PLF';
    component.filters.partnerPkid = 2;
    component.filters.scheduleOnFrom = new Date(2015, 0, 1);
    component.filters.canRepeat = false;
    component.applyFilters();
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(2);
    const sent = service.query.calls.mostRecent().args[0] as CourseQuery;
    expect(sent.keyword).toBe('PLF');
    expect(sent.partnerPkid).toBe(2);
    expect(sent.scheduleOnFrom).toBe('2015-01-01');
    expect(sent.canRepeat).toBeFalse();
    expect(sent.scheduleOffTo).toBeNull();

    expect(JSON.parse(sessionStorage.getItem('course-list-filters') ?? '{}')).toEqual(
      jasmine.objectContaining({ keyword: 'PLF', partnerPkid: 2, scheduleOnFrom: '2015-01-01' }),
    );
  });

  it('restores saved filters from session storage on init, dates included', () => {
    sessionStorage.setItem(
      'course-list-filters',
      JSON.stringify({ keyword: 'restored', scheduleOffTo: '2021-11-01', canRepeat: true }),
    );

    setup();

    expect(service.query.calls.mostRecent().args[0]).toEqual(
      jasmine.objectContaining({ keyword: 'restored', scheduleOffTo: '2021-11-01', canRepeat: true }),
    );
    expect(component.filters.keyword).toBe('restored');
  });

  it('lets an incoming partnerPkid query param override the saved filter', () => {
    // The contract recorded in spec/course/Partner.md: /courses?partnerPkid={pkid}.
    sessionStorage.setItem('course-list-filters', JSON.stringify({ keyword: 'stale', partnerPkid: 1 }));

    setup(of(COURSES), { partnerPkid: '19' });

    const sent = service.query.calls.mostRecent().args[0] as CourseQuery;
    expect(sent.partnerPkid).toBe(19);
    expect(sent.keyword).toBeNull();
    expect(component.filters.partnerPkid).toBe(19);
  });

  it('accepts courseGroupPkid and publishStatusPkid query params too', () => {
    setup(of(COURSES), { courseGroupPkid: '18', publishStatusPkid: '2' });

    expect(service.query.calls.mostRecent().args[0]).toEqual(
      jasmine.objectContaining({ courseGroupPkid: 18, publishStatusPkid: 2 }),
    );
  });

  it('ignores a malformed incoming query param', () => {
    setup(of(COURSES), { partnerPkid: 'abc' });

    expect((service.query.calls.mostRecent().args[0] as CourseQuery).partnerPkid).toBeNull();
  });

  it('clears every filter and re-queries with the empty filter', () => {
    setup();

    component.filters.keyword = 'PLF';
    component.filters.partnerPkid = 2;
    component.applyFilters();
    component.clearFilters();

    const sent = service.query.calls.mostRecent().args[0] as CourseQuery;
    expect(Object.values(sent).every((value) => value === null)).toBeTrue();
  });

  it('persists sort and page state to session storage', () => {
    setup();

    component.onSort({ field: 'scheduleOn', order: -1 });
    component.onPage({ first: 40, rows: 20 });

    expect(JSON.parse(sessionStorage.getItem('course-list-sort') ?? '{}')).toEqual({
      sortField: 'scheduleOn',
      sortOrder: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('course-list-page') ?? '{}')).toEqual({
      first: 40,
      rows: 20,
    });
  });

  it('deletes a course after the confirmation is accepted, then reloads', () => {
    setup();
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    component.confirmDelete(COURSES[0]);

    expect(confirmation?.message).toContain('確定要刪除主代碼');
    expect(confirmation?.message).toContain('14064GLV');
    expect(service.delete).not.toHaveBeenCalled();

    confirmation?.accept?.();

    expect(service.delete).toHaveBeenCalledWith(2063);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('reports the in-use reason when delete returns 409', () => {
    setup();
    service.delete.and.returnValue(throwError(() => ({ status: 409 })));
    const addSpy = spyOn(TestBed.inject(MessageService), 'add');
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });

    // Referenced only by CourseRecomm — the reference SQL Server does not enforce.
    component.confirmDelete(COURSES[1]);

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('推薦課程使用');
  });

  it('opens the copy dialog for a row and copies under the new courseId, then navigates', () => {
    setup();

    component.openCopy(COURSES[2]);
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('[data-testid="copy-source"]')).nativeElement.textContent).toContain('PLF');

    component.newCourseId = ' PLF-2 ';
    component.copy();

    expect(service.copy).toHaveBeenCalledWith(35, { newCourseId: 'PLF-2' });
    expect(router.navigate).toHaveBeenCalledWith(['/courses', 3341]);
  });

  it('does not call the service when the new courseId is blank', () => {
    setup();

    component.openCopy(COURSES[2]);
    component.newCourseId = '   ';
    component.copy();

    expect(service.copy).not.toHaveBeenCalled();
  });

  it('reports a duplicate courseId when copy returns 409', () => {
    setup();
    service.copy.and.returnValue(throwError(() => ({ status: 409 })));
    const addSpy = spyOn(TestBed.inject(MessageService), 'add');

    component.openCopy(COURSES[2]);
    component.newCourseId = 'IINS';
    component.copy();

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('簡介代碼「IINS」已存在');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('shows an empty message and no rows when the query fails', fakeAsync(() => {
    setup(throwError(() => new Error('boom')));
    tick();
    fixture.detectChanges();

    expect(fixture.debugElement.queryAll(By.css('[data-testid="course-row"]')).length).toBe(0);
  }));
});
