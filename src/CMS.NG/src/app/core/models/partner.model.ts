/**
 * 合作廠商 Partner — mirrors CMS.API `Partner`.
 *
 * `pkid` is a smallint IDENTITY and is immutable: five tables reference it.
 * `name` carries no uniqueness rule (the live table holds duplicates), but `appKey`
 * does — an application-level rule the API enforces with a 409.
 */
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  /**
   * 圖檔名稱 — 自由文字, 不保證帶副檔名.
   * API 以 `WhenWritingNull` 序列化, 所以 null 時整個欄位不會出現在 JSON.
   */
  imageFilename?: string | null;
  /** 使用中的課程數 — Course 參照筆數 */
  courseCount: number;
  /** 使用中的認證數 — Certification 參照筆數 */
  certificationCount: number;
  /** 使用中的廠商課程群組數 — PartnerCourseGroup 參照筆數 */
  partnerCourseGroupCount: number;
  /** 使用中的促銷活動數 — Promotion2.RelatedPartner_pkid 參照筆數 */
  promotion2Count: number;
  /** 使用中的研討會數 — Seminar.Partner_pkid 參照筆數 (資料庫無 FK 約束) */
  seminarCount: number;
}

/** 合作廠商寫入 DTO — mirrors CMS.API `PartnerRequest`. */
export interface PartnerRequest {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

/** 合作廠商查詢條件 — mirrors CMS.API `PartnerQuery`. */
export interface PartnerQuery {
  keyword?: string | null;
}

export const EMPTY_PARTNER_QUERY: PartnerQuery = {
  keyword: null,
};

/** 合作廠商下拉選項 — mirrors CMS.API `PartnerLookup`. */
export interface PartnerLookup {
  pkid: number;
  name: string;
  appKey: string;
  /** 顯示標籤, 例: `國際標準課程 (ISO)` — 名稱不唯一, 故附上 AppKey */
  label: string;
}

/** 顯示順序的「排在最後」慣例值 — 線上有 23 筆使用. */
export const PARTNER_DISPLAY_ORDER_LAST = 9999;

/** 五個使用計數的總和 — 清單頁以單一「使用中」欄位呈現. */
export function totalPartnerUsage(partner: Partner): number {
  return (
    partner.courseCount +
    partner.certificationCount +
    partner.partnerCourseGroupCount +
    partner.promotion2Count +
    partner.seminarCount
  );
}
