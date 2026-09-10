import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { environment } from '@env';
import { AuthService } from '@core/services/auth.service';

/**
 * 把 session storage 裡的 access token 掛到每一個 API 請求上, 並處理 401.
 *
 * Two halves of one rule, kept together because they are the same rule: the token this
 * interceptor attaches is the one the API rejects, and a rejection means the session is over.
 *
 * - **Outgoing**: `Authorization: Bearer <token>` on every request to {@link environment.apiBaseUrl}.
 *   Requests elsewhere (assets, the public site) are left alone — the token is a credential for
 *   this API and must not be handed to any other host.
 * - **Incoming 401**: the token is missing, expired, was signed with a rotated key, or names an
 *   account that has since been deactivated or deleted (the API re-checks that on every request).
 *   Session storage is cleared and the user lands on the login page.
 * - **Incoming 403**: the token is fine and the account is live — this caller simply may not do
 *   this. The session must **not** be cleared: signing out and back in changes nothing, and
 *   throwing the user to /login would misdescribe what happened. A toast says so instead.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  // Optional on purpose. `app.config.ts` provides MessageService application-wide, so the toast
  // always fires in the real app; making it required here would mean every spec that exercises
  // any HTTP call had to provide a toast service it never uses.
  const messageService = inject(MessageService, { optional: true });

  const isApiRequest = req.url.startsWith(environment.apiBaseUrl);
  const token = auth.token();

  const request =
    isApiRequest && token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (isApiRequest && !isLoginRequest(req.url) && error instanceof HttpErrorResponse) {
        // 只有當「被拒絕的那張 token」仍是現在存著的那一張時才清 session。少了這個比對, 一個在
        // 登出前就送出、卻在重新登入之後才回來的孤兒請求, 會把剛簽發的新 token 一起清掉 — 使用者
        // 看到的是「登入隨機失敗」, 重試又會成功 (因為第二次背後沒有滯留的請求), 幾乎無法從回報
        // 中診斷。token 已在第 34 行取出, 比對不需額外成本。
        if (error.status === 401 && auth.token() === token) {
          auth.clearSession();
          void router.navigate(['/login']);
        } else if (error.status === 403) {
          messageService?.add({
            severity: 'warn',
            summary: '權限不足',
            detail: '此功能僅限系統管理員使用。',
          });
        }
      }
      return throwError(() => error);
    }),
  );
};

/**
 * The login endpoint's own 401 means "帳號或密碼錯誤" and belongs to the login form, which shows
 * it as a message. Treating it as an expired session would clear storage and re-navigate to the
 * page the user is already on, swallowing the error.
 */
function isLoginRequest(url: string): boolean {
  return url.toLowerCase().includes('/auth/login');
}
