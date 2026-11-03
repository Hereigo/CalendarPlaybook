import type { EventDetails, EventInput, EventKind, RuleType, WeekDayName } from './api';

/** Form values as typed by the user: every field is a string so inputs stay controlled. */
export type FormState = {
  title: string;
  description: string;
  kind: EventKind;
  startDate: string;
  startTime: string;
  endDate: string;
  endTime: string;
  /** Recurring only: a timed series has start and end times, otherwise it is all-day. */
  timed: boolean;
  ruleType: RuleType;
  interval: string;
  dayOfMonth: string;
  month: string;
  dayOfWeek: WeekDayName;
  untilDate: string;
};

export const weekDays: WeekDayName[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export function emptyForm(date: string): FormState {
  return {
    title: '',
    description: '',
    kind: 'Single',
    startDate: date,
    startTime: '09:00',
    endDate: date,
    endTime: '10:00',
    timed: true,
    ruleType: 'EveryDays',
    interval: '1',
    dayOfMonth: date.slice(8, 10).replace(/^0/, ''),
    month: date.slice(5, 7).replace(/^0/, ''),
    dayOfWeek: 'Monday',
    untilDate: date,
  };
}

const hhmm = (t?: string | null) => (t ? t.slice(0, 5) : '');
const withSeconds = (t: string) => (t.length === 5 ? `${t}:00` : t);

export function fromEvent(event: EventDetails & { description?: string | null }): FormState {
  const base = emptyForm(event.startDate ?? '');
  const r = event.recurrence;
  return {
    ...base,
    title: event.title,
    description: event.description ?? '',
    kind: event.kind,
    startDate: event.startDate ?? '',
    startTime: hhmm(event.startTime) || base.startTime,
    endDate: event.endDate ?? event.startDate ?? '',
    endTime: hhmm(event.endTime) || base.endTime,
    timed: event.kind === 'Recurring' ? Boolean(event.startTime) : true,
    ruleType: r?.ruleType ?? base.ruleType,
    interval: r?.interval != null ? String(r.interval) : base.interval,
    dayOfMonth: r?.dayOfMonth != null ? String(r.dayOfMonth) : base.dayOfMonth,
    month: r?.month != null ? String(r.month) : base.month,
    dayOfWeek: r?.dayOfWeek ?? base.dayOfWeek,
    untilDate: r?.untilDate ?? base.untilDate,
  };
}

/** Builds the request body, sending only the fields the chosen kind and rule type accept (the API rejects the others). */
export function toInput(f: FormState): EventInput {
  const input: EventInput = {
    title: f.title.trim(),
    description: f.description.trim() === '' ? null : f.description,
    kind: f.kind,
    startDate: f.startDate,
  };

  if (f.kind === 'Single') {
    input.startTime = withSeconds(f.startTime);
    input.endDate = f.endDate;
    input.endTime = withSeconds(f.endTime);
  } else if (f.kind === 'AllDay') {
    input.endDate = f.endDate;
  } else {
    if (f.timed) {
      input.startTime = withSeconds(f.startTime);
      input.endTime = withSeconds(f.endTime);
    }
    input.recurrence = {
      ruleType: f.ruleType,
      untilDate: f.untilDate,
      ...(f.ruleType === 'EveryDays' && { interval: Number(f.interval) }),
      ...((f.ruleType === 'MonthDay' || f.ruleType === 'YearDay') && { dayOfMonth: Number(f.dayOfMonth) }),
      ...(f.ruleType === 'YearDay' && { month: Number(f.month) }),
      ...(f.ruleType === 'WeekDay' && { dayOfWeek: f.dayOfWeek }),
    };
  }
  return input;
}
