import { describe, it, expect } from 'vitest';
import { roleLabel, submissionStatusLabel, formatDate, formatPercent } from './format';

describe('format helpers', () => {
  it('maps roles and statuses to Arabic', () => {
    expect(roleLabel.Admin).toBe('مدير النظام');
    expect(submissionStatusLabel.Closed).toBe('مُغلق');
  });

  it('handles null dates and percents', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatPercent(null)).toBe('—');
    expect(formatPercent(75)).toContain('٪');
  });
});
