/**
 * 上稿作業 FeaturedPromoItem — mirrors CMS.API `FeaturedPromoItem`.
 *
 * One row per (scheduleOn, trainingCenterPkid, slot) — the database enforces that triple
 * with a UNIQUE index, so the weekly grid is exactly 7 days × 3 slots per centre. Both FK
 * labels (`trainingCenterName`, `promoCode`) arrive JOINed onto the row. `topic` and
 * `description` are the row's own text, not a copy of the promotion's. Dates are ISO
 * `yyyy-MM-dd` strings on the wire.
 */
export interface FeaturedPromoItem {
  pkid: number;
  scheduleOn: string;
  trainingCenterPkid: number;
  /** 據點名稱 — JOIN TrainingCenter.Name */
  trainingCenterName: string;
  /** 時段 1..3 */
  slot: number;
  promotionPkid: number;
  /** 促銷代碼 — JOIN Promotion2.PromoCode */
  promoCode: string;
  topic: string;
  description: string;
}

/** 上稿寫入 DTO — mirrors CMS.API `FeaturedPromoItemRequest`. */
export interface FeaturedPromoItemRequest {
  pkid: number;
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
}

/**
 * 上稿查詢條件 — mirrors CMS.API `FeaturedPromoItemQuery`. `weekOf` is any day of the
 * wanted week; the API widens it to Monday..Sunday.
 */
export interface FeaturedPromoItemQuery {
  trainingCenterPkid?: number | null;
  weekOf?: string | null;
}

export const EMPTY_FEATURED_PROMO_ITEM_QUERY: FeaturedPromoItemQuery = {
  trainingCenterPkid: null,
  weekOf: null,
};

/** 首頁固定三個時段 — mirrors `FeaturedPromoItemRequest.SlotCount`. */
export const FEATURED_PROMO_SLOT_COUNT = 3;

/** The slot numbers, in grid order. */
export const FEATURED_PROMO_SLOTS: readonly number[] = [1, 2, 3];

/** 據點下拉／頁籤選項 — mirrors CMS.API `TrainingCenterLookup`. 5 rows. */
export interface TrainingCenterLookup {
  pkid: number;
  name: string;
  appKey: string;
  /** 顯示標籤 — 即據點名稱 */
  label: string;
}

/**
 * 促銷活動查詢項目 — mirrors CMS.API `Promotion2Lookup`. Served by an autocomplete endpoint
 * capped at 20 rows, newest first — never a full list.
 */
export interface Promotion2Lookup {
  pkid: number;
  promoCode: string;
  topic: string;
  description: string;
  /** 顯示標籤 — 即促銷代碼 */
  label: string;
}

/** The fixed part of a slot the inline form is editing: which day, centre and slot. */
export interface FeaturedPromoSlotContext {
  scheduleOn: string;
  trainingCenterPkid: number;
  trainingCenterName: string;
  slot: number;
}

/** What 複製 carries to 貼上 — the content, never the key. */
export interface FeaturedPromoClipboard {
  promotionPkid: number;
  promoCode: string;
  topic: string;
  description: string;
}

/** Builds the clipboard payload from a row. */
export function toClipboard(item: FeaturedPromoItem): FeaturedPromoClipboard {
  return {
    promotionPkid: item.promotionPkid,
    promoCode: item.promoCode,
    topic: item.topic,
    description: item.description,
  };
}
