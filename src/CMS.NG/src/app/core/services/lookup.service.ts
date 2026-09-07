import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppUserLookup } from '@core/models/app-user.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { PartnerLookup } from '@core/models/partner.model';
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

  /**
   * 合作廠商下拉選項. 資料庫約有 66 筆 — 消費端請設定 `[filter]="true"`.
   * 未達 ~100 筆門檻, 不需要 `[virtualScroll]`.
   * `label` 已含 AppKey (`國際標準課程 (ISO)`), 因為 Name 並不唯一.
   */
  getPartners(): Observable<PartnerLookup[]> {
    return this.http.get<PartnerLookup[]>(`${this.baseUrl}/partners`);
  }
}
