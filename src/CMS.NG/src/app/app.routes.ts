import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app-roles' },
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
