/**
 * 課程 Course — mirrors CMS.API `Course`.
 *
 * `pkid` is an int IDENTITY. `courseId` is unique (a live UNIQUE index the DDL never
 * declares) and immutable after creation — `CourseRecomm` references courses by that
 * string, not by pkid. `title`, `prodCourseId` and `friendlyUrl` all hold duplicates.
 * The three FK labels arrive JOINed onto the row, so the list renders without lookups.
 * Dates are ISO `yyyy-MM-dd` strings on the wire.
 */
export interface Course {
  pkid: number;
  title: string;
  officialTitle?: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  /** 原廠名稱 — JOIN Partner.Name */
  partnerName: string;
  courseGroupPkid?: number | null;
  /** 課程群組名稱 — LEFT JOIN CourseGroup.Description */
  courseGroupDescription?: string | null;
  publishStatusPkid: number;
  /** 上架狀態名稱 — JOIN PublishStatus.Description */
  publishStatusDescription: string;
  scheduleOn: string;
  scheduleOff: string;
  hour: number;
  listPrice: number;
  /** 點數 — decimal(9,1); 線上 387 筆帶小數 */
  learningCredit: number;
  material?: string | null;
  objective?: string | null;
  target?: string | null;
  prerequisites?: string | null;
  /** 課程大綱 — 線上 112 筆含 HTML 標籤; 以純文字呈現, 不用 innerHTML */
  outline?: string | null;
  towardCertOrExam?: string | null;
  note?: string | null;
  otherInfo?: string | null;
  canRepeat: boolean;
  /** 對應課程問答數 — CourseFAQ 參照筆數 */
  faqCount: number;
  /** 對應相關連結數 — CourseRelatedLink 參照筆數 */
  relatedLinkCount: number;
  /** 對應熱門課程數 — HotCourse 參照筆數 */
  hotCourseCount: number;
  /** 對應推薦課程數 — CourseRecomm 以 CourseId 參照的筆數 (資料庫無 FK 約束) */
  recommCount: number;
  /** 對應認證 pkid — 只有 getById 回傳 */
  certificationPkids: number[];
  /** 職務類別 pkid — 只有 getById 回傳 */
  jobCategoryPkids: number[];
}

/** 課程寫入 DTO — mirrors CMS.API `CourseRequest`. `courseId` is ignored on update. */
export interface CourseRequest {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  scheduleOn: string;
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

/** 課程查詢條件 — mirrors CMS.API `CourseQuery`. Dates are ISO strings. */
export interface CourseQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  courseGroupPkid?: number | null;
  publishStatusPkid?: number | null;
  scheduleOnFrom?: string | null;
  scheduleOnTo?: string | null;
  scheduleOffFrom?: string | null;
  scheduleOffTo?: string | null;
  canRepeat?: boolean | null;
}

export const EMPTY_COURSE_QUERY: CourseQuery = {
  keyword: null,
  partnerPkid: null,
  courseGroupPkid: null,
  publishStatusPkid: null,
  scheduleOnFrom: null,
  scheduleOnTo: null,
  scheduleOffFrom: null,
  scheduleOffTo: null,
  canRepeat: null,
};

/** 複製課程的請求 — mirrors CMS.API `CourseCopyRequest`. */
export interface CourseCopyRequest {
  newCourseId: string;
}

/** 課程下拉選項 — mirrors CMS.API `CourseLookup`. ~1084 rows: needs virtual scroll. */
export interface CourseLookup {
  pkid: number;
  courseId: string;
  title: string;
  /** 顯示標籤, 例: `PLF Oracle資料庫之PL／SQL基礎` */
  label: string;
}

/** 認證下拉選項 — mirrors CMS.API `CertificationLookup`. */
export interface CertificationLookup {
  pkid: number;
  title: string;
  partnerName: string;
  /** 顯示標籤, 例: `FCP-PCS (Fortinet資安專家認證課程)` */
  label: string;
}

/** 職務類別下拉選項 — mirrors CMS.API `JobCategoryLookup`. */
export interface JobCategoryLookup {
  pkid: number;
  description: string;
  /** 顯示標籤 — 即類別名稱 */
  label: string;
}

/** 四個使用計數的總和 — 全為 0 時課程才可刪除. */
export function totalCourseUsage(course: Course): number {
  return course.faqCount + course.relatedLinkCount + course.hotCourseCount + course.recommCount;
}
