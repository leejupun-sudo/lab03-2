import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import { AppRoleLookup } from '@core/models/app-role.model';
import { AppUser, AppUserQuery, EMPTY_APP_USER_QUERY } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { parseIsoDate, toIsoDate } from '@core/utils/date.util';

const FILTERS_KEY = 'app-user-list-filters';
const SORT_KEY = 'app-user-list-sort';
const PAGE_KEY = 'app-user-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

/** Drawer-side shape: the same fields as `AppUserQuery`, but dates as `Date` for p-datepicker. */
interface FilterForm {
  keyword: string | null;
  isActive: boolean | null;
  roleId: string | null;
  passwordUpdatedFrom: Date | null;
  passwordUpdatedTo: Date | null;
}

const EMPTY_FILTER_FORM: FilterForm = {
  keyword: null,
  isActive: null,
  roleId: null,
  passwordUpdatedFrom: null,
  passwordUpdatedTo: null,
};

@Component({
  selector: 'app-app-user-list',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    DatePickerModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule,
  ],
  templateUrl: './app-user-list.html',
  styleUrl: './app-user-list.scss',
})
export class AppUserList implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly users = signal<AppUser[]>([]);
  protected readonly roles = signal<AppRoleLookup[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  /** Filters bound to the drawer inputs; committed to `appliedFilters` (wire shape) on 搜尋. */
  protected filters: FilterForm = { ...EMPTY_FILTER_FORM };
  protected readonly appliedFilters = signal<AppUserQuery>({ ...EMPTY_APP_USER_QUERY });

  protected sortState: SortState = { sortField: 'userId', sortOrder: 1 };
  protected pageState: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  /** A checkbox cannot express "no filter", so the bit column filters through a tri-state select. */
  protected readonly triStateOptions = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false },
  ];

  ngOnInit(): void {
    const saved: AppUserQuery = { ...EMPTY_APP_USER_QUERY, ...readSession<AppUserQuery>(FILTERS_KEY) };
    this.appliedFilters.set(saved);
    this.filters = toFilterForm(saved);
    this.sortState = { ...this.sortState, ...readSession<SortState>(SORT_KEY) };
    this.pageState = { ...this.pageState, ...readSession<PageState>(PAGE_KEY) };

    // Only the drawer needs the role lookup; the table carries a count, not names.
    this.lookupService
      .getAppRoles()
      .pipe(catchError(() => of([] as AppRoleLookup[])))
      .subscribe((roles) => this.roles.set(roles));

    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters()).subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: () => {
        this.users.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入使用者清單。',
        });
      },
    });
  }

  protected get hasActiveFilters(): boolean {
    const applied = this.appliedFilters();
    return Object.values(applied).some(
      (value) => value !== null && value !== undefined && value !== '',
    );
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

  protected confirmDelete(user: AppUser): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${user.pkid}</b>「${user.userId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(user),
    });
  }

  private delete(user: AppUser): void {
    this.service.delete(user.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `使用者「${user.userId}」已刪除。`,
        });
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail:
            error.status === 404
              ? `使用者「${user.userId}」已不存在。`
              : `無法刪除使用者「${user.userId}」。`,
        }),
    });
  }

  protected goToNew(): void {
    void this.router.navigate(['/app-users/new']);
  }
}

function toQuery(form: FilterForm): AppUserQuery {
  return {
    keyword: form.keyword?.trim() || null,
    isActive: form.isActive ?? null,
    roleId: form.roleId ?? null,
    passwordUpdatedFrom: form.passwordUpdatedFrom ? toIsoDate(form.passwordUpdatedFrom) : null,
    passwordUpdatedTo: form.passwordUpdatedTo ? toIsoDate(form.passwordUpdatedTo) : null,
  };
}

function toFilterForm(query: AppUserQuery): FilterForm {
  return {
    keyword: query.keyword ?? null,
    isActive: query.isActive ?? null,
    roleId: query.roleId ?? null,
    passwordUpdatedFrom: parseIsoDate(query.passwordUpdatedFrom),
    passwordUpdatedTo: parseIsoDate(query.passwordUpdatedTo),
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
