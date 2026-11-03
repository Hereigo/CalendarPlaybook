import { describe, expect, it } from 'vitest';
import { emptyForm, toInput } from './formModel';

describe('toInput', () => {
  const base = emptyForm('2026-11-05');

  it('sends only the fields a single event accepts', () => {
    const input = toInput({ ...base, title: ' Lunch ' });
    expect(input).toEqual({
      title: 'Lunch',
      description: null,
      kind: 'Single',
      startDate: '2026-11-05',
      startTime: '09:00:00',
      endDate: '2026-11-05',
      endTime: '10:00:00',
    });
  });

  it('omits times for an all-day event', () => {
    const input = toInput({ ...base, kind: 'AllDay' });
    expect(input).not.toHaveProperty('startTime');
    expect(input).not.toHaveProperty('endTime');
    expect(input.endDate).toBe('2026-11-05');
  });

  it('sends only the parameters of the chosen rule and no end date for a series', () => {
    const input = toInput({ ...base, kind: 'Recurring', ruleType: 'YearDay', untilDate: '2030-01-01' });
    expect(input).not.toHaveProperty('endDate');
    expect(input.recurrence).toEqual({ ruleType: 'YearDay', untilDate: '2030-01-01', dayOfMonth: 5, month: 11 });
  });

  it('leaves times out of an all-day series', () => {
    const input = toInput({ ...base, kind: 'Recurring', timed: false });
    expect(input).not.toHaveProperty('startTime');
    expect(input.recurrence).toMatchObject({ ruleType: 'EveryDays', interval: 1 });
  });
});
