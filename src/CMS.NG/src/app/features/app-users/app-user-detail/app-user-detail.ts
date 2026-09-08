import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';

import { AppRoleLookup } from '@core/models/app-role.model';
import { AppUser } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';

/** A role tag: the label to show, and the pkid to link to when the lookup knows it. */
interface RoleTag {
  roleId: string;
  label: string;
  pkid: number | null;
}

@Component({
  selector: 'app-app-user-detail',
  imports: [DatePipe, RouterLink, ButtonModule, TagModule],
  templateUrl: './app-user-detail.html',
  styleUrl: './app-user-detail.scss',
})
export class AppUserDetail implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly user = signal<AppUser | null>(null);
  protected readonly roles = signal<AppRoleLookup[]>([]);
  protected readonly loading = signal(true);
  protected readonly resetting = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!pkid) {
      this.loading.set(false);
      return;
    }

    forkJoin({
      user: this.service.getById(pkid),
      roles: this.lookupService.getAppRoles().pipe(catchError(() => of([] as AppRoleLookup[]))),
    }).subscribe({
      next: ({ user, roles }) => {
        this.user.set(user);
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: `找不到主代碼 ${pkid} 的使用者。`,
        });
      },
    });
  }

  /** Role tags for the assigned roleIds, falling back to the raw id when the lookup misses. */
  protected get assignedRoles(): RoleTag[] {
    const roles = this.roles();
    return (this.user()?.roleIds ?? []).map((id) => {
      const match = roles.find((role) => role.roleId === id);
      return { roleId: id, label: match?.label ?? id, pkid: match?.pkid ?? null };
    });
  }

  protected confirmResetPassword(): void {
    const user = this.user();
    if (!user) {
      return;
    }

    this.confirmationService.confirm({
      header: '重設密碼',
      message: `確定要將 <b>${user.userId}</b>「${user.userName}」的密碼重設為系統預設密碼？`,
      icon: 'pi pi-key',
      acceptLabel: '重設',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-warning',
      accept: () => this.resetPassword(user),
    });
  }

  private resetPassword(user: AppUser): void {
    this.resetting.set(true);
    this.service.resetPassword(user.pkid).subscribe({
      next: (updated) => {
        this.user.set(updated);
        this.resetting.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '重設成功',
          detail: `使用者「${user.userId}」的密碼已重設為系統預設密碼。`,
        });
      },
      error: () => {
        this.resetting.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '重設失敗',
          detail: `無法重設使用者「${user.userId}」的密碼。`,
        });
      },
    });
  }

  protected goBack(): void {
    void this.router.navigate(['/app-users']);
  }
}
