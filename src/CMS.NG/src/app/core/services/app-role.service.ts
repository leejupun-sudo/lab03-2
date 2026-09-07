import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppRole, AppRoleQuery, AppRoleRequest } from '@core/models/app-role.model';

@Injectable({ providedIn: 'root' })
export class AppRoleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/app-roles`;

  getAll(): Observable<AppRole[]> {
    return this.http.get<AppRole[]>(this.baseUrl);
  }

  query(query: AppRoleQuery): Observable<AppRole[]> {
    return this.http.post<AppRole[]>(`${this.baseUrl}/query`, query);
  }

  getById(pkid: number): Observable<AppRole> {
    return this.http.get<AppRole>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  create(request: AppRoleRequest): Observable<AppRole> {
    return this.http.post<AppRole>(this.baseUrl, request);
  }

  /** 更新 — 主代碼由 body 帶入 (無 route param). */
  update(request: AppRoleRequest): Observable<AppRole> {
    return this.http.put<AppRole>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }
}
