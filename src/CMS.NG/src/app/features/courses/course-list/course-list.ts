import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { DrawerModule } from 'primeng/drawer';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

import { Course, CourseQuery, CourseRequest, EMPTY_COURSE_QUERY } from '@core/models/course.model';
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

// ---------------------------------------------------------------------------
// 表格內即時編輯 (inline cell editing)
// ---------------------------------------------------------------------------

/**
 * The list columns a cell editor is offered for. Four columns are deliberately absent:
 *
 * - `pkid` (主代碼) — int IDENTITY, never writable.
 * - `courseId` (簡介代碼) — `CourseRepository.UpdateAsync` omits it from the `SET` list
 *   because `CourseRecomm` references courses by that *string* with no FK, so a PUT
 *   carrying a changed value is silently discarded. Renaming happens through 複製.
 * - `partnerName` (原廠) / `courseGroupDescription` (課程群組) — JOINed FK labels, not
 *   columns of `Course`; changing them means picking a different FK target, which is the
 *   edit form's job. (上架狀態 is an FK too, but its five options fit a cell dropdown.)
 */
export type EditableField =
  | 'displayOrder'
  | 'prodCourseId'
  | 'title'
  | 'publishStatusPkid'
  | 'scheduleOn'
  | 'scheduleOff'
  | 'hour'
  | 'listPrice'
  | 'learningCredit'
  | 'canRepeat';

/** What a cell editor can hand back: `p-datepicker` gives `Date`, the rest primitives. */
export type EditValue = string | number | boolean | Date | null;

interface CellEdit {
  pkid: number;
  field: EditableField;
  value: EditValue;
}

const FIELD_LABELS: Record<EditableField, string> = {
  displayOrder: '顯示順序',
  prodCourseId: '科目代碼',
  title: '課程名稱',
  publishStatusPkid: '上架狀態',
  scheduleOn: '上架日期',
  scheduleOff: '下架日期',
  hour: '時數',
  listPrice: '定價',
  learningCredit: '點數',
  canRepeat: '允許重聽',
};

/** `nvarchar` lengths from `database/course.sql` — the same caps the edit form applies. */
const TEXT_MAX_LENGTH: Record<'prodCourseId' | 'title', number> = {
  prodCourseId: 50,
  title: 200,
};

/** Column ranges: `DisplayOrder`/`Hour` are smallint-ish, the money/credit columns wider. */
const NUMBER_MAX: Record<'displayOrder' | 'hour' | 'listPrice' | 'learningCredit', number> = {
  displayOrder: 9999,
  hour: 32767,
  listPrice: 999_999_999,
  learningCredit: 99_999_999.9,
};

/** `DisplayOrder` and `Hour` are integer columns; 定價/點數 accept decimals. */
const INTEGER_FIELDS: readonly EditableField[] = ['displayOrder', 'hour'];

/**
 * Validates one edited cell against the row it belongs to. Returns a zh-TW message, or
 * `null` when the value may be persisted.
 *
 * The date rule is cross-field, so it needs `row`: 上架日期 must not fall after 下架日期.
 * SQL has no CHECK for it (pkid 1980 violates it live) — this mirrors the edit form's
 * `scheduleRangeValidator`, and neither is a claim about what the API will accept.
 */
export function validateCell(field: EditableField, raw: EditValue, row: Course): string | null {
  const label = FIELD_LABELS[field];

  switch (field) {
    // A checkbox has no invalid state.
    case 'canRepeat':
      return null;

    case 'prodCourseId':
    case 'title': {
      const text = typeof raw === 'string' ? raw.trim() : '';
      if (text === '') {
        return `${label}不可空白。`;
      }
      if (text.length > TEXT_MAX_LENGTH[field]) {
        return `${label}不可超過 ${TEXT_MAX_LENGTH[field]} 個字。`;
      }
      return null;
    }

    case 'publishStatusPkid': {
      const pkid = Number(raw);
      if (isBlank(raw) || !Number.isInteger(pkid) || pkid <= 0) {
        return `${label}不可空白。`;
      }
      return null;
    }

    case 'displayOrder':
    case 'hour':
    case 'listPrice':
    case 'learningCredit': {
      if (isBlank(raw)) {
        return `${label}不可空白。`;
      }
      const value = typeof raw === 'number' ? raw : Number(raw);
      if (!Number.isFinite(value)) {
        return `${label}必須是數字。`;
      }
      if (value < 0) {
        return `${label}不可為負數。`;
      }
      if (INTEGER_FIELDS.includes(field) && !Number.isInteger(value)) {
        return `${label}必須是整數。`;
      }
      if (value > NUMBER_MAX[field]) {
        return `${label}不可大於 ${NUMBER_MAX[field]}。`;
      }
      return null;
    }

    case 'scheduleOn':
    case 'scheduleOff': {
      if (isBlank(raw)) {
        return `${label}不可空白。`;
      }
      const edited = toDate(raw);
      if (!edited) {
        return `${label}必須是有效日期。`;
      }
      // The other end of the range is whatever the row still holds.
      const other = parseIsoDate(field === 'scheduleOn' ? row.scheduleOff : row.scheduleOn);
      if (other) {
        const on = field === 'scheduleOn' ? edited : other;
        const off = field === 'scheduleOn' ? other : edited;
        if (off.getTime() < on.getTime()) {
          return '上架日期不可晚於下架日期。';
        }
      }
      return null;
    }
  }
}

@Component({
  selector: 'app-course-list',
  imports: [
    DecimalPipe,
    NgTemplateOutlet,
    FormsModule,
    RouterLink,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    DrawerModule,
    InputNumberModule,
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

  // ---- Inline cell editing ----
  protected readonly editing = signal<CellEdit | null>(null);
  protected readonly editError = signal<string | null>(null);
  protected readonly savingCell = signal(false);
  /** True while a cell editor's own overlay (calendar / dropdown panel) is open. */
  protected readonly overlayOpen = signal(false);

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

  // ---- Inline cell editing ----
  //
  // PrimeNG's own `pEditableColumn` opens on a *single* click and offers no hook for an
  // async, validated commit, so the cell editors are driven from here instead and reuse
  // the same PrimeNG input widgets the edit form uses. The `p-datatable` shape is
  // otherwise untouched — `.cms-cell--editable` supplies the hover affordance.

  protected isEditing(course: Course, field: EditableField): boolean {
    const edit = this.editing();
    return edit !== null && edit.pkid === course.pkid && edit.field === field;
  }

  /** Double-click only — a single click must leave the cell alone. */
  protected startEdit(course: Course, field: EditableField, cell?: EventTarget | null): void {
    if (this.savingCell()) {
      return;
    }
    this.editError.set(null);
    this.overlayOpen.set(false);
    this.editing.set({ pkid: course.pkid, field, value: currentValue(course, field) });

    // Same trick as PrimeNG's EditableColumn: focus whatever the cell just rendered.
    if (cell instanceof HTMLElement) {
      setTimeout(() => cell.querySelector<HTMLElement>('input, textarea, select')?.focus(), 0);
    }
  }

  protected setEditValue(value: EditValue): void {
    const edit = this.editing();
    if (edit) {
      this.editing.set({ ...edit, value });
    }
  }

  protected cancelEdit(): void {
    this.editing.set(null);
    this.editError.set(null);
    this.overlayOpen.set(false);
  }

  /**
   * Blur handler for the overlay-backed editors (上架狀態 / the two dates). Their panels are
   * `appendTo="body"`, so opening one blurs the input — committing then would close the
   * editor the moment the user reached for the calendar. Picking a value fires
   * `onSelect` / `onChange`, which commits instead.
   */
  protected commitOnBlur(): void {
    if (!this.overlayOpen()) {
      this.commit();
    }
  }

  /** Persists the edited cell. Called on blur, on Enter, and on an overlay selection. */
  protected commit(): void {
    const edit = this.editing();
    if (!edit || this.savingCell()) {
      return;
    }

    const row = this.courses().find((course) => course.pkid === edit.pkid);
    if (!row) {
      this.cancelEdit();
      return;
    }

    // Invalid: surface the message and stay in edit mode so the value can be corrected.
    const message = validateCell(edit.field, edit.value, row);
    if (message) {
      this.editError.set(message);
      return;
    }

    const value = normaliseValue(edit.field, edit.value);
    if (value === row[edit.field]) {
      this.cancelEdit();
      return;
    }

    this.editError.set(null);
    this.savingCell.set(true);

    // `UpdateAsync` re-syncs CourseInCertification and CourseJobCategories from the
    // request on every PUT, and list rows do not carry `certificationPkids` /
    // `jobCategoryPkids` (only `GET /{id}` does). Sending a request built from the list
    // row alone would silently delete both junction sets, so read the full row first.
    this.service
      .getById(edit.pkid)
      .pipe(switchMap((full) => this.service.update(applyEdit(full, edit.field, value))))
      .subscribe({
        next: (saved) => {
          this.savingCell.set(false);
          // The PUT response is a fresh GetByIdAsync, so the JOINed labels come with it.
          this.courses.update((rows) =>
            rows.map((course) => (course.pkid === saved.pkid ? { ...course, ...saved } : course)),
          );
          this.cancelEdit();
          this.messageService.add({
            severity: 'success',
            summary: '更新成功',
            detail: `課程「${saved.courseId}」的${FIELD_LABELS[edit.field]}已更新。`,
          });
        },
        error: (error: HttpErrorResponse) => {
          this.savingCell.set(false);
          // The row was never mutated, so closing the editor *is* the revert.
          this.cancelEdit();
          this.messageService.add({
            severity: 'error',
            summary: '更新失敗',
            detail:
              error.status === 404
                ? `課程「${row.courseId}」已不存在，請重新整理清單。`
                : `無法更新課程「${row.courseId}」的${FIELD_LABELS[edit.field]}，已還原原值。`,
          });
        },
      });
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

function isBlank(value: EditValue): boolean {
  return value === null || value === undefined || value === '';
}

/** `Date` straight through (rejecting an Invalid Date), ISO string parsed, anything else null. */
function toDate(value: EditValue): Date | null {
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value;
  }
  return typeof value === 'string' ? parseIsoDate(value) : null;
}

/** The row's current value in the shape its editor binds to. */
function currentValue(course: Course, field: EditableField): EditValue {
  if (field === 'scheduleOn' || field === 'scheduleOff') {
    return parseIsoDate(course[field]);
  }
  return course[field] ?? null;
}

/** Editor output → the shape the column stores. Runs only after `validateCell` passed. */
function normaliseValue(field: EditableField, value: EditValue): string | number | boolean {
  switch (field) {
    case 'prodCourseId':
    case 'title':
      return String(value).trim();
    case 'canRepeat':
      return value === true;
    case 'scheduleOn':
    case 'scheduleOff':
      return toIsoDate(toDate(value)!);
    default:
      return Number(value);
  }
}

/** A full `CourseRequest` from a `GET /{id}` row, with the one edited column replaced. */
function applyEdit(
  course: Course,
  field: EditableField,
  value: string | number | boolean,
): CourseRequest {
  return {
    pkid: course.pkid,
    title: course.title,
    officialTitle: course.officialTitle ?? null,
    // Sent for shape only — the UPDATE statement never writes CourseId.
    courseId: course.courseId,
    prodCourseId: course.prodCourseId,
    friendlyUrl: course.friendlyUrl,
    displayOrder: course.displayOrder,
    partnerPkid: course.partnerPkid,
    courseGroupPkid: course.courseGroupPkid ?? null,
    publishStatusPkid: course.publishStatusPkid,
    scheduleOn: course.scheduleOn,
    scheduleOff: course.scheduleOff,
    hour: course.hour,
    listPrice: course.listPrice,
    learningCredit: course.learningCredit,
    material: course.material ?? null,
    objective: course.objective ?? null,
    target: course.target ?? null,
    prerequisites: course.prerequisites ?? null,
    outline: course.outline ?? null,
    towardCertOrExam: course.towardCertOrExam ?? null,
    note: course.note ?? null,
    otherInfo: course.otherInfo ?? null,
    canRepeat: course.canRepeat,
    certificationPkids: course.certificationPkids ?? [],
    jobCategoryPkids: course.jobCategoryPkids ?? [],
    [field]: value,
  };
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
