import type { DynoPoint } from './types';

/** Colors for overlaid saved runs. The open run is always dyno-sheet red. */
export const OVERLAY_COLORS = ['#1f5fbf', '#12856f', '#c77b00', '#6b47b8', '#3f6f8f'];
export const CURRENT_COLOR = '#d7263d';

export interface Series {
  id: string;           // 'cur' for the open run, otherwise the saved run id
  name: string;
  color: string;
  points: DynoPoint[];
}

export interface Row {
  rpm: number;
  [key: string]: number | null;
}

const STEP = 20;

/**
 * Put every run on one shared RPM grid so the chart, the trace strips and the tooltip all line up
 * by index. Each series only has values inside the RPM range it actually covered.
 */
export function buildRows(series: Series[], traces: DynoPoint[] | null): { rows: Row[]; minRpm: number; maxRpm: number } {
  const all = series.flatMap(s => s.points.map(p => p.rpm));
  if (all.length === 0) return { rows: [], minRpm: 0, maxRpm: 0 };

  const lo = Math.floor(Math.min(...all) / STEP) * STEP;
  const hi = Math.ceil(Math.max(...all) / STEP) * STEP;

  const sorted = series.map(s => ({ s, pts: [...s.points].sort((a, b) => a.rpm - b.rpm) }));
  const traceSorted = traces ? [...traces].sort((a, b) => a.rpm - b.rpm) : null;

  const rows: Row[] = [];
  for (let rpm = lo; rpm <= hi; rpm += STEP) {
    const row: Row = { rpm };
    for (const { s, pts } of sorted) {
      row[`p_${s.id}`] = interpolate(pts, rpm, p => p.whp);
      row[`t_${s.id}`] = interpolate(pts, rpm, p => p.wtq);
    }
    if (traceSorted) {
      row.boost = interpolate(traceSorted, rpm, p => p.boostPsi);
      row.afr = interpolate(traceSorted, rpm, p => p.afr);
      row.spark = interpolate(traceSorted, rpm, p => p.sparkAdvance);
      row.knock = interpolate(traceSorted, rpm, p => p.knockRetard);
    }
    rows.push(row);
  }
  return { rows, minRpm: lo, maxRpm: hi };
}

function interpolate(points: DynoPoint[], rpm: number, value: (p: DynoPoint) => number | null): number | null {
  if (points.length === 0 || rpm < points[0].rpm || rpm > points[points.length - 1].rpm) return null;
  let i = 1;
  while (i < points.length && points[i].rpm < rpm) i++;
  if (i >= points.length) return value(points[points.length - 1]);
  const a = points[i - 1];
  const b = points[i];
  const va = value(a);
  const vb = value(b);
  if (va === null || vb === null) return va ?? vb;
  if (b.rpm === a.rpm) return va;
  return va + ((vb - va) * (rpm - a.rpm)) / (b.rpm - a.rpm);
}

export function niceMax(value: number, step: number): number {
  return Math.max(step, Math.ceil(value / step) * step);
}
