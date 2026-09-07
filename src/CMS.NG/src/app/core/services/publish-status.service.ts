import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env';
import {
  PublishStatus,
  PublishStatusQuery,
  PublishStatusRequest,
} from '@core/models/publish-status.model';

@Injectable({ providedIn: 'root' })
export class PublishStatusService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/publish-statuses`;

  getAll(): Observable<PublishStatus[]> {
    return this.http.get<PublishStatus[]>(this.baseUrl);
  }

  query(query: PublishStatusQuery): Observable<PublishStatus[]> {
    return this.http.post<PublishStatus[]>(`${this.baseUrl}/query`, query);
  }

  getById(pkid: number): Observable<PublishStatus> {
    return this.http.get<PublishStatus>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  create(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.post<PublishStatus>(this.baseUrl, request);
  }

  /** 更新 — 主代碼由 body 帶入 (無 route param); 主代碼本身不會被寫入. */
  update(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.put<PublishStatus>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }
}
