import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroupDetail } from './course-group-detail';

const GROUP: CourseGroup = {
  pkid: 1,
  description: 'Azure系列課程',
  courseCount: 48,
  partnerCourseGroupCount: 2,
};

describe('CourseGroupDetail', () => {
  let fixture: ComponentFixture<CourseGroupDetail>;
  let service: jasmine.SpyObj<CourseGroupService>;

  function setup(routeId: string, group: CourseGroup | null = GROUP): void {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getById']);
    service.getById.and.returnValue(group ? of(group) : throwError(() => ({ status: 404 })));

    TestBed.configureTestingModule({
      imports: [CourseGroupDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: CourseGroupService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    fixture = TestBed.createComponent(CourseGroupDetail);
    fixture.detectChanges();
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  it('loads the group identified by the route param', () => {
    setup('1');

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders every CourseGroup field', () => {
    setup('1');

    expect(textOf('detail-pkid')).toBe('1');
    expect(textOf('detail-description')).toBe('Azure系列課程');
  });

  it('renders the Course and PartnerCourseGroup usage counts', () => {
    setup('1');

    expect(textOf('detail-course-count')).toBe('48');
    expect(textOf('detail-partner-course-group-count')).toBe('2');
    expect(fixture.debugElement.query(By.css('[data-testid="detail-unused"]'))).toBeNull();
  });

  it('notes when nothing references the group', () => {
    setup('3', { ...GROUP, pkid: 3, courseCount: 0, partnerCourseGroupCount: 0 });

    expect(textOf('detail-unused')).toContain('尚未被任何課程或廠商課程群組使用');
  });

  it('shows a not-found state when the group does not exist', () => {
    setup('999', null);

    expect(textOf('detail-not-found')).toContain('查無此課程群組');
  });
});
