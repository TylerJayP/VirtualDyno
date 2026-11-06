import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceArea,
  ReferenceDot,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { fmt } from '../format';
import type { Live } from '../replay';
import { niceMax, POWER_COLOR, TORQUE_COLOR, type Row, type Series } from '../series';
import { LineSample } from './LineSample';

interface Props {
  /** What to draw (masked during a replay). */
  rows: Row[];
  /** The complete rows, so the axes are fixed before the curve is drawn. */
  fullRows: Row[];
  series: Series[];
  minRpm: number;
  maxRpm: number;
  peaks: { whp: number; whpRpm: number; wtq: number; wtqRpm: number } | null;
  slip: { from: number; to: number } | null;
  live: Live | null;
}

export const Y_AXIS_WIDTH = 56;

export function rpmTicks(min: number, max: number): number[] {
  const ticks: number[] = [];
  for (let r = Math.ceil(min / 500) * 500; r <= max; r += 500) ticks.push(r);
  return ticks;
}

export function DynoChart({ rows, fullRows, series, minRpm, maxRpm, peaks, slip, live }: Props) {
  const top = niceMax(
    Math.max(0, ...fullRows.flatMap(r => series.flatMap(s => [r[`p_${s.id}`] ?? 0, r[`t_${s.id}`] ?? 0]))) * 1.06,
    50,
  );
  const xMin = Math.floor(minRpm / 500) * 500;
  const xMax = Math.ceil(maxRpm / 500) * 500;
  const yTicks = Array.from({ length: top / 50 + 1 }, (_, i) => i * 50);

  return (
    <ResponsiveContainer width="100%" height={430}>
      <LineChart data={rows} syncId="dyno" margin={{ top: 16, right: 16, bottom: 22, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" strokeWidth={1} />
        <XAxis
          dataKey="rpm"
          type="number"
          domain={[xMin, xMax]}
          ticks={rpmTicks(xMin, xMax)}
          tickFormatter={fmt.whole}
          tick={{ fill: 'var(--ink-2)', fontSize: 13 }}
          stroke="var(--rule)"
          label={{ value: 'Engine RPM', position: 'insideBottom', offset: -14, fill: 'var(--ink-3)', fontSize: 13 }}
        />
        <YAxis
          width={Y_AXIS_WIDTH}
          domain={[0, top]}
          ticks={yTicks}
          tick={{ fill: 'var(--ink-2)', fontSize: 13 }}
          stroke="var(--rule)"
          label={{ value: 'Wheel hp and lb-ft', angle: -90, position: 'insideLeft', offset: 12, dy: 50, fill: 'var(--ink-3)', fontSize: 13 }}
        />

        {slip && (
          <ReferenceArea
            x1={slip.from}
            x2={slip.to}
            fill="#c77b00"
            fillOpacity={0.09}
            stroke="none"
            label={{ value: 'Slip', position: 'insideTop', fill: '#a85700', fontSize: 12 }}
          />
        )}

        {/* Compared runs first, so the open run draws on top. */}
        {[...series].reverse().flatMap(s => [
          <Line
            key={`t_${s.id}`}
            dataKey={`t_${s.id}`}
            name={`${s.name} torque`}
            stroke={TORQUE_COLOR}
            strokeWidth={s.id === 'cur' ? 2.75 : 2}
            strokeDasharray={s.dash}
            strokeOpacity={s.id === 'cur' ? 1 : 0.7}
            dot={false}
            activeDot={{ r: 3 }}
            connectNulls={false}
            isAnimationActive={false}
          />,
          <Line
            key={`p_${s.id}`}
            dataKey={`p_${s.id}`}
            name={`${s.name} power`}
            stroke={POWER_COLOR}
            strokeWidth={s.id === 'cur' ? 3 : 2}
            strokeDasharray={s.dash}
            strokeOpacity={s.id === 'cur' ? 1 : 0.7}
            dot={false}
            activeDot={{ r: 4 }}
            connectNulls={false}
            isAnimationActive={false}
          />,
        ])}

        {peaks && (
          <>
            <ReferenceDot x={peaks.whpRpm} y={peaks.whp} r={5} fill={POWER_COLOR} stroke="#fff" strokeWidth={1.5} />
            <ReferenceDot x={peaks.wtqRpm} y={peaks.wtq} r={5} fill={TORQUE_COLOR} stroke="#fff" strokeWidth={1.5} />
          </>
        )}

        {live && (
          <>
            <ReferenceLine x={live.rpm} stroke="var(--ink-3)" strokeDasharray="3 3" />
            <ReferenceDot x={live.rpm} y={live.wtq} r={5} fill={TORQUE_COLOR} stroke="#fff" strokeWidth={1.5} />
            <ReferenceDot x={live.rpm} y={live.whp} r={5} fill={POWER_COLOR} stroke="#fff" strokeWidth={1.5} />
          </>
        )}

        {!live && (
          <Tooltip content={<ChartTooltip series={series} />} cursor={{ stroke: 'var(--ink-3)', strokeDasharray: '3 3' }} isAnimationActive={false} />
        )}
      </LineChart>
    </ResponsiveContainer>
  );
}

interface TooltipProps {
  series: Series[];
  active?: boolean;
  label?: number | string;
  payload?: { payload: Row }[];
}

function ChartTooltip({ series, active, payload }: TooltipProps) {
  const row = payload?.[0]?.payload;
  if (!active || !row) return null;
  const visible = series.filter(s => row[`p_${s.id}`] != null);
  if (visible.length === 0) return null;
  return (
    <div className="tooltip">
      <div className="rpm">{fmt.whole(row.rpm)} rpm</div>
      <table>
        <thead>
          <tr>
            <td />
            <td style={{ color: POWER_COLOR, fontWeight: 600 }}>whp</td>
            <td style={{ color: TORQUE_COLOR, fontWeight: 600 }}>lb-ft</td>
          </tr>
        </thead>
        <tbody>
          {visible.map(s => (
            <tr key={s.id}>
              <td>
                {visible.length > 1 && <LineSample dash={s.dash} width={2} length={20} />} {s.name}
              </td>
              <td>{fmt.one(row[`p_${s.id}`] as number)}</td>
              <td>{fmt.one(row[`t_${s.id}`] as number)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
