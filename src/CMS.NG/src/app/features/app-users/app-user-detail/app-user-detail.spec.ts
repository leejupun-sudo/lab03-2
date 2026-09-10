import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { AppRoleLookup } from '@core/models/app-role.model';
import { AppUser } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUserDetail } from './app-user-detail';

const USER: AppUser = {
  pkid: 1,
  userId: 'miles@uuu.com.tw',
  userName: 'Miles Sun',
  isActive: true,
  passwordUpdatedTime: '2026-03-01T01:30:00',
  roleCount: 2,
  roleIds: ['Admin', 'Legacy'],
};

const ROLES: AppRoleLookup[] = [
  { pkid: 1, roleId: 'Admin', roleName: 'Administrator', label: 'Administrator (Admin)' },
  { pkid: 2, roleId: 'User', roleName: 'User', label: 'User (User)' },
];

interface DetailInternals {
  confirmResetPassword(): void;
}

describe('AppUserDetail', () => {
  let fixture: ComponentFixture<AppUserDetail>;
  let service: jasmine.SpyObj<AppUserService>;
  let confirmationService: ConfirmationService;

  function setup(routeId: string, user: AppUser | null = USER): void {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['getById', 'resetPassword']);
    service.getById.and.returnValue(user ? of(user) : throwError(() => ({ status: 404 })));
    service.resetPassword.and.returnValue(of({ ...USER, passwordUpdatedTime: null }));

    const lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['getAppRoles']);
    lookupService.getAppRoles.and.returnValue(of(ROLES));

    TestBed.configureTestingModule({
      imports: [AppUserDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service },
        { provide: LookupService, useValue: lookupService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(AppUserDetail);
    fixture.detectChanges();
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  it('loads the user identified by the route param', () => {
    setup('1');

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders every AppUser field', () => {
    setup('1');

    expect(textOf('detail-pkid')).toBe('1');
    expect(textOf('detail-user-id')).toBe('miles@uuu.com.tw');
    expect(textOf('detail-user-name')).toBe('Miles Sun');
    expect(textOf('detail-is-active')).toBe('是');
    expect(textOf('detail-password-updated-time')).toMatch(/^2026-03-01 \d{2}:\d{2}$/);
  });

  it('renders 「—」 and the default-password hint when the time is null', () => {
    setup('1', { ...USER, passwordUpdatedTime: null });

    expect(textOf('detail-password-updated-time')).toContain('—');
    expect(textOf('detail-password-updated-time')).toContain('系統預設密碼');
  });

  it('never renders a password hash anywhere on the page', () => {
    setup('1');

    const pageText = ((fixture.nativeElement as HTMLElement).textContent ?? '').toLowerCase();
    expect(pageText).not.toContain('hash');
  });

  it('resolves assigned role ids to lookup labels, falling back to the raw id', () => {
    setup('1');

    const roles = textOf('detail-roles');
    expect(roles).toContain('Administrator (Admin)');
    expect(roles).toContain('Legacy');
  });

  it('links a resolved role to its /app-roles detail page', () => {
    setup('1');

    const link = fixture.debugElement.query(By.css('[data-testid="detail-roles"] a'));
    expect((link.nativeElement as HTMLAnchorElement).getAttribute('href')).toBe('/app-roles/1');
  });

  it('shows an empty state when the user has no roles', () => {
    setup('1', { ...USER, roleCount: 0, roleIds: [] });

    expect(textOf('detail-roles-empty')).toContain('尚未指派角色');
  });

  it('resets the password only after the confirmation is accepted', () => {
    setup('1');
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    (fixture.componentInstance as unknown as DetailInternals).confirmResetPassword();

    expect(confirmation?.message).toContain('miles@uuu.com.tw');
    expect(confirmation?.message).toContain('系統預設密碼');
    expect(service.resetPassword).not.toHaveBeenCalled();

    confirmation?.accept?.();
    fixture.detectChanges();

    expect(service.resetPassword).toHaveBeenCalledWith(1);
    expect(textOf('detail-password-updated-time')).toContain('—');
  });

  it('shows a not-found state when the user does not exist', () => {
    setup('999', null);

    expect(textOf('detail-not-found')).toContain('查無此使用者');
    expect(fixture.debugElement.query(By.css('[data-testid="reset-password"]'))).toBeNull();
  });
});
