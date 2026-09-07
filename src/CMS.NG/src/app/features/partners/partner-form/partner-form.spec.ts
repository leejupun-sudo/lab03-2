import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { Partner, PartnerRequest } from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';
import { PartnerForm } from './partner-form';

const PARTNER: Partner = {
  pkid: 19,
  name: '國際標準課程',
  appKey: 'ISO',
  nameOnPartnerMenu: '國際標準',
  nameOnCourseDetailPage: 'ISO',
  displayOrder: 1,
  imageFilename: 'iso.svg',
  courseCount: 12,
  certificationCount: 0,
  partnerCourseGroupCount: 0,
  promotion2Count: 0,
  seminarCount: 0,
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

describe('PartnerForm', () => {
  let fixture: ComponentFixture<PartnerForm>;
  let component: FormInternals;
  let service: jasmine.SpyObj<PartnerService>;
  let router: Router;

  function setup(routeId: string | null): void {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', [
      'getById',
      'create',
      'update',
    ]);
    service.getById.and.returnValue(of(PARTNER));
    service.create.and.returnValue(of({ ...PARTNER, pkid: 67, name: 'Cisco', appKey: 'Cisco' }));
    service.update.and.returnValue(of(PARTNER));

    TestBed.configureTestingModule({
      imports: [PartnerForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: PartnerService, useValue: service },
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

    fixture = TestBed.createComponent(PartnerForm);
    component = fixture.componentInstance as unknown as FormInternals;
    fixture.detectChanges();
  }

  function fillValidForm(overrides: Record<string, unknown> = {}): void {
    component.form.patchValue({
      name: 'Cisco',
      appKey: 'Cisco',
      nameOnPartnerMenu: 'Cisco 網路認證課程',
      nameOnCourseDetailPage: 'Cisco',
      displayOrder: 9999,
      imageFilename: 'Cisco.svg',
      ...overrides,
    });
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  // ---------- Add mode ----------

  it('renders the add title when there is no route id', () => {
    setup(null);

    expect(component.isEdit).toBeFalse();
    expect(textOf('form-title')).toBe('新增合作廠商');
    expect(service.getById).not.toHaveBeenCalled();
  });

  it('shows no pkid control in add mode (pkid is IDENTITY)', () => {
    setup(null);

    expect(fixture.debugElement.query(By.css('[data-testid="form-pkid"]'))).toBeNull();
    expect(component.form.controls['pkid']).toBeUndefined();
  });

  it('defaults displayOrder to 9999 so a new partner parks at the end', () => {
    setup(null);

    expect(component.form.getRawValue()['displayOrder']).toBe(9999);
  });

  it('blocks the save when a required field is blank', () => {
    setup(null);
    fillValidForm({ appKey: '' });

    component.save();

    expect(service.create).not.toHaveBeenCalled();
  });

  it('creates the partner and navigates to its detail page', () => {
    setup(null);
    fillValidForm();

    component.save();

    expect(service.create).toHaveBeenCalledTimes(1);
    const request = service.create.calls.mostRecent().args[0] as PartnerRequest;
    expect(request.pkid).toBe(0);
    expect(request.name).toBe('Cisco');
    expect(request.appKey).toBe('Cisco');
    expect(request.displayOrder).toBe(9999);
    expect(router.navigate).toHaveBeenCalledWith(['/partners', 67]);
  });

  it('sends null rather than an empty string for a blank imageFilename', () => {
    setup(null);
    fillValidForm({ imageFilename: '   ' });

    component.save();

    const request = service.create.calls.mostRecent().args[0] as PartnerRequest;
    expect(request.imageFilename).toBeNull();
  });

  it('accepts an extensionless imageFilename', () => {
    setup(null);
    fillValidForm({ imageFilename: '恆逸' });

    component.save();

    const request = service.create.calls.mostRecent().args[0] as PartnerRequest;
    expect(request.imageFilename).toBe('恆逸');
  });

  it('rejects a displayOrder above 9999', () => {
    setup(null);
    fillValidForm({ displayOrder: 10000 });

    component.save();

    expect(service.create).not.toHaveBeenCalled();
  });

  it('reports a duplicate AppKey when create returns 409', () => {
    setup(null);
    service.create.and.returnValue(throwError(() => ({ status: 409 })));
    const addSpy = spyOn(TestBed.inject(MessageService), 'add');

    fillValidForm({ appKey: 'ISO' });
    component.save();

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('應用代碼「ISO」已被其他廠商使用');
  });

  // ---------- Edit mode ----------

  it('renders the edit title and loads the partner by pkid', () => {
    setup('19');

    expect(component.isEdit).toBeTrue();
    expect(textOf('form-title')).toBe('編輯合作廠商');
    expect(service.getById).toHaveBeenCalledWith(19);
  });

  it('shows pkid read-only in edit mode, never as an input', () => {
    setup('19');

    expect(textOf('form-pkid')).toBe('19');
    expect(fixture.debugElement.query(By.css('input#pkid'))).toBeNull();
  });

  it('patches every field from the loaded partner', () => {
    setup('19');

    expect(component.form.getRawValue()).toEqual({
      name: '國際標準課程',
      appKey: 'ISO',
      nameOnPartnerMenu: '國際標準',
      nameOnCourseDetailPage: 'ISO',
      displayOrder: 1,
      imageFilename: 'iso.svg',
    });
  });

  it('patches a null imageFilename to an empty input', () => {
    setup('19');
    service.getById.and.returnValue(of({ ...PARTNER, imageFilename: null }));

    fixture = TestBed.createComponent(PartnerForm);
    component = fixture.componentInstance as unknown as FormInternals;
    fixture.detectChanges();

    expect(component.form.getRawValue()['imageFilename']).toBe('');
  });

  it('updates via PUT with the pkid in the body', () => {
    setup('19');
    fillValidForm({ name: '國際標準課程', appKey: 'ISO' });

    component.save();

    expect(service.update).toHaveBeenCalledTimes(1);
    const request = service.update.calls.mostRecent().args[0] as PartnerRequest;
    expect(request.pkid).toBe(19);
    expect(request.appKey).toBe('ISO');
    expect(service.create).not.toHaveBeenCalled();
  });

  it('allows saving a name another partner already uses', () => {
    setup('19');
    // Name is not unique in the live table — this must not be blocked client-side.
    fillValidForm({ name: '國際標準課程', appKey: 'PCB' });

    component.save();

    expect(service.update).toHaveBeenCalledTimes(1);
  });

  it('cancel returns to the detail page in edit mode', () => {
    setup('19');

    component.cancel();

    expect(router.navigate).toHaveBeenCalledWith(['/partners', 19]);
  });

  it('cancel returns to the list in add mode', () => {
    setup(null);

    component.cancel();

    expect(router.navigate).toHaveBeenCalledWith(['/partners']);
  });
});
