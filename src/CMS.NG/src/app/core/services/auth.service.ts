import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '@env';
import {
  AuthProfile,
  LoginRequest,
  UpdateProfileRequest,
  UserProfile,
} from '@core/models/auth.model';

/** Session-storage key holding the signed-in profile. */
export const AUTH_STORAGE_KEY = 'cms-auth';

/** The role that unlocks the 系統管理 Admin sidebar group. */
export const ADMIN_ROLE = 'Admin';

/** Claim the API writes one of per `AppUserRole` row (short name — see `spec/auth/Login.md`). */
const ROLE_CLAIM = 'role';

/**
 * 登入狀態的單一來源.
 *
 * The profile lives in **session storage**, not local storage: it is scoped to the one browser
 * tab and disappears when that tab closes, so a shared machine does not leave the next person
 * signed in.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  // Routes are case-insensitive server-side; this matches the URL the spec documents.
  private readonly baseUrl = `${environment.apiBaseUrl}/Auth`;

  /** Bumped whenever the stored session changes, so the computed views below recompute. */
  private readonly revision = signal(0);

  readonly profile = computed<AuthProfile | null>(() => {
    this.revision();
    return readProfile();
  });

  readonly userId = computed(() => this.profile()?.userId ?? '');
  readonly userName = computed(() => this.profile()?.userName ?? '');
  readonly isAuthenticated = computed(() => this.profile() !== null);

  /**
   * 角色 — 從 access token 的 `role` claims 解出, **不另外呼叫 API**.
   *
   * This is a UI convenience only. The claims are read without verifying the signature, so a
   * determined user could hand-craft a token that unhides a menu item; the API validates the
   * signature on every request, so nothing behind that menu would actually answer.
   */
  readonly roles = computed(() => rolesFromToken(this.profile()?.accessToken ?? null));

  readonly isAdmin = computed(() => this.hasRole(ADMIN_ROLE));

  /** 登入 — 成功後把 profile 寫進 session storage. */
  login(request: LoginRequest): Observable<AuthProfile> {
    return this.http
      .post<AuthProfile>(`${this.baseUrl}/login`, request)
      .pipe(tap((profile) => this.store(profile)));
  }

  /**
   * 修改個人資料 — 只送姓名, 帳號由後端從 token 取得.
   *
   * On success the stored profile's `userName` is replaced in place, which is what refreshes the
   * shell: {@link userName} is computed off the same storage. The **access token is left
   * untouched** — the API does not re-issue one, so its `userName` claim keeps the pre-rename
   * value. Nothing reads that claim (the shell reads the profile, the API reads `userId` and
   * `role`), and re-issuing would quietly restart the 24-hour expiry on a rename.
   */
  updateProfile(request: UpdateProfileRequest): Observable<UserProfile> {
    return this.http
      .put<UserProfile>(`${this.baseUrl}/profile`, request)
      .pipe(tap((profile) => this.storeUserName(profile.userName)));
  }

  /**
   * The bearer token, read straight from session storage on every call — the interceptor must
   * see a token another tab or a just-completed login wrote, not a memoised copy.
   */
  token(): string | null {
    return readProfile()?.accessToken ?? null;
  }

  /** Role names compare case-insensitively; `AppRole.RoleId` uses a CI collation. */
  hasRole(role: string): boolean {
    return this.roles().some((held) => held.toLowerCase() === role.toLowerCase());
  }

  /** 登出 — 清掉整個 session storage. */
  logout(): void {
    this.clearSession();
  }

  /**
   * Clears the whole of session storage, not just the profile.
   *
   * The list pages park their filters, sort and page there under `{entity}-list-*` keys. Those
   * are the previous user's view of the data, so signing out — or being signed out by a 401 —
   * must not leave them for whoever signs in next.
   */
  clearSession(): void {
    sessionStorage.clear();
    this.revision.update((value) => value + 1);
  }

  private store(profile: AuthProfile): void {
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(profile));
    this.revision.update((value) => value + 1);
  }

  /**
   * Rewrites just the name on the stored profile, keeping the token and the account.
   *
   * Storage is re-read rather than taken from {@link profile}, for the same reason
   * {@link token} re-reads it: the value on disk is the authority.
   */
  private storeUserName(userName: string): void {
    const current = readProfile();
    if (current) {
      this.store({ ...current, userName });
    }
  }
}

/** Reads the stored profile, treating anything unparsable or incomplete as "not signed in". */
function readProfile(): AuthProfile | null {
  const raw = sessionStorage.getItem(AUTH_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<AuthProfile>;
    return parsed.accessToken
      ? {
          userId: parsed.userId ?? '',
          userName: parsed.userName ?? '',
          accessToken: parsed.accessToken,
        }
      : null;
  } catch {
    return null;
  }
}

/** The `role` claims out of a JWS payload. A malformed token yields no roles rather than throwing. */
function rolesFromToken(token: string | null): string[] {
  const payload = token ? decodePayload(token) : null;
  const claim = payload?.[ROLE_CLAIM];

  // One role arrives as a bare string, several as an array — the API writes one claim per row.
  if (typeof claim === 'string') {
    return [claim];
  }
  return Array.isArray(claim) ? claim.filter((role): role is string => typeof role === 'string') : [];
}

function decodePayload(token: string): Record<string, unknown> | null {
  const segments = token.split('.');
  if (segments.length !== 3) {
    return null;
  }

  try {
    // base64url → base64, re-padded; then UTF-8 decoded so a Chinese userName survives.
    const base64 = segments[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
    const bytes = Uint8Array.from(atob(padded), (char) => char.charCodeAt(0));
    return JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>;
  } catch {
    return null;
  }
}
