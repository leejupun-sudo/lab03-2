import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';

import { AuthService } from '@core/services/auth.service';

/**
 * 我的帳號 My Profile — the signed-in user's own record.
 *
 * Not the house list/detail/form shape: there is one row to show and one field to change, so it
 * is a single card with the page header's 儲存 button.
 *
 * **Everything on this page but the name is read-only, and it is read-only in the API too.**
 * 帳號 is the clustered primary key and the `AppUserRole` foreign-key target; 角色 is an admin's
 * to assign. `PUT /api/Auth/profile` takes the account from the token and writes one column, so
 * the read-only rendering here is a statement of what the endpoint does, not the thing enforcing
 * it.
 *
 * 帳號 and 角色 come from the stored session and its token claims rather than from a request —
 * the same no-extra-API-call read the role-gated sidebar already does (`spec/auth/Authorization.md`).
 */
@Component({
  selector: 'app-my-profile',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, TagModule],
  templateUrl: './my-profile.html',
  styleUrl: './my-profile.scss',
})
export class MyProfile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messageService = inject(MessageService);

  protected readonly saving = signal(false);

  /** 帳號 — display only; there is no form control for it, so nothing can submit one. */
  protected readonly userId = this.auth.userId;

  /** 角色 — display only, decoded from the token's `role` claims. */
  protected readonly roles = this.auth.roles;

  protected readonly form = this.fb.nonNullable.group({
    // `required` alone accepts whitespace, so the API's trim-then-reject rule is mirrored with
    // a pattern — the same pairing FeaturedPromoItemForm uses.
    userName: [
      this.auth.userName(),
      [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)],
    ],
  });

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

    const userName = this.form.getRawValue().userName.trim();

    this.saving.set(true);

    this.auth.updateProfile({ userName }).subscribe({
      next: (profile) => {
        this.saving.set(false);
        // The service has already replaced the stored name, so the shell header is current.
        // Re-patching from the response keeps the box showing exactly what was stored.
        this.form.controls.userName.setValue(profile.userName);
        this.form.markAsPristine();
        this.messageService.add({
          severity: 'success',
          summary: '更新成功',
          detail: `姓名已更新為「${profile.userName}」。`,
        });
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '更新失敗',
          detail:
            error.status === 401
              ? '登入狀態已失效，請重新登入。'
              : '更新個人資料時發生錯誤。',
        });
      },
    });
  }

  /** 還原 — drops an uncommitted edit back to the stored name. */
  protected reset(): void {
    this.form.reset({ userName: this.auth.userName() });
  }
}
