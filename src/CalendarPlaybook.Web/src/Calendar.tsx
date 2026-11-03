import { useEffect, useState } from 'react';
import type { ListOccurrences, Occurrence } from './api';
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

type Props = { listOccurrences: ListOccurrences; onSignOut?: () => void };

export default function Calendar({ listOccurrences, onSignOut }: Props) {
  const [view, setView] = useState(() => {
    const now = new Date();
    return { year: now.getFullYear(), month: now.getMonth() };
  });
  const [occurrences, setOccurrences] = useState<Occurrence[]>([]);
  const [error, setError] = useState<string | null>(null);

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
    listOccurrences(from, to)
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
  }, [listOccurrences, from, to]);

  return (
    <div className="page">
      <header className="site-header">
        <h1>Calendar</h1>
        <nav className="month-nav">
          <button type="button" aria-label="Previous month" onClick={() => shift(-1)}>←</button>
          <span id="monthLabel" className="month-label">{label}</span>
          <button type="button" aria-label="Next month" onClick={() => shift(1)}>→</button>
          {onSignOut && <button type="button" onClick={onSignOut}>Sign out</button>}
        </nav>
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
              >
                <span className="num">{d.day}</span>
                {occurrences
                  .filter((o) => o.startDate <= iso && iso <= o.endDate)
                  .map((o) => (
                    <div key={o.eventId} className="event" title={o.title}>{o.title}</div>
                  ))}
              </div>
            );
          })}
        </div>
      </main>
    </div>
  );
}
