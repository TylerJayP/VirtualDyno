import type { CorrectionMode, DynoResult, DynoSettings, RunDetail, RunSummary, Vehicle } from './types';

/** Error carrying the API's ProblemDetails message, ready to show the user. */
export class ApiError extends Error {
  constructor(public readonly status: number, public readonly title: string, message: string) {
    super(message);
  }
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, init);
  } catch (e) {
    if (init?.signal?.aborted) throw e;
    throw new ApiError(0, 'Can’t reach the API', 'Start it with "dotnet run --project VirtualDyno.API" and try again.');
  }
  if (!response.ok) {
    let title = `Request failed (${response.status})`;
    let detail = response.statusText;
    try {
      const problem = await response.json();
      title = problem.title ?? title;
      detail = problem.detail ?? detail;
    } catch {
      /* not JSON */
    }
    throw new ApiError(response.status, title, detail);
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

function upload(file: Blob, fileName: string, settings: DynoSettings, extra: Record<string, string> = {}): FormData {
  const form = new FormData();
  form.append('file', file, fileName);
  form.append('settings', JSON.stringify(settings));
  for (const [k, v] of Object.entries(extra)) form.append(k, v);
  return form;
}

export const api = {
  vehicles: () => request<Vehicle[]>('/api/vehicles'),

  analyze: (file: Blob, fileName: string, settings: DynoSettings, signal?: AbortSignal) =>
    request<DynoResult>('/api/analyze', { method: 'POST', body: upload(file, fileName, settings), signal }),

  runs: () => request<RunSummary[]>('/api/runs'),

  run: (id: number, view?: { correction?: CorrectionMode; smoothing?: number }) => {
    const q = new URLSearchParams();
    if (view?.correction) q.set('correction', view.correction);
    if (view?.smoothing !== undefined) q.set('smoothing', String(view.smoothing));
    return request<RunDetail>(`/api/runs/${id}?${q}`);
  },

  saveRun: (file: Blob, fileName: string, settings: DynoSettings, name: string, notes: string) =>
    request<RunDetail>('/api/runs', { method: 'POST', body: upload(file, fileName, settings, { name, notes }) }),

  updateRun: (id: number, body: { name?: string; notes?: string; settings?: DynoSettings }) =>
    request<RunDetail>(`/api/runs/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),

  deleteRun: (id: number) => request<void>(`/api/runs/${id}`, { method: 'DELETE' }),

  runLog: async (id: number): Promise<Blob> => {
    const response = await fetch(`/api/runs/${id}/log`);
    if (!response.ok) throw new ApiError(response.status, 'Log not found', 'This run’s log could not be loaded.');
    return response.blob();
  },
};
