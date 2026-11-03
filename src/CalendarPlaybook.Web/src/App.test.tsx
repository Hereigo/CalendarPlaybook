import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import App from './App';
import { buildMonthGrid } from './monthGrid';

describe('buildMonthGrid', () => {
  it('starts on Monday and covers 42 days', () => {
    const grid = buildMonthGrid(2026, 9, new Date(2026, 9, 28)); // October 2026
    expect(grid).toHaveLength(42);
    expect(grid[0].date.getDay()).toBe(1);
    expect(grid[0].day).toBe(28); // Mon 28 Sep
    expect(grid[0].outside).toBe(true);
    expect(grid.filter((d) => d.today).map((d) => d.day)).toEqual([28]);
  });
});

describe('App', () => {
  it('navigates between months', () => {
    render(<App />);
    const label = screen.getByText(/\d{4}$/);
    const before = label.textContent;
    fireEvent.click(screen.getByLabelText('Next month'));
    expect(label.textContent).not.toBe(before);
    fireEvent.click(screen.getByLabelText('Previous month'));
    expect(label.textContent).toBe(before);
  });
});
