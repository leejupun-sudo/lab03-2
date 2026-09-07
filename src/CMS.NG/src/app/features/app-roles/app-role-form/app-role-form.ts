import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { TextareaModule } from 'primeng/textarea';

import { AppRole, AppRoleRequest } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user.model';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';

const DEFAULT_PERMISSION_LEVEL = 100;

@Component({
  selector: 'app-app-role-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    MultiSelectModule,
    TextareaModule,
  ],
  templateUrl: './app-role-form.html',
  styleUrl: './app-role-form.scss',
})
export class AppRoleForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppRoleService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly users = signal<AppUserLookup[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly pkid = signal(0);

  protected readonly form = this.fb.nonNullable.group({
    roleId: ['', [Validators.required, Validators.maxLength(200)]],
    roleName: ['', [Validators.required, Validators.maxLength(200)]],
    permissionLevel: [DEFAULT_PERMISSION_LEVEL, [Validators.required, Validators.min(0)]],
    description: this.fb.control<string | null>(null, [Validators.maxLength(400)]),
    userIds: this.fb.nonNullable.control<string[]>([]),
  });

  protected get isEdit(): boolean {
    return this.pkid() > 0;
  }

  protected get title(): string {
    return this.isEdit ? '編輯角色' : '新增角色';
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const pkid = idParam ? Number(idParam) : 0;
    this.pkid.set(Number.isFinite(pkid) ? pkid : 0);

    this.loading.set(true);

    forkJoin({
      users: this.lookupService.getAppUsers().pipe(catchError(() => of([] as AppUserLookup[]))),
      role: this.pkid() > 0 ? this.service.getById(this.pkid()) : of(null),
    }).subscribe({
      next: ({ users, role }) => {
        this.users.set(users);
        if (role) {
          this.patchForm(role);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入角色資料。',
        });
      },
    });
  }

  private patchForm(role: AppRole): void {
    this.form.patchValue({
      roleId: role.roleId,
      roleName: role.roleName,
      permissionLevel: role.permissionLevel,
      description: role.description ?? null,
      userIds: role.userIds ?? [],
    });
    // RoleId is the natural key referenced by AppUserRole — immutable once created.
    this.form.controls.roleId.disable();
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
    const request: AppRoleRequest = {
      pkid: this.pkid(),
      roleId: value.roleId.trim(),
      roleName: value.roleName.trim(),
      permissionLevel: value.permissionLevel,
      description: value.description?.trim() || null,
      userIds: value.userIds,
    };

    this.saving.set(true);
    const request$ = this.isEdit ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit ? '更新成功' : '新增成功',
          detail: `角色「${saved.roleId}」已儲存。`,
        });
        void this.router.navigate(['/app-roles', saved.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error.status === 409
              ? `角色代碼「${request.roleId}」已存在。`
              : '儲存角色時發生錯誤。',
        });
      },
    });
  }

  protected cancel(): void {
    void this.router.navigate(this.isEdit ? ['/app-roles', this.pkid()] : ['/app-roles']);
  }
}
