import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

import {
  CourseGroup,
  CourseGroupQuery,
  EMPTY_COURSE_GROUP_QUERY,
} from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';

const FILTERS_KEY = 'course-group-list-filters';
const SORT_KEY = 'course-group-list-sort';
const PAGE_KEY = 'course-group-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

@Component({
  selector: 'app-course-group-list',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './course-group-list.html',
  styleUrl: './course-group-list.scss',
})
export class CourseGroupList implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly groups = signal<CourseGroup[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  /** Filters bound to the drawer input; committed to `appliedFilters` on 搜尋. */
  protected filters: CourseGroupQuery = { ...EMPTY_COURSE_GROUP_QUERY };
  protected readonly appliedFilters = signal<CourseGroupQuery>({ ...EMPTY_COURSE_GROUP_QUERY });

  // There is no DisplayOrder column, and pkid order is meaningless for a 215-row
  // reference table — sort by name.
  protected sortState: SortState = { sortField: 'description', sortOrder: 1 };
  protected pageState: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  ngOnInit(): void {
    this.filters = { ...EMPTY_COURSE_GROUP_QUERY, ...readSession<CourseGroupQuery>(FILTERS_KEY) };
    this.appliedFilters.set({ ...this.filters });
    this.sortState = { ...this.sortState, ...readSession<SortState>(SORT_KEY) };
    this.pageState = { ...this.pageState, ...readSession<PageState>(PAGE_KEY) };

    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters()).subscribe({
      next: (groups) => {
        this.groups.set(groups);
        this.loading.set(false);
      },
      error: () => {
        this.groups.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入課程群組清單。',
        });
      },
    });
  }

  protected get hasActiveFilters(): boolean {
    return !!this.appliedFilters().keyword;
  }

  protected applyFilters(): void {
    this.appliedFilters.set({ ...this.filters });
    writeSession(FILTERS_KEY, this.filters);
    this.pageState = { ...this.pageState, first: 0 };
    writeSession(PAGE_KEY, this.pageState);
    this.drawerVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_COURSE_GROUP_QUERY };
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

  protected confirmDelete(group: CourseGroup): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${group.pkid}</b>「${group.description}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(group),
    });
  }

  private delete(group: CourseGroup): void {
    this.service.delete(group.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `課程群組「${group.description}」已刪除。`,
        });
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail:
            error.status === 409
              ? '此課程群組已被課程或廠商課程群組使用，無法刪除。'
              : `無法刪除課程群組「${group.description}」。`,
        }),
    });
  }

  protected goToNew(): void {
    void this.router.navigate(['/course-groups/new']);
  }
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
