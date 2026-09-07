/**
 * Converts PascalCase, camelCase, or delimited strings to SCREAMING_SNAKE_CASE.
 * Used for mapping enum values to i18n keys uniformly across the application.
 */
export function toScreamingSnake(input: string | null | undefined): string {
  if (!input) return '';
  return input
    .trim()
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1_$2')
    .replace(/([a-z\d])([A-Z])/g, '$1_$2')
    .replace(/[-\s]+/g, '_')
    .toUpperCase();
}
