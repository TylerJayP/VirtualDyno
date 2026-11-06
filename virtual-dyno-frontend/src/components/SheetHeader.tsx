import type { Live } from '../replay';
import type { DynoResult } from '../types';
import { fmt } from '../format';

/**
 * The top of the dyno sheet. Normally: the three numbers people screenshot, then what they were
 * measured on. During a replay: live readings, then settles on the peaks when the pull ends.
 */
export function SheetHeader({ result, live }: { result: DynoResult; live: Live | null }) {
  const { peaks, vehicle, gearing, environment } = result;
  const corrected = environment.correction !== 'Uncorrected';

  const whp = live ? live.whp : peaks.whp;
  const wtq = live ? live.wtq : peaks.wtq;
  const boost = live ? live.boostPsi : peaks.boostPsi;
  const knocking = (live?.knockRetard ?? 0) > 0.2;

  return (
    <>
      <div className={`readouts${live ? ' live' : ''}`}>
        <div className="readout">
          <span className="value power">{fmt.one(whp)}</span>
          <span className="unit">whp</span>
          <span className="at">at {fmt.rpm(live ? live.rpm : peaks.whpRpm)} rpm</span>
        </div>
        <div className="readout">
          <span className="value torque">{fmt.one(wtq)}</span>
          <span className="unit">lb-ft</span>
          <span className="at">at {fmt.rpm(live ? live.rpm : peaks.wtqRpm)} rpm</span>
        </div>
        {boost != null && (
          <div className="readout">
            <span className="value">{fmt.one(boost)}</span>
            <span className="unit">psi</span>
            <span className="at">{live ? 'boost' : 'peak boost'}</span>
          </div>
        )}
      </div>

      {live ? (
        <dl className="specs">
          <div>
            <dt>Pull</dt>
            <dd>
              {live.t.toFixed(1)} of {live.duration.toFixed(1)} s
            </dd>
          </div>
          <div>
            <dt>Speed</dt>
            <dd>{fmt.whole(live.mph)} mph</dd>
          </div>
          {live.afr != null && (
            <div>
              <dt>AFR</dt>
              <dd>{live.afr.toFixed(1)}</dd>
            </div>
          )}
          {live.sparkAdvance != null && (
            <div>
              <dt>Timing</dt>
              <dd>{live.sparkAdvance.toFixed(1)}°</dd>
            </div>
          )}
          {live.knockRetard != null && (
            <div>
              <dt>Knock</dt>
              <dd className={knocking ? 'knock-on' : undefined}>{live.knockRetard.toFixed(1)}°</dd>
            </div>
          )}
        </dl>
      ) : (
        <dl className="specs">
          <div>
            <dt>Car</dt>
            <dd>{vehicle.name}</dd>
          </div>
          <div>
            <dt>Gear</dt>
            <dd>{gearing.detectedGear ? fmt.ordinal(gearing.detectedGear) : 'Not identified'}</dd>
          </div>
          <div>
            <dt>Weight</dt>
            <dd>{fmt.whole(vehicle.weightLb)} lb</dd>
          </div>
          <div>
            <dt>Correction</dt>
            <dd>
              {fmt.correction(environment.correction)}
              {corrected && ` (×${environment.correctionFactor.toFixed(3)})`}
            </dd>
          </div>
          {corrected && (
            <div>
              <dt>Uncorrected</dt>
              <dd>
                {fmt.one(peaks.uncorrectedWhp)} whp, {fmt.one(peaks.uncorrectedWtq)} lb-ft
              </dd>
            </div>
          )}
        </dl>
      )}
    </>
  );
}
