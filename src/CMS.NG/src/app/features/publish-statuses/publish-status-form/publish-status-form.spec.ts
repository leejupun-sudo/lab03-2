import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatusForm } from './publish-status-form';

const STATUS: PublishStatus = {
  pkid: 2,
  description: '上架中',
  isDraft: false,
  isPublished: true,
  isDiscontinued: false,
  courseCount: 12,
  promotion2Count: 3,
};

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

describe('PublishStatusForm', () => {
  let fixture: ComponentFixture<PublishStatusForm>;
  let component: FormInternals;
  let service: jasmine.SpyObj<PublishStatusService>;
  let router: Router;

  function setup(routeId: string | null): void {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', [
      'getById',
      'create',
      'update',
    ]);
    service.getById.and.returnValue(of(STATUS));
    service.create.and.returnValue(of({ ...STATUS, pkid: 4, description: '審核中' }));
    service.update.and.returnValue(of(STATUS));

    TestBed.configureTestingModule({
      imports: [PublishStatusForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: PublishStatusService, useValue: service },
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

    fixture = TestBed.createComponent(PublishStatusForm);
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

    it('renders the add title and does not fetch a status', () => {
      expect(titleText()).toBe('新增發布狀態');
      expect(component.isEdit).toBeFalse();
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('leaves 主代碼 editable because pkid is not an IDENTITY column', () => {
      expect(component.form.controls['pkid'].disabled).toBeFalse();
      expect(component.form.getRawValue()['pkid']).toBeNull();
    });

    it('defaults all three flags to false', () => {
      const value = component.form.getRawValue();
      expect(value['isDraft']).toBeFalse();
      expect(value['isPublished']).toBeFalse();
      expect(value['isDiscontinued']).toBeFalse();
    });

    it('blocks save and shows required errors when mandatory fields are empty', () => {
      component.save();
      fixture.detectChanges();

      expect(service.create).not.toHaveBeenCalled();
      expect(fixture.debugElement.query(By.css('[data-testid="error-pkid"]'))).toBeTruthy();
      expect(fixture.debugElement.query(By.css('[data-testid="error-description"]'))).toBeTruthy();
    });

    it('rejects a pkid outside the tinyint sentinel range', () => {
      component.form.patchValue({ pkid: 0, description: '無效' });
      component.save();

      expect(service.create).not.toHaveBeenCalled();

      component.form.patchValue({ pkid: 256, description: '無效' });
      component.save();

      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates the status with the supplied pkid, trimming the name', () => {
      component.form.patchValue({
        pkid: 4,
        description: '  審核中  ',
        isDraft: true,
        isPublished: false,
        isDiscontinued: false,
      });

      component.save();

      expect(service.update).not.toHaveBeenCalled();
      const request = service.create.calls.mostRecent().args[0] as PublishStatusRequest;
      expect(request).toEqual({
        pkid: 4,
        description: '審核中',
        isDraft: true,
        isPublished: false,
        isDiscontinued: false,
      });
      expect(router.navigate).toHaveBeenCalledWith(['/publish-statuses', 4]);
    });

    it('allows more than one flag to be set — no exclusivity rule exists in the schema', () => {
      component.form.patchValue({
        pkid: 5,
        description: '混合',
        isDraft: true,
        isPublished: true,
        isDiscontinued: true,
      });

      component.save();

      expect(service.create).toHaveBeenCalled();
    });

    it('reports a duplicate 主代碼 when the API returns 409', () => {
      service.create.and.returnValue(throwError(() => ({ status: 409 })));
      const messageService = TestBed.inject(MessageService);
      const addSpy = spyOn(messageService, 'add');

      component.form.patchValue({ pkid: 1, description: '重複' });
      component.save();

      expect(addSpy.calls.mostRecent().args[0].detail).toContain('已存在');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(() => setup('2'));

    it('renders the edit title and loads the status by pkid', () => {
      expect(titleText()).toBe('編輯發布狀態');
      expect(component.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(2);
    });

    it('patches every field', () => {
      expect(component.form.getRawValue()).toEqual({
        pkid: 2,
        description: '上架中',
        isDraft: false,
        isPublished: true,
        isDiscontinued: false,
      });
    });

    it('disables 主代碼 because Course and Promotion2 reference it', () => {
      expect(component.form.controls['pkid'].disabled).toBeTrue();
    });

    it('updates via PUT with the pkid in the request body', () => {
      component.form.patchValue({
        description: '已封存',
        isPublished: false,
        isDiscontinued: true,
      });

      component.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(service.update.calls.mostRecent().args[0]).toEqual({
        pkid: 2,
        description: '已封存',
        isDraft: false,
        isPublished: false,
        isDiscontinued: true,
      });
      expect(router.navigate).toHaveBeenCalledWith(['/publish-statuses', 2]);
    });

    it('cancel returns to the detail page', () => {
      component.cancel();

      expect(router.navigate).toHaveBeenCalledWith(['/publish-statuses', 2]);
    });
  });
});
