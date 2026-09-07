/** 角色 AppRole — mirrors CMS.API `AppRole`. */
export interface AppRole {
  pkid: number;
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description?: string | null;
  /** 使用者數 — AppUserRole 關聯筆數 */
  userCount: number;
  /** 關聯使用者, 只在 GET by pkid 時回傳 */
  userIds: string[];
}

/** 角色寫入 DTO — mirrors CMS.API `AppRoleRequest`. */
export interface AppRoleRequest {
  pkid: number;
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description?: string | null;
  userIds: string[];
}

/** 角色查詢條件 — mirrors CMS.API `AppRoleQuery`. */
export interface AppRoleQuery {
  keyword?: string | null;
  permissionLevelFrom?: number | null;
  permissionLevelTo?: number | null;
}

export const EMPTY_APP_ROLE_QUERY: AppRoleQuery = {
  keyword: null,
  permissionLevelFrom: null,
  permissionLevelTo: null,
};
