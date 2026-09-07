import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppUserLookup } from '@core/models/app-user.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';

@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/lookups`;

  getAppUsers(): Observable<AppUserLookup[]> {
    return this.http.get<AppUserLookup[]>(`${this.baseUrl}/app-users`);
  }

  getPublishStatuses(): Observable<PublishStatusLookup[]> {
    return this.http.get<PublishStatusLookup[]>(`${this.baseUrl}/publish-statuses`);
  }

  /**
   * 課程群組下拉選項. 資料庫約有 215 筆 — 消費端請設定
   * `[filter]="true"` 與 `[virtualScroll]="true" [virtualScrollItemSize]="43"`.
   */
  getCourseGroups(): Observable<CourseGroupLookup[]> {
    return this.http.get<CourseGroupLookup[]>(`${this.baseUrl}/course-groups`);
  }
}
