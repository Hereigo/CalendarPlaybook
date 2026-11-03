export type Occurrence = {
  eventId: string;
  kind: 'Single' | 'AllDay' | 'Recurring';
  title: string;
  occurrenceStartUtc: string | null;
  occurrenceEndUtc: string | null;
  /** Europe/Helsinki local dates, yyyy-MM-dd, endDate inclusive. */
  startDate: string;
  endDate: string;
};

export type ListOccurrences = (from: string, to: string) => Promise<Occurrence[]>;

export function createListOccurrences(baseUrl: string, getToken: () => Promise<string>): ListOccurrences {
  return async (from, to) => {
    const token = await getToken();
    const res = await fetch(`${baseUrl}/api/events?from=${from}&to=${to}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) throw new Error(`Loading events failed (${res.status})`);
    return (await res.json()) as Occurrence[];
  };
}
