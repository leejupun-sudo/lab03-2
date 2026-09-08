import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import { AppRoleLookup } from '@core/models/app-role.model';
import { AppUser } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUserList } from './app-user-list';

const USERS: AppUser[] = [
  {
    pkid: 2,
    userId: 'helen',
    userName: 'Helen',
    isActive: false,
    passwordUpdatedTime: '2026-03-01T09:30:00',
    roleCount: 1,
    roleIds: [],
  },
  {
    pkid: 1,
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    isActive: true,
    passwordUpdatedTime: null,
    roleCount: 2,
    roleIds: [],
  },
];

const ROLES: AppRoleLookup[] = [
  { pkid: 1, roleId: 'Admin', roleName: 'Administrator', label: 'Administrator (Admin)' },
  { pkid: 2, roleId: 'User', roleName: 'User', label: 'User (User)' },
];

interface ListInternals {
  filters: {
    keyword: string | null;
    isActive: boolean | null;
    roleId: string | null;
    passwordUpdatedFrom: Date | null;
    passwordUpdatedTo: Date | null;
  };
  applyFilters(): void;
  clearFilters(): void;
  onSort(event: { field?: string; order?: number }): void;
  onPage(event: { first?: number; rows?: number }): void;
  confirmDelete(user: AppUser): void;
}

describe('AppUserList', () => {
  let fixture: ComponentFixture<AppUserList>;
  let component: ListInternals;
  let service: jasmine.SpyObj<AppUserService>;
  let confirmationService: ConfirmationService;

  function setup(queryResult: Observable<AppUser[]> = of(USERS)): void {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['query', 'delete']);
    service.query.and.returnValue(queryResult);
    service.delete.and.returnValue(of(void 0));

    const lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['getAppRoles']);
    lookupService.getAppRoles.and.returnValue(of(ROLES));

    TestBed.configureTestingModule({
      imports: [AppUserList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service },
        { provide: LookupService, useValue: lookupService },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(AppUserList);
    component = fixture.componentInstance as unknown as ListInternals;
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('creates and loads users on init', () => {
    setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per user with the schema columns and the 啟用 tag', () => {
    setup();

    const rows = fixture.debugElement.queryAll(By.css('[data-testid="user-row"]'));
    expect(rows.length).toBe(2);

    const helenText = (rows[0].nativeElement as HTMLElement).textContent ?? '';
    expect(helenText).toContain('helen');
    expect(helenText).toContain('Helen');
    expect(helenText).toContain('否');
    expect(helenText).toContain('1');

    const milesText = (rows[1].nativeElement as HTMLElement).textContent ?? '';
    expect(milesText).toContain('miles@uuu.com.tw');
    expect(milesText).toContain('是');
    expect(milesText).toContain('2');
  });

  it('renders 「—」 for a null passwordUpdatedTime and never a password hash', () => {
    setup();

    const rows = fixture.debugElement.queryAll(By.css('[data-testid="user-row"]'));
    const milesText = (rows[1].nativeElement as HTMLElement).textContent ?? '';
    expect(milesText).toContain('—');

    const pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(pageText.toLowerCase()).not.toContain('hash');
  });

  it('sends the drawer filter values to the query endpoint in wire shape and persists them', () => {
    setup();

    component.filters.keyword = ' helen ';
    component.filters.isActive = false;
    component.filters.roleId = 'User';
    component.filters.passwordUpdatedFrom = new Date(2026, 2, 1);
    component.filters.passwordUpdatedTo = new Date(2026, 2, 31);
    component.applyFilters();
    fixture.detectChanges();

    const expected = {
      keyword: 'helen',
      isActive: false,
      roleId: 'User',
      passwordUpdatedFrom: '2026-03-01',
      passwordUpdatedTo: '2026-03-31',
    };
    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0]).toEqual(expected);
    expect(JSON.parse(sessionStorage.getItem('app-user-list-filters') ?? '{}')).toEqual(expected);
  });

  it('restores saved filters from session storage on init, including dates', () => {
    sessionStorage.setItem(
      'app-user-list-filters',
      JSON.stringify({
        keyword: 'restored',
        isActive: true,
        roleId: 'Admin',
        passwordUpdatedFrom: '2026-03-01',
        passwordUpdatedTo: null,
      }),
    );

    setup();

    expect(service.query.calls.mostRecent().args[0]).toEqual(
      jasmine.objectContaining({ keyword: 'restored', isActive: true, roleId: 'Admin' }),
    );
    expect(component.filters.passwordUpdatedFrom).toEqual(new Date(2026, 2, 1));
  });

  it('clears the filters and re-queries with an empty filter', () => {
    setup();

    component.filters.keyword = 'helen';
    component.filters.isActive = false;
    component.applyFilters();
    component.clearFilters();

    expect(service.query.calls.mostRecent().args[0]).toEqual({
      keyword: null,
      isActive: null,
      roleId: null,
      passwordUpdatedFrom: null,
      passwordUpdatedTo: null,
    });
  });

  it('persists sort and page state to session storage', () => {
    setup();

    component.onSort({ field: 'userName', order: -1 });
    component.onPage({ first: 20, rows: 20 });

    expect(JSON.parse(sessionStorage.getItem('app-user-list-sort') ?? '{}')).toEqual({
      sortField: 'userName',
      sortOrder: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('app-user-list-page') ?? '{}')).toEqual({
      first: 20,
      rows: 20,
    });
  });

  it('deletes a user after the confirmation is accepted, then reloads', () => {
    setup();
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    component.confirmDelete(USERS[0]);

    expect(confirmation?.message).toContain('確定要刪除主代碼');
    expect(confirmation?.message).toContain('helen');
    expect(service.delete).not.toHaveBeenCalled();

    confirmation?.accept?.();

    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows an empty message and no rows when the query fails', fakeAsync(() => {
    setup(throwError(() => new Error('boom')));
    tick();
    fixture.detectChanges();

    expect(fixture.debugElement.queryAll(By.css('[data-testid="user-row"]')).length).toBe(0);
  }));
});
