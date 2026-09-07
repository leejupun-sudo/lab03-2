/** 使用者下拉選項 — mirrors CMS.API `AppUserLookup`. */
export interface AppUserLookup {
  userId: string;
  userName: string;
  isActive: boolean;
  /** 顯示標籤, 例: `Miles Sun (miles@uuu.com.tw)` */
  label: string;
}
