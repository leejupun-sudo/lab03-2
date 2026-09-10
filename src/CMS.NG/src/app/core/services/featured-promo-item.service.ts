import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env';
import {
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
} from '@core/models/featured-promo-item.model';

@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;

  /** Every row — ~31 000 live. The weekly page never calls this; it exists for parity. */
  getAll(): Observable<FeaturedPromoItem[]> {
    return this.http.get<FeaturedPromoItem[]>(this.baseUrl);
  }

  /** One centre, one week — the API resolves `weekOf` to its Monday..Sunday. */
  query(query: FeaturedPromoItemQuery): Observable<FeaturedPromoItem[]> {
    return this.http.post<FeaturedPromoItem[]>(`${this.baseUrl}/query`, query);
  }

  getById(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  /** 409 when the (date, centre, slot) is already taken. */
  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** 更新 — 主代碼由 body 帶入 (無 route param). 409 when moving onto a taken slot. */
  update(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.put<FeaturedPromoItem>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  /** 「−」: slot − 1, swapping with whatever sits there. 409 when already on slot 1. */
  moveUp(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(`${this.baseUrl}/${encodeURIComponent(pkid)}/move-up`, null);
  }

  /** 「+」: slot + 1, swapping with whatever sits there. 409 when already on the last slot. */
  moveDown(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(`${this.baseUrl}/${encodeURIComponent(pkid)}/move-down`, null);
  }
}
