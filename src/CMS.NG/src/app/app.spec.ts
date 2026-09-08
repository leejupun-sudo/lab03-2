import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';

import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin group with the 角色 AppRole entry', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');
  });

  it('links the 角色 AppRole entry to /app-roles', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const hrefs = fixture.debugElement
      .queryAll(By.css('.cms-nav-item'))
      .map((el) => (el.nativeElement as HTMLAnchorElement).getAttribute('href'));
    expect(hrefs).toContain('/app-roles');
  });

  it('renders the 使用者 AppUser entry first in 系統管理 Admin, linked to /app-users', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('使用者 AppUser');

    const hrefs = fixture.debugElement
      .queryAll(By.css('.cms-nav-item'))
      .map((el) => (el.nativeElement as HTMLAnchorElement).getAttribute('href'));
    expect(hrefs.indexOf('/app-users')).toBe(hrefs.indexOf('/app-roles') - 1);
  });

  it('renders the 首頁 Home group first, with 上稿作業 FeaturedPromoItem linked to /featured-promo-items', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('首頁 Home');
    expect(text).toContain('上稿作業 FeaturedPromoItem');

    const firstGroup = fixture.debugElement.query(By.css('.cms-nav-group__label'));
    expect(((firstGroup.nativeElement as HTMLElement).textContent ?? '').trim()).toBe('首頁 Home');

    const link = fixture.debugElement.query(By.css('.cms-nav-item'));
    expect((link.nativeElement as HTMLAnchorElement).getAttribute('href')).toBe('/featured-promo-items');
  });

  it('renders the 發布狀態 PublishStatus entry in the same 系統管理 Admin group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('發布狀態 PublishStatus');

    const hrefs = fixture.debugElement
      .queryAll(By.css('.cms-nav-item'))
      .map((el) => (el.nativeElement as HTMLAnchorElement).getAttribute('href'));
    expect(hrefs).toEqual([
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
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('課程管理 Course');
    expect(text).toContain('課程群組 CourseGroup');
  });

  it('renders the three groups in order: 首頁 Home, 系統管理 Admin, 課程管理 Course', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const groupLabels = fixture.debugElement
      .queryAll(By.css('.cms-nav-group__label'))
      .map((el) => ((el.nativeElement as HTMLElement).textContent ?? '').trim());
    expect(groupLabels).toEqual(['首頁 Home', '系統管理 Admin', '課程管理 Course']);
  });

  it('links the 課程群組 CourseGroup entry to /course-groups', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const links = fixture.debugElement.queryAll(By.css('.cms-nav-item'));
    const href = (links[links.length - 1].nativeElement as HTMLAnchorElement).getAttribute('href');
    expect(href).toBe('/course-groups');
  });

  it('renders the 合作廠商 Partner entry in the 課程管理 Course group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('合作廠商 Partner');
  });

  it('links the 合作廠商 Partner entry to /partners, ahead of 課程群組 CourseGroup', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const hrefs = fixture.debugElement
      .queryAll(By.css('.cms-nav-item'))
      .map((el) => (el.nativeElement as HTMLAnchorElement).getAttribute('href'));

    expect(hrefs.indexOf('/partners')).toBeGreaterThan(-1);
    expect(hrefs.indexOf('/partners')).toBeLessThan(hrefs.indexOf('/course-groups'));
  });

  it('toggles the sidebar collapsed state', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const toggle = fixture.debugElement.query(By.css('.cms-sidebar__toggle'));
    toggle.triggerEventHandler('click', {});
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.cms-shell--collapsed'))).toBeTruthy();
  });

  it('renders the 課程 Course entry first in the 課程管理 Course group, linked to /courses', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('課程 Course');

    const hrefs = fixture.debugElement
      .queryAll(By.css('.cms-nav-item'))
      .map((el) => (el.nativeElement as HTMLAnchorElement).getAttribute('href'));

    expect(hrefs.indexOf('/courses')).toBeGreaterThan(-1);
    expect(hrefs.indexOf('/courses')).toBeLessThan(hrefs.indexOf('/partners'));
  });
});
