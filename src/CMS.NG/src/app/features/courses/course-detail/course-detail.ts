import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';

import { environment } from '@env';

import {
  CertificationLookup,
  Course,
  JobCategoryLookup,
  totalCourseUsage,
} from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { QrCodeService } from '@core/services/qr-code.service';

@Component({
  selector: 'app-course-detail',
  imports: [DecimalPipe, RouterLink, ButtonModule],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss',
})
export class CourseDetail implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly qrCodeService = inject(QrCodeService);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);

  /** Data URL of the composed QR image (matrix + CourseId caption); null until it renders. */
  protected readonly qrImage = signal<string | null>(null);
  /** Set when rendering threw, so the placeholder stops promising a code that is not coming. */
  protected readonly qrFailed = signal(false);
  private qrCanvas: HTMLCanvasElement | null = null;

  private readonly certifications = signal<CertificationLookup[]>([]);
  private readonly jobCategories = signal<JobCategoryLookup[]>([]);

  /** Chips for the course's certifications, resolved from the lookup; unknown ids show as #id. */
  protected readonly certificationLabels = computed(() =>
    resolveLabels(this.course()?.certificationPkids ?? [], this.certifications()),
  );

  protected readonly jobCategoryLabels = computed(() =>
    resolveLabels(this.course()?.jobCategoryPkids ?? [], this.jobCategories()),
  );

  /**
   * Public course page the QR code points at. CourseId is free text — live values carry
   * spaces, parentheses and Chinese — so the segment is percent-encoded.
   */
  protected readonly qrTargetUrl = computed(() => {
    const item = this.course();
    if (!item) {
      return null;
    }
    const courseId = encodeURIComponent(item.courseId);
    return `${environment.publicSiteBaseUrl}/Course/Show/${item.pkid}/${courseId}`;
  });

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!pkid) {
      this.loading.set(false);
      return;
    }

    // The FK labels ride on the row; only the two junction lookups are needed here.
    forkJoin({
      course: this.service.getById(pkid),
      certifications: this.lookupService
        .getCertifications()
        .pipe(catchError(() => of([] as CertificationLookup[]))),
      jobCategories: this.lookupService
        .getJobCategories()
        .pipe(catchError(() => of([] as JobCategoryLookup[]))),
    }).subscribe({
      next: ({ course, certifications, jobCategories }) => {
        this.certifications.set(certifications);
        this.jobCategories.set(jobCategories);
        this.course.set(course);
        this.loading.set(false);
        void this.renderQrCode(course);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: `找不到主代碼 ${pkid} 的課程。`,
        });
      },
    });
  }

  /** True when none of the four child tables references this course — it is then safe to delete. */
  protected get isUnused(): boolean {
    const course = this.course();
    return !!course && totalCourseUsage(course) === 0;
  }

  protected goBack(): void {
    void this.router.navigate(['/courses']);
  }

  /** Composes the QR image once the course is known; a failure costs only the QR row. */
  private async renderQrCode(course: Course): Promise<void> {
    const url = this.qrTargetUrl();
    if (!url) {
      return;
    }

    try {
      this.qrCanvas = await this.qrCodeService.render(url, course.courseId);
      this.qrImage.set(this.qrCanvas.toDataURL('image/png'));
    } catch {
      this.qrCanvas = null;
      this.qrImage.set(null);
      this.qrFailed.set(true);
      this.messageService.add({
        severity: 'error',
        summary: 'QR Code 產生失敗',
        detail: `無法為 ${course.courseId} 產生 QR Code。`,
      });
    }
  }

  /** Saves the composed canvas as `{CourseId}.png`. */
  protected async downloadQrCode(): Promise<void> {
    const course = this.course();
    const canvas = this.qrCanvas;
    if (!course || !canvas) {
      return;
    }

    try {
      const blob = await this.qrCodeService.toPngBlob(canvas);
      this.qrCodeService.save(blob, `${course.courseId}.png`);
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: '下載失敗',
        detail: '無法產生 QR Code 圖檔。',
      });
    }
  }
}

function resolveLabels(pkids: number[], lookup: { pkid: number; label: string }[]): string[] {
  return pkids.map((pkid) => lookup.find((item) => item.pkid === pkid)?.label ?? `#${pkid}`);
}
