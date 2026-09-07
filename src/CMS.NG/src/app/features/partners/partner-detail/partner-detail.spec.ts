import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { Partner } from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';
import { PartnerDetail } from './partner-detail';

const PARTNER: Partner = {
  pkid: 1,
  name: 'CompTIA',
  appKey: 'CompTIA',
  nameOnPartnerMenu: 'CompTIA 認證課程',
  nameOnCourseDetailPage: 'CompTIA',
  displayOrder: 3,
  imageFilename: 'CompTIA.png',
  courseCount: 42,
  certificationCount: 4,
  partnerCourseGroupCount: 2,
  promotion2Count: 7,
  seminarCount: 0,
};

describe('PartnerDetail', () => {
  let fixture: ComponentFixture<PartnerDetail>;
  let service: jasmine.SpyObj<PartnerService>;

  function setup(routeId: string, partner: Partner | null = PARTNER): void {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['getById']);
    service.getById.and.returnValue(partner ? of(partner) : throwError(() => ({ status: 404 })));

    TestBed.configureTestingModule({
      imports: [PartnerDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: PartnerService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    fixture = TestBed.createComponent(PartnerDetail);
    fixture.detectChanges();
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  it('loads the partner identified by the route param', () => {
    setup('1');

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders every Partner field', () => {
    setup('1');

    expect(textOf('detail-pkid')).toBe('1');
    expect(textOf('detail-name')).toBe('CompTIA');
    expect(textOf('detail-app-key')).toBe('CompTIA');
    expect(textOf('detail-name-on-partner-menu')).toBe('CompTIA 認證課程');
    expect(textOf('detail-name-on-course-detail-page')).toBe('CompTIA');
    expect(textOf('detail-display-order')).toBe('3');
    expect(textOf('detail-image-filename')).toBe('CompTIA.png');
  });

  it('renders all five usage counts separately', () => {
    setup('1');

    expect(textOf('detail-course-count')).toBe('42');
    expect(textOf('detail-certification-count')).toBe('4');
    expect(textOf('detail-partner-course-group-count')).toBe('2');
    expect(textOf('detail-promotion2-count')).toBe('7');
    expect(textOf('detail-seminar-count')).toBe('0');
  });

  it('renders an em dash when imageFilename is null', () => {
    setup('19', { ...PARTNER, pkid: 19, imageFilename: null });

    expect(textOf('detail-image-filename')).toBe('—');
  });

  it('renders an extensionless imageFilename verbatim', () => {
    // 17 of the 62 non-null live values look like this — no format rule applies.
    setup('37', { ...PARTNER, pkid: 37, imageFilename: 'Splunk' });

    expect(textOf('detail-image-filename')).toBe('Splunk');
  });

  it('hides the unused note when the partner is referenced', () => {
    setup('1');

    expect(fixture.debugElement.query(By.css('[data-testid="detail-unused"]'))).toBeNull();
  });

  it('shows the unused note when all five counts are zero', () => {
    setup('50', {
      ...PARTNER,
      pkid: 50,
      courseCount: 0,
      certificationCount: 0,
      partnerCourseGroupCount: 0,
      promotion2Count: 0,
      seminarCount: 0,
    });

    expect(textOf('detail-unused')).toContain('尚未被任何課程');
  });

  it('does not call a partner unused when only Seminar references it', () => {
    // Seminar declares no FK, so this is the row SQL Server would let you delete.
    setup('122', {
      ...PARTNER,
      pkid: 122,
      courseCount: 0,
      certificationCount: 0,
      partnerCourseGroupCount: 0,
      promotion2Count: 0,
      seminarCount: 34,
    });

    expect(fixture.debugElement.query(By.css('[data-testid="detail-unused"]'))).toBeNull();
    expect(textOf('detail-seminar-count')).toBe('34');
  });

  it('shows the not-found state when the partner does not exist', () => {
    setup('999', null);

    expect(textOf('detail-not-found')).toContain('查無此合作廠商');
  });
});
