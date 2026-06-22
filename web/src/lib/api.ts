import type {
  SearchResponse,
  RunListItem,
  ChangesResponse,
  FirmProjection,
  FirmDetail,
} from './types';

export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;
  constructor(status: number, body: unknown) {
    super(`HTTP ${status}`);
    this.status = status;
    this.body = body;
  }
}

async function safeBody(res: Response): Promise<unknown> {
  try {
    return await res.json();
  } catch {
    return await res.text();
  }
}

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new ApiError(res.status, await safeBody(res));
  return res.json() as Promise<T>;
}

export const api = {
  getLocations: () => getJson<string[]>('/api/locations'),

  runSearch: async (locations: string[]): Promise<SearchResponse> => {
    const res = await fetch('/api/searches', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ locations }),
    });
    if (!res.ok) throw new ApiError(res.status, await safeBody(res));
    return res.json();
  },

  listRuns: () => getJson<RunListItem[]>('/api/searches'),
  getRun: (id: string) => getJson<SearchResponse>(`/api/searches/${id}`),
  getChanges: (id: string) => getJson<ChangesResponse>(`/api/searches/${id}/changes`),

  getFirms: (status?: string, addedSince?: string) => {
    const q = new URLSearchParams();
    if (status) q.set('status', status);
    if (addedSince) q.set('addedSince', addedSince);
    const qs = q.toString();
    return getJson<FirmProjection[]>(`/api/firms${qs ? '?' + qs : ''}`);
  },

  getFirm: (id: string) => getJson<FirmDetail>(`/api/firms/${id}`),
};
