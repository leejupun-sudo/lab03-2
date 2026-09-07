import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of } from 'rxjs';

import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroupForm } from './course-group-form';

const GROUP: CourseGroup = {
  pkid: 2,
  description: 'SharePoint系列課程',
  courseCount: 12,
  partnerCourseGroupCount: 0,
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

describe('CourseGroupForm', () => {
  let fixture: ComponentFixture<CourseGroupForm>;
  let component: FormInternals;
  let service: jasmine.SpyObj<CourseGroupService>;
  let router: Router;

  function setup(routeId: string | null): void {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', [
      'getById',
      'create',
      'update',
    ]);
    service.getById.and.returnValue(of(GROUP));
    service.create.and.returnValue(of({ ...GROUP, pkid: 4, description: 'Kubernetes系列課程' }));
    service.update.and.returnValue(of(GROUP));

    TestBed.configureTestingModule({
      imports: [CourseGroupForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: CourseGroupService, useValue: service },
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

    fixture = TestBed.createComponent(CourseGroupForm);
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

    it('renders the add title and does not fetch a group', () => {
      expect(titleText()).toBe('新增課程群組');
      expect(component.isEdit).toBeFalse();
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('shows no 主代碼 control at all — pkid is IDENTITY', () => {
      expect(component.form.controls['pkid']).toBeUndefined();
      expect(fixture.debugElement.query(By.css('[data-testid="form-pkid"]'))).toBeNull();
    });

    it('blocks save and shows the required error when the name is empty', () => {
      component.save();
      fixture.detectChanges();

      expect(service.create).not.toHaveBeenCalled();
      expect(fixture.debugElement.query(By.css('[data-testid="error-description"]'))).toBeTruthy();
    });

    it('creates the group with pkid 0, trimming the name', () => {
      component.form.patchValue({ description: '  Kubernetes系列課程  ' });

      component.save();

      expect(service.update).not.toHaveBeenCalled();
      const request = service.create.calls.mostRecent().args[0] as CourseGroupRequest;
      expect(request).toEqual({ pkid: 0, description: 'Kubernetes系列課程' });
      expect(router.navigate).toHaveBeenCalledWith(['/course-groups', 4]);
    });

    /** Description carries no uniqueness rule, so a repeated name is a normal save. */
    it('submits a name that already exists without any duplicate check', () => {
      component.form.patchValue({ description: 'SharePoint系列課程' });

      component.save();

      expect(service.create).toHaveBeenCalled();
      expect(router.navigate).toHaveBeenCalled();
    });

    it('cancel returns to the list', () => {
      component.cancel();

      expect(router.navigate).toHaveBeenCalledWith(['/course-groups']);
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(() => setup('2'));

    it('renders the edit title and loads the group by pkid', () => {
      expect(titleText()).toBe('編輯課程群組');
      expect(component.isEdit).toBeTrue();
      expect(service.getById).toHaveBeenCalledWith(2);
    });

    it('patches the description', () => {
      expect(component.form.getRawValue()).toEqual({ description: 'SharePoint系列課程' });
    });

    it('displays the pkid read-only rather than as an input', () => {
      const el = fixture.debugElement.query(By.css('[data-testid="form-pkid"]'));
      expect(el).toBeTruthy();
      expect(((el.nativeElement as HTMLElement).textContent ?? '').trim()).toBe('2');
      expect(el.nativeElement.tagName).not.toBe('INPUT');
    });

    it('updates via PUT with the pkid in the request body', () => {
      component.form.patchValue({ description: 'SharePoint進階系列' });

      component.save();

      expect(service.create).not.toHaveBeenCalled();
      expect(service.update.calls.mostRecent().args[0]).toEqual({
        pkid: 2,
        description: 'SharePoint進階系列',
      });
      expect(router.navigate).toHaveBeenCalledWith(['/course-groups', 2]);
    });

    it('cancel returns to the detail page', () => {
      component.cancel();

      expect(router.navigate).toHaveBeenCalledWith(['/course-groups', 2]);
    });
  });
});
