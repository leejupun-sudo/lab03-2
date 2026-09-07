import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { AppRole, AppRoleRequest } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRoleForm } from './app-role-form';

const ROLE: AppRole = {
  pkid: 1,
  roleId: 'Admin',
  roleName: 'Administrator',
  permissionLevel: 1,
  description: '系統管理員',
  userCount: 1,
  userIds: ['helen'],
};

const USERS: AppUserLookup[] = [
  { userId: 'helen', userName: 'helen', isActive: true, label: 'helen (helen)' },
  { userId: 'bob', userName: 'Bob', isActive: true, label: 'Bob (bob)' },
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

describe('AppRoleForm', () => {
  let fixture: ComponentFixture<AppRoleForm>;
  let component: FormInternals;
  let service: jasmine.SpyObj<AppRoleService>;
  let router: Router;

  function setup(routeId: string | null): void {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', [
      'getById',
      'create',
      'update',
    ]);
    service.getById.and.returnValue(of(ROLE));
    service.create.and.returnValue(of({ ...ROLE, pkid: 5, roleId: 'Editor' }));
    service.update.and.returnValue(of(ROLE));

    const lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['getAppUsers']);
    lookupService.getAppUsers.and.returnValue(of(USERS));

    TestBed.configureTestingModule({
      imports: [AppRoleForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: AppRoleService, useValue: service },
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

    fixture = TestBed.createComponent(AppRoleForm);
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

    it('renders the add title and does not fetch a role', () => {
      expect(titleText()).toBe('新增角色');
      expect(component.isEdit).toBeFalse();
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('defaults 權限等級 to 100 and leaves 角色代碼 editable', () => {
      expect(component.form.getRawValue()['permissionLevel']).toBe(100);
      expect(component.form.controls['roleId'].disabled).toBeFalse();
    });

    it('blocks save and shows required errors when mandatory fields are empty', () => {
      component.save();
      fixture.detectChanges();

      expect(service.create).not.toHaveBeenCalled();
      expect(fixture.debugElement.query(By.css('[data-testid="error-role-id"]'))).toBeTruthy();
      expect(fixture.debugElement.query(By.css('[data-testid="error-role-name"]'))).toBeTruthy();
    });

    it('creates the role, trimming text fields and carrying the selected users', () => {
      component.form.patchValue({
        roleId: '  Editor  ',
        roleName: '  Content Editor  ',
        permissionLevel: 50,
        description: '  內容編輯  ',
        userIds: ['helen', 'bob'],
      });

      component.save();

      expect(service.update).not.toHaveBeenCalled();
      const request = service.create.calls.mostRecent().args[0] as AppRoleRequest;
      expect(request).toEqual({
        pkid: 0,
        roleId: 'Editor',
        roleName: 'Content Editor',
        permissionLevel: 50,
        description: '內容編輯',
        userIds: ['helen', 'bob'],
      });
      expect(router.navigate).toHaveBeenCalledWith(['/app-roles', 5]);
    });

    it('sends null for an empty description', () => {
      component.form.patchValue({
        roleId: 'Editor',
        roleName: 'Content Editor',
        permissionLevel: 50,
        description: '   ',
        userIds: [],
      });

      component.save();

      expect((service.create.calls.mostRecent().args[0] as AppRoleRequest).description).toBeNull();
    });

    it('reports a duplicate role code when the API returns 409', () => {
      service.create.and.returnValue(throwError(() => ({ status: 409 })));
      const messageService = TestBed.inject(MessageService);
      const addSpy = spyOn(messageService, 'add');

      component.form.patchValue({
        roleId: 'Admin',
        roleName: 'Duplicate',
        permissionLevel: 1,
        description: null,
        userIds: [],
      });
      component.save();

      expect(addSpy.calls.mostRecent().args[0].detail).toContain('已存在');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(() => setup('1'));

    it('renders the edit title and loads the role by pkid', () => {
      expect(titleText()).toBe('編輯角色');
      expect(component.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(1);
    });

    it('patches every field including the assigned users', () => {
      expect(component.form.getRawValue()).toEqual({
        roleId: 'Admin',
        roleName: 'Administrator',
        permissionLevel: 1,
        description: '系統管理員',
        userIds: ['helen'],
      });
    });

    it('disables 角色代碼 because AppUserRole references it', () => {
      expect(component.form.controls['roleId'].disabled).toBeTrue();
    });

    it('updates via PUT with the pkid in the request body', () => {
      component.form.patchValue({
        roleName: 'Administrator 2',
        permissionLevel: 2,
        description: '更新後的描述',
        userIds: ['helen', 'bob'],
      });

      component.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(service.update.calls.mostRecent().args[0]).toEqual({
        pkid: 1,
        roleId: 'Admin',
        roleName: 'Administrator 2',
        permissionLevel: 2,
        description: '更新後的描述',
        userIds: ['helen', 'bob'],
      });
      expect(router.navigate).toHaveBeenCalledWith(['/app-roles', 1]);
    });

    it('cancel returns to the detail page', () => {
      component.cancel();

      expect(router.navigate).toHaveBeenCalledWith(['/app-roles', 1]);
    });
  });
});
