import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import {
  AutoCompleteCompleteEvent,
  AutoCompleteModule,
  AutoCompleteSelectEvent,
} from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';

import {
  FeaturedPromoClipboard,
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  FeaturedPromoSlotContext,
  Promotion2Lookup,
} from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { formatDayLabel, parseIsoDate } from '@core/utils/date.util';

/**
 * Inline 新增／編輯 form for one slot of the weekly grid — rendered in place of the slot row,
 * as the mockup draws it, rather than on a route of its own.
 *
 * The slot's key (date, centre, slot) is fixed by `context` and never edited here; the
 * +/− buttons on the list are the way to change a slot. `item` puts the form in edit mode;
 * `initial` (the 複製 clipboard) pre-fills a new row for 貼上.
 */
@Component({
  selector: 'app-featured-promo-item-form',
  imports: [ReactiveFormsModule, AutoCompleteModule, ButtonModule, InputTextModule, TextareaModule],
  templateUrl: './featured-promo-item-form.html',
  styleUrl: './featured-promo-item-form.scss',
})
export class FeaturedPromoItemForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookupService = inject(LookupService);
  private readonly messageService = inject(MessageService);

  readonly context = input.required<FeaturedPromoSlotContext>();
  readonly item = input<FeaturedPromoItem | null>(null);
  readonly initial = input<FeaturedPromoClipboard | null>(null);

  readonly saved = output<FeaturedPromoItem>();
  readonly cancelled = output<void>();

  protected readonly saving = signal(false);
  protected readonly suggestions = signal<Promotion2Lookup[]>([]);

  // The autocomplete binds the whole lookup object so the pkid travels with the code.
  // `required` alone accepts whitespace, which the API would trim to '' and reject with 400 —
  // so both text fields also demand at least one non-blank character.
  protected readonly form = this.fb.group({
    promotion: this.fb.control<Promotion2Lookup | null>(null, [Validators.required]),
    topic: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.pattern(/\S/),
      Validators.maxLength(100),
    ]),
    description: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.pattern(/\S/),
      Validators.maxLength(300),
    ]),
  });

  protected get isEdit(): boolean {
    return this.item() !== null;
  }

  protected get title(): string {
    return this.isEdit ? '編輯上稿' : '新增上稿';
  }

  /** `3/16 (一) 台北 第 1 格` — the fixed key, shown so the editor knows which cell this is. */
  protected get contextLabel(): string {
    const ctx = this.context();
    const date = parseIsoDate(ctx.scheduleOn);
    const day = date ? formatDayLabel(date) : ctx.scheduleOn;
    return `${day} ${ctx.trainingCenterName} 第 ${ctx.slot} 格`;
  }

  ngOnInit(): void {
    const item = this.item();
    if (item) {
      // The row carries the code but not the promotion's own topic/description — the
      // label is all the autocomplete needs to display the current value.
      this.form.patchValue({
        promotion: asLookup(item.promotionPkid, item.promoCode),
        topic: item.topic,
        description: item.description,
      });
      return;
    }

    const initial = this.initial();
    if (initial) {
      this.form.patchValue({
        promotion: asLookup(initial.promotionPkid, initial.promoCode),
        topic: initial.topic,
        description: initial.description,
      });
    }
  }

  /** Server-side search — the endpoint returns at most 20 rows, newest first. */
  protected search(event: AutoCompleteCompleteEvent): void {
    this.lookupService.getPromotion2s(event.query).subscribe({
      next: (promotions) => this.suggestions.set(promotions),
      error: () => this.suggestions.set([]),
    });
  }

  /**
   * Picking a promotion fills 主題 / 說明 from it — but only the blank ones. 99.7% of live rows
   * carry text that differs from the promotion's, so an editor's own wording is never
   * overwritten by a re-selection.
   */
  protected onPromotionSelected(event: AutoCompleteSelectEvent): void {
    const promotion = event.value as Promotion2Lookup;
    if (this.form.controls.topic.value.trim() === '') {
      this.form.controls.topic.setValue(promotion.topic ?? '');
    }
    if (this.form.controls.description.value.trim() === '') {
      this.form.controls.description.setValue(promotion.description ?? '');
    }
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.messageService.add({
        severity: 'warn',
        summary: '欄位有誤',
        detail: '請修正紅字標示的欄位後再儲存。',
      });
      return;
    }

    const ctx = this.context();
    const value = this.form.getRawValue();
    const request: FeaturedPromoItemRequest = {
      pkid: this.item()?.pkid ?? 0,
      scheduleOn: ctx.scheduleOn,
      trainingCenterPkid: ctx.trainingCenterPkid,
      slot: ctx.slot,
      promotionPkid: value.promotion!.pkid,
      topic: value.topic.trim(),
      description: value.description.trim(),
    };

    this.saving.set(true);
    const request$ = this.isEdit ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit ? '更新成功' : '新增成功',
          detail: `${this.contextLabel}「${saved.promoCode}」已儲存。`,
        });
        this.saved.emit(saved);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error.status === 409
              ? '此日期、據點、時段已有資料，請重新整理後再試。'
              : '儲存上稿資料時發生錯誤。',
        });
      },
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }
}

/** A minimal lookup object for a code we already know — enough for the autocomplete to display. */
function asLookup(pkid: number, promoCode: string): Promotion2Lookup {
  return { pkid, promoCode, topic: '', description: '', label: promoCode };
}
