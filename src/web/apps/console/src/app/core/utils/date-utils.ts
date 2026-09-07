/**
 * Date and time utility functions for form controls and timezone-safe conversions.
 *
 * <input type="datetime-local"> inputs expect wall-clock strings in the format "YYYY-MM-DDTHH:mm".
 * <input type="date"> inputs expect wall-clock strings in the format "YYYY-MM-DD".
 *
 * These utilities use standard platform local time getters/constructors to accurately
 * convert between UTC ISO strings (backend format) and local wall-clock values without
 * using hardcoded offsets, correctly handling Daylight Saving Time (DST) changes.
 */

/**
 * Converts a Date, ISO string, or timestamp into a local wall-clock string "YYYY-MM-DDTHH:mm"
 * suitable for binding to <input type="datetime-local">.
 */
export function toDateTimeLocalValue(date: Date | string | number | null | undefined): string {
  if (date === null || date === undefined || date === '') {
    return '';
  }

  const d = typeof date === 'string' || typeof date === 'number' ? new Date(date) : date;
  if (!d || isNaN(d.getTime())) {
    return '';
  }

  const pad = (n: number) => n.toString().padStart(2, '0');
  const year = d.getFullYear();
  const month = pad(d.getMonth() + 1);
  const day = pad(d.getDate());
  const hours = pad(d.getHours());
  const minutes = pad(d.getMinutes());

  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

/**
 * Converts a Date, ISO string, or timestamp into a local wall-clock string "YYYY-MM-DD"
 * suitable for binding to <input type="date">.
 */
export function toDateInputValue(date: Date | string | number | null | undefined): string {
  if (date === null || date === undefined || date === '') {
    return '';
  }

  const d = typeof date === 'string' || typeof date === 'number' ? new Date(date) : date;
  if (!d || isNaN(d.getTime())) {
    return '';
  }

  const pad = (n: number) => n.toString().padStart(2, '0');
  const year = d.getFullYear();
  const month = pad(d.getMonth() + 1);
  const day = pad(d.getDate());

  return `${year}-${month}-${day}`;
}

/**
 * Parses a local wall-clock string (e.g. from <input type="datetime-local"> "YYYY-MM-DDTHH:mm")
 * into a UTC ISO 8601 string (e.g. "2026-09-07T06:00:00.000Z") for sending to the API.
 *
 * If the input is empty or invalid, returns undefined.
 */
export function parseDateTimeLocalToIso(value: string | null | undefined): string | undefined {
  if (!value || !value.trim()) {
    return undefined;
  }

  const trimmed = value.trim();
  const d = new Date(trimmed);
  if (isNaN(d.getTime())) {
    return undefined;
  }

  return d.toISOString();
}

/**
 * Parses a local date string (e.g. from <input type="date"> "YYYY-MM-DD")
 * into a UTC ISO 8601 string representing the start of that day in local time.
 *
 * If the input is empty or invalid, returns undefined.
 */
export function parseDateInputToIso(value: string | null | undefined): string | undefined {
  if (!value || !value.trim()) {
    return undefined;
  }

  const trimmed = value.trim();
  // Standard 'YYYY-MM-DD' in JS is parsed as UTC in ISO format,
  // so we construct local Date using year/month/day components
  const parts = trimmed.split('-').map(Number);
  if (parts.length !== 3 || parts.some(isNaN)) {
    return undefined;
  }

  const d = new Date(parts[0], parts[1] - 1, parts[2], 0, 0, 0, 0);
  if (isNaN(d.getTime())) {
    return undefined;
  }

  return d.toISOString();
}
