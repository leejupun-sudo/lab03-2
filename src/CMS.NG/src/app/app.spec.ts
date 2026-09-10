import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';

import { environment } from '@env';
import { AuthProfile } from '@core/models/auth.model';
import { AUTH_STORAGE_KEY, AuthService } from '@core/services/auth.service';
import { makeProfile, makeToken, seedSession } from '@core/services/auth.service.spec';
import { App } from './app';

/**
 * The shell only exists for a signed-in user, so every spec seeds a session first. Roles come
 * from the token, so the seeded token is what decides which nav groups render.
 */
function createApp(profile: AuthProfile | null = makeProfile()): ComponentFixture<App> {
  if (profile) {
    seedSession(profile);
  }
  const fixture = TestBed.createComponent(App);
  fixture.detectChanges();
  return fixture;
}

function hrefsOf(fixture: ComponentFixture<App>): (string | null)[] {
  return fixture.debugElement
    .queryAll(By.css('.cms-nav-item'))
    .map((el) => (el.nativeElement as HTMLAnchorElement).getAttribute('href'));
}

describe('App', () => {
  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();
  });

  afterEach(() => sessionStorage.clear());

  it('should create the app', () => {
    expect(createApp().componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin group with the 角色 AppRole entry', () => {
    const text = (createApp().nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');
  });

  it('links the 角色 AppRole entry to /app-roles', () => {
    expect(hrefsOf(createApp())).toContain('/app-roles');
  });

  it('renders the 使用者 AppUser entry first in 系統管理 Admin, linked to /app-users', () => {
    const fixture = createApp();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('使用者 AppUser');

    const hrefs = hrefsOf(fixture);
    expect(hrefs.indexOf('/app-users')).toBe(hrefs.indexOf('/app-roles') - 1);
  });

  it('renders the 首頁 Home group first, with 上稿作業 FeaturedPromoItem linked to /featured-promo-items', () => {
    const fixture = createApp();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('首頁 Home');
    expect(text).toContain('上稿作業 FeaturedPromoItem');

    const firstGroup = fixture.debugElement.query(By.css('.cms-nav-group__label'));
    expect(((firstGroup.nativeElement as HTMLElement).textContent ?? '').trim()).toBe('首頁 Home');

    const link = fixture.debugElement.query(By.css('.cms-nav-item'));
    expect((link.nativeElement as HTMLAnchorElement).getAttribute('href')).toBe('/featured-promo-items');
  });

  it('renders the 發布狀態 PublishStatus entry in the same 系統管理 Admin group', () => {
    const fixture = createApp();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('發布狀態 PublishStatus');
    expect(hrefsOf(fixture)).toEqual([
      '/featured-promo-items',
      '/app-users',
      '/app-roles',
      '/publish-statuses',
      '/courses',
      '/partners',
      '/course-groups',
    ]);
  });

  it('renders the 課程管理 Course group with the 課程群組 CourseGroup entry', () => {
    const text = (createApp().nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('課程管理 Course');
    expect(text).toContain('課程群組 CourseGroup');
  });

  it('renders the three groups in order: 首頁 Home, 系統管理 Admin, 課程管理 Course', () => {
    const groupLabels = createApp()
      .debugElement.queryAll(By.css('.cms-nav-group__label'))
      .map((el) => ((el.nativeElement as HTMLElement).textContent ?? '').trim());

    expect(groupLabels).toEqual(['首頁 Home', '系統管理 Admin', '課程管理 Course']);
  });

  it('links the 課程群組 CourseGroup entry to /course-groups', () => {
    const hrefs = hrefsOf(createApp());

    expect(hrefs[hrefs.length - 1]).toBe('/course-groups');
  });

  it('renders the 合作廠商 Partner entry in the 課程管理 Course group', () => {
    expect((createApp().nativeElement as HTMLElement).textContent).toContain('合作廠商 Partner');
  });

  it('links the 合作廠商 Partner entry to /partners, ahead of 課程群組 CourseGroup', () => {
    const hrefs = hrefsOf(createApp());

    expect(hrefs.indexOf('/partners')).toBeGreaterThan(-1);
    expect(hrefs.indexOf('/partners')).toBeLessThan(hrefs.indexOf('/course-groups'));
  });

  it('toggles the sidebar collapsed state', () => {
    const fixture = createApp();

    fixture.debugElement.query(By.css('.cms-sidebar__toggle')).triggerEventHandler('click', {});
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.cms-shell--collapsed'))).toBeTruthy();
  });

  it('renders the 課程 Course entry first in the 課程管理 Course group, linked to /courses', () => {
    const fixture = createApp();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('課程 Course');

    const hrefs = hrefsOf(fixture);
    expect(hrefs.indexOf('/courses')).toBeGreaterThan(-1);
    expect(hrefs.indexOf('/courses')).toBeLessThan(hrefs.indexOf('/partners'));
  });

  // ---------- Sign-in state ----------

  it('renders no sidebar or header when nobody is signed in', () => {
    const fixture = createApp(null);

    expect(fixture.debugElement.query(By.css('.cms-sidebar'))).toBeNull();
    expect(fixture.debugElement.query(By.css('.cms-nav-item'))).toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="signed-in-user"]'))).toBeNull();
    expect(fixture.debugElement.query(By.css('.cms-shell--bare'))).toBeTruthy();
  });

  it('shows the signed-in UserName in the header', () => {
    const fixture = createApp(makeProfile({ userName: '孫小明' }));

    const user = fixture.debugElement.query(By.css('[data-testid="signed-in-user"]'));
    expect((user.nativeElement as HTMLElement).textContent).toContain('孫小明');
  });

  it('links the header user chip to 我的帳號 at /my-profile', () => {
    const fixture = createApp();

    const chip = fixture.debugElement.query(By.css('[data-testid="signed-in-user"]'));
    expect((chip.nativeElement as HTMLAnchorElement).getAttribute('href')).toBe('/my-profile');
    expect((chip.nativeElement as HTMLElement).textContent).toContain('我的帳號');

    // The profile link is not a sidebar entry — the nav list is unchanged by it.
    expect(hrefsOf(fixture)).not.toContain('/my-profile');
  });

  it('refreshes the header name when the signed-in user renames themselves', () => {
    const fixture = createApp(makeProfile({ userName: 'Miles Sun' }));
    const httpMock = TestBed.inject(HttpTestingController);

    TestBed.inject(AuthService).updateProfile({ userName: '孫小明' }).subscribe();
    httpMock
      .expectOne(`${environment.apiBaseUrl}/Auth/profile`)
      .flush({ userId: 'miles@uuu.com.tw', userName: '孫小明' });
    fixture.detectChanges();

    const user = fixture.debugElement.query(By.css('[data-testid="signed-in-user"]'));
    expect((user.nativeElement as HTMLElement).textContent).toContain('孫小明');
    expect((user.nativeElement as HTMLElement).textContent).not.toContain('Miles Sun');
    httpMock.verify();
  });

  it('logout clears session storage and returns to the login page', () => {
    const fixture = createApp();
    sessionStorage.setItem('course-list-filters', '{"keyword":"Azure"}');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fixture.debugElement.query(By.css('[data-testid="logout"]')).triggerEventHandler('click', {});
    fixture.detectChanges();

    expect(sessionStorage.length).toBe(0);
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
    expect(fixture.debugElement.query(By.css('.cms-sidebar'))).toBeNull();
  });

  // ---------- The 系統管理 Admin group is role-gated ----------

  it('shows the 系統管理 Admin group when the token roles include Admin', () => {
    const fixture = createApp(makeProfile({ accessToken: makeToken({ role: ['Admin'] }) }));

    const groupLabels = fixture.debugElement
      .queryAll(By.css('.cms-nav-group__label'))
      .map((el) => ((el.nativeElement as HTMLElement).textContent ?? '').trim());
    expect(groupLabels).toContain('系統管理 Admin');
  });

  it('hides the 系統管理 Admin group from a user without the Admin role', () => {
    const fixture = createApp(makeProfile({ accessToken: makeToken({ role: ['User'] }) }));

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('系統管理 Admin');
    expect(text).not.toContain('使用者 AppUser');
    expect(text).not.toContain('角色 AppRole');
    expect(text).not.toContain('發布狀態 PublishStatus');
  });

  it('hides the admin routes from the nav for a non-Admin, keeping the other two groups', () => {
    const fixture = createApp(makeProfile({ accessToken: makeToken({ role: ['User'] }) }));

    expect(hrefsOf(fixture)).toEqual([
      '/featured-promo-items',
      '/courses',
      '/partners',
      '/course-groups',
    ]);
  });

  it('hides the 系統管理 Admin group from a user with no roles at all', () => {
    const fixture = createApp(makeProfile({ accessToken: makeToken({}) }));

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('系統管理 Admin');
  });
});
