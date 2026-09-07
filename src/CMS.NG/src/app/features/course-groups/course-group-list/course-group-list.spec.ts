import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroupList } from './course-group-list';

const GROUPS: CourseGroup[] = [
  { pkid: 1, description: 'Azure系列課程', courseCount: 48, partnerCourseGroupCount: 2 },
  { pkid: 3, description: 'Azure系列課程', courseCount: 0, partnerCourseGroupCount: 0 },
  { pkid: 2, description: 'SharePoint系列課程', courseCount: 12, partnerCourseGroupCount: 0 },
];

describe('CourseGroupList', () => {
  let fixture: ComponentFixture<CourseGroupList>;
  let service: jasmine.SpyObj<CourseGroupService>;
  let confirmationService: ConfirmationService;

  function setup(queryResult: Observable<CourseGroup[]> = of(GROUPS)): void {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['query', 'delete']);
    service.query.and.returnValue(queryResult);
    service.delete.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      imports: [CourseGroupList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: CourseGroupService, useValue: service },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(CourseGroupList);
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('creates and loads groups on init', () => {
    setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per group with the schema columns', () => {
    setup();

    const rows = fixture.debugElement.queryAll(By.css('[data-testid="group-row"]'));
    expect(rows.length).toBe(3);

    const firstRowText = (rows[0].nativeElement as HTMLElement).textContent ?? '';
    expect(firstRowText).toContain('Azure系列課程');
    expect(firstRowText).toContain('48');
  });

  it('renders duplicate descriptions as separate rows', () => {
    setup();

    const rowTexts = fixture.debugElement
      .queryAll(By.css('[data-testid="group-row"]'))
      .map((row) => (row.nativeElement as HTMLElement).textContent ?? '');

    expect(rowTexts.filter((text) => text.includes('Azure系列課程')).length).toBe(2);
  });

  it('defaults the sort to description ascending', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      sortState: { sortField: string; sortOrder: number };
    };

    expect(component.sortState).toEqual({ sortField: 'description', sortOrder: 1 });
  });

  it('sends the drawer filter value to the query endpoint and persists it', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      applyFilters(): void;
    };

    component.filters.keyword = 'Azure';
    component.applyFilters();
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0]).toEqual({ keyword: 'Azure' });
    expect(JSON.parse(sessionStorage.getItem('course-group-list-filters') ?? '{}')).toEqual(
      jasmine.objectContaining({ keyword: 'Azure' }),
    );
  });

  it('restores saved filters from session storage on init', () => {
    sessionStorage.setItem(
      'course-group-list-filters',
      JSON.stringify({ keyword: 'restored' }),
    );

    setup();

    expect(service.query.calls.mostRecent().args[0]).toEqual(
      jasmine.objectContaining({ keyword: 'restored' }),
    );
  });

  it('clears the filter and re-queries with an empty filter', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      applyFilters(): void;
      clearFilters(): void;
    };

    component.filters.keyword = 'Azure';
    component.applyFilters();
    component.clearFilters();

    expect(service.query.calls.mostRecent().args[0]).toEqual({ keyword: null });
  });

  it('persists sort and page state to session storage', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      onSort(event: { field?: string; order?: number }): void;
      onPage(event: { first?: number; rows?: number }): void;
    };

    component.onSort({ field: 'courseCount', order: -1 });
    component.onPage({ first: 40, rows: 20 });

    expect(JSON.parse(sessionStorage.getItem('course-group-list-sort') ?? '{}')).toEqual({
      sortField: 'courseCount',
      sortOrder: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('course-group-list-page') ?? '{}')).toEqual({
      first: 40,
      rows: 20,
    });
  });

  it('deletes a group after the confirmation is accepted, then reloads', () => {
    setup();
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    const component = fixture.componentInstance as unknown as {
      confirmDelete(group: CourseGroup): void;
    };
    component.confirmDelete(GROUPS[1]);

    expect(confirmation?.message).toContain('確定要刪除主代碼');
    expect(confirmation?.message).toContain('Azure系列課程');
    expect(service.delete).not.toHaveBeenCalled();

    confirmation?.accept?.();

    expect(service.delete).toHaveBeenCalledWith(3);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('reports the in-use reason when delete returns 409', () => {
    setup();
    service.delete.and.returnValue(throwError(() => ({ status: 409 })));
    const messageService = TestBed.inject(MessageService);
    const addSpy = spyOn(messageService, 'add');

    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });

    const component = fixture.componentInstance as unknown as {
      confirmDelete(group: CourseGroup): void;
    };
    component.confirmDelete(GROUPS[0]);

    expect(addSpy.calls.mostRecent().args[0].detail).toContain(
      '已被課程或廠商課程群組使用',
    );
  });

  it('shows an empty message and no rows when the query fails', fakeAsync(() => {
    setup(throwError(() => new Error('boom')));
    tick();
    fixture.detectChanges();

    expect(fixture.debugElement.queryAll(By.css('[data-testid="group-row"]')).length).toBe(0);
  }));
});
