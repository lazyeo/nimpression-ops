export const NZ_TIME_ZONE = 'Pacific/Auckland';

/** Format instants in New Zealand while preserving calendar-only dates. */
export function formatNzDate(
  value: Date | string | number | null | undefined,
  options: Intl.DateTimeFormatOptions = { day: '2-digit', month: '2-digit', year: 'numeric' },
): string {
  if (value === null || value === undefined || value === '') return '';
  const dateOnly = typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value);
  const date = dateOnly
    ? new Date(`${value}T00:00:00.000Z`)
    : value instanceof Date
      ? value
      : new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  if (dateOnly && date.toISOString().slice(0, 10) !== value) return '';

  return new Intl.DateTimeFormat('en-NZ', {
    ...options,
    timeZone: dateOnly ? 'UTC' : NZ_TIME_ZONE,
  }).format(date);
}
