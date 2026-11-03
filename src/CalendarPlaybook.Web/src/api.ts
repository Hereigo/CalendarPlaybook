export type EventKind = 'Single' | 'AllDay' | 'Recurring';
export type RuleType = 'EveryDays' | 'MonthDay' | 'WeekDay' | 'YearDay';
export type WeekDayName = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';

export type Occurrence = {
  eventId: string;
  kind: EventKind;
  title: string;
  occurrenceStartUtc: string | null;
  occurrenceEndUtc: string | null;
  /** Europe/Helsinki local dates, yyyy-MM-dd, endDate inclusive. */
  startDate: string;
  endDate: string;
};

export type Recurrence = {
  ruleType: RuleType;
  interval?: number | null;
  dayOfMonth?: number | null;
  month?: number | null;
  dayOfWeek?: WeekDayName | null;
  untilDate: string;
};

/** Body of POST and PUT /api/events. Local Europe/Helsinki dates (yyyy-MM-dd) and times (HH:mm:ss). */
export type EventInput = {
  title: string;
  description: string | null;
  kind: EventKind;
  startDate?: string;
  startTime?: string;
  endDate?: string;
  endTime?: string;
  recurrence?: Recurrence;
};

export type EventDetails = EventInput & { id: string };

/** Problem-details failure. `fields` holds the 400 validation messages by JSON field path. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly fields: Record<string, string[]> = {},
  ) {
    super(message);
  }
}

export type CalendarApi = {
  listOccurrences: (from: string, to: string) => Promise<Occurrence[]>;
  getEvent: (id: string) => Promise<EventDetails>;
  createEvent: (input: EventInput) => Promise<EventDetails>;
  updateEvent: (id: string, input: EventInput) => Promise<EventDetails>;
  deleteEvent: (id: string) => Promise<void>;
};

export function createApi(baseUrl: string, getToken: () => Promise<string>): CalendarApi {
  async function send(path: string, init: RequestInit = {}): Promise<Response> {
    const token = await getToken();
    const res = await fetch(`${baseUrl}/api/events${path}`, {
      ...init,
      headers: {
        Authorization: `Bearer ${token}`,
        ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      },
    });
    if (res.ok) return res;
    let fields: Record<string, string[]> = {};
    try {
      fields = ((await res.json()) as { errors?: Record<string, string[]> }).errors ?? {};
    } catch {
      // no problem-details body
    }
    throw new ApiError(
      res.status === 404 ? 'The event no longer exists.' : `Request failed (${res.status})`,
      res.status,
      fields,
    );
  }

  const json = async <T>(res: Response) => (await res.json()) as T;
  const body = (input: EventInput) => JSON.stringify(input);

  return {
    listOccurrences: async (from, to) => json(await send(`?from=${from}&to=${to}`)),
    getEvent: async (id) => json(await send(`/${id}`)),
    createEvent: async (input) => json(await send('', { method: 'POST', body: body(input) })),
    updateEvent: async (id, input) => json(await send(`/${id}`, { method: 'PUT', body: body(input) })),
    deleteEvent: async (id) => {
      await send(`/${id}`, { method: 'DELETE' });
    },
  };
}
