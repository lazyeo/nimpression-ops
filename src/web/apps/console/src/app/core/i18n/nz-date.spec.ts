import { afterEach, describe, expect, it, vi } from 'vitest';
import { formatNzDate } from './nz-date';

const dateTimeOptions: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
};

describe('New Zealand display dates', () => {
  afterEach(() => vi.unstubAllEnvs());

  it.each(['UTC', 'America/Los_Angeles', 'Pacific/Auckland'])(
    'preserves calendar dates in browser timezone %s',
    (timezone) => {
      vi.stubEnv('TZ', timezone);
      expect(formatNzDate('2026-01-01')).toBe('01/01/2026');
      expect(formatNzDate('2026-09-27')).toBe('27/09/2026');
      expect(formatNzDate('2026-12-31T12:00:00Z')).toBe('01/01/2027');
    },
  );

  it.each([
    ['2026-09-26T13:59:00Z', '27/09/2026, 01:59'],
    ['2026-09-26T14:00:00Z', '27/09/2026, 03:00'],
    ['2026-04-04T13:59:00Z', '05/04/2026, 02:59'],
    ['2026-04-04T14:00:00Z', '05/04/2026, 02:00'],
  ])('handles Auckland daylight saving transition for %s', (instant, expected) => {
    expect(formatNzDate(instant, dateTimeOptions)).toBe(expected);
  });

  it('formats equivalent offsets as the same local instant', () => {
    expect(formatNzDate('2026-09-09T09:00:00+12:00', dateTimeOptions)).toBe('09/09/2026, 09:00');
    expect(formatNzDate('2026-09-08T21:00:00Z', dateTimeOptions)).toBe('09/09/2026, 09:00');
  });

  it('does not display invalid dates or malformed calendar values', () => {
    for (const value of [null, undefined, '', 'not-a-date', '2026-02-30', new Date(NaN)]) {
      expect(formatNzDate(value)).toBe('');
    }
  });
});
