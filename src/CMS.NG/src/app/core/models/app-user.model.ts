/**
 * 使用者 AppUser — mirrors CMS.API `AppUser`.
 *
 * There is deliberately no `passwordHash` here, on `AppUserRequest`, or anywhere in the
 * frontend: the column never crosses the API (see `spec/auth/AppUser.md`).
 */
export interface AppUser {
  pkid: number;
  /** 帳號 — 叢集主鍵, 建立後不可變更 */
  userId: string;
  userName: string;
  isActive: boolean;
  /** 密碼更新時間 (API datetime, no timezone suffix); null = 仍為系統預設密碼 */
  passwordUpdatedTime?: string | null;
  /** 角色數 — AppUserRole 關聯筆數 */
  roleCount: number;
  /** 關聯角色代碼, 只在 GET by pkid 時回傳 */
  roleIds: string[];
}

/** 使用者寫入 DTO — mirrors CMS.API `AppUserRequest`. No password field by contract. */
export interface AppUserRequest {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  roleIds: string[];
}

/** 使用者查詢條件 — mirrors CMS.API `AppUserQuery`. Dates travel as `yyyy-MM-dd`. */
export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
  roleId?: string | null;
  passwordUpdatedFrom?: string | null;
  passwordUpdatedTo?: string | null;
}

export const EMPTY_APP_USER_QUERY: AppUserQuery = {
  keyword: null,
  isActive: null,
  roleId: null,
  passwordUpdatedFrom: null,
  passwordUpdatedTo: null,
};

/** 使用者下拉選項 — mirrors CMS.API `AppUserLookup`. */
export interface AppUserLookup {
  userId: string;
  userName: string;
  isActive: boolean;
  /** 顯示標籤, 例: `Miles Sun (miles@uuu.com.tw)` */
  label: string;
}
