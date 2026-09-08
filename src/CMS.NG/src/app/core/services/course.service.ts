import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { Course, CourseCopyRequest, CourseQuery, CourseRequest } from '@core/models/course.model';

@Injectable({ providedIn: 'root' })
export class CourseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/courses`;

  getAll(): Observable<Course[]> {
    return this.http.get<Course[]>(this.baseUrl);
  }

  query(query: CourseQuery): Observable<Course[]> {
    return this.http.post<Course[]>(`${this.baseUrl}/query`, query);
  }

  /** Single row — the only call that carries `certificationPkids` / `jobCategoryPkids`. */
  getById(pkid: number): Observable<Course> {
    return this.http.get<Course>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  create(request: CourseRequest): Observable<Course> {
    return this.http.post<Course>(this.baseUrl, request);
  }

  /** 更新 — 主代碼由 body 帶入 (無 route param); `courseId` in the body is ignored. */
  update(request: CourseRequest): Observable<Course> {
    return this.http.put<Course>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(pkid)}`);
  }

  /** 複製 — every field and both junction sets, under a new 簡介代碼. 409 when it is taken. */
  copy(pkid: number, request: CourseCopyRequest): Observable<Course> {
    return this.http.post<Course>(`${this.baseUrl}/${encodeURIComponent(pkid)}/copy`, request);
  }
}
