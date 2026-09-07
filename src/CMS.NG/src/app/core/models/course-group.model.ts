/**
 * 課程群組 CourseGroup — mirrors CMS.API `CourseGroup`.
 *
 * `pkid` is a smallint IDENTITY. `description` carries no uniqueness rule — the live
 * table holds duplicates, so the API never rejects a repeated name.
 */
export interface CourseGroup {
  pkid: number;
  description: string;
  /** 使用中的課程數 — Course 參照筆數 */
  courseCount: number;
  /** 使用中的廠商課程群組數 — PartnerCourseGroup 參照筆數 */
  partnerCourseGroupCount: number;
}

/** 課程群組寫入 DTO — mirrors CMS.API `CourseGroupRequest`. */
export interface CourseGroupRequest {
  pkid: number;
  description: string;
}

/** 課程群組查詢條件 — mirrors CMS.API `CourseGroupQuery`. */
export interface CourseGroupQuery {
  keyword?: string | null;
}

export const EMPTY_COURSE_GROUP_QUERY: CourseGroupQuery = {
  keyword: null,
};

/** 課程群組下拉選項 — mirrors CMS.API `CourseGroupLookup`. */
export interface CourseGroupLookup {
  pkid: number;
  description: string;
  /** 顯示標籤 — 即群組名稱 */
  label: string;
}
