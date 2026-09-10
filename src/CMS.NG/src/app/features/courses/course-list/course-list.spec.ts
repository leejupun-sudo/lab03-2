import { EventEmitter } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import { Course, CourseQuery, CourseRequest } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { makeCourse } from '@core/services/course.service.spec';
import { CourseList, EditValue, EditableField, validateCell } from './course-list';

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

/**
 * What `GET /api/courses/{id}` adds over a list row: the two junction id lists. Inline
 * save must read this before it PUTs, or `UpdateAsync` re-syncs both junctions from an
 * empty request and wipes them.
 */
const FULL_COURSE: Course = makeCourse({
  pkid: 2063,
  courseId: '14064GLV',
  title: 'ISO 14064溫室氣體主導查證師／確證師訓練課程',
  publishStatusPkid: 2,
  publishStatusDescription: '上架中',
  learningCredit: 12.5,
  canRepeat: true,
  certificationPkids: [7, 9],
  jobCategoryPkids: [3],
});

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
  // ---- inline cell editing ----
  editing(): { pkid: number; field: EditableField; value: EditValue } | null;
  editError(): string | null;
  savingCell(): boolean;
  overlayOpen: { set(open: boolean): void };
  startEdit(course: Course, field: EditableField, cell?: EventTarget | null): void;
  setEditValue(value: EditValue): void;
  commit(): void;
  commitOnBlur(): void;
  cancelEdit(): void;
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
    service = jasmine.createSpyObj<CourseService>('CourseService', [
      'query',
      'delete',
      'copy',
      'getById',
      'update',
    ]);
    service.query.and.returnValue(queryResult);
    service.delete.and.returnValue(of(void 0));
    service.copy.and.returnValue(of(makeCourse({ pkid: 3341, courseId: 'PLF-2' })));
    service.getById.and.returnValue(of(FULL_COURSE));
    service.update.and.callFake((request: CourseRequest) =>
      of({ ...FULL_COURSE, ...request } as Course),
    );

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

  // ---- inline-edit DOM helpers (row 0 is pkid 2063 unless a row index is given) ----

  function cell(field: string, rowIndex = 0): HTMLElement {
    return fixture.debugElement.queryAll(By.css(`[data-testid="editcell-${field}"]`))[rowIndex]
      .nativeElement as HTMLElement;
  }

  function fire(field: string, type: 'dblclick' | 'click', rowIndex = 0): void {
    cell(field, rowIndex).dispatchEvent(new MouseEvent(type, { bubbles: true }));
    fixture.detectChanges();
  }

  function editor(field: string): HTMLElement | null {
    const found = fixture.debugElement.query(By.css(`[data-testid="editor-${field}"]`));
    return found ? (found.nativeElement as HTMLElement) : null;
  }

  function anyEditor(): boolean {
    return fixture.debugElement.query(By.css('[data-testid^="editor-"]')) !== null;
  }

  /**
   * Blurs an editor. A plain `<input>` takes a DOM event, but the PrimeNG editors expose
   * `onBlur` as an `EventEmitter` that no native event reaches — emit on it instead.
   */
  function blurEditor(field: string): void {
    const found = fixture.debugElement.query(By.css(`[data-testid="editor-${field}"]`));
    const instance = found.componentInstance as { onBlur?: EventEmitter<unknown> } | null;
    if (instance?.onBlur instanceof EventEmitter) {
      instance.onBlur.emit(new FocusEvent('blur'));
    } else {
      (found.nativeElement as HTMLElement).dispatchEvent(new FocusEvent('blur'));
    }
    fixture.detectChanges();
  }

  function errorText(): string | null {
    const found = fixture.debugElement.query(By.css('[data-testid="edit-error"]'));
    return found ? ((found.nativeElement as HTMLElement).textContent ?? '').trim() : null;
  }

  function lastRequest(): CourseRequest {
    return service.update.calls.mostRecent().args[0] as CourseRequest;
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

  // -------------------------------------------------------------------------
  // 表格內即時編輯 (inline cell editing)
  // -------------------------------------------------------------------------

  describe('inline cell editing', () => {
    describe('entering edit mode', () => {
      it('opens the editor on a double-click', () => {
        setup();

        fire('title', 'dblclick');

        expect(component.editing()).toEqual({
          pkid: 2063,
          field: 'title',
          value: 'ISO 14064溫室氣體主導查證師／確證師訓練課程',
        });
        expect(editor('title')).not.toBeNull();
      });

      it('leaves the cell alone on a single click', () => {
        setup();

        fire('title', 'click');

        expect(component.editing()).toBeNull();
        expect(anyEditor()).toBeFalse();
      });

      it('opens the matching editor for every editable column', () => {
        setup();

        const columns: [string, EditableField][] = [
          ['display-order', 'displayOrder'],
          ['prod-course-id', 'prodCourseId'],
          ['title', 'title'],
          ['publish-status', 'publishStatusPkid'],
          ['schedule-on', 'scheduleOn'],
          ['schedule-off', 'scheduleOff'],
          ['hour', 'hour'],
          ['list-price', 'listPrice'],
          ['learning-credit', 'learningCredit'],
          ['can-repeat', 'canRepeat'],
        ];

        for (const [testId, field] of columns) {
          fire(testId, 'dblclick');
          expect(component.editing()?.field).withContext(testId).toBe(field);
          expect(editor(testId)).withContext(testId).not.toBeNull();
          component.cancelEdit();
          fixture.detectChanges();
        }
      });

      it('uses the input type each column calls for', () => {
        setup();

        fire('title', 'dblclick');
        expect(editor('title')!.tagName).toBe('INPUT');
        component.cancelEdit();
        fixture.detectChanges();

        fire('hour', 'dblclick');
        expect(editor('hour')!.tagName.toLowerCase()).toBe('p-inputnumber');
        component.cancelEdit();
        fixture.detectChanges();

        fire('schedule-on', 'dblclick');
        expect(editor('schedule-on')!.tagName.toLowerCase()).toBe('p-datepicker');
        component.cancelEdit();
        fixture.detectChanges();

        fire('publish-status', 'dblclick');
        expect(editor('publish-status')!.tagName.toLowerCase()).toBe('p-select');
        component.cancelEdit();
        fixture.detectChanges();

        fire('can-repeat', 'dblclick');
        expect(editor('can-repeat')!.tagName.toLowerCase()).toBe('p-checkbox');
      });

      it('hands the date editor a Date and the select the FK pkid, not the rendered label', () => {
        setup();

        fire('schedule-on', 'dblclick');
        expect(component.editing()!.value).toEqual(new Date(2015, 10, 10));
        component.cancelEdit();

        fire('publish-status', 'dblclick');
        expect(component.editing()!.value).toBe(2);
      });

      it('closes the editor on Escape without saving', () => {
        setup();

        fire('title', 'dblclick');
        component.setEditValue('改一半就放棄');
        component.cancelEdit();
        fixture.detectChanges();

        expect(component.editing()).toBeNull();
        expect(service.update).not.toHaveBeenCalled();
        expect(cellsOf('editcell-title')[0]).toBe('ISO 14064溫室氣體主導查證師／確證師訓練課程');
      });
    });

    describe('read-only columns', () => {
      // 主代碼 is IDENTITY; 簡介代碼 is omitted from the UPDATE statement because
      // CourseRecomm keys on the string; 原廠 / 課程群組 are JOINed FK labels.
      const READ_ONLY = ['pkid', 'course-id', 'partner-name', 'course-group'];

      it('does not open an editor on a double-click', () => {
        setup();

        for (const testId of READ_ONLY) {
          fire(testId, 'dblclick');
          expect(component.editing()).withContext(testId).toBeNull();
          expect(anyEditor()).withContext(testId).toBeFalse();
        }
      });

      it('carries no editable affordance, unlike every other column', () => {
        setup();

        for (const testId of READ_ONLY) {
          expect(cell(testId).classList.contains('cms-cell--editable'))
            .withContext(testId)
            .toBeFalse();
        }
        expect(cell('title').classList.contains('cms-cell--editable')).toBeTrue();
      });
    });

    describe('persisting on blur', () => {
      it('reads the full row, then PUTs it with the edited column replaced', () => {
        setup();

        fire('title', 'dblclick');
        component.setEditValue('  ISO 14064 溫室氣體訓練  ');
        blurEditor('title');

        expect(service.getById).toHaveBeenCalledWith(2063);
        expect(service.update).toHaveBeenCalledTimes(1);
        expect(lastRequest().title).toBe('ISO 14064 溫室氣體訓練');
        expect(lastRequest().pkid).toBe(2063);
      });

      it('keeps both junction id lists, which the list row does not carry', () => {
        // A request built from the list row alone would send [] and UpdateAsync would
        // delete every CourseInCertification / CourseJobCategories row for the course.
        setup();

        fire('hour', 'dblclick');
        component.setEditValue(21);
        blurEditor('hour');

        expect(lastRequest().certificationPkids).toEqual([7, 9]);
        expect(lastRequest().jobCategoryPkids).toEqual([3]);
      });

      it('sends a date as an ISO yyyy-MM-dd string built from local components', () => {
        setup();

        fire('schedule-off', 'dblclick');
        component.setEditValue(new Date(2030, 0, 1));
        component.commit();

        expect(lastRequest().scheduleOff).toBe('2030-01-01');
      });

      it('sends the checkbox as a boolean', () => {
        setup();

        fire('can-repeat', 'dblclick');
        component.setEditValue(false);
        blurEditor('can-repeat');

        expect(lastRequest().canRepeat).toBeFalse();
      });

      it('patches the row from the PUT response and closes the editor', () => {
        setup();

        fire('title', 'dblclick');
        component.setEditValue('新課程名稱');
        blurEditor('title');

        expect(component.editing()).toBeNull();
        expect(cellsOf('editcell-title')[0]).toBe('新課程名稱');
      });

      it('does not call the API when the value is unchanged', () => {
        setup();

        fire('title', 'dblclick');
        blurEditor('title');

        expect(service.getById).not.toHaveBeenCalled();
        expect(service.update).not.toHaveBeenCalled();
        expect(component.editing()).toBeNull();
      });

      it('ignores a blur raised while the editor own overlay is open', () => {
        // p-datepicker / p-select panels are appendTo="body", so reaching for the
        // calendar blurs the input; committing there would close the editor.
        setup();

        fire('schedule-on', 'dblclick');
        component.overlayOpen.set(true);
        component.setEditValue(new Date(2016, 0, 1));
        component.commitOnBlur();

        expect(service.update).not.toHaveBeenCalled();
        expect(component.editing()).not.toBeNull();

        component.overlayOpen.set(false);
        component.commitOnBlur();

        expect(lastRequest().scheduleOn).toBe('2016-01-01');
      });
    });

    describe('validation', () => {
      /** Blur an editor whose value is invalid and return the message shown. */
      function reject(testId: string, value: EditValue): string | null {
        fire(testId, 'dblclick');
        component.setEditValue(value);
        component.commit();
        fixture.detectChanges();
        return errorText();
      }

      it('blocks a cleared required text field and stays in edit mode', () => {
        setup();

        expect(reject('title', '   ')).toBe('課程名稱不可空白。');
        expect(service.update).not.toHaveBeenCalled();
        expect(component.editing()?.field).toBe('title');
        expect(editor('title')).not.toBeNull();
      });

      it('blocks a cleared required number, select and date', () => {
        setup();

        expect(reject('hour', null)).toBe('時數不可空白。');
        component.cancelEdit();
        expect(reject('publish-status', null)).toBe('上架狀態不可空白。');
        component.cancelEdit();
        expect(reject('schedule-on', null)).toBe('上架日期不可空白。');

        expect(service.update).not.toHaveBeenCalled();
      });

      it('blocks a negative number in each numeric column', () => {
        setup();

        expect(reject('hour', -1)).toBe('時數不可為負數。');
        component.cancelEdit();
        expect(reject('list-price', -0.5)).toBe('定價不可為負數。');
        component.cancelEdit();
        expect(reject('learning-credit', -3)).toBe('點數不可為負數。');
        component.cancelEdit();
        expect(reject('display-order', -1)).toBe('顯示順序不可為負數。');

        expect(service.update).not.toHaveBeenCalled();
      });

      it('accepts zero, which is not negative', () => {
        setup();

        fire('hour', 'dblclick');
        component.setEditValue(0);
        component.commit();

        expect(lastRequest().hour).toBe(0);
      });

      it('blocks a non-numeric value', () => {
        setup();

        expect(reject('list-price', '免費')).toBe('定價必須是數字。');
        expect(service.update).not.toHaveBeenCalled();
      });

      it('blocks an invalid date', () => {
        setup();

        expect(reject('schedule-on', new Date('nonsense'))).toBe('上架日期必須是有效日期。');
        expect(service.update).not.toHaveBeenCalled();
      });

      it('blocks 上架日期 after 下架日期, from either end of the range', () => {
        setup();

        // The row holds 2015-11-10 .. 2021-11-01.
        expect(reject('schedule-on', new Date(2022, 0, 1))).toBe('上架日期不可晚於下架日期。');
        component.cancelEdit();
        expect(reject('schedule-off', new Date(2015, 0, 1))).toBe('上架日期不可晚於下架日期。');

        expect(service.update).not.toHaveBeenCalled();
      });

      it('accepts the two dates being equal', () => {
        setup();

        fire('schedule-off', 'dblclick');
        component.setEditValue(new Date(2015, 10, 10));
        component.commit();

        expect(lastRequest().scheduleOff).toBe('2015-11-10');
      });

      it('clears the message once the value is corrected', () => {
        setup();

        expect(reject('title', '')).toBe('課程名稱不可空白。');

        component.setEditValue('改好了');
        component.commit();
        fixture.detectChanges();

        expect(errorText()).toBeNull();
        expect(lastRequest().title).toBe('改好了');
      });

      it('applies the column length and range caps', () => {
        const row = makeCourse();

        expect(validateCell('title', 'a'.repeat(201), row)).toBe('課程名稱不可超過 200 個字。');
        expect(validateCell('prodCourseId', 'a'.repeat(51), row)).toBe('科目代碼不可超過 50 個字。');
        expect(validateCell('hour', 32768, row)).toBe('時數不可大於 32767。');
        expect(validateCell('displayOrder', 10000, row)).toBe('顯示順序不可大於 9999。');
        expect(validateCell('displayOrder', 1.5, row)).toBe('顯示順序必須是整數。');
        // 點數 is decimal(9,1) — 387 live rows carry a fraction.
        expect(validateCell('learningCredit', 12.5, row)).toBeNull();
        // A checkbox has no invalid state.
        expect(validateCell('canRepeat', false, row)).toBeNull();
      });
    });

    describe('failed save', () => {
      it('reverts the cell to its previous value and reports the error', () => {
        setup();
        service.update.and.returnValue(throwError(() => ({ status: 500 })));
        const addSpy = spyOn(TestBed.inject(MessageService), 'add');

        fire('title', 'dblclick');
        component.setEditValue('不會存進去的名稱');
        blurEditor('title');

        expect(component.editing()).toBeNull();
        expect(cellsOf('editcell-title')[0]).toBe('ISO 14064溫室氣體主導查證師／確證師訓練課程');
        expect(addSpy.calls.mostRecent().args[0].severity).toBe('error');
        expect(addSpy.calls.mostRecent().args[0].detail).toContain('已還原原值');
      });

      it('reverts when the preparatory read fails too', () => {
        setup();
        service.getById.and.returnValue(throwError(() => ({ status: 500 })));

        fire('hour', 'dblclick');
        component.setEditValue(99);
        blurEditor('hour');

        expect(service.update).not.toHaveBeenCalled();
        expect(component.editing()).toBeNull();
        expect(cellsOf('editcell-hour')[0]).toBe('12');
      });

      it('tells the user to reload when the row has since been deleted', () => {
        setup();
        service.update.and.returnValue(throwError(() => ({ status: 404 })));
        const addSpy = spyOn(TestBed.inject(MessageService), 'add');

        fire('hour', 'dblclick');
        component.setEditValue(99);
        blurEditor('hour');

        expect(addSpy.calls.mostRecent().args[0].detail).toContain('請重新整理清單');
      });
    });
  });
});
