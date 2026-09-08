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
import { makeCourse } from '@core/services/course.service.spec';
import { CourseDetail } from './course-detail';

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

    fixture = TestBed.createComponent(CourseDetail);
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
});
