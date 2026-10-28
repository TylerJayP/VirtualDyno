import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { Row } from '../series';
import { fmt } from '../format';
import { rpmTicks, Y_AXIS_WIDTH } from './DynoChart';

interface Props {
  rows: Row[];
  minRpm: number;
  maxRpm: number;
}

/**
 * Boost, AFR and timing on the same RPM axis as the dyno curve, the way data-acquisition software
 * stacks channels. Hovering the dyno chart moves the cursor here too.
 */
export function TraceStrips({ rows, minRpm, maxRpm }: Props) {
  const has = (key: string) => rows.some(r => r[key] != null);
  const xMin = Math.floor(minRpm / 500) * 500;
  const xMax = Math.ceil(maxRpm / 500) * 500;
  const knockRows = rows.filter(r => (r.knock ?? 0) > 0.2);
  // Same rows (so the cursor stays in sync), plus a marker wherever timing was pulled for knock.
  const sparkRows = rows.map(r => ({ ...r, knockMark: (r.knock ?? 0) > 0.2 ? r.spark : null }));

  const strips: { key: string; title: string; unit: string; color: string; domain?: [number, number] }[] = [];
  if (has('boost')) strips.push({ key: 'boost', title: 'Boost', unit: 'psi', color: '#15233a' });
  if (has('afr')) strips.push({ key: 'afr', title: 'Air/fuel ratio', unit: 'AFR', color: '#12856f', domain: [10, 14] });
  if (has('spark')) strips.push({ key: 'spark', title: 'Ignition timing', unit: '°', color: '#c77b00' });

  if (strips.length === 0) return null;

  return (
    <div className="strips">
      {strips.map(s => (
        <div key={s.key}>
          <div className="strip-title">
            {s.title} ({s.unit}){s.key === 'spark' && knockRows.length > 0 ? ', red marks are knock retard' : ''}
          </div>
          <ResponsiveContainer width="100%" height={92}>
            <LineChart data={s.key === 'spark' ? sparkRows : rows} syncId="dyno" margin={{ top: 6, right: 16, bottom: 0, left: 0 }}>
              <CartesianGrid stroke="var(--grid)" vertical horizontal={false} />
              <XAxis dataKey="rpm" type="number" domain={[xMin, xMax]} ticks={rpmTicks(xMin, xMax)} hide />
              <YAxis
                width={Y_AXIS_WIDTH}
                domain={s.domain ?? ['auto', 'auto']}
                allowDataOverflow={!!s.domain}
                tickCount={3}
                tick={{ fill: 'var(--ink-3)', fontSize: 12 }}
                stroke="var(--rule)"
                tickFormatter={(v: number) => fmt.one(v)}
              />
              <Line dataKey={s.key} stroke={s.color} strokeWidth={1.75} dot={false} isAnimationActive={false} connectNulls={false} />
              {s.key === 'spark' && (
                <Line dataKey="knockMark" stroke="none" dot={{ r: 3, fill: '#d7263d', stroke: 'none' }} activeDot={false} isAnimationActive={false} legendType="none" />
              )}
              <Tooltip content={<StripTooltip unit={s.unit} name={s.title} dataKey={s.key} />} cursor={{ stroke: 'var(--ink-3)', strokeDasharray: '3 3' }} isAnimationActive={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      ))}
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
  const knock = dataKey === 'spark' && (row.knock ?? 0) > 0.2 ? `, knock retard ${fmt.one(row.knock as number)}°` : '';
  return (
    <div className="tooltip">
      {name}: <strong>{fmt.one(value)}</strong> {unit}
      {knock} at {fmt.whole(row.rpm)} rpm
    </div>
  );
}
