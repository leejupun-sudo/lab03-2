import {
  addDays,
  addYears,
  formatDayLabel,
  formatMonthDay,
  parseIsoDate,
  startOfWeek,
  toIsoDate,
} from './date.util';

describe('date.util', () => {
  it('toIsoDate() uses local components, so a local midnight never shifts a day', () => {
    // toISOString() on this Date would yield the previous day in any UTC+ zone.
    expect(toIsoDate(new Date(2026, 0, 16))).toBe('2026-01-16');
    expect(toIsoDate(new Date(2015, 10, 9))).toBe('2015-11-09');
  });

  it('parseIsoDate() builds a local-midnight Date', () => {
    const date = parseIsoDate('2015-11-10');

    expect(date).not.toBeNull();
    expect(date!.getFullYear()).toBe(2015);
    expect(date!.getMonth()).toBe(10);
    expect(date!.getDate()).toBe(10);
    expect(date!.getHours()).toBe(0);
  });

  it('parseIsoDate() tolerates a trailing time part and rejects garbage', () => {
    expect(parseIsoDate('2021-11-01T00:00:00')?.getDate()).toBe(1);
    expect(parseIsoDate('')).toBeNull();
    expect(parseIsoDate(null)).toBeNull();
    expect(parseIsoDate('not-a-date')).toBeNull();
  });

  it('round-trips through toIsoDate and parseIsoDate unchanged', () => {
    expect(toIsoDate(parseIsoDate('2099-12-31')!)).toBe('2099-12-31');
  });

  it('addYears() keeps month and day and leaves the input untouched', () => {
    const on = new Date(2026, 0, 16);
    const off = addYears(on, 10);

    expect(toIsoDate(off)).toBe('2036-01-16');
    expect(toIsoDate(on)).toBe('2026-01-16');
  });

  it('addDays() crosses month boundaries and leaves the input untouched', () => {
    const start = new Date(2026, 2, 30);

    expect(toIsoDate(addDays(start, 3))).toBe('2026-04-02');
    expect(toIsoDate(addDays(start, -7))).toBe('2026-03-23');
    expect(toIsoDate(start)).toBe('2026-03-30');
  });

  it('startOfWeek() maps every day of a week to its Monday, Sunday included', () => {
    // 2026-03-16 is a Monday.
    for (let offset = 0; offset < 7; offset++) {
      expect(toIsoDate(startOfWeek(new Date(2026, 2, 16 + offset)))).toBe('2026-03-16');
    }
    // Sunday 03-15 belongs to the PREVIOUS week — getDay() is 0, not 7.
    expect(toIsoDate(startOfWeek(new Date(2026, 2, 15)))).toBe('2026-03-09');
  });

  it('startOfWeek() returns local midnight', () => {
    const monday = startOfWeek(new Date(2026, 2, 18, 15, 45));

    expect(monday.getHours()).toBe(0);
    expect(monday.getMinutes()).toBe(0);
  });

  it('formatMonthDay() and formatDayLabel() render the mockup shapes', () => {
    expect(formatMonthDay(new Date(2026, 2, 16))).toBe('3/16');
    expect(formatMonthDay(new Date(2026, 11, 5))).toBe('12/5');
    expect(formatDayLabel(new Date(2026, 2, 16))).toBe('3/16 (一)');
    expect(formatDayLabel(new Date(2026, 2, 22))).toBe('3/22 (日)');
  });
});
