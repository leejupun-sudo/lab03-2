/**
 * Date helpers for `date` columns, which travel as ISO `yyyy-MM-dd` strings.
 *
 * Every conversion uses LOCAL date components. `Date.prototype.toISOString()` converts to
 * UTC first, which for a UTC+8 user turns a midnight `Date` into the previous day — the
 * classic off-by-one. Never use it for a date-only column.
 */

function pad2(value: number): string {
  return value < 10 ? `0${value}` : `${value}`;
}

/** `Date` → `yyyy-MM-dd` from local year/month/day. */
export function toIsoDate(date: Date): string {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
}

/** `yyyy-MM-dd` → local-midnight `Date`. Anything unparsable yields `null`. */
export function parseIsoDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) {
    return null;
  }
  const [, year, month, day] = match;
  return new Date(Number(year), Number(month) - 1, Number(day));
}

/** A new `Date` `years` after `date`, keeping month and day (Feb 29 rolls forward). */
export function addYears(date: Date, years: number): Date {
  const result = new Date(date.getTime());
  result.setFullYear(result.getFullYear() + years);
  return result;
}

/** A new `Date` `days` after `date` (negative goes back), at the same local time. */
export function addDays(date: Date, days: number): Date {
  const result = new Date(date.getTime());
  result.setDate(result.getDate() + days);
  return result;
}

/**
 * Local-midnight Monday of the week containing `date`. `getDay()` is 0 for Sunday, so a
 * Sunday goes back six days rather than forward one — the same rule as the API's
 * `FeaturedPromoItemQuery.StartOfWeek`, and both sides must agree on it.
 */
export function startOfWeek(date: Date): Date {
  const daysSinceMonday = (date.getDay() + 6) % 7;
  const monday = addDays(date, -daysSinceMonday);
  monday.setHours(0, 0, 0, 0);
  return monday;
}

const WEEKDAY_ZH = ['日', '一', '二', '三', '四', '五', '六'] as const;

/** `M/d`, no padding — matches the 上稿作業 mockup (`3/16`). */
export function formatMonthDay(date: Date): string {
  return `${date.getMonth() + 1}/${date.getDate()}`;
}

/** `M/d (一)` — the day header in the weekly grid. */
export function formatDayLabel(date: Date): string {
  return `${formatMonthDay(date)} (${WEEKDAY_ZH[date.getDay()]})`;
}
