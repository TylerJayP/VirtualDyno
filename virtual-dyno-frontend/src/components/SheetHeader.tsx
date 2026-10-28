import type { DynoResult } from '../types';
import { fmt } from '../format';

/** The top of the dyno sheet: the three numbers people screenshot, then what they were measured on. */
export function SheetHeader({ result }: { result: DynoResult }) {
  const { peaks, vehicle, gearing, environment } = result;
  const corrected = environment.correction !== 'Uncorrected';

  return (
    <>
      <div className="readouts">
        <div className="readout">
          <span className="value power">{fmt.one(peaks.whp)}</span>
          <span className="unit">whp</span>
          <span className="at">at {fmt.rpm(peaks.whpRpm)} rpm</span>
        </div>
        <div className="readout">
          <span className="value">{fmt.one(peaks.wtq)}</span>
          <span className="unit">lb-ft</span>
          <span className="at">at {fmt.rpm(peaks.wtqRpm)} rpm</span>
        </div>
        {peaks.boostPsi != null && (
          <div className="readout">
            <span className="value">{fmt.one(peaks.boostPsi)}</span>
            <span className="unit">psi</span>
            <span className="at">peak boost</span>
          </div>
        )}
      </div>

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
    </>
  );
}
