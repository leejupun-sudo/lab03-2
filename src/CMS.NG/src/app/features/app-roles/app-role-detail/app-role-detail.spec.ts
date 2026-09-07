import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { AppRole } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRoleDetail } from './app-role-detail';

const ROLE: AppRole = {
  pkid: 1,
  roleId: 'Admin',
  roleName: 'Administrator',
  permissionLevel: 1,
  description: '系統管理員',
  userCount: 2,
  userIds: ['helen', 'miles@uuu.com.tw'],
};

const USERS: AppUserLookup[] = [
  { userId: 'helen', userName: 'helen', isActive: true, label: 'helen (helen)' },
  {
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    isActive: true,
    label: 'Miles Sun (miles@uuu.com.tw)',
  },
];

describe('AppRoleDetail', () => {
  let fixture: ComponentFixture<AppRoleDetail>;
  let service: jasmine.SpyObj<AppRoleService>;

  function setup(routeId: string, role: AppRole | null = ROLE): void {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['getById']);
    service.getById.and.returnValue(
      role ? of(role) : throwError(() => ({ status: 404 })),
    );

    const lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['getAppUsers']);
    lookupService.getAppUsers.and.returnValue(of(USERS));

    TestBed.configureTestingModule({
      imports: [AppRoleDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: AppRoleService, useValue: service },
        { provide: LookupService, useValue: lookupService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    fixture = TestBed.createComponent(AppRoleDetail);
    fixture.detectChanges();
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  it('loads the role identified by the route param', () => {
    setup('1');

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders every AppRole field', () => {
    setup('1');

    expect(textOf('detail-pkid')).toBe('1');
    expect(textOf('detail-role-id')).toBe('Admin');
    expect(textOf('detail-role-name')).toBe('Administrator');
    expect(textOf('detail-permission-level')).toBe('1');
    expect(textOf('detail-description')).toBe('系統管理員');
  });

  it('resolves assigned user ids to their lookup labels', () => {
    setup('1');

    const users = textOf('detail-users');
    expect(users).toContain('helen (helen)');
    expect(users).toContain('Miles Sun (miles@uuu.com.tw)');
  });

  it('shows an empty state when the role has no users', () => {
    setup('1', { ...ROLE, userCount: 0, userIds: [] });

    expect(textOf('detail-users-empty')).toContain('尚未指派使用者');
  });

  it('shows a not-found state when the role does not exist', () => {
    setup('999', null);

    expect(textOf('detail-not-found')).toContain('查無此角色');
  });
});
