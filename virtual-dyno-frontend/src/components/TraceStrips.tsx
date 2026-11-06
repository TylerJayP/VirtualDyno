import { CartesianGrid, Line, LineChart, ReferenceDot, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { Live } from '../replay';
import type { Row } from '../series';
import { fmt } from '../format';
import { rpmTicks, Y_AXIS_WIDTH } from './DynoChart';

interface Props {
  /** What to draw (masked during a replay). */
  rows: Row[];
  /** The complete rows: which strips exist and their scales are fixed from these. */
  fullRows: Row[];
  minRpm: number;
  maxRpm: number;
  live: Live | null;
}

const KNOCK_THRESHOLD = 0.2;

interface Strip {
  key: 'boost' | 'afr' | 'spark';
  title: string;
  unit: string;
  color: string;
  domain: [number, number];
  liveValue: number | null;
}

/** A fixed scale covering the whole pull, rounded out to whole steps. */
function niceDomain(rows: Row[], key: string): [number, number] {
  const values = rows.map(r => r[key]).filter((v): v is number => v != null);
  if (values.length === 0) return [0, 1];
  const lo = Math.min(...values);
  const hi = Math.max(...values);
  const step = hi - lo > 20 ? 5 : 2;
  return [Math.floor(lo / step) * step, Math.max(Math.ceil(hi / step) * step, Math.floor(lo / step) * step + step)];
}

/**
 * Boost, AFR and timing on the same RPM axis as the dyno curve, the way data-acquisition software
 * stacks channels. Hovering the dyno chart moves the cursor here too, and during a replay they draw
 * in step with it.
 */
export function TraceStrips({ rows, fullRows, minRpm, maxRpm, live }: Props) {
  const has = (key: string) => fullRows.some(r => r[key] != null);
  const xMin = Math.floor(minRpm / 500) * 500;
  const xMax = Math.ceil(maxRpm / 500) * 500;
  const anyKnock = fullRows.some(r => (r.knock ?? 0) > KNOCK_THRESHOLD);
  // Same rows (so the cursor stays in sync), plus a marker wherever timing was pulled for knock.
  const sparkRows = rows.map(r => ({ ...r, knockMark: (r.knock ?? 0) > KNOCK_THRESHOLD ? r.spark : null }));

  const strips: Strip[] = [];
  if (has('boost')) strips.push({ key: 'boost', title: 'Boost', unit: 'psi', color: '#15233a', domain: niceDomain(fullRows, 'boost'), liveValue: live?.boostPsi ?? null });
  if (has('afr')) strips.push({ key: 'afr', title: 'Air/fuel ratio', unit: 'AFR', color: '#12856f', domain: [10, 14], liveValue: live?.afr ?? null });
  if (has('spark')) strips.push({ key: 'spark', title: 'Ignition timing', unit: '°', color: '#c77b00', domain: niceDomain(fullRows, 'spark'), liveValue: live?.sparkAdvance ?? null });

  if (strips.length === 0) return null;

  return (
    <div className="strips">
      {strips.map(s => {
        const knocking = s.key === 'spark' && (live?.knockRetard ?? 0) > KNOCK_THRESHOLD;
        return (
          <div key={s.key}>
            <div className="strip-title">
              {s.title} ({s.unit}){s.key === 'spark' && anyKnock ? ', red marks are knock retard' : ''}
            </div>
            <ResponsiveContainer width="100%" height={92}>
              <LineChart data={s.key === 'spark' ? sparkRows : rows} syncId="dyno" margin={{ top: 6, right: 16, bottom: 0, left: 0 }}>
                <CartesianGrid stroke="var(--grid)" vertical horizontal={false} />
                <XAxis dataKey="rpm" type="number" domain={[xMin, xMax]} ticks={rpmTicks(xMin, xMax)} hide />
                <YAxis
                  width={Y_AXIS_WIDTH}
                  domain={s.domain}
                  allowDataOverflow
                  tickCount={3}
                  tick={{ fill: 'var(--ink-3)', fontSize: 12 }}
                  stroke="var(--rule)"
                  tickFormatter={(v: number) => fmt.one(v)}
                />
                <Line dataKey={s.key} stroke={s.color} strokeWidth={1.75} dot={false} isAnimationActive={false} connectNulls={false} />
                {s.key === 'spark' && (
                  <Line dataKey="knockMark" stroke="none" dot={{ r: 3, fill: '#d7263d', stroke: 'none' }} activeDot={false} isAnimationActive={false} legendType="none" />
                )}
                {live && <ReferenceLine x={live.rpm} stroke="var(--ink-3)" strokeDasharray="3 3" />}
                {live && s.liveValue != null && (
                  <ReferenceDot
                    x={live.rpm}
                    y={Math.min(Math.max(s.liveValue, s.domain[0]), s.domain[1])}
                    r={knocking ? 4.5 : 3.5}
                    fill={knocking ? '#d7263d' : s.color}
                    stroke="#fff"
                    strokeWidth={1.5}
                  />
                )}
                {!live && (
                  <Tooltip content={<StripTooltip unit={s.unit} name={s.title} dataKey={s.key} />} cursor={{ stroke: 'var(--ink-3)', strokeDasharray: '3 3' }} isAnimationActive={false} />
                )}
              </LineChart>
            </ResponsiveContainer>
          </div>
        );
      })}
    </div>
  );
}

interface StripTooltipProps {
  unit: string;
  name: string;
  dataKey: string;
  active?: boolean;
  payload?: { payload: Row }[];
}

function StripTooltip({ unit, name, dataKey, active, payload }: StripTooltipProps) {
  const row = payload?.[0]?.payload;
  const value = row?.[dataKey];
  if (!active || !row || value == null) return null;
  const knock = dataKey === 'spark' && (row.knock ?? 0) > KNOCK_THRESHOLD ? `, knock retard ${fmt.one(row.knock as number)}°` : '';
  return (
    <div className="tooltip">
      {name}: <strong>{fmt.one(value)}</strong> {unit}
      {knock} at {fmt.whole(row.rpm)} rpm
    </div>
  );
}
