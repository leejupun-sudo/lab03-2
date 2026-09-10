/**
 * Production environment. Replaced by `environment.development.ts`
 * in the `development` build configuration (see angular.json fileReplacements).
 *
 * Import via the `@env` short-hand path, e.g. `import { environment } from '@env';`
 */
export const environment = {
  production: true,
  apiBaseUrl: '/api',
  /** Public site the course QR codes point at. */
  publicSiteBaseUrl: 'https://www.uuu.com.tw',
};
