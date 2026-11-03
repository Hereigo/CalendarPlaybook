import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import Calendar from './Calendar';
import { buildMonthGrid, toIsoDate } from './monthGrid';

describe('buildMonthGrid', () => {
  it('starts on Monday and covers 42 days', () => {
    const grid = buildMonthGrid(2026, 9, new Date(2026, 9, 28)); // October 2026
    expect(grid).toHaveLength(42);
    expect(grid[0].date.getDay()).toBe(1);
    expect(grid[0].day).toBe(28); // Mon 28 Sep
    expect(grid[0].outside).toBe(true);
    expect(grid.filter((d) => d.today).map((d) => d.day)).toEqual([28]);
    expect(toIsoDate(grid[0].date)).toBe('2026-09-28');
  });
});

describe('Calendar', () => {
  it('navigates between months and reloads the range', async () => {
    const list = vi.fn().mockResolvedValue([]);
    render(<Calendar listOccurrences={list} />);
    const label = screen.getByText(/\d{4}$/);
    const before = label.textContent;
    await waitFor(() => expect(list).toHaveBeenCalledTimes(1));
    fireEvent.click(screen.getByLabelText('Next month'));
    expect(label.textContent).not.toBe(before);
    await waitFor(() => expect(list).toHaveBeenCalledTimes(2));
    fireEvent.click(screen.getByLabelText('Previous month'));
    expect(label.textContent).toBe(before);
  });

  it('shows a multi-day occurrence in every day cell it spans', async () => {
    const now = new Date();
    const d = (n: number) => toIsoDate(new Date(now.getFullYear(), now.getMonth(), n));
    const list = vi.fn().mockResolvedValue([
      { eventId: '1', kind: 'AllDay', title: 'Trip', occurrenceStartUtc: null, occurrenceEndUtc: null, startDate: d(10), endDate: d(12) },
    ]);
    render(<Calendar listOccurrences={list} />);
    await waitFor(() => expect(screen.getAllByText('Trip')).toHaveLength(3));
  });

  it('shows an error when loading fails', async () => {
    render(<Calendar listOccurrences={() => Promise.reject(new Error('Loading events failed (500)'))} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('500');
  });
});
