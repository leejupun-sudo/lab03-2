import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

import { AppRole, AppRoleQuery, EMPTY_APP_ROLE_QUERY } from '@core/models/app-role.model';
import { AppRoleService } from '@core/services/app-role.service';

const FILTERS_KEY = 'app-role-list-filters';
const SORT_KEY = 'app-role-list-sort';
const PAGE_KEY = 'app-role-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

@Component({
  selector: 'app-app-role-list',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    DrawerModule,
    InputNumberModule,
    InputTextModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './app-role-list.html',
  styleUrl: './app-role-list.scss',
})
export class AppRoleList implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly roles = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  /** Filters bound to the drawer inputs; committed to `appliedFilters` on 搜尋. */
  protected filters: AppRoleQuery = { ...EMPTY_APP_ROLE_QUERY };
  protected readonly appliedFilters = signal<AppRoleQuery>({ ...EMPTY_APP_ROLE_QUERY });

  protected sortState: SortState = { sortField: 'roleId', sortOrder: 1 };
  protected pageState: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  ngOnInit(): void {
    this.filters = { ...EMPTY_APP_ROLE_QUERY, ...readSession<AppRoleQuery>(FILTERS_KEY) };
    this.appliedFilters.set({ ...this.filters });
    this.sortState = { ...this.sortState, ...readSession<SortState>(SORT_KEY) };
    this.pageState = { ...this.pageState, ...readSession<PageState>(PAGE_KEY) };

    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters()).subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => {
        this.roles.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入角色清單。',
        });
      },
    });
  }

  protected get hasActiveFilters(): boolean {
    const applied = this.appliedFilters();
    return (
      !!applied.keyword ||
      applied.permissionLevelFrom !== null ||
      applied.permissionLevelTo !== null
    );
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
    this.filters = { ...EMPTY_APP_ROLE_QUERY };
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

  protected confirmDelete(role: AppRole): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${role.pkid}</b>「${role.roleId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(role),
    });
  }

  private delete(role: AppRole): void {
    this.service.delete(role.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `角色「${role.roleId}」已刪除。`,
        });
        this.load();
      },
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: `無法刪除角色「${role.roleId}」。`,
        }),
    });
  }

  protected goToNew(): void {
    void this.router.navigate(['/app-roles/new']);
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
