import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';

import { AppRole } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-app-role-detail',
  imports: [RouterLink, ButtonModule, TagModule],
  templateUrl: './app-role-detail.html',
  styleUrl: './app-role-detail.scss',
})
export class AppRoleDetail implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly role = signal<AppRole | null>(null);
  protected readonly users = signal<AppUserLookup[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!pkid) {
      this.loading.set(false);
      return;
    }

    forkJoin({
      role: this.service.getById(pkid),
      users: this.lookupService.getAppUsers().pipe(catchError(() => of([] as AppUserLookup[]))),
    }).subscribe({
      next: ({ role, users }) => {
        this.role.set(role);
        this.users.set(users);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: `找不到主代碼 ${pkid} 的角色。`,
        });
      },
    });
  }

  /** User labels for the assigned userIds, falling back to the raw id when the lookup misses. */
  protected get assignedUserLabels(): string[] {
    const users = this.users();
    return (this.role()?.userIds ?? []).map(
      (id) => users.find((user) => user.userId === id)?.label ?? id,
    );
  }

  protected goBack(): void {
    void this.router.navigate(['/app-roles']);
  }
}
