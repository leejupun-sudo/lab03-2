import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatusDetail } from './publish-status-detail';

const STATUS: PublishStatus = {
  pkid: 2,
  description: '上架中',
  isDraft: false,
  isPublished: true,
  isDiscontinued: false,
  courseCount: 12,
  promotion2Count: 3,
};

describe('PublishStatusDetail', () => {
  let fixture: ComponentFixture<PublishStatusDetail>;
  let service: jasmine.SpyObj<PublishStatusService>;

  function setup(routeId: string, status: PublishStatus | null = STATUS): void {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getById']);
    service.getById.and.returnValue(status ? of(status) : throwError(() => ({ status: 404 })));

    TestBed.configureTestingModule({
      imports: [PublishStatusDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: PublishStatusService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    fixture = TestBed.createComponent(PublishStatusDetail);
    fixture.detectChanges();
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  it('loads the status identified by the route param', () => {
    setup('2');

    expect(service.getById).toHaveBeenCalledWith(2);
  });

  it('renders every PublishStatus field', () => {
    setup('2');

    expect(textOf('detail-pkid')).toBe('2');
    expect(textOf('detail-description')).toBe('上架中');
    expect(textOf('detail-is-draft')).toBe('否');
    expect(textOf('detail-is-published')).toBe('是');
    expect(textOf('detail-is-discontinued')).toBe('否');
  });

  it('renders the Course and Promotion2 usage counts', () => {
    setup('2');

    expect(textOf('detail-course-count')).toBe('12');
    expect(textOf('detail-promotion2-count')).toBe('3');
    expect(fixture.debugElement.query(By.css('[data-testid="detail-unused"]'))).toBeNull();
  });

  it('notes when nothing references the status', () => {
    setup('1', { ...STATUS, pkid: 1, courseCount: 0, promotion2Count: 0 });

    expect(textOf('detail-unused')).toContain('尚未被任何課程或活動使用');
  });

  it('shows a not-found state when the status does not exist', () => {
    setup('99', null);

    expect(textOf('detail-not-found')).toContain('查無此發布狀態');
  });
});
