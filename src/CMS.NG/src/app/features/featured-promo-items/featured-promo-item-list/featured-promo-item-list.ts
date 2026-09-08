import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TabsModule } from 'primeng/tabs';
import { TooltipModule } from 'primeng/tooltip';

import {
  FEATURED_PROMO_SLOTS,
  FEATURED_PROMO_SLOT_COUNT,
  FeaturedPromoClipboard,
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoSlotContext,
  TrainingCenterLookup,
  toClipboard,
} from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  addDays,
  formatDayLabel,
  formatMonthDay,
  parseIsoDate,
  startOfWeek,
  toIsoDate,
} from '@core/utils/date.util';
import { FeaturedPromoItemForm } from '../featured-promo-item-form/featured-promo-item-form';

const FILTERS_KEY = 'featured-promo-item-list-filters';

/** What the page remembers between visits: which centre tab and which week. */
interface SavedFilters {
  trainingCenterPkid: number | null;
  weekOf: string | null;
}

/** One cell of the 7 × 3 grid. */
export interface SlotCell {
  slot: number;
  item: FeaturedPromoItem | null;
}

/** One day of the week, with its three slots in order. */
export interface DayGroup {
  iso: string;
  label: string;
  slots: SlotCell[];
}

/** Which cell has the inline form open, and in which mode. */
interface EditTarget {
  scheduleOn: string;
  slot: number;
  item: FeaturedPromoItem | null;
  paste: FeaturedPromoClipboard | null;
}

@Component({
  selector: 'app-featured-promo-item-list',
  imports: [ButtonModule, TabsModule, TooltipModule, FeaturedPromoItemForm],
  templateUrl: './featured-promo-item-list.html',
  styleUrl: './featured-promo-item-list.scss',
})
export class FeaturedPromoItemList implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly trainingCenters = signal<TrainingCenterLookup[]>([]);
  protected readonly trainingCenterPkid = signal<number | null>(null);
  protected readonly weekStart = signal<Date>(startOfWeek(new Date()));
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);

  protected readonly editing = signal<EditTarget | null>(null);
  protected readonly clipboard = signal<FeaturedPromoClipboard | null>(null);

  protected readonly slotCount = FEATURED_PROMO_SLOT_COUNT;

  protected readonly selectedCenter = computed(
    () => this.trainingCenters().find((c) => c.pkid === this.trainingCenterPkid()) ?? null,
  );

  /** `3/16 – 3/22` */
  protected readonly weekLabel = computed(() => {
    const start = this.weekStart();
    return `${formatMonthDay(start)} – ${formatMonthDay(addDays(start, 6))}`;
  });

  /** The grid: Monday..Sunday × slots 1..3, each cell holding its row or null. */
  protected readonly days = computed<DayGroup[]>(() => {
    const start = this.weekStart();
    const byKey = new Map<string, FeaturedPromoItem>();
    for (const item of this.items()) {
      byKey.set(`${item.scheduleOn}#${item.slot}`, item);
    }
    return Array.from({ length: 7 }, (_, offset) => {
      const date = addDays(start, offset);
      const iso = toIsoDate(date);
      return {
        iso,
        label: formatDayLabel(date),
        slots: FEATURED_PROMO_SLOTS.map((slot) => ({ slot, item: byKey.get(`${iso}#${slot}`) ?? null })),
      };
    });
  });

  ngOnInit(): void {
    const saved = readSession<SavedFilters>(FILTERS_KEY);
    let centerPkid = saved.trainingCenterPkid ?? null;
    let weekOf = parseIsoDate(saved.weekOf);

    // Incoming query params win over the saved state — the cross-entity contract.
    const params = this.route.snapshot.queryParamMap;
    const incomingCenter = Number(params.get('trainingCenterPkid'));
    if (params.get('trainingCenterPkid') && Number.isInteger(incomingCenter) && incomingCenter > 0) {
      centerPkid = incomingCenter;
    }
    const incomingWeek = parseIsoDate(params.get('weekOf'));
    if (incomingWeek) {
      weekOf = incomingWeek;
    }

    this.weekStart.set(startOfWeek(weekOf ?? new Date()));

    this.lookupService.getTrainingCenters().subscribe({
      next: (centers) => {
        this.trainingCenters.set(centers);
        // An unknown or missing centre falls back to the first tab (台北, DisplayOrder 1).
        const known = centers.some((c) => c.pkid === centerPkid);
        this.trainingCenterPkid.set(known ? centerPkid : (centers[0]?.pkid ?? null));
        this.persist();
        this.load();
      },
      error: () => {
        this.trainingCenters.set([]);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入據點清單。',
        });
      },
    });
  }

  protected load(): void {
    const trainingCenterPkid = this.trainingCenterPkid();
    if (trainingCenterPkid === null) {
      this.items.set([]);
      return;
    }

    const query: FeaturedPromoItemQuery = {
      trainingCenterPkid,
      weekOf: toIsoDate(this.weekStart()),
    };

    this.loading.set(true);
    this.service.query(query).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入上稿資料。',
        });
      },
    });
  }

  // ---- Tabs and week navigation ----

  protected onTabChange(value: string | number | undefined): void {
    const pkid = Number(value);
    if (!Number.isInteger(pkid) || pkid === this.trainingCenterPkid()) {
      return;
    }
    this.trainingCenterPkid.set(pkid);
    this.editing.set(null);
    this.persist();
    this.load();
  }

  protected previousWeek(): void {
    this.shiftWeek(-7);
  }

  protected nextWeek(): void {
    this.shiftWeek(7);
  }

  protected thisWeek(): void {
    this.weekStart.set(startOfWeek(new Date()));
    this.editing.set(null);
    this.persist();
    this.load();
  }

  private shiftWeek(days: number): void {
    this.weekStart.set(addDays(this.weekStart(), days));
    this.editing.set(null);
    this.persist();
    this.load();
  }

  // ---- Inline form ----

  protected isEditing(day: DayGroup, cell: SlotCell): boolean {
    const target = this.editing();
    return target !== null && target.scheduleOn === day.iso && target.slot === cell.slot;
  }

  /** The fixed key handed to the inline form. */
  protected contextFor(day: DayGroup, cell: SlotCell): FeaturedPromoSlotContext {
    return {
      scheduleOn: day.iso,
      trainingCenterPkid: this.trainingCenterPkid() ?? 0,
      trainingCenterName: this.selectedCenter()?.name ?? '',
      slot: cell.slot,
    };
  }

  /** Edit opens the row if there is one, otherwise a blank new form for that cell. */
  protected edit(day: DayGroup, cell: SlotCell): void {
    this.editing.set({ scheduleOn: day.iso, slot: cell.slot, item: cell.item, paste: null });
  }

  /** Paste opens a new form pre-filled from the clipboard — only offered on empty cells. */
  protected paste(day: DayGroup, cell: SlotCell): void {
    const clip = this.clipboard();
    if (!clip || cell.item) {
      return;
    }
    this.editing.set({ scheduleOn: day.iso, slot: cell.slot, item: null, paste: clip });
  }

  protected copy(item: FeaturedPromoItem): void {
    this.clipboard.set(toClipboard(item));
    this.messageService.add({
      severity: 'info',
      summary: '已複製',
      detail: `「${item.promoCode}」已複製，可貼到任一空白時段。`,
    });
  }

  protected onSaved(): void {
    this.editing.set(null);
    this.load();
  }

  protected onCancelled(): void {
    this.editing.set(null);
  }

  // ---- Move ----

  protected moveUp(item: FeaturedPromoItem): void {
    if (item.slot <= 1) {
      return;
    }
    this.service.moveUp(item.pkid).subscribe({
      next: () => this.load(),
      error: (error: HttpErrorResponse) => this.reportMoveError(error, item),
    });
  }

  protected moveDown(item: FeaturedPromoItem): void {
    if (item.slot >= FEATURED_PROMO_SLOT_COUNT) {
      return;
    }
    this.service.moveDown(item.pkid).subscribe({
      next: () => this.load(),
      error: (error: HttpErrorResponse) => this.reportMoveError(error, item),
    });
  }

  private reportMoveError(error: HttpErrorResponse, item: FeaturedPromoItem): void {
    this.messageService.add({
      severity: 'error',
      summary: '移動失敗',
      detail:
        error.status === 409
          ? `「${item.promoCode}」已在邊界時段，無法再移動。`
          : `無法移動「${item.promoCode}」。`,
    });
  }

  // ---- Delete ----

  protected confirmDelete(item: FeaturedPromoItem): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.promoCode}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(item),
    });
  }

  private delete(item: FeaturedPromoItem): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `「${item.promoCode}」已刪除。`,
        });
        this.editing.set(null);
        this.load();
      },
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: `無法刪除「${item.promoCode}」。`,
        }),
    });
  }

  private persist(): void {
    writeSession(FILTERS_KEY, {
      trainingCenterPkid: this.trainingCenterPkid(),
      weekOf: toIsoDate(this.weekStart()),
    } satisfies SavedFilters);
  }
}

function readSession<T>(key: string): Partial<T> {
  try {
    const raw = sessionStorage.getItem(key);
    return raw ? (JSON.parse(raw) as Partial<T>) : {};
  } catch {
    return {};
  }
}

function writeSession(key: string, value: unknown): void {
  try {
    sessionStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Session storage unavailable (private mode) — the selection simply does not persist.
  }
}
