import { addYears, parseIsoDate, toIsoDate } from './date.util';

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
});
