/**
 * 發布狀態 PublishStatus — mirrors CMS.API `PublishStatus`.
 *
 * `pkid` is a non-IDENTITY tinyint: the client chooses it on create and it can never
 * change afterwards (Course and Promotion2 reference it).
 */
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
  /** 使用中的課程數 — Course 參照筆數 */
  courseCount: number;
  /** 使用中的活動數 — Promotion2 參照筆數 */
  promotion2Count: number;
}

/** 發布狀態寫入 DTO — mirrors CMS.API `PublishStatusRequest`. */
export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

/** 發布狀態查詢條件 — mirrors CMS.API `PublishStatusQuery`. */
export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}

export const EMPTY_PUBLISH_STATUS_QUERY: PublishStatusQuery = {
  keyword: null,
  isDraft: null,
  isPublished: null,
  isDiscontinued: null,
};

/** 發布狀態下拉選項 — mirrors CMS.API `PublishStatusLookup`. */
export interface PublishStatusLookup {
  pkid: number;
  description: string;
  /** 顯示標籤 — 即狀態名稱 */
  label: string;
}
