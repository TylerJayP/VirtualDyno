import { useCallback, useEffect, useRef, useState } from 'react';
import type { Row } from './series';
import type { DynoPoint } from './types';

/** Every channel at one instant of the pull, interpolated between logged samples. */
export interface Live {
  t: number;
  duration: number;
  /** Highest RPM reached so far. Everything up to here has been "drawn". */
  reachedRpm: number;
  rpm: number;
  whp: number;
  wtq: number;
  mph: number;
  boostPsi: number | null;
  afr: number | null;
  sparkAdvance: number | null;
  knockRetard: number | null;
}

export function liveAt(points: DynoPoint[], t: number): Live | null {
  if (points.length === 0) return null;
  const duration = points[points.length - 1].timeSec;

  let i = 1;
  let reached = points[0].rpm;
  while (i < points.length && points[i].timeSec < t) {
    reached = Math.max(reached, points[i].rpm);
    i++;
  }
  if (i >= points.length) i = points.length - 1;
  const a = points[Math.max(0, i - 1)];
  const b = points[i];
  const f = b.timeSec > a.timeSec ? Math.min(1, Math.max(0, (t - a.timeSec) / (b.timeSec - a.timeSec))) : 1;
  const mix = (x: number | null, y: number | null) => (x == null || y == null ? (y ?? x) : x + (y - x) * f);

  const rpm = mix(a.rpm, b.rpm) as number;
  return {
    t,
    duration,
    reachedRpm: Math.max(reached, rpm),
    rpm,
    whp: mix(a.whp, b.whp) as number,
    wtq: mix(a.wtq, b.wtq) as number,
    mph: mix(a.mph, b.mph) as number,
    boostPsi: mix(a.boostPsi, b.boostPsi),
    afr: mix(a.afr, b.afr),
    sparkAdvance: mix(a.sparkAdvance, b.sparkAdvance),
    knockRetard: mix(a.knockRetard, b.knockRetard),
  };
}

const CURRENT_KEYS = ['p_cur', 't_cur', 'boost', 'afr', 'spark', 'knock'];

/** Hide the open run (and its traces) past the RPM it has reached. Overlaid saved runs stay fully drawn. */
export function maskRows(rows: Row[], uptoRpm: number): Row[] {
  return rows.map(r => {
    if (r.rpm <= uptoRpm) return r;
    const hidden: Row = { ...r };
    for (const k of CURRENT_KEYS) if (k in hidden) hidden[k] = null;
    return hidden;
  });
}

/**
 * Plays the pull back in real time: t runs from 0 to the pull's duration, then returns to null
 * (fully drawn). State updates are capped at ~30 per second; that's smooth for a chart and keeps
 * the four charts from re-rendering on every animation frame.
 */
export function useReplay(durationSec: number) {
  const [t, setT] = useState<number | null>(null);
  const frame = useRef(0);
  const duration = useRef(durationSec);

  useEffect(() => {
    duration.current = durationSec;
  }, [durationSec]);

  const stop = useCallback(() => {
    cancelAnimationFrame(frame.current);
    frame.current = 0;
    setT(null);
  }, []);

  const start = useCallback(() => {
    cancelAnimationFrame(frame.current);
    const startedAt = performance.now();
    let lastUpdate = -Infinity;
    const tick = (now: number) => {
      const elapsed = (now - startedAt) / 1000;
      if (elapsed >= duration.current) {
        frame.current = 0;
        setT(null);
        return;
      }
      if (now - lastUpdate >= 33) {
        lastUpdate = now;
        setT(elapsed);
      }
      frame.current = requestAnimationFrame(tick);
    };
    setT(0);
    frame.current = requestAnimationFrame(tick);
  }, []);

  useEffect(() => () => cancelAnimationFrame(frame.current), []);

  return { t, playing: t !== null, start, stop };
}

export const prefersReducedMotion = () =>
  typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
