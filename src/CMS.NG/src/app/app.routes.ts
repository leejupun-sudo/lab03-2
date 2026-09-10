import { Routes } from '@angular/router';
import { adminGuard } from '@core/guards/admin.guard';
import { authGuard } from '@core/guards/auth.guard';

export const routes: Routes = [
  {
    // 唯一的公開路由.
    path: 'login',
    loadComponent: () => import('@features/auth/login/login').then((m) => m.Login),
    title: '登入 Login',
  },
  {
    // Every app route hangs off this pathless parent, so one guard covers the lot — including
    // whatever route is added next.
    path: '',
    canActivateChild: [authGuard],
    children: [
      // 上稿作業, not 角色: the 系統管理 routes now need the Admin role, and the landing page
      // must be one every signed-in user can actually open. Same target as `login.ts`'s
      // DEFAULT_LANDING — the two are the same decision and must not drift.
      { path: '', pathMatch: 'full', redirectTo: 'featured-promo-items' },
      {
        // 我的帳號 — reached from the header, not the sidebar: it belongs to whoever is signed
        // in rather than to any of the feature groups.
        path: 'my-profile',
        loadComponent: () =>
          import('@features/auth/my-profile/my-profile').then((m) => m.MyProfile),
        title: '我的帳號 My Profile',
      },
      {
        // One route only: 新增／編輯 happen inline in the weekly grid (see spec/promotion/FeaturedPromoItem.md).
        path: 'featured-promo-items',
        loadComponent: () =>
          import('@features/featured-promo-items/featured-promo-item-list/featured-promo-item-list').then(
            (m) => m.FeaturedPromoItemList,
          ),
        title: '上稿作業 FeaturedPromoItem',
      },
      {
        // 系統管理 Admin — 使用者／角色／發布狀態. One pathless parent for the whole group, the
        // same shape as the authGuard above it, so a maintenance route added here is guarded by
        // construction rather than by remembering.
        //
        // The guard mirrors the sidebar filter in `app.ts`; the API is what actually enforces it
        // (`[Authorize(Roles = "Admin")]` on the three controllers and the two Admin-only lookup
        // actions), so a hand-edited session storage entry gets past this and straight into a 403.
        path: '',
        canActivateChild: [adminGuard],
        children: [
          {
            path: 'app-users',
            loadComponent: () =>
              import('@features/app-users/app-user-list/app-user-list').then((m) => m.AppUserList),
            title: '使用者 AppUser',
          },
          {
            path: 'app-users/new',
            loadComponent: () =>
              import('@features/app-users/app-user-form/app-user-form').then((m) => m.AppUserForm),
            title: '新增使用者',
          },
          {
            path: 'app-users/:id/edit',
            loadComponent: () =>
              import('@features/app-users/app-user-form/app-user-form').then((m) => m.AppUserForm),
            title: '編輯使用者',
          },
          {
            path: 'app-users/:id',
            loadComponent: () =>
              import('@features/app-users/app-user-detail/app-user-detail').then(
                (m) => m.AppUserDetail,
              ),
            title: '使用者明細',
          },
          {
            path: 'app-roles',
            loadComponent: () =>
              import('@features/app-roles/app-role-list/app-role-list').then((m) => m.AppRoleList),
            title: '角色 AppRole',
          },
          {
            path: 'app-roles/new',
            loadComponent: () =>
              import('@features/app-roles/app-role-form/app-role-form').then((m) => m.AppRoleForm),
            title: '新增角色',
          },
          {
            path: 'app-roles/:id/edit',
            loadComponent: () =>
              import('@features/app-roles/app-role-form/app-role-form').then((m) => m.AppRoleForm),
            title: '編輯角色',
          },
          {
            path: 'app-roles/:id',
            loadComponent: () =>
              import('@features/app-roles/app-role-detail/app-role-detail').then(
                (m) => m.AppRoleDetail,
              ),
            title: '角色明細',
          },
          {
            path: 'publish-statuses',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-list/publish-status-list').then(
                (m) => m.PublishStatusList,
              ),
            title: '發布狀態 PublishStatus',
          },
          {
            path: 'publish-statuses/new',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-form/publish-status-form').then(
                (m) => m.PublishStatusForm,
              ),
            title: '新增發布狀態',
          },
          {
            path: 'publish-statuses/:id/edit',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-form/publish-status-form').then(
                (m) => m.PublishStatusForm,
              ),
            title: '編輯發布狀態',
          },
          {
            path: 'publish-statuses/:id',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-detail/publish-status-detail').then(
                (m) => m.PublishStatusDetail,
              ),
            title: '發布狀態明細',
          },
        ],
      },
      {
        path: 'courses',
        loadComponent: () =>
          import('@features/courses/course-list/course-list').then((m) => m.CourseList),
        title: '課程 Course',
      },
      {
        path: 'courses/new',
        loadComponent: () =>
          import('@features/courses/course-form/course-form').then((m) => m.CourseForm),
        title: '新增課程',
      },
      {
        path: 'courses/:id/edit',
        loadComponent: () =>
          import('@features/courses/course-form/course-form').then((m) => m.CourseForm),
        title: '編輯課程',
      },
      {
        path: 'courses/:id',
        loadComponent: () =>
          import('@features/courses/course-detail/course-detail').then((m) => m.CourseDetail),
        title: '課程明細',
      },
      {
        path: 'partners',
        loadComponent: () =>
          import('@features/partners/partner-list/partner-list').then((m) => m.PartnerList),
        title: '合作廠商 Partner',
      },
      {
        path: 'partners/new',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form').then((m) => m.PartnerForm),
        title: '新增合作廠商',
      },
      {
        path: 'partners/:id/edit',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form').then((m) => m.PartnerForm),
        title: '編輯合作廠商',
      },
      {
        path: 'partners/:id',
        loadComponent: () =>
          import('@features/partners/partner-detail/partner-detail').then((m) => m.PartnerDetail),
        title: '合作廠商明細',
      },
      {
        path: 'course-groups',
        loadComponent: () =>
          import('@features/course-groups/course-group-list/course-group-list').then(
            (m) => m.CourseGroupList,
          ),
        title: '課程群組 CourseGroup',
      },
      {
        path: 'course-groups/new',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form').then(
            (m) => m.CourseGroupForm,
          ),
        title: '新增課程群組',
      },
      {
        path: 'course-groups/:id/edit',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form').then(
            (m) => m.CourseGroupForm,
          ),
        title: '編輯課程群組',
      },
      {
        path: 'course-groups/:id',
        loadComponent: () =>
          import('@features/course-groups/course-group-detail/course-group-detail').then(
            (m) => m.CourseGroupDetail,
          ),
        title: '課程群組明細',
      },
      { path: '**', redirectTo: 'featured-promo-items' },
    ],
  },
];
