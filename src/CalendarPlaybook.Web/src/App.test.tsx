import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { CalendarApi } from './api';
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

const fakeApi = (over: Partial<CalendarApi> = {}): CalendarApi => ({
  listOccurrences: vi.fn().mockResolvedValue([]),
  getEvent: vi.fn(),
  createEvent: vi.fn().mockResolvedValue({}),
  updateEvent: vi.fn().mockResolvedValue({}),
  deleteEvent: vi.fn().mockResolvedValue(undefined),
  ...over,
});

describe('Calendar', () => {
  it('navigates between months and reloads the range', async () => {
    const list = vi.fn().mockResolvedValue([]);
    render(<Calendar api={fakeApi({ listOccurrences: list })} />);
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
    render(<Calendar api={fakeApi({ listOccurrences: list })} />);
    await waitFor(() => expect(screen.getAllByText('Trip')).toHaveLength(3));
  });

  it('shows an error when loading fails', async () => {
    render(<Calendar api={fakeApi({ listOccurrences: () => Promise.reject(new Error('Loading events failed (500)')) })} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('500');
  });

  it('creates an event from a day click', async () => {
    const api = fakeApi();
    render(<Calendar api={api} />);
    fireEvent.click(screen.getAllByRole('gridcell')[10]);
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Dentist' } });
    fireEvent.click(screen.getByText('Save'));
    await waitFor(() => expect(api.createEvent).toHaveBeenCalledTimes(1));
    const body = vi.mocked(api.createEvent).mock.calls[0][0];
    expect(body).toMatchObject({ title: 'Dentist', kind: 'Single', startTime: '09:00:00', endTime: '10:00:00' });
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(api.listOccurrences).toHaveBeenCalledTimes(2);
  });

  it('shows field errors from the API and keeps the form open', async () => {
    const { ApiError } = await import('./api');
    const api = fakeApi({
      createEvent: vi.fn().mockRejectedValue(new ApiError('bad', 400, { title: ['Title is required.'] })),
    });
    render(<Calendar api={api} />);
    fireEvent.click(screen.getAllByRole('gridcell')[10]);
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'x' } });
    fireEvent.click(screen.getByText('Save'));
    expect(await screen.findByText('Title is required.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('opens an existing recurring event, warns about the series and confirms the series delete', async () => {
    const now = new Date();
    const date = toIsoDate(new Date(now.getFullYear(), now.getMonth(), 10));
    const api = fakeApi({
      listOccurrences: vi.fn().mockResolvedValue([
        { eventId: 'e1', kind: 'Recurring', title: 'Gym', occurrenceStartUtc: null, occurrenceEndUtc: null, startDate: date, endDate: date },
      ]),
      getEvent: vi.fn().mockResolvedValue({
        id: 'e1', title: 'Gym', description: null, kind: 'Recurring', startDate: date,
        startTime: '07:00:00', endTime: '08:00:00',
        recurrence: { ruleType: 'WeekDay', dayOfWeek: 'Friday', untilDate: date },
      }),
    });
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    render(<Calendar api={api} />);
    fireEvent.click(await screen.findByText('Gym'));
    expect(await screen.findByText(/apply to the whole series/)).toBeInTheDocument();
    expect(screen.getByLabelText('Weekday')).toHaveValue('Friday');
    fireEvent.click(screen.getByText('Delete'));
    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('whole series'));
    await waitFor(() => expect(api.deleteEvent).toHaveBeenCalledWith('e1'));
    confirm.mockRestore();
  });
});
