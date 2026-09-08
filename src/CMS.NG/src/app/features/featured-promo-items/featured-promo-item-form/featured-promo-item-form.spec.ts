import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { of, throwError } from 'rxjs';

import {
  FeaturedPromoClipboard,
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  FeaturedPromoSlotContext,
  Promotion2Lookup,
} from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { makeFeaturedPromoItem } from '@core/services/featured-promo-item.service.spec';
import { FeaturedPromoItemForm } from './featured-promo-item-form';

const CONTEXT: FeaturedPromoSlotContext = {
  scheduleOn: '2026-03-16',
  trainingCenterPkid: 1,
  trainingCenterName: '台北',
  slot: 1,
};

const N8N: Promotion2Lookup = {
  pkid: 3423,
  promoCode: '20251215_n8n',
  topic: 'n8n自動化三部曲',
  description: '從自動化新手到企業級AI架構師學習路徑',
  label: '20251215_n8n',
};

const CLIPBOARD: FeaturedPromoClipboard = {
  promotionPkid: 3393,
  promoCode: '20251219_GoogleAIseminar',
  topic: 'Google AI工具一次掌握',
  description: '不需技術基礎！最新Google AI實戰課程幫你快速提升工作效率',
};

interface FormInternals {
  form: {
    controls: Record<string, { value: unknown; setValue(value: unknown): void }>;
    getRawValue(): Record<string, unknown>;
    patchValue(value: Record<string, unknown>): void;
  };
  isEdit: boolean;
  title: string;
  contextLabel: string;
  suggestions: () => Promotion2Lookup[];
  search(event: { originalEvent: Event; query: string }): void;
  onPromotionSelected(event: { originalEvent: Event; value: Promotion2Lookup }): void;
  save(): void;
  cancel(): void;
}

describe('FeaturedPromoItemForm', () => {
  let fixture: ComponentFixture<FeaturedPromoItemForm>;
  let component: FormInternals;
  let service: jasmine.SpyObj<FeaturedPromoItemService>;
  let lookupService: jasmine.SpyObj<LookupService>;
  let saved: FeaturedPromoItem[];
  let cancelled: number;

  function setup(options: { item?: FeaturedPromoItem; initial?: FeaturedPromoClipboard } = {}): void {
    service = jasmine.createSpyObj<FeaturedPromoItemService>('FeaturedPromoItemService', ['create', 'update']);
    service.create.and.returnValue(of(makeFeaturedPromoItem({ pkid: 78900, promoCode: N8N.promoCode })));
    service.update.and.returnValue(of(makeFeaturedPromoItem()));

    lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['getPromotion2s']);
    lookupService.getPromotion2s.and.returnValue(of([N8N]));

    TestBed.configureTestingModule({
      imports: [FeaturedPromoItemForm],
      providers: [
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        { provide: FeaturedPromoItemService, useValue: service },
        { provide: LookupService, useValue: lookupService },
      ],
    });

    fixture = TestBed.createComponent(FeaturedPromoItemForm);
    fixture.componentRef.setInput('context', CONTEXT);
    if (options.item) {
      fixture.componentRef.setInput('item', options.item);
    }
    if (options.initial) {
      fixture.componentRef.setInput('initial', options.initial);
    }

    saved = [];
    cancelled = 0;
    fixture.componentInstance.saved.subscribe((item) => saved.push(item));
    fixture.componentInstance.cancelled.subscribe(() => cancelled++);

    component = fixture.componentInstance as unknown as FormInternals;
    fixture.detectChanges();
  }

  function fillValidForm(overrides: Record<string, unknown> = {}): void {
    component.form.patchValue({
      promotion: N8N,
      topic: '  n8n自動化三部曲  ',
      description: '從自動化新手到企業級AI架構師學習路徑',
      ...overrides,
    });
  }

  function textOf(testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  // ---------- New ----------

  it('renders the add title and the fixed slot context, with every control blank', () => {
    setup();

    expect(component.isEdit).toBeFalse();
    expect(textOf('form-title')).toBe('新增上稿');
    expect(textOf('form-context')).toBe('3/16 (一) 台北 第 1 格');
    expect(component.form.getRawValue()).toEqual({ promotion: null, topic: '', description: '' });
  });

  it('search() asks the lookup endpoint for the typed code and shows the results', () => {
    setup();

    component.search({ originalEvent: new Event('input'), query: 'n8n' });

    expect(lookupService.getPromotion2s).toHaveBeenCalledWith('n8n');
    expect(component.suggestions()).toEqual([N8N]);
  });

  it('search() clears the suggestions when the lookup fails', () => {
    setup();
    lookupService.getPromotion2s.and.returnValue(throwError(() => new Error('boom')));

    component.search({ originalEvent: new Event('input'), query: 'n8n' });

    expect(component.suggestions()).toEqual([]);
  });

  it('selecting a promotion fills the blank 主題 and 說明 from it', () => {
    setup();

    component.onPromotionSelected({ originalEvent: new Event('select'), value: N8N });

    expect(component.form.controls['topic'].value).toBe('n8n自動化三部曲');
    expect(component.form.controls['description'].value).toBe('從自動化新手到企業級AI架構師學習路徑');
  });

  it('selecting a promotion never overwrites text the editor already typed', () => {
    setup();
    component.form.patchValue({ topic: '自己寫的主題', description: '  ' });

    component.onPromotionSelected({ originalEvent: new Event('select'), value: N8N });

    expect(component.form.controls['topic'].value).toBe('自己寫的主題');
    // Whitespace-only counts as blank and is filled.
    expect(component.form.controls['description'].value).toBe(N8N.description);
  });

  it('blocks the save when no promotion has been picked', () => {
    setup();
    const addSpy = spyOn(TestBed.inject(MessageService), 'add');
    fillValidForm({ promotion: null });

    component.save();

    expect(service.create).not.toHaveBeenCalled();
    expect(addSpy.calls.mostRecent().args[0].severity).toBe('warn');
  });

  it('blocks the save when 主題 is blank', () => {
    setup();
    fillValidForm({ topic: '   ' });

    component.save();

    expect(service.create).not.toHaveBeenCalled();
  });

  it('creates the row under the fixed slot key with pkid 0, trimmed text and the picked pkid, then emits saved', () => {
    setup();
    fillValidForm();

    component.save();

    expect(service.create).toHaveBeenCalledTimes(1);
    const request = service.create.calls.mostRecent().args[0] as FeaturedPromoItemRequest;
    expect(request).toEqual({
      pkid: 0,
      scheduleOn: '2026-03-16',
      trainingCenterPkid: 1,
      slot: 1,
      promotionPkid: 3423,
      topic: 'n8n自動化三部曲',
      description: '從自動化新手到企業級AI架構師學習路徑',
    });
    expect(service.update).not.toHaveBeenCalled();
    expect(saved.length).toBe(1);
    expect(saved[0].pkid).toBe(78900);
  });

  it('reports the taken slot when create returns 409 and does not emit saved', () => {
    setup();
    service.create.and.returnValue(throwError(() => ({ status: 409 })));
    const addSpy = spyOn(TestBed.inject(MessageService), 'add');
    fillValidForm();

    component.save();

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('已有資料');
    expect(saved.length).toBe(0);
  });

  // ---------- Paste ----------

  it('pre-fills a new form from the clipboard and creates with the copied promotion', () => {
    setup({ initial: CLIPBOARD });

    expect(component.isEdit).toBeFalse();
    expect(textOf('form-title')).toBe('新增上稿');
    const raw = component.form.getRawValue();
    expect((raw['promotion'] as Promotion2Lookup).pkid).toBe(3393);
    expect((raw['promotion'] as Promotion2Lookup).label).toBe('20251219_GoogleAIseminar');
    expect(raw['topic']).toBe('Google AI工具一次掌握');

    component.save();

    const request = service.create.calls.mostRecent().args[0] as FeaturedPromoItemRequest;
    expect(request.promotionPkid).toBe(3393);
    expect(request.scheduleOn).toBe('2026-03-16');
    expect(request.slot).toBe(1);
  });

  // ---------- Edit ----------

  it('renders the edit title and patches every field from the row', () => {
    setup({ item: makeFeaturedPromoItem({ pkid: 78801 }) });

    expect(component.isEdit).toBeTrue();
    expect(textOf('form-title')).toBe('編輯上稿');
    const raw = component.form.getRawValue();
    expect((raw['promotion'] as Promotion2Lookup).pkid).toBe(3403);
    expect((raw['promotion'] as Promotion2Lookup).label).toBe('20251204_SkillTrainAI');
    expect(raw['topic']).toBe('成為能AI協作的程式設計師');
    expect(raw['description']).toContain('轉職就業養成班');
  });

  it('updates via PUT with the pkid in the body and emits saved', () => {
    setup({ item: makeFeaturedPromoItem({ pkid: 78801 }) });
    component.form.patchValue({ topic: '改過的主題' });

    component.save();

    expect(service.update).toHaveBeenCalledTimes(1);
    const request = service.update.calls.mostRecent().args[0] as FeaturedPromoItemRequest;
    expect(request.pkid).toBe(78801);
    expect(request.promotionPkid).toBe(3403);
    expect(request.topic).toBe('改過的主題');
    expect(service.create).not.toHaveBeenCalled();
    expect(saved.length).toBe(1);
  });

  it('cancel emits cancelled without touching the service', () => {
    setup({ item: makeFeaturedPromoItem() });

    component.cancel();

    expect(cancelled).toBe(1);
    expect(service.update).not.toHaveBeenCalled();
  });
});
