import type { DynoResult } from '../types';
import { fmt, sourceLabel } from '../format';

const Check = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
    <path d="M3 8.5l3 3 7-7" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const Alert = () => (
  <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
    <path d="M8 1.5l7 13H1z" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round" />
    <path d="M8 6v4M8 12.2v.3" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
  </svg>
);

const Info = () => (
  <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
    <circle cx="8" cy="8" r="6.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
    <path d="M8 7v4.5M8 4.6v.3" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
  </svg>
);

/** Why you can trust (or shouldn't trust) the number: the fuel check, the inputs, and anything odd in the log. */
export function ChecksPanel({ result }: { result: DynoResult }) {
  const { crossCheck: cc, gearing, environment, smoothing, vehicle, breakdown } = result;
  const pull = result.pulls[result.selectedPull];
  const total = breakdown.accelerationHp + breakdown.aeroHp + breakdown.rollingHp + Math.max(0, breakdown.gradeHp);

  return (
    <>
      {cc && (
        <section>
          <h2>Fuel check</h2>
          <div className={`verdict ${cc.agrees ? 'ok' : 'bad'}`}>
            {cc.agrees ? <Check /> : <Alert />}
            {cc.agrees ? 'The fuel burned backs up this result' : 'The fuel burned doesn’t match this result'}
          </div>
          <p>
            {fmt.whole(cc.mafGs)} g/s of air at {cc.afr.toFixed(1)} AFR{cc.afrAssumed ? ' (assumed)' : ''} is{' '}
            {fmt.whole(cc.fuelLbPerHr)} lb/hr of fuel, enough for {fmt.whole(cc.estimatedWhpLow)}–{fmt.whole(cc.estimatedWhpHigh)} whp.
            The pull measured {fmt.one(result.peaks.uncorrectedWhp)} whp uncorrected.
          </p>
        </section>
      )}

      <section>
        <h2>How it was measured</h2>
        <dl className="facts">
          <div>
            <dt>Car</dt>
            <dd>
              {vehicle.name} <small>{vehicle.autoDetected ? 'identified from the log' : vehicle.key ? 'you picked' : ''}</small>
            </dd>
          </div>
          <div>
            <dt>Weight</dt>
            <dd>
              {fmt.whole(vehicle.weightLb)} lb <small>{sourceLabel(vehicle.weightSource)}</small>
            </dd>
          </div>
          <div>
            <dt>Gearing</dt>
            <dd>
              {gearing.detectedGear ? `${fmt.ordinal(gearing.detectedGear)} gear, ` : ''}
              {gearing.rpmPerMph.toFixed(1)} rpm per mph <small>{sourceLabel(gearing.source)}</small>
            </dd>
          </div>
          {pull && (
            <div>
              <dt>Pull</dt>
              <dd>
                {fmt.rpm(pull.startRpm)} to {fmt.rpm(pull.endRpm)} rpm in {pull.duration.toFixed(1)} s
              </dd>
            </div>
          )}
          <div>
            <dt>Logging</dt>
            <dd>{fmt.whole(smoothing.sampleRateHz)} samples per second</dd>
          </div>
          <div>
            <dt>Air</dt>
            <dd>
              {environment.baroInHg.toFixed(2)} inHg, {fmt.whole(environment.ambientTempF)}°F{' '}
              <small>temperature {sourceLabel(environment.ambientSource)}</small>
            </dd>
          </div>
          <div>
            <dt>Smoothing</dt>
            <dd>{smoothing.bias === 0 ? 'Automatic' : smoothing.bias < 0 ? 'Less than automatic' : 'More than automatic'}</dd>
          </div>
        </dl>
      </section>

      {result.warnings.length > 0 && (
        <section>
          <h2>Notes on this log</h2>
          <ul className="notes">
            {result.warnings.map(w => (
              <li key={w.code} className={w.severity === 'Warning' ? 'warning' : 'info'}>
                {w.severity === 'Warning' ? <Alert /> : <Info />}
                <span>{w.message}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {total > 0 && (
        <section>
          <h2>Where the power went</h2>
          <p>At peak power, this is what the car was pushing against.</p>
          <div className="breakdown-bar" role="img" aria-label="Power split at peak">
            <span style={{ width: `${(breakdown.accelerationHp / total) * 100}%`, background: 'var(--ink)' }} />
            <span style={{ width: `${(breakdown.aeroHp / total) * 100}%`, background: '#1f5fbf' }} />
            <span style={{ width: `${(breakdown.rollingHp / total) * 100}%`, background: '#8aa4bb' }} />
            {breakdown.gradeHp > 0 && <span style={{ width: `${(breakdown.gradeHp / total) * 100}%`, background: '#c77b00' }} />}
          </div>
          <div className="breakdown-keys">
            <span><i style={{ background: 'var(--ink)' }} />Accelerating the car {fmt.whole(breakdown.accelerationHp)} hp</span>
            <span><i style={{ background: '#1f5fbf' }} />Air drag {fmt.whole(breakdown.aeroHp)} hp</span>
            <span><i style={{ background: '#8aa4bb' }} />Tires {fmt.whole(breakdown.rollingHp)} hp</span>
            {breakdown.gradeHp > 0 && <span><i style={{ background: '#c77b00' }} />Hill {fmt.whole(breakdown.gradeHp)} hp</span>}
          </div>
        </section>
      )}
    </>
  );
}
