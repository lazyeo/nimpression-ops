import { describe, expect, it } from 'vitest';
import { toScreamingSnake } from './case.utils';

describe('case.utils', () => {
  describe('toScreamingSnake', () => {
    it('should convert PascalCase to SCREAMING_SNAKE_CASE', () => {
      expect(toScreamingSnake('UnderReview')).toBe('UNDER_REVIEW');
      expect(toScreamingSnake('AutoClosed')).toBe('AUTO_CLOSED');
      expect(toScreamingSnake('InProgress')).toBe('IN_PROGRESS');
      expect(toScreamingSnake('OnLeave')).toBe('ON_LEAVE');
      expect(toScreamingSnake('NotStarted')).toBe('NOT_STARTED');
    });

    it('should convert single-word PascalCase', () => {
      expect(toScreamingSnake('Active')).toBe('ACTIVE');
      expect(toScreamingSnake('Draft')).toBe('DRAFT');
      expect(toScreamingSnake('Submitted')).toBe('SUBMITTED');
      expect(toScreamingSnake('Cancelled')).toBe('CANCELLED');
    });

    it('should preserve already SCREAMING_SNAKE_CASE strings', () => {
      expect(toScreamingSnake('UNDER_REVIEW')).toBe('UNDER_REVIEW');
      expect(toScreamingSnake('NOT_STARTED')).toBe('NOT_STARTED');
      expect(toScreamingSnake('ACTIVE')).toBe('ACTIVE');
      expect(toScreamingSnake('IN_PROGRESS')).toBe('IN_PROGRESS');
    });

    it('should convert camelCase strings', () => {
      expect(toScreamingSnake('underReview')).toBe('UNDER_REVIEW');
      expect(toScreamingSnake('autoClosed')).toBe('AUTO_CLOSED');
    });

    it('should convert space-separated or kebab-case strings', () => {
      expect(toScreamingSnake('under review')).toBe('UNDER_REVIEW');
      expect(toScreamingSnake('under-review')).toBe('UNDER_REVIEW');
      expect(toScreamingSnake('  auto closed  ')).toBe('AUTO_CLOSED');
    });

    it('should handle acronyms and abbreviations correctly', () => {
      expect(toScreamingSnake('GPSLocation')).toBe('GPS_LOCATION');
      expect(toScreamingSnake('HTTPResponse')).toBe('HTTP_RESPONSE');
    });

    it('should return empty string for null, undefined, or empty string', () => {
      expect(toScreamingSnake('')).toBe('');
      expect(toScreamingSnake(null)).toBe('');
      expect(toScreamingSnake(undefined)).toBe('');
    });
  });
});
