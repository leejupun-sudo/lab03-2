import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import { AppRole } from '@core/models/app-role.model';
import { AppRoleService } from '@core/services/app-role.service';
import { AppRoleList } from './app-role-list';

const ROLES: AppRole[] = [
  {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 3,
    userIds: [],
  },
  {
    pkid: 2,
    roleId: 'User',
    roleName: 'User',
    permissionLevel: 100,
    description: '一般使用者',
    userCount: 9,
    userIds: [],
  },
];

describe('AppRoleList', () => {
  let fixture: ComponentFixture<AppRoleList>;
  let service: jasmine.SpyObj<AppRoleService>;
  let confirmationService: ConfirmationService;

  function setup(queryResult: Observable<AppRole[]> = of(ROLES)): void {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['query', 'delete']);
    service.query.and.returnValue(queryResult);
    service.delete.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      imports: [AppRoleList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: AppRoleService, useValue: service },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(AppRoleList);
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('creates and loads roles on init', () => {
    setup();

    expect(fixture.componentInstance).toBeTruthy();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('renders one row per role with the schema columns', () => {
    setup();

    const rows = fixture.debugElement.queryAll(By.css('[data-testid="role-row"]'));
    expect(rows.length).toBe(2);

    const firstRowText = (rows[0].nativeElement as HTMLElement).textContent ?? '';
    expect(firstRowText).toContain('Admin');
    expect(firstRowText).toContain('Administrator');
    expect(firstRowText).toContain('系統管理員');
    expect(firstRowText).toContain('3');
  });

  it('sends the drawer filter values to the query endpoint and persists them', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      filters: { keyword: string | null; permissionLevelFrom: number | null };
      applyFilters(): void;
    };

    component.filters.keyword = 'admin';
    component.filters.permissionLevelFrom = 1;
    component.applyFilters();
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(2);
    expect(service.query.calls.mostRecent().args[0]).toEqual(
      jasmine.objectContaining({ keyword: 'admin', permissionLevelFrom: 1 }),
    );
    expect(JSON.parse(sessionStorage.getItem('app-role-list-filters') ?? '{}')).toEqual(
      jasmine.objectContaining({ keyword: 'admin', permissionLevelFrom: 1 }),
    );
  });

  it('restores saved filters from session storage on init', () => {
    sessionStorage.setItem(
      'app-role-list-filters',
      JSON.stringify({ keyword: 'restored', permissionLevelFrom: null, permissionLevelTo: null }),
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

    component.filters.keyword = 'admin';
    component.applyFilters();
    component.clearFilters();

    expect(service.query.calls.mostRecent().args[0]).toEqual({
      keyword: null,
      permissionLevelFrom: null,
      permissionLevelTo: null,
    });
  });

  it('persists sort and page state to session storage', () => {
    setup();
    const component = fixture.componentInstance as unknown as {
      onSort(event: { field?: string; order?: number }): void;
      onPage(event: { first?: number; rows?: number }): void;
    };

    component.onSort({ field: 'permissionLevel', order: -1 });
    component.onPage({ first: 20, rows: 20 });

    expect(JSON.parse(sessionStorage.getItem('app-role-list-sort') ?? '{}')).toEqual({
      sortField: 'permissionLevel',
      sortOrder: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('app-role-list-page') ?? '{}')).toEqual({
      first: 20,
      rows: 20,
    });
  });

  it('deletes a role after the confirmation is accepted, then reloads', () => {
    setup();
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    const component = fixture.componentInstance as unknown as {
      confirmDelete(role: AppRole): void;
    };
    component.confirmDelete(ROLES[1]);

    expect(confirmation?.message).toContain('確定要刪除主代碼');
    expect(confirmation?.message).toContain('User');
    expect(service.delete).not.toHaveBeenCalled();

    confirmation?.accept?.();

    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows an empty message and no rows when the query fails', fakeAsync(() => {
    setup(throwError(() => new Error('boom')));
    tick();
    fixture.detectChanges();

    expect(fixture.debugElement.queryAll(By.css('[data-testid="role-row"]')).length).toBe(0);
  }));
});
