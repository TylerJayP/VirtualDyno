import { useEffect, useMemo, type ReactNode } from 'react';
import { liveAt, maskRows, prefersReducedMotion, useReplay } from '../replay';
import type { Row, Series } from '../series';
import type { DynoResult } from '../types';
import { DynoChart } from './DynoChart';
import { SheetHeader } from './SheetHeader';
import { TraceStrips } from './TraceStrips';

interface Props {
  result: DynoResult;
  series: Series[];
  rows: Row[];
  minRpm: number;
  maxRpm: number;
  busy: boolean;
  notice: ReactNode;
  /** Bumped by the app when a new log or run is opened: plays the pull back automatically. */
  replayToken: number;
}

/**
 * The dyno sheet. Owns the replay so its ~30 updates per second only re-render the sheet,
 * not the setup and checks panels around it.
 */
export function DynoSheet({ result, series, rows, minRpm, maxRpm, busy, notice, replayToken }: Props) {
  const duration = result.points.length ? result.points[result.points.length - 1].timeSec : 0;
  const replay = useReplay(duration);
  const { start } = replay;

  useEffect(() => {
    if (replayToken > 0 && !prefersReducedMotion()) start();
  }, [replayToken, start]);

  const live = replay.t !== null ? liveAt(result.points, replay.t) : null;
  const reached = live?.reachedRpm ?? null;
  const shownRows = useMemo(() => (reached === null ? rows : maskRows(rows, reached)), [rows, reached]);

  let slip =
    result.gearing.slipFromRpm != null && result.gearing.slipToRpm != null
      ? { from: result.gearing.slipFromRpm, to: result.gearing.slipToRpm }
      : null;
  if (slip && reached !== null) slip = reached > slip.from ? { from: slip.from, to: Math.min(slip.to, reached) } : null;

  return (
    <>
      <div className="sheet-top">
        <SheetHeader result={result} live={live} />
        {notice}
        <div className="status-line" aria-live="polite">
          {busy ? 'Updating…' : ''}
        </div>
      </div>
      <div className="chart-wrap">
        <DynoChart
          rows={shownRows}
          fullRows={rows}
          series={series}
          minRpm={minRpm}
          maxRpm={maxRpm}
          peaks={live ? null : result.peaks}
          slip={slip}
          live={live}
        />
      </div>
      <div className="legend">
        <span className="key">
          <svg width="26" height="8" aria-hidden="true">
            <line x1="1" y1="4" x2="25" y2="4" stroke="var(--ink)" strokeWidth="2.5" />
          </svg>
          Power
        </span>
        <span className="key">
          <svg width="26" height="8" aria-hidden="true">
            <line x1="1" y1="4" x2="25" y2="4" stroke="var(--ink)" strokeWidth="2" strokeDasharray="6 4" />
          </svg>
          Torque
        </span>
        {series.length > 1 &&
          series.map(s => (
            <span className="key" key={s.id}>
              <svg width="10" height="10" aria-hidden="true">
                <rect width="10" height="10" rx="2" fill={s.color} />
              </svg>
              {s.name}
            </span>
          ))}
        <button className="button quiet replay" onClick={replay.playing ? replay.stop : replay.start}>
          {replay.playing ? 'Skip to end' : 'Replay pull'}
        </button>
      </div>
      <TraceStrips rows={shownRows} fullRows={rows} minRpm={minRpm} maxRpm={maxRpm} live={live} />
    </>
  );
}
