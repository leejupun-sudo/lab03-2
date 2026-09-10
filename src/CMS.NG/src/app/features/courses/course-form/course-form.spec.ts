import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { Course, CourseRequest } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { makeCourse } from '@core/services/course.service.spec';
import { CourseForm } from './course-form';

const COURSE: Course = makeCourse({
  officialTitle: null,
  outline: '<p>Day 1</p>',
  certificationPkids: [34, 36],
  jobCategoryPkids: [19, 22],
});

interface FormInternals {
  form: {
    controls: Record<string, { disabled: boolean; dirty: boolean; setValue(value: unknown): void; markAsDirty(): void }>;
    getRawValue(): Record<string, unknown>;
    patchValue(value: Record<string, unknown>): void;
    valid: boolean;
    hasError(code: string): boolean;
  };
  isEdit: boolean;
  title: string;
  save(): void;
  cancel(): void;
}

describe('CourseForm', () => {
  let fixture: ComponentFixture<CourseForm>;
  let component: FormInternals;
  let service: jasmine.SpyObj<CourseService>;
  let lookupService: jasmine.SpyObj<LookupService>;
  let router: Router;

  function setup(routeId: string | null): void {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById', 'create', 'update']);
    service.getById.and.returnValue(of(COURSE));
    service.create.and.returnValue(of(makeCourse({ pkid: 3341, courseId: 'AZ-900' })));
    service.update.and.returnValue(of(COURSE));

    lookupService = jasmine.createSpyObj<LookupService>('LookupService', [
      'getPartners',
      'getCourseGroups',
      'getPublishStatuses',
      'getCertifications',
      'getJobCategories',
    ]);
    lookupService.getPartners.and.returnValue(of([{ pkid: 1, name: 'Microsoft', appKey: 'MS', label: 'Microsoft (MS)' }]));
    lookupService.getCourseGroups.and.returnValue(of([{ pkid: 3, description: 'Azure系列課程', label: 'Azure系列課程' }]));
    lookupService.getPublishStatuses.and.returnValue(of([{ pkid: 1, description: '草稿', label: '草稿' }]));
    lookupService.getCertifications.and.returnValue(of([{ pkid: 34, title: 'FCP-SN', partnerName: 'Fortinet', label: 'FCP-SN (Fortinet)' }]));
    lookupService.getJobCategories.and.returnValue(of([{ pkid: 16, description: '雲端技術', label: '雲端技術' }]));

    TestBed.configureTestingModule({
      imports: [CourseForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookupService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) } },
        },
      ],
    });

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(CourseForm);
    component = fixture.componentInstance as unknown as FormInternals;
    fixture.detectChanges();
  }

  function fillValidForm(overrides: Record<string, unknown> = {}): void {
    const patch: Record<string, unknown> = {
      courseId: 'AZ-900',
      prodCourseId: 'AZ-900',
      title: 'Azure基礎',
      officialTitle: '',
      friendlyUrl: 'Azure-Fundamentals',
      partnerPkid: 1,
      courseGroupPkid: 3,
      publishStatusPkid: 1,
      scheduleOn: new Date(2026, 0, 16),
      scheduleOff: new Date(2036, 0, 16),
      displayOrder: 0,
      hour: 7,
      listPrice: 9000,
      learningCredit: 3.5,
      canRepeat: false,
      certificationPkids: [34],
      jobCategoryPkids: [16],
      material: '',
      objective: '  了解 Azure  ',
      outline: '',
      ...overrides,
    };
    // patchValue writes to disabled controls too, so never touch courseId once it is frozen.
    if (component.form.controls['courseId'].disabled) {
      delete patch['courseId'];
    }
    component.form.patchValue(patch);
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  // ---------- Add mode ----------

  it('renders the add title and loads all five lookups but no course', () => {
    setup(null);

    expect(component.isEdit).toBeFalse();
    expect(textOf('form-title')).toBe('新增課程');
    expect(service.getById).not.toHaveBeenCalled();
    expect(lookupService.getPartners).toHaveBeenCalledTimes(1);
    expect(lookupService.getCertifications).toHaveBeenCalledTimes(1);
    expect(lookupService.getJobCategories).toHaveBeenCalledTimes(1);
  });

  it('shows no pkid control in add mode (pkid is IDENTITY)', () => {
    setup(null);

    expect(fixture.debugElement.query(By.css('[data-testid="form-pkid"]'))).toBeNull();
    expect(component.form.controls['pkid']).toBeUndefined();
  });

  it('keeps courseId editable in add mode', () => {
    setup(null);

    expect(component.form.controls['courseId'].disabled).toBeFalse();
  });

  it('defaults 下架日期 to 上架日期 + 10 years when 上架日期 is picked in add mode', () => {
    setup(null);

    component.form.controls['scheduleOn'].setValue(new Date(2026, 0, 16));

    const off = component.form.getRawValue()['scheduleOff'] as Date;
    expect(off.getFullYear()).toBe(2036);
    expect(off.getMonth()).toBe(0);
    expect(off.getDate()).toBe(16);
  });

  it('blocks the save when a required field is blank', () => {
    setup(null);
    fillValidForm({ friendlyUrl: '' });

    component.save();

    expect(service.create).not.toHaveBeenCalled();
  });

  it('blocks the save when no partner is chosen', () => {
    setup(null);
    fillValidForm({ partnerPkid: null });

    component.save();

    expect(service.create).not.toHaveBeenCalled();
  });

  it('blocks the save when 下架日期 precedes 上架日期', () => {
    setup(null);
    fillValidForm({ scheduleOn: new Date(2023, 1, 17), scheduleOff: new Date(2013, 11, 17) });

    component.save();

    expect(component.form.hasError('scheduleRange')).toBeTrue();
    expect(service.create).not.toHaveBeenCalled();
  });

  it('creates the course with ISO dates, trimmed text, nulls for blanks and both junction lists', () => {
    setup(null);
    fillValidForm();

    component.save();

    expect(service.create).toHaveBeenCalledTimes(1);
    const request = service.create.calls.mostRecent().args[0] as CourseRequest;
    expect(request.pkid).toBe(0);
    expect(request.courseId).toBe('AZ-900');
    expect(request.scheduleOn).toBe('2026-01-16');
    expect(request.scheduleOff).toBe('2036-01-16');
    expect(request.officialTitle).toBeNull();
    expect(request.material).toBeNull();
    expect(request.outline).toBeNull();
    expect(request.objective).toBe('了解 Azure');
    expect(request.learningCredit).toBe(3.5);
    expect(request.certificationPkids).toEqual([34]);
    expect(request.jobCategoryPkids).toEqual([16]);
    expect(router.navigate).toHaveBeenCalledWith(['/courses', 3341]);
  });

  it('sends null for a cleared course group', () => {
    setup(null);
    fillValidForm({ courseGroupPkid: null });

    component.save();

    expect((service.create.calls.mostRecent().args[0] as CourseRequest).courseGroupPkid).toBeNull();
  });

  it('reports a duplicate courseId when create returns 409', () => {
    setup(null);
    service.create.and.returnValue(throwError(() => ({ status: 409 })));
    const addSpy = spyOn(TestBed.inject(MessageService), 'add');

    fillValidForm({ courseId: 'PLF' });
    component.save();

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('簡介代碼「PLF」已存在');
  });

  // ---------- Edit mode ----------

  it('renders the edit title and loads the course by pkid', () => {
    setup('35');

    expect(component.isEdit).toBeTrue();
    expect(textOf('form-title')).toBe('編輯課程');
    expect(service.getById).toHaveBeenCalledWith(35);
  });

  it('shows pkid read-only and disables courseId in edit mode', () => {
    setup('35');

    expect(textOf('form-pkid')).toBe('35');
    expect(component.form.controls['courseId'].disabled).toBeTrue();
  });

  it('patches every field from the loaded course, dates as Date and nulls as empty strings', () => {
    setup('35');

    const raw = component.form.getRawValue();
    expect(raw['courseId']).toBe('PLF');
    expect(raw['title']).toBe('Oracle資料庫之PL／SQL基礎');
    expect(raw['officialTitle']).toBe('');
    expect(raw['partnerPkid']).toBe(2);
    expect(raw['courseGroupPkid']).toBe(18);
    expect(raw['publishStatusPkid']).toBe(3);
    expect((raw['scheduleOn'] as Date).getFullYear()).toBe(2015);
    expect((raw['scheduleOff'] as Date).getMonth()).toBe(10);
    expect(raw['listPrice']).toBe(49000);
    expect(raw['outline']).toBe('<p>Day 1</p>');
    expect(raw['certificationPkids']).toEqual([34, 36]);
    expect(raw['jobCategoryPkids']).toEqual([19, 22]);
  });

  it('does not touch 下架日期 when 上架日期 changes in edit mode', () => {
    setup('35');

    component.form.controls['scheduleOn'].setValue(new Date(2020, 0, 1));

    const off = component.form.getRawValue()['scheduleOff'] as Date;
    expect(off.getFullYear()).toBe(2021);
  });

  it('updates via PUT with the pkid in the body and the original courseId', () => {
    setup('35');
    fillValidForm({ title: 'Oracle資料庫之PL／SQL基礎（改版）' });

    component.save();

    expect(service.update).toHaveBeenCalledTimes(1);
    const request = service.update.calls.mostRecent().args[0] as CourseRequest;
    expect(request.pkid).toBe(35);
    // The frozen control still contributes its loaded value via getRawValue().
    expect(request.courseId).toBe('PLF');
    expect(service.create).not.toHaveBeenCalled();
  });

  it('cancel returns to the detail page in edit mode', () => {
    setup('35');

    component.cancel();

    expect(router.navigate).toHaveBeenCalledWith(['/courses', 35]);
  });

  it('cancel returns to the list in add mode', () => {
    setup(null);

    component.cancel();

    expect(router.navigate).toHaveBeenCalledWith(['/courses']);
  });
});
