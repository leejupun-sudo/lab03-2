import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';

import {
  CertificationLookup,
  Course,
  JobCategoryLookup,
  totalCourseUsage,
} from '@core/models/course.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';

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

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);

  private readonly certifications = signal<CertificationLookup[]>([]);
  private readonly jobCategories = signal<JobCategoryLookup[]>([]);

  /** Chips for the course's certifications, resolved from the lookup; unknown ids show as #id. */
  protected readonly certificationLabels = computed(() =>
    resolveLabels(this.course()?.certificationPkids ?? [], this.certifications()),
  );

  protected readonly jobCategoryLabels = computed(() =>
    resolveLabels(this.course()?.jobCategoryPkids ?? [], this.jobCategories()),
  );

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
}

function resolveLabels(pkids: number[], lookup: { pkid: number; label: string }[]): string[] {
  return pkids.map((pkid) => lookup.find((item) => item.pkid === pkid)?.label ?? `#${pkid}`);
}
