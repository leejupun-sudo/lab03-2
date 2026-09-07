import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import { Partner } from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';
import { PartnerList } from './partner-list';

function makePartner(overrides: Partial<Partner> = {}): Partner {
  return {
    pkid: 1,
    name: 'CompTIA',
    appKey: 'CompTIA',
    nameOnPartnerMenu: 'CompTIA',
    nameOnCourseDetailPage: 'CompTIA',
    displayOrder: 3,
    imageFilename: 'CompTIA.png',
    courseCount: 0,
    certificationCount: 0,
    partnerCourseGroupCount: 0,
    promotion2Count: 0,
    seminarCount: 0,
    ...overrides,
  };
}

// Server-ordered: DisplayOrder ASC, Name ASC, pkid ASC. The last two share both a
// DisplayOrder and a Name — the live table holds duplicate names.
const PARTNERS: Partner[] = [
  makePartner({
    pkid: 1,
    courseCount: 42,
    certificationCount: 4,
    partnerCourseGroupCount: 2,
    promotion2Count: 7,
  }),
  makePartner({
    pkid: 19,
    name: '國際標準課程',
    appKey: 'ISO',
    displayOrder: 9999,
    imageFilename: null,
    seminarCount: 34,
  }),
  makePartner({
    pkid: 31,
    name: '國際標準課程',
    appKey: 'PCB',
    displayOrder: 9999,
    imageFilename: null,
  }),
];

describe('PartnerList', () => {
  let fixture: ComponentFixture<PartnerList>;
  let service: jasmine.SpyObj<PartnerService>;
  let confirmationService: ConfirmationService;

  function setup(queryResult: Observable<Partner[]> = of(PARTNERS)): void {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['query', 'delete']);
    service.query.and.returnValue(queryResult);
    service.delete.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: PartnerService, useValue: service },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(PartnerList);
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('creates and loads partners on init', () => {
    setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per partner with the schema columns', () => {
    setup();

    const rows = fixture.debugElement.queryAll(By.css('[data-testid="partner-row"]'));
    expect(rows.length).toBe(3);

    const firstRowText = (rows[0].nativeElement as HTMLElement).textContent ?? '';
    expect(firstRowText).toContain('CompTIA');
    expect(firstRowText).toContain('3');
  });

  it('renders duplicate names as separate rows', () => {
    setup();

    const rowTexts = fixture.debugElement
      .queryAll(By.css('[data-testid="partner-row"]'))
      .map((row) => (row.nativeElement as HTMLElement).textContent ?? '');

    expect(rowTexts.filter((text) => text.includes('國際標準課程')).length).toBe(2);
  });

  it('shows the summed usage total, not the five separate counts', () => {
    setup();

    const cells = fixture.debugElement
      .queryAll(By.css('[data-testid="partner-usage"]'))
      .map((cell) => ((cell.nativeElement as HTMLElement).textContent ?? '').trim());

    expect(cells).toEqual(['55', '34', '0']);
  });

  it('defaults the sort to displayOrder ascending', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      sortState: { sortField: string; sortOrder: number };
    };

    expect(component.sortState).toEqual({ sortField: 'displayOrder', sortOrder: 1 });
  });

  it('sends the drawer filter value to the query endpoint and persists it', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null };
      applyFilters(): void;
    };

    component.filters.keyword = 'ISO';
    component.applyFilters();
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0]).toEqual({ keyword: 'ISO' });
    expect(JSON.parse(sessionStorage.getItem('partner-list-filters') ?? '{}')).toEqual(
      jasmine.objectContaining({ keyword: 'ISO' }),
    );
  });

  it('restores saved filters from session storage on init', () => {
    sessionStorage.setItem('partner-list-filters', JSON.stringify({ keyword: 'restored' }));

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

    component.filters.keyword = 'ISO';
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

    component.onSort({ field: 'appKey', order: -1 });
    component.onPage({ first: 40, rows: 20 });

    expect(JSON.parse(sessionStorage.getItem('partner-list-sort') ?? '{}')).toEqual({
      sortField: 'appKey',
      sortOrder: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('partner-list-page') ?? '{}')).toEqual({
      first: 40,
      rows: 20,
    });
  });

  it('deletes a partner after the confirmation is accepted, then reloads', () => {
    setup();
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    const component = fixture.componentInstance as unknown as {
      confirmDelete(partner: Partner): void;
    };
    component.confirmDelete(PARTNERS[2]);

    expect(confirmation?.message).toContain('確定要刪除主代碼');
    expect(confirmation?.message).toContain('國際標準課程');
    expect(service.delete).not.toHaveBeenCalled();

    confirmation?.accept?.();

    expect(service.delete).toHaveBeenCalledWith(31);
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
      confirmDelete(partner: Partner): void;
    };
    // Referenced only by Seminar — the reference SQL Server does not enforce.
    component.confirmDelete(PARTNERS[1]);

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('研討會使用');
  });

  it('shows an empty message and no rows when the query fails', fakeAsync(() => {
    setup(throwError(() => new Error('boom')));
    tick();
    fixture.detectChanges();

    expect(fixture.debugElement.queryAll(By.css('[data-testid="partner-row"]')).length).toBe(0);
  }));
});
