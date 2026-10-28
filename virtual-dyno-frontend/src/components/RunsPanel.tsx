import type { RunSummary } from '../types';
import { fmt } from '../format';

interface Props {
  runs: RunSummary[];
  overlayIds: number[];
  colors: Record<number, string>;
  openRunId: number | null;
  currentGear: number | null;
  onToggleOverlay: (id: number) => void;
  onOpen: (id: number) => void;
  onDelete: (run: RunSummary) => void;
}

export function RunsPanel({ runs, overlayIds, colors, openRunId, currentGear, onToggleOverlay, onOpen, onDelete }: Props) {
  return (
    <section>
      <h2>Saved runs</h2>
      {runs.length === 0 ? (
        <p className="empty-note">Save a run to compare it against later pulls, like before and after a tune.</p>
      ) : (
        <ul className="runs">
          {runs.map(run => {
            const on = overlayIds.includes(run.id);
            const gearMismatch = on && currentGear && run.gear && run.gear !== currentGear;
            return (
              <li key={run.id} className={run.id === openRunId ? 'open' : undefined}>
                <div className="top">
                  <span className="swatch" style={on ? { background: colors[run.id], borderColor: colors[run.id] } : { visibility: 'hidden' }} />
                  <span className="name" title={run.name}>{run.name}</span>
                  {run.id !== openRunId && (
                    <label className="checkbox">
                      <input type="checkbox" checked={on} onChange={() => onToggleOverlay(run.id)} />
                      Compare
                    </label>
                  )}
                </div>
                <div className="nums">
                  {fmt.one(run.peakWhp)} whp, {fmt.one(run.peakWtq)} lb-ft
                </div>
                <div className="meta">
                  {fmt.date(run.createdAtUtc)}, {run.gear ? `${fmt.ordinal(run.gear)} gear, ` : ''}
                  {fmt.whole(run.weightLb)} lb
                </div>
                {gearMismatch ? (
                  <div className="meta" style={{ color: 'var(--warn)' }}>
                    Different gear from the open run, so the curves won’t line up exactly.
                  </div>
                ) : null}
                <div className="actions">
                  {run.id !== openRunId && <button onClick={() => onOpen(run.id)}>Open</button>}
                  <a href={`/api/runs/${run.id}/log`}>Download log</a>
                  <button className="danger" onClick={() => onDelete(run)}>
                    Delete
                  </button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
