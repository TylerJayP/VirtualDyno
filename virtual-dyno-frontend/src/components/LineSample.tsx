/** A short line drawn in a given color and dash: used in the legend, tooltip and runs list. */
export function LineSample({ color = 'var(--ink)', dash, width = 2.5, length = 26 }: {
  color?: string;
  dash?: string;
  width?: number;
  length?: number;
}) {
  return (
    <svg width={length} height="8" aria-hidden="true" style={{ flex: 'none' }}>
      <line x1="1" y1="4" x2={length - 1} y2="4" stroke={color} strokeWidth={width} strokeDasharray={dash} strokeLinecap="butt" />
    </svg>
  );
}
