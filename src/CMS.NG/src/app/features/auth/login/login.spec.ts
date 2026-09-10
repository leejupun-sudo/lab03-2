import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { providePrimeNG } from 'primeng/config';
import { environment } from '@env';

import { AUTH_STORAGE_KEY } from '@core/services/auth.service';
import { makeProfile } from '@core/services/auth.service.spec';
import { Login } from './login';

const LOGIN_URL = `${environment.apiBaseUrl}/Auth/login`;

interface Internals {
  form: {
    setValue(value: { userId: string; password: string }): void;
    controls: { password: { value: string } };
  };
  submit(): void;
  errorMessage(): string;
}

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let page: Internals;
  let httpMock: HttpTestingController;
  let router: jasmine.SpyObj<Router>;

  function setup(queryParams: Record<string, string> = {}): void {
    sessionStorage.clear();
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);
    router.navigateByUrl.and.resolveTo(true);

    TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        providePrimeNG({ theme: { preset: Aura } }),
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Login);
    page = fixture.componentInstance as unknown as Internals;
    fixture.detectChanges();
  }

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('renders the account and password fields', () => {
    setup();

    expect(fixture.debugElement.query(By.css('[data-testid="input-user-id"]'))).toBeTruthy();
    expect(fixture.debugElement.query(By.css('[data-testid="input-password"]'))).toBeTruthy();
  });

  it('does not call the API when the form is empty', () => {
    setup();

    page.submit();

    httpMock.expectNone(LOGIN_URL);
  });

  it('POSTs { userId, password } and stores the profile in session storage', () => {
    setup();
    const profile = makeProfile();

    page.form.setValue({ userId: '  miles@uuu.com.tw  ', password: 'CMS4fun#' });
    page.submit();

    const req = httpMock.expectOne(LOGIN_URL);
    expect(req.request.body).toEqual({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' });
    req.flush(profile);

    expect(JSON.parse(sessionStorage.getItem(AUTH_STORAGE_KEY) ?? 'null')).toEqual(profile);
  });

  it('navigates to the default landing page on success', () => {
    setup();

    page.form.setValue({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' });
    page.submit();
    httpMock.expectOne(LOGIN_URL).flush(makeProfile());

    expect(router.navigateByUrl).toHaveBeenCalledWith('/featured-promo-items');
  });

  it('returns the user to the page the guard bounced them off', () => {
    setup({ returnUrl: '/courses/41' });

    page.form.setValue({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' });
    page.submit();
    httpMock.expectOne(LOGIN_URL).flush(makeProfile());

    expect(router.navigateByUrl).toHaveBeenCalledWith('/courses/41');
  });

  it('ignores an off-site returnUrl', () => {
    setup({ returnUrl: 'https://example.com/phish' });

    page.form.setValue({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' });
    page.submit();
    httpMock.expectOne(LOGIN_URL).flush(makeProfile());

    expect(router.navigateByUrl).toHaveBeenCalledWith('/featured-promo-items');
  });

  it('shows the generic message on a 401 and stores nothing', () => {
    setup();

    page.form.setValue({ userId: 'miles@uuu.com.tw', password: 'wrong' });
    page.submit();
    httpMock.expectOne(LOGIN_URL).flush(
      { title: '登入失敗', detail: '帳號或密碼錯誤。' },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    expect(page.errorMessage()).toBe('帳號或密碼錯誤。');
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(router.navigateByUrl).not.toHaveBeenCalled();

    const error = fixture.debugElement.query(By.css('[data-testid="login-error"]'));
    expect((error.nativeElement as HTMLElement).textContent).toContain('帳號或密碼錯誤。');
  });

  it('shows a different message when the server itself fails', () => {
    setup();

    page.form.setValue({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' });
    page.submit();
    httpMock.expectOne(LOGIN_URL).flush(null, { status: 500, statusText: 'Server Error' });

    expect(page.errorMessage()).toBe('登入時發生錯誤，請稍後再試。');
  });

  // ---------- Found by the ship-workflow coverage audit ----------

  /**
   * The rejected password is cleared but the account is kept: retyping one field is the whole of
   * the retry, and a password left sitting in a form control is a credential the page no longer
   * needs.
   */
  it('clears the rejected password and keeps the account', () => {
    setup();

    page.form.setValue({ userId: 'miles@uuu.com.tw', password: 'wrong' });
    page.submit();
    httpMock.expectOne(LOGIN_URL).flush(
      { title: '登入失敗', detail: '帳號或密碼錯誤。' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(page.form.controls.password.value).toBe('');
    expect(
      (
        fixture.debugElement.query(By.css('[data-testid="input-user-id"]'))
          .nativeElement as HTMLInputElement
      ).value,
    ).toBe('miles@uuu.com.tw');
  });
});
