import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';

import { ADMIN_ROLE, AuthService } from '@core/services/auth.service';

/** A single leaf item in the sidebar. */
export interface NavItem {
  label: string;
  icon: string;
  route: string;
}

/** A collapsible group in the sidebar. */
export interface NavGroup {
  label: string;
  icon: string;
  expanded: boolean;
  items: NavItem[];
  /** Role required to see the group. Absent means everyone signed in sees it. */
  requiresRole?: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastModule, ConfirmDialogModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly title = signal('UWA');
  protected readonly sidebarCollapsed = signal(false);

  /** The shell (sidebar + header) exists only for a signed-in user; /login renders bare. */
  protected readonly signedIn = this.auth.isAuthenticated;
  protected readonly userName = this.auth.userName;

  /** Sidebar nav groups. Add new features under the matching group. */
  private readonly allNavGroups = signal<NavGroup[]>([
    {
      label: '首頁 Home',
      icon: 'pi pi-home',
      expanded: true,
      items: [
        { label: '上稿作業 FeaturedPromoItem', icon: 'pi pi-calendar', route: '/featured-promo-items' },
      ],
    },
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      expanded: true,
      // Only an Admin sees the user/role/status maintenance group.
      requiresRole: ADMIN_ROLE,
      items: [
        { label: '使用者 AppUser', icon: 'pi pi-users', route: '/app-users' },
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' },
      ],
    },
    {
      label: '課程管理 Course',
      icon: 'pi pi-book',
      expanded: true,
      items: [
        { label: '課程 Course', icon: 'pi pi-book', route: '/courses' },
        { label: '合作廠商 Partner', icon: 'pi pi-building', route: '/partners' },
        { label: '課程群組 CourseGroup', icon: 'pi pi-tags', route: '/course-groups' },
      ],
    },
  ]);

  /**
   * The groups this user may see. Roles come from the stored token's claims, so hiding a group
   * costs no extra request — and it hides the menu only: the routes and the API are the places
   * access is actually decided.
   */
  protected readonly navGroups = computed(() =>
    this.allNavGroups().filter((group) => !group.requiresRole || this.auth.hasRole(group.requiresRole)),
  );

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update((collapsed) => !collapsed);
  }

  protected toggleGroup(target: NavGroup): void {
    this.allNavGroups.update((groups) =>
      groups.map((group) =>
        group.label === target.label ? { ...group, expanded: !group.expanded } : group,
      ),
    );
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
