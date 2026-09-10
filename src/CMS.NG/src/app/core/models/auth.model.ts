/** 登入請求 — `POST /api/Auth/login`. */
export interface LoginRequest {
  userId: string;
  password: string;
}

/**
 * 登入成功後的使用者資料, 存放於 **session storage**.
 *
 * These are exactly the three properties `LoginResponse` carries; roles are not among them —
 * they travel as `role` claims inside {@link AuthProfile.accessToken}.
 */
export interface AuthProfile {
  userId: string;
  userName: string;
  accessToken: string;
}

/**
 * 修改個人資料請求 — `PUT /api/Auth/profile`.
 *
 * One property, deliberately. The account is taken from the token server-side, so there is no
 * `userId` to send — and a page cannot accidentally offer to change one it never carries.
 */
export interface UpdateProfileRequest {
  userName: string;
}

/** 個人資料 — what `PUT /api/Auth/profile` returns once the rename is stored. */
export interface UserProfile {
  userId: string;
  userName: string;
}
