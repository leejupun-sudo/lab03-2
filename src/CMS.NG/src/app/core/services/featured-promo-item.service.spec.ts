import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@env';

import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  toClipboard,
} from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemService } from './featured-promo-item.service';

const BASE_URL = `${environment.apiBaseUrl}/featured-promo-items`;

export function makeFeaturedPromoItem(overrides: Partial<FeaturedPromoItem> = {}): FeaturedPromoItem {
  return {
    pkid: 78801,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    trainingCenterName: '台北',
    slot: 1,
    promotionPkid: 3403,
    promoCode: '20251204_SkillTrainAI',
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班，三大主流語言任你選，讓你「學得會＋找得到工作」',
    ...overrides,
  };
}

export function makeFeaturedPromoRequest(
  overrides: Partial<FeaturedPromoItemRequest> = {},
): FeaturedPromoItemRequest {
  return {
    pkid: 0,
    scheduleOn: '2026-03-26',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 3423,
    topic: 'n8n自動化三部曲',
    description: '從自動化新手到企業級AI架構師學習路徑',
    ...overrides,
  };
}

describe('FeaturedPromoItemService', () => {
  let service: FeaturedPromoItemService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FeaturedPromoItemService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(FeaturedPromoItemService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection', () => {
    const expected = [makeFeaturedPromoItem()];
    let actual: FeaturedPromoItem[] | undefined;

    service.getAll().subscribe((items) => (actual = items));

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('query() POSTs the centre and the weekOf date to /query', () => {
    const query = { trainingCenterPkid: 1, weekOf: '2026-03-18' };

    service.query(query).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([]);
  });

  it('getById() issues GET with the pkid in the route', () => {
    let actual: FeaturedPromoItem | undefined;

    service.getById(78801).subscribe((item) => (actual = item));

    const req = httpMock.expectOne(`${BASE_URL}/78801`);
    expect(req.request.method).toBe('GET');
    req.flush(makeFeaturedPromoItem());

    expect(actual?.promoCode).toBe('20251204_SkillTrainAI');
  });

  it('create() POSTs the request body', () => {
    const request = makeFeaturedPromoRequest();

    service.create(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(makeFeaturedPromoItem({ pkid: 78900 }));
  });

  it('update() PUTs to the collection with the pkid in the body (no route param)', () => {
    const request = makeFeaturedPromoRequest({ pkid: 78801 });

    service.update(request).subscribe();

    const req = httpMock.expectOne(BASE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(makeFeaturedPromoItem());
  });

  it('delete() issues DELETE with the pkid in the route', () => {
    service.delete(78801).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/78801`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('moveUp() POSTs an empty body to /{pkid}/move-up', () => {
    let actual: FeaturedPromoItem | undefined;

    service.moveUp(78801).subscribe((item) => (actual = item));

    const req = httpMock.expectOne(`${BASE_URL}/78801/move-up`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(makeFeaturedPromoItem({ slot: 1 }));

    expect(actual?.slot).toBe(1);
  });

  it('moveDown() POSTs an empty body to /{pkid}/move-down', () => {
    service.moveDown(78801).subscribe();

    const req = httpMock.expectOne(`${BASE_URL}/78801/move-down`);
    expect(req.request.method).toBe('POST');
    req.flush(makeFeaturedPromoItem({ slot: 2 }));
  });

  it('surfaces a 409 from create to the caller (slot already taken)', () => {
    let status: number | undefined;

    service.create(makeFeaturedPromoRequest()).subscribe({
      error: (error) => (status = error.status),
    });

    httpMock.expectOne(BASE_URL).flush('taken', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('surfaces a 409 from moveUp to the caller (already first)', () => {
    let status: number | undefined;

    service.moveUp(78801).subscribe({ error: (error) => (status = error.status) });

    httpMock
      .expectOne(`${BASE_URL}/78801/move-up`)
      .flush('first', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('toClipboard() carries the content but not the key', () => {
    const clip = toClipboard(makeFeaturedPromoItem({ pkid: 5, scheduleOn: '2026-03-16', slot: 3 }));

    expect(clip).toEqual({
      promotionPkid: 3403,
      promoCode: '20251204_SkillTrainAI',
      topic: '成為能AI協作的程式設計師',
      description: '轉職就業養成班，三大主流語言任你選，讓你「學得會＋找得到工作」',
    });
    expect('pkid' in clip).toBeFalse();
    expect('slot' in clip).toBeFalse();
  });
});
