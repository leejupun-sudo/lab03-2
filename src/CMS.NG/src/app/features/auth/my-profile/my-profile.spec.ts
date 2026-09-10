import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { environment } from '@env';

import { AuthProfile } from '@core/models/auth.model';
import { AUTH_STORAGE_KEY, AuthService } from '@core/services/auth.service';
import { makeProfile, makeToken, seedSession } from '@core/services/auth.service.spec';
import { MyProfile } from './my-profile';

const PROFILE_URL = `${environment.apiBaseUrl}/Auth/profile`;

/** Reach the protected members the template binds to, without widening them for the tests. */
interface ProfileInternals {
  form: {
    controls: Record<string, { value: unknown; setValue(value: unknown): void }>;
  };
  save(): void;
  reset(): void;
}

describe('MyProfile', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [MyProfile],
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

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  /** The page reads the signed-in profile, so every spec seeds a session first. */
  function createPage(profile: AuthProfile = makeProfile()): ComponentFixture<MyProfile> {
    seedSession(profile);
    const fixture = TestBed.createComponent(MyProfile);
    fixture.detectChanges();
    return fixture;
  }

  function internals(fixture: ComponentFixture<MyProfile>): ProfileInternals {
    return fixture.componentInstance as unknown as ProfileInternals;
  }

  function textOf(fixture: ComponentFixture<MyProfile>, testId: string): string {
    const el = fixture.debugElement.query(By.css(`[data-testid="${testId}"]`));
    return ((el?.nativeElement as HTMLElement | undefined)?.textContent ?? '').trim();
  }

  // ---------- Read-only fields ----------

  it('creates', () => {
    expect(createPage().componentInstance).toBeTruthy();
  });

  it('shows the signed-in UserId', () => {
    const fixture = createPage(makeProfile({ userId: 'helen@uuu.com.tw' }));

    expect(textOf(fixture, 'profile-user-id')).toBe('helen@uuu.com.tw');
  });

  it('renders the UserId read-only — no form control can submit one', () => {
    const fixture = createPage();

    // Nothing to type into, and nothing bound: the API takes the account from the token.
    expect(fixture.debugElement.query(By.css('[data-testid="profile-user-id"] input'))).toBeNull();
    expect(fixture.debugElement.query(By.css('#userId'))).toBeNull();
    expect(Object.keys(internals(fixture).form.controls)).toEqual(['userName']);
  });

  it('shows the roles from the token, read-only', () => {
    const fixture = createPage(
      makeProfile({ accessToken: makeToken({ role: ['Admin', 'User'] }) }),
    );

    const roles = fixture.debugElement.query(By.css('[data-testid="profile-roles"]'));
    const text = (roles.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Admin');
    expect(text).toContain('User');

    // Display only — no editor of any kind inside the roles block.
    expect(roles.query(By.css('input, select, textarea'))).toBeNull();
  });

  it('reads a single role claim that arrives as a bare string', () => {
    const fixture = createPage(makeProfile({ accessToken: makeToken({ role: 'Admin' }) }));

    expect(textOf(fixture, 'profile-roles')).toContain('Admin');
  });

  it('shows an empty-state when the token carries no roles', () => {
    const fixture = createPage(makeProfile({ accessToken: makeToken({}) }));

    expect(fixture.debugElement.query(By.css('[data-testid="profile-roles"]'))).toBeNull();
    expect(textOf(fixture, 'profile-roles-empty')).toBe('尚未指派角色。');
  });

  it('pre-fills the UserName box with the stored name', () => {
    const fixture = createPage(makeProfile({ userName: '孫小明' }));

    expect(internals(fixture).form.controls['userName'].value).toBe('孫小明');
  });

  // ---------- Saving ----------

  it('PUTs only the userName — never the account', () => {
    const fixture = createPage();
    internals(fixture).form.controls['userName'].setValue('Miles S.');

    internals(fixture).save();

    const request = httpMock.expectOne(PROFILE_URL);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ userName: 'Miles S.' });
    expect(Object.keys(request.request.body as object)).toEqual(['userName']);
    request.flush({ userId: 'miles@uuu.com.tw', userName: 'Miles S.' });
  });

  it('trims the UserName before sending', () => {
    const fixture = createPage();
    internals(fixture).form.controls['userName'].setValue('  Miles S.  ');

    internals(fixture).save();

    const request = httpMock.expectOne(PROFILE_URL);
    expect(request.request.body).toEqual({ userName: 'Miles S.' });
    request.flush({ userId: 'miles@uuu.com.tw', userName: 'Miles S.' });
  });

  it('updates the shell name and session storage on success, keeping the token and account', () => {
    const fixture = createPage();
    const auth = TestBed.inject(AuthService);
    const tokenBefore = auth.token();

    internals(fixture).form.controls['userName'].setValue('孫小明');
    internals(fixture).save();
    httpMock.expectOne(PROFILE_URL).flush({ userId: 'miles@uuu.com.tw', userName: '孫小明' });
    fixture.detectChanges();

    // What the app shell header renders:
    expect(auth.userName()).toBe('孫小明');

    const stored = JSON.parse(sessionStorage.getItem(AUTH_STORAGE_KEY)!) as AuthProfile;
    expect(stored.userName).toBe('孫小明');
    expect(stored.userId).toBe('miles@uuu.com.tw');
    expect(stored.accessToken).toBe(tokenBefore!);
  });

  it('shows the name the API stored, not the one that was typed', () => {
    const fixture = createPage();
    internals(fixture).form.controls['userName'].setValue('  Miles S.  ');

    internals(fixture).save();
    httpMock.expectOne(PROFILE_URL).flush({ userId: 'miles@uuu.com.tw', userName: 'Miles S.' });

    expect(internals(fixture).form.controls['userName'].value).toBe('Miles S.');
    expect(TestBed.inject(AuthService).userName()).toBe('Miles S.');
  });

  it('toasts on success', () => {
    const fixture = createPage();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    internals(fixture).form.controls['userName'].setValue('Miles S.');
    internals(fixture).save();
    httpMock.expectOne(PROFILE_URL).flush({ userId: 'miles@uuu.com.tw', userName: 'Miles S.' });

    expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
  });

  // ---------- Validation ----------

  it('does not call the API for an empty UserName', () => {
    const fixture = createPage();
    internals(fixture).form.controls['userName'].setValue('');

    internals(fixture).save();

    httpMock.expectNone(PROFILE_URL);
  });

  it('does not call the API for a whitespace-only UserName', () => {
    const fixture = createPage();
    internals(fixture).form.controls['userName'].setValue('   ');

    internals(fixture).save();

    httpMock.expectNone(PROFILE_URL);
  });

  it('shows the required message once the invalid field is touched', () => {
    const fixture = createPage();
    internals(fixture).form.controls['userName'].setValue('   ');

    internals(fixture).save();
    fixture.detectChanges();

    expect(textOf(fixture, 'error-user-name')).toBe('姓名為必填。');
  });

  it('leaves the stored name alone when the save fails', () => {
    const fixture = createPage(makeProfile({ userName: 'Miles Sun' }));
    internals(fixture).form.controls['userName'].setValue('孫小明');

    internals(fixture).save();
    httpMock.expectOne(PROFILE_URL).flush(null, { status: 500, statusText: 'Server Error' });

    expect(TestBed.inject(AuthService).userName()).toBe('Miles Sun');
  });

  it('toasts on failure', () => {
    const fixture = createPage();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    internals(fixture).form.controls['userName'].setValue('孫小明');
    internals(fixture).save();
    httpMock.expectOne(PROFILE_URL).flush(null, { status: 500, statusText: 'Server Error' });

    expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error' }));
  });

  // ---------- Error and edge states found by the ship-workflow coverage audit ----------

  /**
   * A 401 here means the session died between opening the page and pressing 儲存 — the account was
   * deactivated, deleted, or the signing key rotated. The interceptor is already clearing storage
   * and bouncing to /login; the toast has to say why, not repeat the generic 更新失敗 line.
   */
  it('names the dead session when the save comes back 401', () => {
    const fixture = createPage();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    internals(fixture).form.controls['userName'].setValue('孫小明');
    internals(fixture).save();
    httpMock.expectOne(PROFILE_URL).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: '登入狀態已失效，請重新登入。' }),
    );
  });

  /** The other statuses share one message — the user cannot act on the difference. */
  it('uses the generic message for a non-401 failure', () => {
    const fixture = createPage();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    internals(fixture).form.controls['userName'].setValue('孫小明');
    internals(fixture).save();
    httpMock.expectOne(PROFILE_URL).flush(null, { status: 500, statusText: 'Server Error' });

    expect(add).toHaveBeenCalledWith(
      jasmine.objectContaining({ detail: '更新個人資料時發生錯誤。' }),
    );
  });

  /**
   * Pressing 儲存 on an invalid form is silent otherwise: the button is not disabled, so without
   * the toast the page would simply do nothing and look broken.
   */
  it('toasts 欄位有誤 when 儲存 is pressed on an invalid form', () => {
    const fixture = createPage();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    internals(fixture).form.controls['userName'].setValue('   ');
    internals(fixture).save();

    expect(add).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({ severity: 'warn', summary: '欄位有誤' }),
    );
    httpMock.expectNone(PROFILE_URL);
  });

  /** `AppUser.UserName` is nvarchar(200); the form mirrors the cap so the API never sees a 400. */
  it('does not call the API for a UserName over 200 characters', () => {
    const fixture = createPage();
    internals(fixture).form.controls['userName'].setValue('x'.repeat(201));

    internals(fixture).save();

    httpMock.expectNone(PROFILE_URL);
  });

  it('還原 drops an uncommitted edit back to the stored name', () => {
    const fixture = createPage(makeProfile({ userName: 'Miles Sun' }));
    internals(fixture).form.controls['userName'].setValue('typed but not saved');

    internals(fixture).reset();

    expect(internals(fixture).form.controls['userName'].value).toBe('Miles Sun');
    httpMock.expectNone(PROFILE_URL);
  });
});
