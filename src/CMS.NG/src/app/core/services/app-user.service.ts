import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppUser, AppUserQuery, AppUserRequest } from '@core/models/app-user.model';

@Injectable({ providedIn: 'root' })
export class AppUserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/app-users`;

  getAll(): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.baseUrl);
  }

  query(query: AppUserQuery): Observable<AppUser[]> {
    return this.http.post<AppUser[]>(`${this.baseUrl}/query`, query);
  }

  getById(pkid: number): Observable<AppUser> {
    return this.http.get<AppUser>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  create(request: AppUserRequest): Observable<AppUser> {
    return this.http.post<AppUser>(this.baseUrl, request);
  }

  /** 更新 — 主代碼由 body 帶入 (無 route param). 帳號與密碼不會被更新. */
  update(request: AppUserRequest): Observable<AppUser> {
    return this.http.put<AppUser>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  /** 重設為系統預設密碼. 不送任何 body — 密碼無法經由前端指定. */
  resetPassword(pkid: number): Observable<AppUser> {
    return this.http.post<AppUser>(`${this.baseUrl}/${encodeURIComponent(pkid)}/reset-password`, null);
  }
}
