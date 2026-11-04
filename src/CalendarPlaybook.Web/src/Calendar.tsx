import { useEffect, useState } from 'react';
import type { CalendarApi, EventInput, Occurrence } from './api';
import EventForm from './EventForm';
import { emptyForm, fromEvent, type FormState } from './formModel';
import { buildMonthGrid, toIsoDate } from './monthGrid';

const weekdays = [
  ['M', 'Mon'],
  ['T', 'Tue'],
  ['W', 'Wed'],
  ['T', 'Thu'],
  ['F', 'Fri'],
  ['S', 'Sat'],
  ['S', 'Sun'],
];

type Props = { api: CalendarApi; onSignOut?: () => void };

/** `id` is set when editing an existing event. */
type Editor = { id: string | null; initial: FormState };

export default function Calendar({ api, onSignOut }: Props) {
  const [view, setView] = useState(() => {
    const now = new Date();
    return { year: now.getFullYear(), month: now.getMonth() };
  });
  const [occurrences, setOccurrences] = useState<Occurrence[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [editor, setEditor] = useState<Editor | null>(null);
  const [reload, setReload] = useState(0);

  const shift = (delta: number) =>
    setView(({ year, month }) => {
      const d = new Date(year, month + delta, 1);
      return { year: d.getFullYear(), month: d.getMonth() };
    });

  const label = new Date(view.year, view.month, 1).toLocaleDateString('en-GB', {
    month: 'long',
    year: 'numeric',
  });
  const days = buildMonthGrid(view.year, view.month, new Date());
  const from = toIsoDate(days[0].date);
  const to = toIsoDate(days[days.length - 1].date);

  useEffect(() => {
    let current = true;
    api
      .listOccurrences(from, to)
      .then((items) => {
        if (!current) return;
        setOccurrences(items);
        setError(null);
      })
      .catch((e: Error) => {
        if (!current) return;
        setOccurrences([]);
        setError(e.message);
      });
    return () => {
      current = false;
    };
  }, [api, from, to, reload]);

  const close = () => setEditor(null);
  const saved = () => {
    close();
    setReload((n) => n + 1);
  };
  const save = async (input: EventInput) => {
    if (editor?.id) await api.updateEvent(editor.id, input);
    else await api.createEvent(input);
    saved();
  };
  const remove = async () => {
    if (editor?.id) await api.deleteEvent(editor.id);
    saved();
  };
  const openEvent = (id: string) =>
    api
      .getEvent(id)
      .then((event) => setEditor({ id, initial: fromEvent(event) }))
      .catch((e: Error) => setError(e.message));

  return (
    <div className="page">
      <header className="site-header">
        <nav className="month-nav">
          <button type="button" aria-label="Previous month" onClick={() => shift(-1)}>←</button>
          <span id="monthLabel" className="month-label">{label}</span>
          <button type="button" aria-label="Next month" onClick={() => shift(1)}>→</button>
        </nav>
        {onSignOut && <button type="button" onClick={onSignOut}>Sign out</button>}
      </header>
      <aside className="sidebar">
        <h2>Upcoming</h2>
        <ul />
      </aside>
      <main className="main">
        {error && <p className="error" role="alert">{error}</p>}
        <div className="calendar" role="grid" aria-labelledby="monthLabel">
          {weekdays.map(([short, long], i) => (
            <div key={i} className="weekday" role="columnheader">
              <span>{short}</span>
              <span>{long}</span>
            </div>
          ))}
          {days.map((d) => {
            const iso = toIsoDate(d.date);
            return (
              <div
                key={iso}
                className={`day${d.outside ? ' outside' : ''}${d.today ? ' today' : ''}`}
                role="gridcell"
                onClick={() => setEditor({ id: null, initial: emptyForm(iso) })}
              >
                <span className="num">{d.day}</span>
                {occurrences
                  .filter((o) => o.startDate <= iso && iso <= o.endDate)
                  .map((o) => (
                    <button
                      key={o.eventId}
                      type="button"
                      className="event"
                      title={o.title}
                      onClick={(e) => {
                        e.stopPropagation();
                        void openEvent(o.eventId);
                      }}
                    >
                      {o.title}
                    </button>
                  ))}
              </div>
            );
          })}
        </div>
      </main>
      {editor && (
        <EventForm
          initial={editor.initial}
          editing={editor.id !== null}
          onSave={save}
          onDelete={remove}
          onClose={close}
        />
      )}
    </div>
  );
}
