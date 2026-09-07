import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
  toDateTimeLocalValue,
  toDateInputValue,
  parseDateTimeLocalToIso,
  parseDateInputToIso,
} from './date-utils';

declare const process: { env: Record<string, string | undefined> };

describe('date-utils', () => {
  const originalTz = process.env['TZ'];

  afterEach(() => {
    if (originalTz !== undefined) {
      process.env['TZ'] = originalTz;
    } else {
      delete process.env['TZ'];
    }
  });

  describe('Non-UTC Timezone Wall-Clock Conversions (AC 1)', () => {
    it('accurately converts UTC ISO to NZ local wall-clock string (UTC+12)', () => {
      process.env['TZ'] = 'Pacific/Auckland';

      // 2026-09-07T06:00:00.000Z in UTC -> 18:00 in NZ Standard Time (UTC+12)
      const utcIso = '2026-09-07T06:00:00.000Z';
      const localWallClock = toDateTimeLocalValue(utcIso);

      expect(localWallClock).toBe('2026-09-07T18:00');
    });

    it('accurately converts NZ local wall-clock string to UTC ISO (UTC+12)', () => {
      process.env['TZ'] = 'Pacific/Auckland';

      const localWallClock = '2026-09-07T18:00';
      const utcIso = parseDateTimeLocalToIso(localWallClock);

      expect(utcIso).toBe('2026-09-07T06:00:00.000Z');
    });

    it('preserves exact wall-clock time across round-trip in Pacific/Auckland (UTC+12)', () => {
      process.env['TZ'] = 'Pacific/Auckland';

      const enteredWallClock = '2026-09-07T17:59';
      const toBackendIso = parseDateTimeLocalToIso(enteredWallClock);
      expect(toBackendIso).toBe('2026-09-07T05:59:00.000Z');

      // Server echoes toBackendIso, frontend populates assign form
      const echoedWallClock = toDateTimeLocalValue(toBackendIso);
      expect(echoedWallClock).toBe(enteredWallClock);
    });

    it('works correctly in other non-UTC timezones (America/New_York, Asia/Shanghai, Europe/London)', () => {
      const utcIso = '2026-09-07T06:00:00.000Z';

      // New York is UTC-4 in September (EDT)
      process.env['TZ'] = 'America/New_York';
      expect(toDateTimeLocalValue(utcIso)).toBe('2026-09-07T02:00');
      expect(parseDateTimeLocalToIso('2026-09-07T02:00')).toBe(utcIso);

      // Shanghai is UTC+8 (CST)
      process.env['TZ'] = 'Asia/Shanghai';
      expect(toDateTimeLocalValue(utcIso)).toBe('2026-09-07T14:00');
      expect(parseDateTimeLocalToIso('2026-09-07T14:00')).toBe(utcIso);

      // London is UTC+1 in September (BST)
      process.env['TZ'] = 'Europe/London';
      expect(toDateTimeLocalValue(utcIso)).toBe('2026-09-07T07:00');
      expect(parseDateTimeLocalToIso('2026-09-07T07:00')).toBe(utcIso);

      // UTC
      process.env['TZ'] = 'UTC';
      expect(toDateTimeLocalValue(utcIso)).toBe('2026-09-07T06:00');
      expect(parseDateTimeLocalToIso('2026-09-07T06:00')).toBe(utcIso);
    });
  });

  describe('New Zealand Daylight Saving Time (DST) Boundary Tests (AC 2)', () => {
    beforeEach(() => {
      process.env['TZ'] = 'Pacific/Auckland';
    });

    it('handles Spring DST forward transition (Sun 27 Sep 2026: UTC+12 -> UTC+13)', () => {
      // 1. Before DST starts (NZST = UTC+12, Sat 26 Sep 18:00)
      const preDstIso = '2026-09-26T06:00:00.000Z';
      expect(toDateTimeLocalValue(preDstIso)).toBe('2026-09-26T18:00');
      expect(parseDateTimeLocalToIso('2026-09-26T18:00')).toBe(preDstIso);

      // 2. Day after DST starts (NZDT = UTC+13, Mon 28 Sep 18:00)
      const postDstIso = '2026-09-28T05:00:00.000Z';
      expect(toDateTimeLocalValue(postDstIso)).toBe('2026-09-28T18:00');
      expect(parseDateTimeLocalToIso('2026-09-28T18:00')).toBe(postDstIso);

      // Notice: If a fixed +12h offset had been used, postDstIso would yield 17:00 (wrong by 1h).
      // Platform conversion yields 18:00 (correct).
    });

    it('handles Autumn DST backward transition (Sun 5 Apr 2026: UTC+13 -> UTC+12)', () => {
      // 1. Before DST ends (NZDT = UTC+13)
      const preAutumnIso = '2026-04-04T13:00:00.000Z'; // 02:00 on Apr 5 (NZDT)
      expect(toDateTimeLocalValue(preAutumnIso)).toBe('2026-04-05T02:00');
      expect(parseDateTimeLocalToIso('2026-04-05T02:00')).toBe(preAutumnIso);

      // 2. Day after DST ends (NZST = UTC+12)
      const postAutumnIso = '2026-04-06T06:00:00.000Z'; // 18:00 on Apr 6 (NZST)
      expect(toDateTimeLocalValue(postAutumnIso)).toBe('2026-04-06T18:00');
      expect(parseDateTimeLocalToIso('2026-04-06T18:00')).toBe(postAutumnIso);
    });
  });

  describe('Date Input Utilities', () => {
    it('formats Date and ISO strings as YYYY-MM-DD local date', () => {
      process.env['TZ'] = 'Pacific/Auckland';

      // 2026-09-06T17:00:00Z in UTC is 2026-09-07 05:00 NZST
      const iso = '2026-09-06T17:00:00.000Z';
      expect(toDateInputValue(iso)).toBe('2026-09-07');
    });

    it('parses YYYY-MM-DD into start-of-day ISO in local timezone', () => {
      process.env['TZ'] = 'Pacific/Auckland';

      const iso = parseDateInputToIso('2026-09-07');
      // 2026-09-07 00:00 NZST -> 2026-09-06 12:00:00.000Z
      expect(iso).toBe('2026-09-06T12:00:00.000Z');
    });
  });

  describe('Edge Cases and Safety Handling', () => {
    it('handles null, undefined, empty, and invalid inputs gracefully', () => {
      expect(toDateTimeLocalValue(null)).toBe('');
      expect(toDateTimeLocalValue(undefined)).toBe('');
      expect(toDateTimeLocalValue('')).toBe('');
      expect(toDateTimeLocalValue('invalid-date')).toBe('');

      expect(toDateInputValue(null)).toBe('');
      expect(toDateInputValue(undefined)).toBe('');
      expect(toDateInputValue('')).toBe('');
      expect(toDateInputValue('invalid-date')).toBe('');

      expect(parseDateTimeLocalToIso(null)).toBeUndefined();
      expect(parseDateTimeLocalToIso(undefined)).toBeUndefined();
      expect(parseDateTimeLocalToIso('')).toBeUndefined();
      expect(parseDateTimeLocalToIso('invalid-date')).toBeUndefined();

      expect(parseDateInputToIso(null)).toBeUndefined();
      expect(parseDateInputToIso(undefined)).toBeUndefined();
      expect(parseDateInputToIso('')).toBeUndefined();
      expect(parseDateInputToIso('invalid-date')).toBeUndefined();
    });
  });
});
