import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';

import {
  CertificationLookup,
  Course,
  CourseRequest,
  JobCategoryLookup,
} from '@core/models/course.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { PartnerLookup } from '@core/models/partner.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { addYears, parseIsoDate, toIsoDate } from '@core/utils/date.util';

/** The two newest live rows follow this convention; older rows predate it. */
const SCHEDULE_OFF_YEARS_AFTER_ON = 10;

/**
 * 下架日期 must not precede 上架日期. There is no CHECK in SQL and one live row
 * (pkid 1980) violates it — the API accepts what SQL accepts, the form does not.
 */
function scheduleRangeValidator(group: AbstractControl): ValidationErrors | null {
  const on = group.get('scheduleOn')?.value as Date | null;
  const off = group.get('scheduleOff')?.value as Date | null;
  if (on instanceof Date && off instanceof Date && off.getTime() < on.getTime()) {
    return { scheduleRange: true };
  }
  return null;
}

@Component({
  selector: 'app-course-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    InputNumberModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TextareaModule,
  ],
  templateUrl: './course-form.html',
  styleUrl: './course-form.scss',
})
export class CourseForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly pkid = signal(0);

  protected readonly partners = signal<PartnerLookup[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatusLookup[]>([]);
  protected readonly certifications = signal<CertificationLookup[]>([]);
  protected readonly jobCategories = signal<JobCategoryLookup[]>([]);

  // pkid is IDENTITY — never an input. CourseId is an input in add mode only.
  protected readonly form = this.fb.group(
    {
      courseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
      prodCourseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
      title: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
      officialTitle: this.fb.nonNullable.control('', [Validators.maxLength(300)]),
      friendlyUrl: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
      partnerPkid: this.fb.control<number | null>(null, [Validators.required]),
      courseGroupPkid: this.fb.control<number | null>(null),
      publishStatusPkid: this.fb.control<number | null>(null, [Validators.required]),
      scheduleOn: this.fb.control<Date | null>(null, [Validators.required]),
      scheduleOff: this.fb.control<Date | null>(null, [Validators.required]),
      displayOrder: this.fb.nonNullable.control(0, [
        Validators.required,
        Validators.min(0),
        Validators.max(9999),
      ]),
      hour: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0), Validators.max(32767)]),
      listPrice: this.fb.nonNullable.control(0, [
        Validators.required,
        Validators.min(0),
        Validators.max(999_999_999),
      ]),
      learningCredit: this.fb.nonNullable.control(0, [
        Validators.required,
        Validators.min(0),
        Validators.max(99_999_999.9),
      ]),
      canRepeat: this.fb.nonNullable.control(false),
      certificationPkids: this.fb.nonNullable.control<number[]>([]),
      jobCategoryPkids: this.fb.nonNullable.control<number[]>([]),
      material: this.fb.nonNullable.control('', [Validators.maxLength(500)]),
      objective: this.fb.nonNullable.control('', [Validators.maxLength(4000)]),
      target: this.fb.nonNullable.control('', [Validators.maxLength(500)]),
      prerequisites: this.fb.nonNullable.control('', [Validators.maxLength(4000)]),
      outline: this.fb.nonNullable.control(''),
      towardCertOrExam: this.fb.nonNullable.control(''),
      note: this.fb.nonNullable.control('', [Validators.maxLength(4000)]),
      otherInfo: this.fb.nonNullable.control('', [Validators.maxLength(4000)]),
    },
    { validators: scheduleRangeValidator },
  );

  protected get isEdit(): boolean {
    return this.pkid() > 0;
  }

  protected get title(): string {
    return this.isEdit ? '編輯課程' : '新增課程';
  }

  protected get hasScheduleRangeError(): boolean {
    const off = this.form.controls.scheduleOff;
    return this.form.hasError('scheduleRange') && (off.dirty || off.touched);
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const pkid = idParam ? Number(idParam) : 0;
    this.pkid.set(Number.isFinite(pkid) ? pkid : 0);

    if (!this.isEdit) {
      // Add mode only: a fresh course defaults 下架日期 to 上架日期 + 10 years. In edit
      // mode a deliberately set 下架日期 must survive a change to 上架日期.
      this.form.controls.scheduleOn.valueChanges.subscribe((on) => {
        if (on instanceof Date) {
          this.form.controls.scheduleOff.setValue(
            addYears(on, SCHEDULE_OFF_YEARS_AFTER_ON),
            { emitEvent: false },
          );
        }
      });
    }

    this.loading.set(true);

    forkJoin({
      partners: this.lookupService.getPartners().pipe(catchError(() => of([] as PartnerLookup[]))),
      courseGroups: this.lookupService
        .getCourseGroups()
        .pipe(catchError(() => of([] as CourseGroupLookup[]))),
      publishStatuses: this.lookupService
        .getPublishStatuses()
        .pipe(catchError(() => of([] as PublishStatusLookup[]))),
      certifications: this.lookupService
        .getCertifications()
        .pipe(catchError(() => of([] as CertificationLookup[]))),
      jobCategories: this.lookupService
        .getJobCategories()
        .pipe(catchError(() => of([] as JobCategoryLookup[]))),
      course: this.isEdit ? this.service.getById(this.pkid()) : of(null),
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses, certifications, jobCategories, course }) => {
        this.partners.set(partners);
        this.courseGroups.set(courseGroups);
        this.publishStatuses.set(publishStatuses);
        this.certifications.set(certifications);
        this.jobCategories.set(jobCategories);
        if (course) {
          this.patchForm(course);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入課程資料。',
        });
      },
    });
  }

  private patchForm(course: Course): void {
    this.form.patchValue({
      courseId: course.courseId,
      prodCourseId: course.prodCourseId,
      title: course.title,
      officialTitle: course.officialTitle ?? '',
      friendlyUrl: course.friendlyUrl,
      partnerPkid: course.partnerPkid,
      courseGroupPkid: course.courseGroupPkid ?? null,
      publishStatusPkid: course.publishStatusPkid,
      scheduleOn: parseIsoDate(course.scheduleOn),
      scheduleOff: parseIsoDate(course.scheduleOff),
      displayOrder: course.displayOrder,
      hour: course.hour,
      listPrice: course.listPrice,
      learningCredit: course.learningCredit,
      canRepeat: course.canRepeat,
      certificationPkids: course.certificationPkids ?? [],
      jobCategoryPkids: course.jobCategoryPkids ?? [],
      material: course.material ?? '',
      objective: course.objective ?? '',
      target: course.target ?? '',
      prerequisites: course.prerequisites ?? '',
      outline: course.outline ?? '',
      towardCertOrExam: course.towardCertOrExam ?? '',
      note: course.note ?? '',
      otherInfo: course.otherInfo ?? '',
    });
    // CourseRecomm keys on the CourseId value — immutable once created.
    this.form.controls.courseId.disable();
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.messageService.add({
        severity: 'warn',
        summary: '欄位有誤',
        detail: this.form.hasError('scheduleRange')
          ? '下架日期不可早於上架日期。'
          : '請修正紅字標示的欄位後再儲存。',
      });
      return;
    }

    const value = this.form.getRawValue();
    const request: CourseRequest = {
      pkid: this.pkid(),
      title: value.title.trim(),
      officialTitle: nullIfBlank(value.officialTitle),
      courseId: value.courseId.trim(),
      prodCourseId: value.prodCourseId.trim(),
      friendlyUrl: value.friendlyUrl.trim(),
      displayOrder: value.displayOrder,
      partnerPkid: value.partnerPkid!,
      courseGroupPkid: value.courseGroupPkid ?? null,
      publishStatusPkid: value.publishStatusPkid!,
      scheduleOn: toIsoDate(value.scheduleOn!),
      scheduleOff: toIsoDate(value.scheduleOff!),
      hour: value.hour,
      listPrice: value.listPrice,
      learningCredit: value.learningCredit,
      material: nullIfBlank(value.material),
      objective: nullIfBlank(value.objective),
      target: nullIfBlank(value.target),
      prerequisites: nullIfBlank(value.prerequisites),
      outline: nullIfBlank(value.outline),
      towardCertOrExam: nullIfBlank(value.towardCertOrExam),
      note: nullIfBlank(value.note),
      otherInfo: nullIfBlank(value.otherInfo),
      canRepeat: value.canRepeat,
      certificationPkids: value.certificationPkids,
      jobCategoryPkids: value.jobCategoryPkids,
    };

    this.saving.set(true);
    const request$ = this.isEdit ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit ? '更新成功' : '新增成功',
          detail: `課程「${saved.courseId}」已儲存。`,
        });
        void this.router.navigate(['/courses', saved.pkid]);
      },
      // The only 409 on save is a duplicate CourseId, and only on create.
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error.status === 409
              ? `簡介代碼「${request.courseId}」已存在。`
              : '儲存課程時發生錯誤。',
        });
      },
    });
  }

  protected cancel(): void {
    void this.router.navigate(this.isEdit ? ['/courses', this.pkid()] : ['/courses']);
  }
}

/** The optional text columns hold NULLs, never empty strings. */
function nullIfBlank(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
