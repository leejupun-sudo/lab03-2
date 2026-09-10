import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { AppRoleLookup } from '@core/models/app-role.model';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUserForm } from './app-user-form';

const USER: AppUser = {
  pkid: 1,
  userId: 'miles@uuu.com.tw',
  userName: 'Miles Sun',
  isActive: true,
  passwordUpdatedTime: null,
  roleCount: 1,
  roleIds: ['Admin'],
};

const ROLES: AppRoleLookup[] = [
  { pkid: 1, roleId: 'Admin', roleName: 'Administrator', label: 'Administrator (Admin)' },
  { pkid: 2, roleId: 'User', roleName: 'User', label: 'User (User)' },
];

interface FormInternals {
  form: {
    controls: Record<string, { disabled: boolean; setValue(value: unknown): void }>;
    getRawValue(): Record<string, unknown>;
    patchValue(value: Record<string, unknown>): void;
    valid: boolean;
  };
  isEdit: boolean;
  title: string;
  save(): void;
  cancel(): void;
}

describe('AppUserForm', () => {
  let fixture: ComponentFixture<AppUserForm>;
  let component: FormInternals;
  let service: jasmine.SpyObj<AppUserService>;
  let router: Router;

  function setup(routeId: string | null): void {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', [
      'getById',
      'create',
      'update',
    ]);
    service.getById.and.returnValue(of(USER));
    service.create.and.returnValue(of({ ...USER, pkid: 5, userId: 'bob' }));
    service.update.and.returnValue(of(USER));

    const lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['getAppRoles']);
    lookupService.getAppRoles.and.returnValue(of(ROLES));

    TestBed.configureTestingModule({
      imports: [AppUserForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: AppUserService, useValue: service },
        { provide: LookupService, useValue: lookupService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) },
          },
        },
      ],
    });

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(AppUserForm);
    component = fixture.componentInstance as unknown as FormInternals;
    fixture.detectChanges();
  }

  function titleText(): string {
    const el = fixture.debugElement.query(By.css('[data-testid="form-title"]'));
    return ((el.nativeElement as HTMLElement).textContent ?? '').trim();
  }

  // ---------- Add mode ----------

  describe('add mode', () => {
    beforeEach(() => setup(null));

    it('renders the add title and does not fetch a user', () => {
      expect(titleText()).toBe('新增使用者');
      expect(component.isEdit).toBeFalse();
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('defaults 啟用 to true and leaves 帳號 editable', () => {
      expect(component.form.getRawValue()['isActive']).toBeTrue();
      expect(component.form.controls['userId'].disabled).toBeFalse();
    });

    it('has no password control and explains the default-password rule instead', () => {
      expect(fixture.debugElement.query(By.css('input[type="password"]'))).toBeNull();
      expect(Object.keys(component.form.controls)).not.toContain('password');
      expect(Object.keys(component.form.controls)).not.toContain('passwordHash');

      const note = fixture.debugElement.query(By.css('[data-testid="default-password-note"]'));
      expect(((note.nativeElement as HTMLElement).textContent ?? '').trim()).toContain(
        '系統預設密碼',
      );
    });

    it('blocks save and shows required errors when mandatory fields are empty', () => {
      component.save();
      fixture.detectChanges();

      expect(service.create).not.toHaveBeenCalled();
      expect(fixture.debugElement.query(By.css('[data-testid="error-user-id"]'))).toBeTruthy();
      expect(fixture.debugElement.query(By.css('[data-testid="error-user-name"]'))).toBeTruthy();
    });

    it('creates the user, trimming text fields and carrying the selected roles', () => {
      component.form.patchValue({
        userId: '  bob  ',
        userName: '  Bob Chen  ',
        isActive: false,
        roleIds: ['User'],
      });

      component.save();

      expect(service.update).not.toHaveBeenCalled();
      const request = service.create.calls.mostRecent().args[0] as AppUserRequest;
      expect(request).toEqual({
        pkid: 0,
        userId: 'bob',
        userName: 'Bob Chen',
        isActive: false,
        roleIds: ['User'],
      });
      expect(Object.keys(request)).not.toContain('passwordHash');
      expect(router.navigate).toHaveBeenCalledWith(['/app-users', 5]);
    });

    it('reports a duplicate 帳號 when the API returns 409', () => {
      service.create.and.returnValue(throwError(() => ({ status: 409 })));
      const messageService = TestBed.inject(MessageService);
      const addSpy = spyOn(messageService, 'add');

      component.form.patchValue({
        userId: 'miles@uuu.com.tw',
        userName: 'Duplicate',
        isActive: true,
        roleIds: [],
      });
      component.save();

      expect(addSpy.calls.mostRecent().args[0].detail).toContain('已存在');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(() => setup('1'));

    it('renders the edit title and loads the user by pkid', () => {
      expect(titleText()).toBe('編輯使用者');
      expect(component.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(1);
    });

    it('patches every field including the assigned roles', () => {
      expect(component.form.getRawValue()).toEqual({
        userId: 'miles@uuu.com.tw',
        userName: 'Miles Sun',
        isActive: true,
        roleIds: ['Admin'],
      });
    });

    it('disables 帳號 because AppUserRole references it', () => {
      expect(component.form.controls['userId'].disabled).toBeTrue();
    });

    it('does not show the default-password note', () => {
      expect(fixture.debugElement.query(By.css('[data-testid="default-password-note"]'))).toBeNull();
    });

    it('updates via PUT with the pkid in the request body', () => {
      component.form.patchValue({
        userName: 'Miles Sun 2',
        isActive: false,
        roleIds: ['Admin', 'User'],
      });

      component.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(service.update.calls.mostRecent().args[0]).toEqual({
        pkid: 1,
        userId: 'miles@uuu.com.tw',
        userName: 'Miles Sun 2',
        isActive: false,
        roleIds: ['Admin', 'User'],
      });
      expect(router.navigate).toHaveBeenCalledWith(['/app-users', 1]);
    });

    it('cancel returns to the detail page', () => {
      component.cancel();

      expect(router.navigate).toHaveBeenCalledWith(['/app-users', 1]);
    });
  });
});
