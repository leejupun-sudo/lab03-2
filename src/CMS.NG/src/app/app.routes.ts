import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app-roles' },
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
      import('@features/app-users/app-user-detail/app-user-detail').then((m) => m.AppUserDetail),
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
      import('@features/app-roles/app-role-detail/app-role-detail').then((m) => m.AppRoleDetail),
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
  { path: '**', redirectTo: 'app-roles' },
];
