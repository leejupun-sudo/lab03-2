import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { Course } from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { QrCodeService } from '@core/services/qr-code.service';
import { makeCourse } from '@core/services/course.service.spec';
import { CourseDetail } from './course-detail';

/** The component keeps its members protected; the download action is driven through this. */
interface DetailInternals {
  downloadQrCode(): Promise<void>;
}

const COURSE: Course = makeCourse({
  officialTitle: 'Oracle Database: PL/SQL Fundamentals',
  outline: '<p>Day 1</p>\n<p>Day 2</p>',
  faqCount: 2,
  relatedLinkCount: 3,
  certificationPkids: [34, 36],
  jobCategoryPkids: [22, 99],
});

describe('CourseDetail', () => {
  let fixture: ComponentFixture<CourseDetail>;
  let service: jasmine.SpyObj<CourseService>;
  // The real QR service (a browser canvas is all it needs), watched through spies.
  let qrCodeService: QrCodeService;

  function setup(routeId: string, course: Course | null = COURSE): void {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById']);
    service.getById.and.returnValue(course ? of(course) : throwError(() => ({ status: 404 })));

    const lookupService = jasmine.createSpyObj<LookupService>('LookupService', [
      'getCertifications',
      'getJobCategories',
    ]);
    lookupService.getCertifications.and.returnValue(
      of([
        { pkid: 34, title: 'FCP-SN', partnerName: 'Fortinet', label: 'FCP-SN (Fortinet)' },
        { pkid: 36, title: 'FCP-PCS', partnerName: 'Fortinet', label: 'FCP-PCS (Fortinet)' },
      ]),
    );
    lookupService.getJobCategories.and.returnValue(
      of([{ pkid: 22, description: '資料庫管理 Database', label: '資料庫管理 Database' }]),
    );

    TestBed.configureTestingModule({
      imports: [CourseDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookupService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    qrCodeService = TestBed.inject(QrCodeService);
    spyOn(qrCodeService, 'render').and.callThrough();
    spyOn(qrCodeService, 'save');

    fixture = TestBed.createComponent(CourseDetail);
    fixture.detectChanges();
  }

  /** Lets the QR render promise settle and re-renders the view. */
  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  function chipsOf(testId: string): string[] {
    return fixture.debugElement
      .queryAll(By.css(`[data-testid="${testId}"]`))
      .map((el) => ((el.nativeElement as HTMLElement).textContent ?? '').trim());
  }

  it('loads the course identified by the route param', () => {
    setup('35');

    expect(service.getById).toHaveBeenCalledWith(35);
  });

  it('renders the identity, FK labels, dates and numbers', () => {
    setup('35');

    expect(textOf('detail-pkid')).toBe('35');
    expect(textOf('detail-course-id')).toBe('PLF');
    expect(textOf('detail-prod-course-id')).toBe('PLF');
    expect(textOf('detail-title')).toBe('Oracle資料庫之PL／SQL基礎');
    expect(textOf('detail-official-title')).toBe('Oracle Database: PL/SQL Fundamentals');
    expect(textOf('detail-partner')).toBe('Oracle');
    expect(textOf('detail-course-group')).toBe('Oracle DB/My SQL資料庫系列課程');
    expect(textOf('detail-publish-status')).toBe('已下架');
    expect(textOf('detail-schedule-on')).toBe('2015-11-10');
    expect(textOf('detail-schedule-off')).toBe('2021-11-01');
    expect(textOf('detail-hour')).toBe('12');
    expect(textOf('detail-list-price')).toBe('49,000');
    expect(textOf('detail-learning-credit')).toBe('14');
    expect(textOf('detail-can-repeat')).toBe('否');
  });

  it('links the FK labels to the parent detail pages', () => {
    setup('35');

    const href = (testId: string) =>
      (fixture.debugElement.query(By.css(`[data-testid="${testId}"]`)).nativeElement as HTMLAnchorElement)
        .getAttribute('href');
    expect(href('detail-partner')).toBe('/partners/2');
    expect(href('detail-course-group')).toBe('/course-groups/18');
    expect(href('detail-publish-status')).toBe('/publish-statuses/3');
  });

  it('renders the outline as text, never as HTML', () => {
    setup('35');

    const outline = fixture.debugElement.query(By.css('[data-testid="detail-outline"]'));
    expect((outline.nativeElement as HTMLElement).querySelector('p')).toBeNull();
    expect(textOf('detail-outline')).toContain('<p>Day 1</p>');
  });

  it('renders an em dash for null text fields and a null course group', () => {
    setup('2063', { ...COURSE, pkid: 2063, courseGroupPkid: null, courseGroupDescription: null, note: null });

    expect(textOf('detail-course-group')).toBe('—');
    expect(textOf('detail-note')).toBe('—');
    expect(textOf('detail-other-info')).toBe('—');
  });

  it('resolves certification and job-category chips from the lookups, falling back to #id', () => {
    setup('35');

    expect(chipsOf('detail-certification-chip')).toEqual(['FCP-SN (Fortinet)', 'FCP-PCS (Fortinet)']);
    // 99 is not in the lookup — it must still be visible rather than silently dropped.
    expect(chipsOf('detail-job-category-chip')).toEqual(['資料庫管理 Database', '#99']);
  });

  it('renders all four usage counts separately', () => {
    setup('35');

    expect(textOf('detail-faq-count')).toBe('2');
    expect(textOf('detail-related-link-count')).toBe('3');
    expect(textOf('detail-hot-course-count')).toBe('0');
    expect(textOf('detail-recomm-count')).toBe('0');
  });

  it('hides the unused note when the course is referenced', () => {
    setup('35');

    expect(fixture.debugElement.query(By.css('[data-testid="detail-unused"]'))).toBeNull();
  });

  it('shows the unused note when all four counts are zero', () => {
    setup('3', { ...COURSE, pkid: 3, faqCount: 0, relatedLinkCount: 0, hotCourseCount: 0, recommCount: 0 });

    expect(textOf('detail-unused')).toContain('尚未被任何課程問答');
  });

  it('does not call a course unused when only CourseRecomm references it', () => {
    // CourseRecomm declares no FK, so this is the row SQL Server would let you delete.
    setup('41', { ...COURSE, pkid: 41, faqCount: 0, relatedLinkCount: 0, hotCourseCount: 0, recommCount: 5 });

    expect(fixture.debugElement.query(By.css('[data-testid="detail-unused"]'))).toBeNull();
    expect(textOf('detail-recomm-count')).toBe('5');
  });

  it('shows the not-found state when the course does not exist', () => {
    setup('999', null);

    expect(textOf('detail-not-found')).toContain('查無此課程');
  });

  describe('QR code', () => {
    function imageEl(): HTMLImageElement | null {
      const el = fixture.debugElement.query(By.css('[data-testid="detail-qr-image"]'));
      return (el?.nativeElement as HTMLImageElement | undefined) ?? null;
    }

    it('encodes the public course URL built from pkid and CourseId', async () => {
      setup('35');
      await settle();

      expect(qrCodeService.render).toHaveBeenCalledWith(
        'https://www.uuu.com.tw/Course/Show/35/PLF',
        'PLF',
      );
    });

    it('percent-encodes a CourseId carrying a space', async () => {
      // 15 live CourseIds carry spaces, parentheses or Chinese — pkid 1125 is 「CCNA Cloud」.
      setup('1125', { ...COURSE, pkid: 1125, courseId: 'CCNA Cloud' });
      await settle();

      expect(qrCodeService.render).toHaveBeenCalledWith(
        'https://www.uuu.com.tw/Course/Show/1125/CCNA%20Cloud',
        'CCNA Cloud',
      );
    });

    it('shows the target URL as a link beside the code', async () => {
      setup('35');
      await settle();

      const link = fixture.debugElement.query(By.css('[data-testid="detail-qr-url"]'))
        .nativeElement as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('https://www.uuu.com.tw/Course/Show/35/PLF');
      expect(textOf('detail-qr-url')).toBe('https://www.uuu.com.tw/Course/Show/35/PLF');
    });

    it('shows the CourseId as the title of the code', async () => {
      setup('35');
      await settle();

      expect(textOf('detail-qr-title')).toBe('PLF');
      expect(imageEl()?.alt).toBe('課程 PLF 的 QR Code');
    });

    it('renders the composed code as a PNG image inside 基本資料', async () => {
      setup('35');
      await settle();

      const image = imageEl();
      expect(image).not.toBeNull();
      expect(image!.src.startsWith('data:image/png;base64,')).toBeTrue();
      // The block lives in the first card, 基本資料 — not in a card of its own.
      const card = image!.closest('.cms-card');
      expect(card?.querySelector('.cms-card__title')?.textContent?.trim()).toBe('基本資料');
    });

    it('shows a placeholder and no image until the code has rendered', () => {
      setup('35');

      expect(imageEl()).toBeNull();
      expect(textOf('detail-qr-pending')).toContain('QR Code');
    });

    it('says so, and leaves the rest of the page intact, when rendering fails', async () => {
      setup('35');
      (qrCodeService.render as jasmine.Spy).and.returnValue(
        Promise.reject(new Error('canvas unavailable')),
      );
      // Re-create the component so the failing spy is the one it calls.
      fixture = TestBed.createComponent(CourseDetail);
      fixture.detectChanges();
      await settle();

      expect(imageEl()).toBeNull();
      expect(textOf('detail-qr-pending')).toBe('QR Code 產生失敗');
      expect(textOf('detail-course-id')).toBe('PLF');
    });

    it('produces a PNG image named after the CourseId when the download runs', async () => {
      setup('35');
      await settle();

      await (fixture.componentInstance as unknown as DetailInternals).downloadQrCode();

      expect(qrCodeService.save).toHaveBeenCalledTimes(1);
      const [blob, filename] = (qrCodeService.save as jasmine.Spy).calls.mostRecent().args as [
        Blob,
        string,
      ];
      expect(filename).toBe('PLF.png');
      expect(blob.type).toBe('image/png');
      expect(blob.size).toBeGreaterThan(0);

      const header = new Uint8Array(await blob.arrayBuffer()).subarray(0, 8);
      expect(Array.from(header)).toEqual([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
    });

    it('enables the download button only once the code exists', async () => {
      setup('35');

      const button = () =>
        fixture.debugElement.query(By.css('[data-testid="detail-qr-download"] button'))
          .nativeElement as HTMLButtonElement;
      expect(button().disabled).toBeTrue();

      await settle();

      expect(button().disabled).toBeFalse();
    });

    it('downloads nothing when there is no course', async () => {
      setup('999', null);
      await settle();

      await (fixture.componentInstance as unknown as DetailInternals).downloadQrCode();

      expect(qrCodeService.render).not.toHaveBeenCalled();
      expect(qrCodeService.save).not.toHaveBeenCalled();
    });
  });
});
