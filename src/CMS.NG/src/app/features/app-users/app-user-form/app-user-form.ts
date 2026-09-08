import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';

import { AppRoleLookup } from '@core/models/app-role.model';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';

@Component({
  selector: 'app-app-user-form',
  imports: [ReactiveFormsModule, ButtonModule, CheckboxModule, InputTextModule, MultiSelectModule],
  templateUrl: './app-user-form.html',
  styleUrl: './app-user-form.scss',
})
export class AppUserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly roles = signal<AppRoleLookup[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly pkid = signal(0);

  // No password control, by contract: the API sets the default password on create and
  // only the reset-password endpoint (detail page) can change it afterwards.
  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(200)]],
    userName: ['', [Validators.required, Validators.maxLength(200)]],
    isActive: [true],
    roleIds: this.fb.nonNullable.control<string[]>([]),
  });

  protected get isEdit(): boolean {
    return this.pkid() > 0;
  }

  protected get title(): string {
    return this.isEdit ? '編輯使用者' : '新增使用者';
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const pkid = idParam ? Number(idParam) : 0;
    this.pkid.set(Number.isFinite(pkid) ? pkid : 0);

    this.loading.set(true);

    forkJoin({
      roles: this.lookupService.getAppRoles().pipe(catchError(() => of([] as AppRoleLookup[]))),
      user: this.pkid() > 0 ? this.service.getById(this.pkid()) : of(null),
    }).subscribe({
      next: ({ roles, user }) => {
        this.roles.set(roles);
        if (user) {
          this.patchForm(user);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入使用者資料。',
        });
      },
    });
  }

  private patchForm(user: AppUser): void {
    this.form.patchValue({
      userId: user.userId,
      userName: user.userName,
      isActive: user.isActive,
      roleIds: user.roleIds ?? [],
    });
    // UserId is the clustered PK referenced by AppUserRole — immutable once created.
    this.form.controls.userId.disable();
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
        detail: '請修正紅字標示的欄位後再儲存。',
      });
      return;
    }

    const value = this.form.getRawValue();
    const request: AppUserRequest = {
      pkid: this.pkid(),
      userId: value.userId.trim(),
      userName: value.userName.trim(),
      isActive: value.isActive,
      roleIds: value.roleIds,
    };

    this.saving.set(true);
    const request$ = this.isEdit ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit ? '更新成功' : '新增成功',
          detail: this.isEdit
            ? `使用者「${saved.userId}」已儲存。`
            : `使用者「${saved.userId}」已建立，密碼為系統預設密碼。`,
        });
        void this.router.navigate(['/app-users', saved.pkid]);
      },
      // UserId is the only natural key; it is immutable on update, so 409 is a create-only outcome.
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error.status === 409
              ? `帳號「${request.userId}」已存在。`
              : '儲存使用者時發生錯誤。',
        });
      },
    });
  }

  protected cancel(): void {
    void this.router.navigate(this.isEdit ? ['/app-users', this.pkid()] : ['/app-users']);
  }
}
