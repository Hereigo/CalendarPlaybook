import { useState, type FormEvent, type ReactNode } from 'react';
import { ApiError, type EventInput, type EventKind, type RuleType } from './api';
import { toInput, weekDays, type FormState } from './formModel';

type Props = {
  initial: FormState;
  /** True when editing an existing event; enables Delete. */
  editing: boolean;
  onSave: (input: EventInput) => Promise<void>;
  onDelete: () => Promise<void>;
  onClose: () => void;
};

const kinds: [EventKind, string][] = [
  ['Single', 'Single'],
  ['AllDay', 'All day'],
  ['Recurring', 'Recurring'],
];

const ruleTypes: [RuleType, string][] = [
  ['EveryDays', 'Every N days'],
  ['MonthDay', 'Day of month'],
  ['WeekDay', 'Day of week'],
  ['YearDay', 'Day of year'],
];

type FieldProps = { label: string; name: string; fields: Record<string, string[]>; children: ReactNode };

function FieldBox({ label, name, fields, children }: FieldProps) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {fields[name]?.map((m) => (
        <small key={m} className="field-error">{m}</small>
      ))}
    </label>
  );
}

export default function EventForm({ initial, editing, onSave, onDelete, onClose }: Props) {
  const [f, setF] = useState(initial);
  const [fields, setFields] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => setF((s) => ({ ...s, [key]: value }));
  const text = (key: keyof FormState) => ({
    value: f[key] as string,
    onChange: (e: { target: { value: string } }) => set(key, e.target.value as never),
  });

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(null);
    setFields({});
    try {
      await action();
    } catch (e) {
      if (e instanceof ApiError) {
        setFields(e.fields);
        setError(Object.keys(e.fields).length > 0 ? 'Please fix the highlighted fields.' : e.message);
      } else {
        setError('Something went wrong.');
      }
      setBusy(false);
    }
  }

  const submit = (e: FormEvent) => {
    e.preventDefault();
    void run(() => onSave(toInput(f)));
  };

  const remove = () => {
    const message =
      f.kind === 'Recurring'
        ? 'Delete this event? The whole series will be deleted.'
        : 'Delete this event?';
    if (window.confirm(message)) void run(onDelete);
  };

  const timed = f.kind === 'Single' || (f.kind === 'Recurring' && f.timed);

  return (
    <div className="backdrop">
      <form className="dialog" role="dialog" aria-label={editing ? 'Edit event' : 'New event'} onSubmit={submit}>
        <h2>{editing ? 'Edit event' : 'New event'}</h2>

        <FieldBox fields={fields} label="Title" name="title">
          <input required maxLength={200} {...text('title')} />
        </FieldBox>
        <FieldBox fields={fields} label="Description" name="description">
          <textarea maxLength={4000} rows={3} {...text('description')} />
        </FieldBox>
        <FieldBox fields={fields} label="Type" name="kind">
          <select value={f.kind} onChange={(e) => set('kind', e.target.value as EventKind)}>
            {kinds.map(([value, name]) => (
              <option key={value} value={value}>{name}</option>
            ))}
          </select>
        </FieldBox>

        {editing && f.kind === 'Recurring' && (
          <p className="notice">This is a recurring event: changes apply to the whole series.</p>
        )}

        <FieldBox fields={fields} label={f.kind === 'Recurring' ? 'First date' : 'Start date'} name="startDate">
          <input type="date" required {...text('startDate')} />
        </FieldBox>

        {f.kind === 'Recurring' && (
          <label className="check">
            <input type="checkbox" checked={f.timed} onChange={(e) => set('timed', e.target.checked)} />
            Timed (otherwise all-day)
          </label>
        )}

        {timed && (
          <FieldBox fields={fields} label="Start time" name="startTime">
            <input type="time" required {...text('startTime')} />
          </FieldBox>
        )}
        {f.kind !== 'Recurring' && (
          <FieldBox fields={fields} label={f.kind === 'AllDay' ? 'Last day (inclusive)' : 'End date'} name="endDate">
            <input type="date" required {...text('endDate')} />
          </FieldBox>
        )}
        {timed && (
          <FieldBox fields={fields} label="End time" name="endTime">
            <input type="time" required {...text('endTime')} />
          </FieldBox>
        )}

        {f.kind === 'Recurring' && (
          <>
            <FieldBox fields={fields} label="Repeat" name="recurrence.ruleType">
              <select value={f.ruleType} onChange={(e) => set('ruleType', e.target.value as RuleType)}>
                {ruleTypes.map(([value, name]) => (
                  <option key={value} value={value}>{name}</option>
                ))}
              </select>
            </FieldBox>
            {f.ruleType === 'EveryDays' && (
              <FieldBox fields={fields} label="Every N days" name="recurrence.interval">
                <input type="number" min={1} required {...text('interval')} />
              </FieldBox>
            )}
            {(f.ruleType === 'MonthDay' || f.ruleType === 'YearDay') && (
              <FieldBox fields={fields} label="Day of month" name="recurrence.dayOfMonth">
                <input type="number" min={1} max={31} required {...text('dayOfMonth')} />
              </FieldBox>
            )}
            {f.ruleType === 'YearDay' && (
              <FieldBox fields={fields} label="Month" name="recurrence.month">
                <input type="number" min={1} max={12} required {...text('month')} />
              </FieldBox>
            )}
            {f.ruleType === 'WeekDay' && (
              <FieldBox fields={fields} label="Weekday" name="recurrence.dayOfWeek">
                <select value={f.dayOfWeek} onChange={(e) => set('dayOfWeek', e.target.value as FormState['dayOfWeek'])}>
                  {weekDays.map((d) => (
                    <option key={d} value={d}>{d}</option>
                  ))}
                </select>
              </FieldBox>
            )}
            <FieldBox fields={fields} label="Until (inclusive)" name="recurrence.untilDate">
              <input type="date" required {...text('untilDate')} />
            </FieldBox>
          </>
        )}

        {error && <p className="error" role="alert">{error}</p>}

        <div className="actions">
          {editing && (
            <button type="button" className="danger" disabled={busy} onClick={remove}>Delete</button>
          )}
          <span className="spacer" />
          <button type="button" disabled={busy} onClick={onClose}>Cancel</button>
          <button type="submit" disabled={busy}>Save</button>
        </div>
      </form>
    </div>
  );
}
