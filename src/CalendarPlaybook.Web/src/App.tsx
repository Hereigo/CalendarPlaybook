import { useState } from 'react';
import { buildMonthGrid } from './monthGrid';

const weekdays = [
  ['M', 'Mon'],
  ['T', 'Tue'],
  ['W', 'Wed'],
  ['T', 'Thu'],
  ['F', 'Fri'],
  ['S', 'Sat'],
  ['S', 'Sun'],
];

export default function App() {
  const [view, setView] = useState(() => {
    const now = new Date();
    return { year: now.getFullYear(), month: now.getMonth() };
  });

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

  return (
    <div className="page">
      <header className="site-header">
        <h1>Calendar</h1>
        <nav className="month-nav">
          <button type="button" aria-label="Previous month" onClick={() => shift(-1)}>←</button>
          <span id="monthLabel" className="month-label">{label}</span>
          <button type="button" aria-label="Next month" onClick={() => shift(1)}>→</button>
        </nav>
      </header>
      <aside className="sidebar">
        <h2>Upcoming</h2>
        <ul />
      </aside>
      <main className="main">
        <div className="calendar" role="grid" aria-labelledby="monthLabel">
          {weekdays.map(([short, long], i) => (
            <div key={i} className="weekday" role="columnheader">
              <span>{short}</span>
              <span>{long}</span>
            </div>
          ))}
          {days.map((d) => (
            <div
              key={d.date.getTime()}
              className={`day${d.outside ? ' outside' : ''}${d.today ? ' today' : ''}`}
              role="gridcell"
            >
              <span className="num">{d.day}</span>
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}
