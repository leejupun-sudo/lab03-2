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

    const link = fixture.debugElement.query(By.css('.cms-nav-item'));
    expect((link.nativeElement as HTMLAnchorElement).getAttribute('href')).toBe('/app-roles');
  });

  it('renders the 發布狀態 PublishStatus entry in the same 系統管理 Admin group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('發布狀態 PublishStatus');

    const hrefs = fixture.debugElement
      .queryAll(By.css('.cms-nav-item'))
      .map((el) => (el.nativeElement as HTMLAnchorElement).getAttribute('href'));
    expect(hrefs).toEqual(['/app-roles', '/publish-statuses']);
  });

  it('toggles the sidebar collapsed state', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const toggle = fixture.debugElement.query(By.css('.cms-sidebar__toggle'));
    toggle.triggerEventHandler('click', {});
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.cms-shell--collapsed'))).toBeTruthy();
  });
});
