import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';

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
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastModule, ConfirmDialogModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('UWA');
  protected readonly sidebarCollapsed = signal(false);

  /** Sidebar nav groups. Add new features under the matching group. */
  protected readonly navGroups = signal<NavGroup[]>([
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      expanded: true,
      items: [
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' },
      ],
    },
  ]);

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update((collapsed) => !collapsed);
  }

  protected toggleGroup(target: NavGroup): void {
    this.navGroups.update((groups) =>
      groups.map((group) =>
        group.label === target.label ? { ...group, expanded: !group.expanded } : group,
      ),
    );
  }
}
