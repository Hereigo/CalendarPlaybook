export type GridDay = { date: Date; day: number; outside: boolean; today: boolean };

const sameDay = (a: Date, b: Date) =>
  a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();

/** 6 weeks x 7 days, Monday first, starting with the week that contains the 1st of `month`. */
export function buildMonthGrid(year: number, month: number, today: Date): GridDay[] {
  const first = new Date(year, month, 1);
  const offset = (first.getDay() + 6) % 7;
  return Array.from({ length: 42 }, (_, i) => {
    const date = new Date(year, month, 1 - offset + i);
    return { date, day: date.getDate(), outside: date.getMonth() !== month, today: sameDay(date, today) };
  });
}

export const toIsoDate = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
