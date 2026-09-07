import { describe, expect, it } from 'vitest';
import { ScreamingSnakePipe } from './screaming-snake.pipe';

describe('ScreamingSnakePipe', () => {
  const pipe = new ScreamingSnakePipe();

  it('should transform PascalCase to SCREAMING_SNAKE_CASE', () => {
    expect(pipe.transform('UnderReview')).toBe('UNDER_REVIEW');
    expect(pipe.transform('AutoClosed')).toBe('AUTO_CLOSED');
  });

  it('should handle empty/null/undefined', () => {
    expect(pipe.transform('')).toBe('');
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
  });
});
