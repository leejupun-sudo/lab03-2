import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatusList } from './publish-status-list';

const STATUSES: PublishStatus[] = [
  {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false,
    courseCount: 0,
    promotion2Count: 0,
  },
  {
    pkid: 2,
    description: '上架中',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false,
    courseCount: 12,
    promotion2Count: 3,
  },
];

describe('PublishStatusList', () => {
  let fixture: ComponentFixture<PublishStatusList>;
  let service: jasmine.SpyObj<PublishStatusService>;
  let confirmationService: ConfirmationService;

  function setup(queryResult: Observable<PublishStatus[]> = of(STATUSES)): void {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', [
      'query',
      'delete',
    ]);
    service.query.and.returnValue(queryResult);
    service.delete.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      imports: [PublishStatusList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: PublishStatusService, useValue: service },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(PublishStatusList);
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('creates and loads statuses on init', () => {
    setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per status with the schema columns', () => {
    setup();

    const rows = fixture.debugElement.queryAll(By.css('[data-testid="status-row"]'));
    expect(rows.length).toBe(2);

    const secondRowText = (rows[1].nativeElement as HTMLElement).textContent ?? '';
    expect(secondRowText).toContain('2');
    expect(secondRowText).toContain('上架中');
    expect(secondRowText).toContain('12');
  });

  it('renders the bit columns as 是/否 tags rather than raw booleans', () => {
    setup();

    const firstRowText =
      (
        fixture.debugElement.queryAll(By.css('[data-testid="status-row"]'))[0]
          .nativeElement as HTMLElement
      ).textContent ?? '';

    expect(firstRowText).toContain('是');
    expect(firstRowText).toContain('否');
    expect(firstRowText).not.toContain('true');
    expect(firstRowText).not.toContain('false');
  });

  it('sends the drawer filter values to the query endpoint and persists them', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null; isPublished: boolean | null };
      applyFilters(): void;
    };

    component.filters.keyword = '上架';
    component.filters.isPublished = true;
    component.applyFilters();
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0]).toEqual(
      jasmine.objectContaining({ keyword: '上架', isPublished: true }),
    );
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-filters') ?? '{}')).toEqual(
      jasmine.objectContaining({ keyword: '上架', isPublished: true }),
    );
  });

  it('keeps a false bool filter distinct from "no filter"', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      filters: { isDraft: boolean | null };
      applyFilters(): void;
    };

    component.filters.isDraft = false;
    component.applyFilters();

    expect(service.query.calls.mostRecent().args[0].isDraft).toBe(false);
  });

  it('restores saved filters from session storage on init', () => {
    sessionStorage.setItem(
      'publish-status-list-filters',
      JSON.stringify({
        keyword: 'restored',
        isDraft: null,
        isPublished: null,
        isDiscontinued: null,
      }),
    );

    setup();

    expect(service.query.calls.mostRecent().args[0]).toEqual(
      jasmine.objectContaining({ keyword: 'restored' }),
    );
  });

  it('clears the filters and re-queries with an empty filter', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      applyFilters(): void;
      clearFilters(): void;
    };

    component.filters.keyword = '草稿';
    component.applyFilters();
    component.clearFilters();

    expect(service.query.calls.mostRecent().args[0]).toEqual({
      keyword: null,
      isDraft: null,
      isPublished: null,
      isDiscontinued: null,
    });
  });

  it('persists sort and page state to session storage', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      onSort(event: { field?: string; order?: number }): void;
      onPage(event: { first?: number; rows?: number }): void;
    };

    component.onSort({ field: 'description', order: -1 });
    component.onPage({ first: 20, rows: 20 });

    expect(JSON.parse(sessionStorage.getItem('publish-status-list-sort') ?? '{}')).toEqual({
      sortField: 'description',
      sortOrder: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-page') ?? '{}')).toEqual({
      first: 20,
      rows: 20,
    });
  });

  it('deletes a status after the confirmation is accepted, then reloads', () => {
    setup();
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    const component = fixture.componentInstance as unknown as {
      confirmDelete(status: PublishStatus): void;
    };
    component.confirmDelete(STATUSES[0]);

    expect(confirmation?.message).toContain('確定要刪除主代碼');
    expect(confirmation?.message).toContain('草稿');
    expect(service.delete).not.toHaveBeenCalled();

    confirmation?.accept?.();

    expect(service.delete).toHaveBeenCalledWith(1);
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
      confirmDelete(status: PublishStatus): void;
    };
    component.confirmDelete(STATUSES[1]);

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('已被課程或活動使用');
  });

  it('shows an empty message and no rows when the query fails', fakeAsync(() => {
    setup(throwError(() => new Error('boom')));
    tick();
    fixture.detectChanges();

    expect(fixture.debugElement.queryAll(By.css('[data-testid="status-row"]')).length).toBe(0);
  }));
});
