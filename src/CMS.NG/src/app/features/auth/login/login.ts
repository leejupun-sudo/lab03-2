import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '@core/services/auth.service';

/** Where a successful login lands when no `returnUrl` was carried in. */
const DEFAULT_LANDING = '/featured-promo-items';

/**
 * 登入頁 — the one public route.
 *
 * Deliberately not the house list/detail/form shape: there is no shell around it (the app
 * renders the sidebar only once signed in), so it is a single centred card.
 */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, PasswordModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly submitting = signal(false);

  /** Shown in the card rather than as a toast — the failure belongs to the form. */
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(200)]],
    password: ['', [Validators.required]],
  });

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { userId, password } = this.form.getRawValue();

    this.submitting.set(true);
    this.errorMessage.set('');

    this.auth.login({ userId: userId.trim(), password }).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl(this.returnUrl());
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        // The API answers every rejected credential with the same 401 body, on purpose.
        this.errorMessage.set(
          error.status === 401 ? '帳號或密碼錯誤。' : '登入時發生錯誤，請稍後再試。',
        );
        this.form.controls.password.reset();
      },
    });
  }

  /**
   * Only same-origin paths are honoured. `returnUrl` comes from the query string, so a value
   * pointing at another site would turn the login page into an open redirect.
   */
  private returnUrl(): string {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl');
    return requested?.startsWith('/') && !requested.startsWith('//') ? requested : DEFAULT_LANDING;
  }
}
