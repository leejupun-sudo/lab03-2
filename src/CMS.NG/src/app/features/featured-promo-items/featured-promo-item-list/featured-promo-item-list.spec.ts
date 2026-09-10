import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { Observable, of, throwError } from 'rxjs';

import {
  FeaturedPromoClipboard,
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  TrainingCenterLookup,
} from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { makeFeaturedPromoItem } from '@core/services/featured-promo-item.service.spec';
import { startOfWeek, toIsoDate } from '@core/utils/date.util';
import { DayGroup, FeaturedPromoItemList, SlotCell } from './featured-promo-item-list';

const FILTERS_KEY = 'featured-promo-item-list-filters';

const CENTERS: TrainingCenterLookup[] = [
  { pkid: 1, name: '台北', appKey: 'TPE', label: '台北' },
  { pkid: 2, name: '新竹', appKey: 'HSU', label: '新竹' },
  { pkid: 54, name: '線上研討會', appKey: 'ONL', label: '線上研討會' },
];

// Server-ordered: ScheduleOn, centre, slot. Monday 03-16 is full; Tuesday has slot 1 only.
const ITEMS: FeaturedPromoItem[] = [
  makeFeaturedPromoItem({ pkid: 1, scheduleOn: '2026-03-16', slot: 1, promoCode: '20251204_SkillTrainAI' }),
  makeFeaturedPromoItem({ pkid: 2, scheduleOn: '2026-03-16', slot: 2, promoCode: '20251215_n8n', topic: 'n8n自動化三部曲' }),
  makeFeaturedPromoItem({ pkid: 3, scheduleOn: '2026-03-16', slot: 3, promoCode: '20251219_GoogleAIseminar' }),
  makeFeaturedPromoItem({ pkid: 4, scheduleOn: '2026-03-17', slot: 1, promoCode: '20251215_n8n' }),
];

interface ListInternals {
  trainingCenterPkid: () => number | null;
  weekLabel: () => string;
  days: () => DayGroup[];
  editing: () => { scheduleOn: string; slot: number; item: FeaturedPromoItem | null; paste: FeaturedPromoClipboard | null } | null;
  clipboard: () => FeaturedPromoClipboard | null;
  onTabChange(value: string | number | undefined): void;
  previousWeek(): void;
  nextWeek(): void;
  thisWeek(): void;
  edit(day: DayGroup, cell: SlotCell): void;
  paste(day: DayGroup, cell: SlotCell): void;
  copy(item: FeaturedPromoItem): void;
  onSaved(): void;
  onCancelled(): void;
  moveUp(item: FeaturedPromoItem): void;
  moveDown(item: FeaturedPromoItem): void;
  confirmDelete(item: FeaturedPromoItem): void;
}

describe('FeaturedPromoItemList', () => {
  let fixture: ComponentFixture<FeaturedPromoItemList>;
  let component: ListInternals;
  let service: jasmine.SpyObj<FeaturedPromoItemService>;
  let lookupService: jasmine.SpyObj<LookupService>;
  let confirmationService: ConfirmationService;

  function setup(
    options: {
      queryResult?: Observable<FeaturedPromoItem[]>;
      queryParams?: Record<string, string>;
      centers?: TrainingCenterLookup[];
      savedWeek?: string | null;
      /** Runs after TestBed is configured but before the component is created — for spies. */
      onConfigured?: () => void;
    } = {},
  ): void {
    // Pin the week under test unless a test wants the "nothing saved" path.
    if (options.savedWeek !== null) {
      const existing = JSON.parse(sessionStorage.getItem(FILTERS_KEY) ?? '{}');
      sessionStorage.setItem(
        FILTERS_KEY,
        JSON.stringify({ weekOf: options.savedWeek ?? '2026-03-16', ...existing }),
      );
    }

    service = jasmine.createSpyObj<FeaturedPromoItemService>('FeaturedPromoItemService', [
      'query',
      'delete',
      'moveUp',
      'moveDown',
      'create',
      'update',
    ]);
    service.query.and.returnValue(options.queryResult ?? of(ITEMS));
    service.delete.and.returnValue(of(void 0));
    service.moveUp.and.returnValue(of(makeFeaturedPromoItem()));
    service.moveDown.and.returnValue(of(makeFeaturedPromoItem()));

    lookupService = jasmine.createSpyObj<LookupService>('LookupService', [
      'getTrainingCenters',
      'getPromotion2s',
    ]);
    lookupService.getTrainingCenters.and.returnValue(of(options.centers ?? CENTERS));
    lookupService.getPromotion2s.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [FeaturedPromoItemList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
        { provide: FeaturedPromoItemService, useValue: service },
        { provide: LookupService, useValue: lookupService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(options.queryParams ?? {}),
              paramMap: convertToParamMap({}),
            },
          },
        },
      ],
    });

    confirmationService = TestBed.inject(ConfirmationService);
    options.onConfigured?.();
    fixture = TestBed.createComponent(FeaturedPromoItemList);
    component = fixture.componentInstance as unknown as ListInternals;
    fixture.detectChanges();
  }

  function lastQuery(): FeaturedPromoItemQuery {
    return service.query.calls.mostRecent().args[0] as FeaturedPromoItemQuery;
  }

  function cellsOf(testId: string): string[] {
    return fixture.debugElement
      .queryAll(By.css(`[data-testid="${testId}"]`))
      .map((cell) => ((cell.nativeElement as HTMLElement).textContent ?? '').trim());
  }

  function savedFilters(): Record<string, unknown> {
    return JSON.parse(sessionStorage.getItem(FILTERS_KEY) ?? '{}');
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  // ---------- Load ----------

  it('loads the centres, selects the first tab, and queries that centre for the saved week', () => {
    setup();

    expect(lookupService.getTrainingCenters).toHaveBeenCalledTimes(1);
    expect(component.trainingCenterPkid()).toBe(1);
    expect(service.query).toHaveBeenCalledTimes(1);
    expect(lastQuery()).toEqual({ trainingCenterPkid: 1, weekOf: '2026-03-16' });
  });

  it('defaults to the current week when nothing is saved', () => {
    setup({ savedWeek: null });

    expect(lastQuery().weekOf).toBe(toIsoDate(startOfWeek(new Date())));
  });

  it('renders one tab per centre in server order', () => {
    setup();

    expect(cellsOf('tab-training-center')).toEqual(['台北', '新竹', '線上研討會']);
  });

  it('renders Monday..Sunday day headers and 3 slot rows per day', () => {
    setup();

    expect(cellsOf('day-header')).toEqual([
      '3/16 (一)',
      '3/17 (二)',
      '3/18 (三)',
      '3/19 (四)',
      '3/20 (五)',
      '3/21 (六)',
      '3/22 (日)',
    ]);
    expect(fixture.debugElement.queryAll(By.css('[data-testid="slot-row"]')).length).toBe(21);
    expect(component.weekLabel()).toBe('3/16 – 3/22');
  });

  it('places each row in its (date, slot) cell and leaves the other cells blank', () => {
    setup();

    const codes = cellsOf('cell-promo-code');
    expect(codes.slice(0, 4)).toEqual([
      '20251204_SkillTrainAI',
      '20251215_n8n',
      '20251219_GoogleAIseminar',
      '20251215_n8n',
    ]);
    expect(codes.slice(4).every((code) => code === '')).toBeTrue();
    expect(cellsOf('cell-topic')[1]).toBe('n8n自動化三部曲');
  });

  it('shows an empty grid and reports the failure when the query errors', fakeAsync(() => {
    let addSpy: jasmine.Spy | undefined;
    setup({
      queryResult: throwError(() => new Error('boom')),
      onConfigured: () => (addSpy = spyOn(TestBed.inject(MessageService), 'add').and.callThrough()),
    });
    tick();
    fixture.detectChanges();

    expect(cellsOf('cell-promo-code').every((code) => code === '')).toBeTrue();
    expect(addSpy!.calls.mostRecent().args[0].summary).toBe('載入失敗');
  }));

  // ---------- Tabs ----------

  it('switching tab re-queries that centre for the same week and persists the choice', () => {
    setup();

    component.onTabChange(2);

    expect(service.query).toHaveBeenCalledTimes(2);
    expect(lastQuery()).toEqual({ trainingCenterPkid: 2, weekOf: '2026-03-16' });
    expect(savedFilters()).toEqual({ trainingCenterPkid: 2, weekOf: '2026-03-16' });
  });

  it('re-selecting the current tab does not re-query', () => {
    setup();

    component.onTabChange(1);

    expect(service.query).toHaveBeenCalledTimes(1);
  });

  it('restores the saved centre tab and falls back to the first tab for an unknown one', () => {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify({ trainingCenterPkid: 54 }));
    setup();
    expect(component.trainingCenterPkid()).toBe(54);

    TestBed.resetTestingModule();
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify({ trainingCenterPkid: 999 }));
    setup();
    expect(component.trainingCenterPkid()).toBe(1);
  });

  it('lets incoming trainingCenterPkid and weekOf query params override the saved state', () => {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify({ trainingCenterPkid: 54, weekOf: '2026-03-16' }));

    setup({ queryParams: { trainingCenterPkid: '2', weekOf: '2026-04-01' } });

    // 2026-04-01 is a Wednesday — the page normalises to its Monday before asking the API.
    expect(lastQuery()).toEqual({ trainingCenterPkid: 2, weekOf: '2026-03-30' });
  });

  // ---------- Week navigation ----------

  it('moves a week forward and back, updating the label and re-querying', () => {
    setup();

    component.nextWeek();
    expect(lastQuery().weekOf).toBe('2026-03-23');
    expect(component.weekLabel()).toBe('3/23 – 3/29');

    component.previousWeek();
    component.previousWeek();
    expect(lastQuery().weekOf).toBe('2026-03-09');
    expect(component.weekLabel()).toBe('3/9 – 3/15');
    expect(savedFilters()['weekOf']).toBe('2026-03-09');
  });

  it('本週 jumps back to the current week', () => {
    setup();

    component.thisWeek();

    expect(lastQuery().weekOf).toBe(toIsoDate(startOfWeek(new Date())));
  });

  it('closes an open inline form when the week changes', () => {
    setup();
    const monday = component.days()[0];
    component.edit(monday, monday.slots[0]);
    expect(component.editing()).not.toBeNull();

    component.nextWeek();

    expect(component.editing()).toBeNull();
  });

  // ---------- Inline form ----------

  it('Edit on a filled cell opens the inline form in edit mode for that row', () => {
    setup();
    const monday = component.days()[0];

    component.edit(monday, monday.slots[1]);
    fixture.detectChanges();

    expect(component.editing()).toEqual(
      jasmine.objectContaining({ scheduleOn: '2026-03-16', slot: 2, paste: null }),
    );
    expect(component.editing()?.item?.pkid).toBe(2);
    expect(fixture.debugElement.queryAll(By.css('[data-testid="inline-form"]')).length).toBe(1);
    expect(cellsOf('form-title')).toEqual(['編輯上稿']);
    expect(cellsOf('form-context')).toEqual(['3/16 (一) 台北 第 2 格']);
    // The edited slot row is replaced by the form; the other 20 stay.
    expect(fixture.debugElement.queryAll(By.css('[data-testid="cell-promo-code"]')).length).toBe(20);
  });

  it('Edit on an empty cell opens the inline form in new mode', () => {
    setup();
    const tuesday = component.days()[1];

    component.edit(tuesday, tuesday.slots[2]);
    fixture.detectChanges();

    expect(component.editing()?.item).toBeNull();
    expect(cellsOf('form-title')).toEqual(['新增上稿']);
    expect(cellsOf('form-context')).toEqual(['3/17 (二) 台北 第 3 格']);
  });

  it('saving from the inline form closes it and reloads the week', () => {
    setup();
    const monday = component.days()[0];
    component.edit(monday, monday.slots[0]);

    component.onSaved();

    expect(component.editing()).toBeNull();
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('cancelling the inline form closes it without reloading', () => {
    setup();
    const monday = component.days()[0];
    component.edit(monday, monday.slots[0]);

    component.onCancelled();

    expect(component.editing()).toBeNull();
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  // ---------- Copy / Paste ----------

  it('Copy fills the clipboard with the content (not the key) and Paste opens a pre-filled new form', () => {
    setup();
    expect(fixture.debugElement.queryAll(By.css('[data-testid="paste-item"]')).length).toBe(0);

    component.copy(ITEMS[1]);
    fixture.detectChanges();

    expect(component.clipboard()).toEqual({
      promotionPkid: 3403,
      promoCode: '20251215_n8n',
      topic: 'n8n自動化三部曲',
      description: ITEMS[1].description,
    });
    // Paste is offered on every EMPTY cell (21 − 4 filled), never on a filled one.
    expect(fixture.debugElement.queryAll(By.css('[data-testid="paste-item"]')).length).toBe(17);

    const tuesday = component.days()[1];
    component.paste(tuesday, tuesday.slots[1]);
    fixture.detectChanges();

    expect(component.editing()).toEqual(
      jasmine.objectContaining({ scheduleOn: '2026-03-17', slot: 2, item: null }),
    );
    expect(component.editing()?.paste?.promoCode).toBe('20251215_n8n');
    expect(cellsOf('form-title')).toEqual(['新增上稿']);
  });

  it('Paste is a no-op on a filled cell or with an empty clipboard', () => {
    setup();
    const monday = component.days()[0];

    component.paste(monday, monday.slots[1]);
    expect(component.editing()).toBeNull();

    component.copy(ITEMS[0]);
    component.paste(monday, monday.slots[1]);
    expect(component.editing()).toBeNull();
  });

  // ---------- Move ----------

  it('「+」 calls moveDown for the row and reloads; it is a no-op on the last slot', () => {
    setup();

    component.moveDown(ITEMS[0]);
    expect(service.moveDown).toHaveBeenCalledWith(1);
    expect(service.query).toHaveBeenCalledTimes(2);

    component.moveDown(ITEMS[2]);
    expect(service.moveDown).toHaveBeenCalledTimes(1);
  });

  it('「−」 calls moveUp for the row and reloads; it is a no-op on the first slot', () => {
    setup();

    component.moveUp(ITEMS[1]);
    expect(service.moveUp).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);

    component.moveUp(ITEMS[0]);
    expect(service.moveUp).toHaveBeenCalledTimes(1);
  });

  it('disables the move buttons on empty cells and at the slot bounds', () => {
    setup();

    const rows = fixture.debugElement.queryAll(By.css('[data-testid="slot-row"]'));
    const isDisabled = (row: (typeof rows)[number], testId: string): boolean =>
      (row.query(By.css(`[data-testid="${testId}"] button`)).nativeElement as HTMLButtonElement).disabled;

    // Monday slot 1: filled, first — can go down, not up.
    expect(isDisabled(rows[0], 'move-down')).toBeFalse();
    expect(isDisabled(rows[0], 'move-up')).toBeTrue();
    // Monday slot 3: filled, last — can go up, not down.
    expect(isDisabled(rows[2], 'move-down')).toBeTrue();
    expect(isDisabled(rows[2], 'move-up')).toBeFalse();
    // Tuesday slot 2: empty — neither.
    expect(isDisabled(rows[4], 'move-down')).toBeTrue();
    expect(isDisabled(rows[4], 'move-up')).toBeTrue();
  });

  it('reports a 409 from move as a boundary error', () => {
    setup();
    service.moveDown.and.returnValue(throwError(() => ({ status: 409 })));
    const addSpy = spyOn(TestBed.inject(MessageService), 'add');

    component.moveDown(ITEMS[1]);

    expect(addSpy.calls.mostRecent().args[0].detail).toContain('邊界');
    expect(service.query).toHaveBeenCalledTimes(1);
  });

  // ---------- Delete ----------

  it('deletes a row after the confirmation is accepted, then reloads', () => {
    setup();
    let confirmation: Confirmation | undefined;
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      confirmation = options;
      return confirmationService;
    });

    component.confirmDelete(ITEMS[1]);

    expect(confirmation?.message).toContain('確定要刪除主代碼');
    expect(confirmation?.message).toContain('<b>2</b>');
    expect(confirmation?.message).toContain('20251215_n8n');
    expect(service.delete).not.toHaveBeenCalled();

    confirmation?.accept?.();

    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });
});
