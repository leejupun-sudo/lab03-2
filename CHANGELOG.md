# Changelog

All notable changes to the CMS admin app are recorded here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions are
four-digit `MAJOR.MINOR.PATCH.MICRO`.

## [0.1.0.0] - 2026-09-10

The release that closes the admin app. Everything in it was previously reachable by anyone who
could open the page; now you sign in, and what you can do depends on who you are.

### Added

- **Sign-in.** A login page at `/login` and `POST /api/auth/login`. Signing in issues a token that
  lasts 24 hours and is kept for the tab you are working in, so closing it signs you out and a
  shared machine does not leave the next person in your account. A wrong account, a wrong password
  and a disabled account all return the same message, so the page never confirms whether an account
  exists.
- **系統管理 is now restricted to administrators.** 使用者, 角色 and 發布狀態 — and the account and
  role pickers behind them — answer only to a signed-in administrator. Anyone else gets 權限不足 and
  stays signed in. The content areas (課程, 合作廠商, 課程群組, 上稿作業) stay open to every
  signed-in user.
- **我的帳號.** A signed-in user can change their own display name from the header user chip. The
  name updates in the header immediately; nothing else about the account can be changed from there.
- **課程 Course management** — list, detail and form, with a QR code on the detail page and
  double-click inline editing on the list. An inline edit that fails leaves the cell open with your
  value still in it, so nothing is lost to a failed save.
- **合作廠商 Partner, 課程群組 CourseGroup, 發布狀態 PublishStatus, 使用者 AppUser and 上稿作業
  FeaturedPromoItem management** — list, detail and form for each.
- **A record of what is left.** `TODOS.md` now carries the findings from this release's review that
  were deliberately deferred, each with a priority and a file reference.

### Changed

- **Breaking: every endpoint except sign-in now requires a token.** Any script, bookmark or
  generated client that called this API without one will receive `401`. The Angular app in this
  repository is updated in the same change; anything outside it is not.
- **Disabling or deleting an account takes effect on that person's next request**, rather than
  whenever their token happens to expire. Unticking 啟用 is the way to end someone's access
  immediately.
- **The token signing key is read from the database on every request**, so replacing it in
  `SysConfig` takes effect without a restart. Note that replacing it also signs out everyone who is
  currently working.

### Fixed

- **Signing back in after being signed out no longer fails intermittently.** A request that was
  already on its way when your session ended could return after you had signed back in and clear the
  session you had just created, which looked like a random login failure that worked on the second
  try.

### Known issues

Found during this release's review and recorded in `TODOS.md` rather than fixed:

- Deleting the `Admin` role, or removing the last administrator, locks 系統管理 with no way back in
  from the app.
- Removing someone's administrator role does not take effect until their token expires, up to 24
  hours later. Disabling the account is immediate.
- The sign-in endpoint has no rate limiting.
- `/swagger` is reachable without signing in and lists every endpoint.
- A database fault during authentication returns an unhandled error, and in a development build
  that response includes a stack trace.
