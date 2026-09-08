import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

import { Course, CourseQuery, EMPTY_COURSE_QUERY } from '@core/models/course.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { PartnerLookup } from '@core/models/partner.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { parseIsoDate, toIsoDate } from '@core/utils/date.util';

const FILTERS_KEY = 'course-list-filters';
const SORT_KEY = 'course-list-sort';
const PAGE_KEY = 'course-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

/** Drawer-side shape: the same fields as `CourseQuery`, but dates as `Date` for p-datepicker. */
interface FilterForm {
  keyword: string | null;
  partnerPkid: number | null;
  courseGroupPkid: number | null;
  publishStatusPkid: number | null;
  scheduleOnFrom: Date | null;
  scheduleOnTo: Date | null;
  scheduleOffFrom: Date | null;
  scheduleOffTo: Date | null;
  canRepeat: boolean | null;
}

const EMPTY_FILTER_FORM: FilterForm = {
  keyword: null,
  partnerPkid: null,
  courseGroupPkid: null,
  publishStatusPkid: null,
  scheduleOnFrom: null,
  scheduleOnTo: null,
  scheduleOffFrom: null,
  scheduleOffTo: null,
  canRepeat: null,
};

@Component({
  selector: 'app-course-list',
  imports: [
    DecimalPipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './course-list.html',
  styleUrl: './course-list.scss',
})
export class CourseList implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  protected readonly partners = signal<PartnerLookup[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatusLookup[]>([]);

  /** Tri-state 允許重聽 filter — a checkbox cannot express "no filter". */
  protected readonly canRepeatOptions = [
    { label: '是', value: true },
    { label: '否', value: false },
  ];

  /** Filters bound to the drawer; committed to `appliedFilters` on 搜尋. */
  protected filters: FilterForm = { ...EMPTY_FILTER_FORM };
  protected readonly appliedFilters = signal<CourseQuery>({ ...EMPTY_COURSE_QUERY });

  // The API orders by CourseId (unique, so a single key is a total order).
  protected sortState: SortState = { sortField: 'courseId', sortOrder: 1 };
  protected pageState: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  // ---- Copy dialog ----
  protected readonly copyTarget = signal<Course | null>(null);
  protected readonly copying = signal(false);
  protected newCourseId = '';

  ngOnInit(): void {
    const saved = readSession<CourseQuery>(FILTERS_KEY);
    let query: CourseQuery = { ...EMPTY_COURSE_QUERY, ...saved };

    // Cross-entity navigation (Partner / CourseGroup / PublishStatus detail pages) wins
    // over whatever filter was saved — the contract those specs recorded.
    const params = this.route.snapshot.queryParamMap;
    const incoming = readIncoming(params.get('partnerPkid'), params.get('courseGroupPkid'), params.get('publishStatusPkid'));
    if (incoming) {
      query = { ...EMPTY_COURSE_QUERY, ...incoming };
      writeSession(FILTERS_KEY, query);
    }

    this.appliedFilters.set(query);
    this.filters = toFilterForm(query);
    this.sortState = { ...this.sortState, ...readSession<SortState>(SORT_KEY) };
    this.pageState = { ...this.pageState, ...readSession<PageState>(PAGE_KEY) };

    // Lookups only feed the drawer selects — the rows carry their own resolved labels.
    forkJoin({
      partners: this.lookupService.getPartners().pipe(catchError(() => of([] as PartnerLookup[]))),
      courseGroups: this.lookupService
        .getCourseGroups()
        .pipe(catchError(() => of([] as CourseGroupLookup[]))),
      publishStatuses: this.lookupService
        .getPublishStatuses()
        .pipe(catchError(() => of([] as PublishStatusLookup[]))),
    }).subscribe(({ partners, courseGroups, publishStatuses }) => {
      this.partners.set(partners);
      this.courseGroups.set(courseGroups);
      this.publishStatuses.set(publishStatuses);
    });

    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters()).subscribe({
      next: (courses) => {
        this.courses.set(courses);
        this.loading.set(false);
      },
      error: () => {
        this.courses.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入課程清單。',
        });
      },
    });
  }

  protected get hasActiveFilters(): boolean {
    const applied = this.appliedFilters();
    return Object.values(applied).some((value) => value !== null && value !== undefined && value !== '');
  }

  protected applyFilters(): void {
    const query = toQuery(this.filters);
    this.appliedFilters.set(query);
    writeSession(FILTERS_KEY, query);
    this.pageState = { ...this.pageState, first: 0 };
    writeSession(PAGE_KEY, this.pageState);
    this.drawerVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_FILTER_FORM };
    this.applyFilters();
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortState = {
      sortField: event.field ?? this.sortState.sortField,
      sortOrder: event.order ?? this.sortState.sortOrder,
    };
    writeSession(SORT_KEY, this.sortState);
  }

  protected onPage(event: TableLazyLoadEvent): void {
    this.pageState = {
      first: event.first ?? 0,
      rows: event.rows ?? this.pageState.rows,
    };
    writeSession(PAGE_KEY, this.pageState);
  }

  // ---- Delete ----

  protected confirmDelete(course: Course): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${course.pkid}</b>「${course.courseId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(course),
    });
  }

  private delete(course: Course): void {
    this.service.delete(course.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `課程「${course.courseId}」已刪除。`,
        });
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail:
            error.status === 409
              ? '此課程已被課程問答、相關連結、熱門課程或推薦課程使用，無法刪除。'
              : `無法刪除課程「${course.courseId}」。`,
        }),
    });
  }

  // ---- Copy ----

  protected openCopy(course: Course): void {
    this.newCourseId = '';
    this.copyTarget.set(course);
  }

  protected closeCopy(): void {
    this.copyTarget.set(null);
  }

  protected copy(): void {
    const source = this.copyTarget();
    const newCourseId = this.newCourseId.trim();
    if (!source || newCourseId === '') {
      return;
    }

    this.copying.set(true);
    this.service.copy(source.pkid, { newCourseId }).subscribe({
      next: (created) => {
        this.copying.set(false);
        this.copyTarget.set(null);
        this.messageService.add({
          severity: 'success',
          summary: '複製成功',
          detail: `已由「${source.courseId}」複製出「${created.courseId}」。`,
        });
        void this.router.navigate(['/courses', created.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.copying.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '複製失敗',
          detail:
            error.status === 409
              ? `簡介代碼「${newCourseId}」已存在。`
              : `無法複製課程「${source.courseId}」。`,
        });
      },
    });
  }

  protected goToNew(): void {
    void this.router.navigate(['/courses/new']);
  }
}

function readIncoming(
  partnerPkid: string | null,
  courseGroupPkid: string | null,
  publishStatusPkid: string | null,
): Partial<CourseQuery> | null {
  const incoming: Partial<CourseQuery> = {};
  const partner = Number(partnerPkid);
  const group = Number(courseGroupPkid);
  const status = Number(publishStatusPkid);
  if (partnerPkid && Number.isInteger(partner) && partner > 0) {
    incoming.partnerPkid = partner;
  }
  if (courseGroupPkid && Number.isInteger(group) && group > 0) {
    incoming.courseGroupPkid = group;
  }
  if (publishStatusPkid && Number.isInteger(status) && status > 0) {
    incoming.publishStatusPkid = status;
  }
  return Object.keys(incoming).length > 0 ? incoming : null;
}

function toQuery(form: FilterForm): CourseQuery {
  return {
    keyword: form.keyword?.trim() || null,
    partnerPkid: form.partnerPkid ?? null,
    courseGroupPkid: form.courseGroupPkid ?? null,
    publishStatusPkid: form.publishStatusPkid ?? null,
    scheduleOnFrom: form.scheduleOnFrom ? toIsoDate(form.scheduleOnFrom) : null,
    scheduleOnTo: form.scheduleOnTo ? toIsoDate(form.scheduleOnTo) : null,
    scheduleOffFrom: form.scheduleOffFrom ? toIsoDate(form.scheduleOffFrom) : null,
    scheduleOffTo: form.scheduleOffTo ? toIsoDate(form.scheduleOffTo) : null,
    canRepeat: form.canRepeat ?? null,
  };
}

function toFilterForm(query: CourseQuery): FilterForm {
  return {
    keyword: query.keyword ?? null,
    partnerPkid: query.partnerPkid ?? null,
    courseGroupPkid: query.courseGroupPkid ?? null,
    publishStatusPkid: query.publishStatusPkid ?? null,
    scheduleOnFrom: parseIsoDate(query.scheduleOnFrom),
    scheduleOnTo: parseIsoDate(query.scheduleOnTo),
    scheduleOffFrom: parseIsoDate(query.scheduleOffFrom),
    scheduleOffTo: parseIsoDate(query.scheduleOffTo),
    canRepeat: query.canRepeat ?? null,
  };
}

function readSession<T>(key: string): Partial<T> {
  try {
    const raw = sessionStorage.getItem(key);
    return raw ? (JSON.parse(raw) as Partial<T>) : {};
  } catch {
    return {};
  }
}

function writeSession(key: string, value: unknown): void {
  try {
    sessionStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Session storage unavailable (private mode) — filters simply do not persist.
  }
}
