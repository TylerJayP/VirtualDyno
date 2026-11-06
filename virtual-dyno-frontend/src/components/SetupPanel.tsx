import { useEffect, useState } from 'react';
import type { CorrectionMode, DynoResult, DynoSettings, Vehicle } from '../types';
import { fmt } from '../format';

interface Props {
  vehicles: Vehicle[];
  settings: DynoSettings;
  result: DynoResult | null;
  fileName: string | null;
  openRunName: string | null;
  saving: boolean;
  savedNote: string | null;
  onChange: (patch: Partial<DynoSettings>) => void;
  onWeightChange: (weightLb: number | null) => void;
  onChooseFile: () => void;
  onSave: (name: string, notes: string) => void;
  onUpdate: (name: string) => void;
}

/** Number input that only reports parsed values, so typing "3,4" doesn't fire analyses with junk. */
function NumberField({ id, label, unit, value, placeholder, hint, hintAttention, step, onCommit }: {
  id: string;
  label: string;
  unit: string;
  value: number | null | undefined;
  placeholder?: string;
  hint?: string;
  hintAttention?: boolean;
  step?: number;
  onCommit: (v: number | null) => void;
}) {
  const [text, setText] = useState(value == null ? '' : String(value));
  useEffect(() => setText(value == null ? '' : String(value)), [value]);

  const commit = (raw: string) => {
    const cleaned = raw.replace(/,/g, '').trim();
    if (cleaned === '') return onCommit(null);
    const n = Number(cleaned);
    if (Number.isFinite(n)) onCommit(n);
  };

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <div className="input-unit">
        <input
          id={id}
          className="input"
          inputMode="decimal"
          step={step}
          value={text}
          placeholder={placeholder}
          onChange={e => setText(e.target.value)}
          onBlur={e => commit(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && commit((e.target as HTMLInputElement).value)}
        />
        <span>{unit}</span>
      </div>
      {hint && <div className={`hint${hintAttention ? ' attention' : ''}`}>{hint}</div>}
    </div>
  );
}

function Segmented<T extends string | number>({ name, label, value, options, onChange }: {
  name: string;
  label: string;
  value: T;
  options: { value: T; label: string }[];
  onChange: (v: T) => void;
}) {
  return (
    <div className="field" role="radiogroup" aria-label={label}>
      <span className="label">{label}</span>
      <div className="segmented">
        {options.map(o => (
          <label key={String(o.value)}>
            <input type="radio" name={name} checked={value === o.value} onChange={() => onChange(o.value)} />
            {o.label}
          </label>
        ))}
      </div>
    </div>
  );
}

export function SetupPanel(props: Props) {
  const { vehicles, settings, result, fileName, openRunName, saving, savedNote, onChange, onWeightChange } = props;
  const [name, setName] = useState('');
  const [notes, setNotes] = useState('');
  useEffect(() => setName(openRunName ?? ''), [openRunName, fileName]);

  const detected = result?.vehicle.autoDetected ? result.vehicle.name : null;
  const weightFromUser = result?.vehicle.weightSource === 'User';
  const pulls = result?.pulls ?? [];

  return (
    <aside className="setup">

      <div className="group">
        <h2>Log</h2>
        <div className="field">
          <button className="button quiet full" onClick={props.onChooseFile}>
            {fileName ? 'Choose a different log' : 'Choose a log'}
          </button>
          {fileName && <div className="hint">{fileName}</div>}
        </div>
        {pulls.length > 1 && (
          <div className="field">
            <label htmlFor="pull">Pull</label>
            <select
              id="pull"
              className="input"
              value={result?.selectedPull ?? 0}
              onChange={e => onChange({ pullIndex: Number(e.target.value) })}
            >
              {pulls.map(p => (
                <option key={p.index} value={p.index}>
                  Pull {p.index + 1}: {fmt.rpm(p.startRpm)}–{fmt.rpm(p.endRpm)} rpm
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      <div className="group">
        <h2>Car</h2>
        <div className="field">
          <label htmlFor="car">Model</label>
          <select
            id="car"
            className="input"
            value={settings.vehicleKey ?? ''}
            onChange={e => onChange({ vehicleKey: e.target.value || null })}
          >
            <option value="">{detected ? `From the log: ${detected}` : 'Identify from the log'}</option>
            {vehicles.map(v => (
              <option key={v.key} value={v.key}>
                {v.name}
              </option>
            ))}
          </select>
        </div>
        <NumberField
          id="weight"
          label="Weight with you in it"
          unit="lb"
          value={settings.weightLb}
          placeholder={result ? fmt.whole(result.vehicle.weightLb) : undefined}
          hint={
            !result
              ? 'Enter it once and it’s saved for that car.'
              : weightFromUser
                ? 'Saved for this car. A scale reading beats any estimate.'
                : 'Using factory weight + 200 lb. Enter a scale weight: 1% off here is 1% off the result.'
          }
          hintAttention={!weightFromUser && !!result}
          onCommit={onWeightChange}
        />
      </div>

      <div className="group">
        <h2>Conditions</h2>
        <div className="pair">
          <NumberField id="alt" label="Altitude" unit="ft" value={settings.altitudeFt} onCommit={v => onChange({ altitudeFt: v })} />
          <NumberField id="hum" label="Humidity" unit="%" value={settings.humidityPct} onCommit={v => onChange({ humidityPct: v ?? 0 })} />
        </div>
        <details className="more">
          <summary>More conditions</summary>
          <NumberField
            id="temp"
            label="Outside temperature"
            unit="°F"
            value={settings.ambientTempF}
            placeholder={result ? `${fmt.whole(result.environment.ambientTempF)} from log` : undefined}
            hint="Leave blank to use the intake air temperature from the log."
            onCommit={v => onChange({ ambientTempF: v })}
          />
          <NumberField
            id="grade"
            label="Road grade"
            unit="%"
            value={settings.roadGradePct ?? 0}
            hint="Uphill is positive. Better: run both directions and compare."
            onCommit={v => onChange({ roadGradePct: v ?? 0 })}
          />
        </details>
      </div>

      <div className="group">
        <h2>View</h2>
        <Segmented<CorrectionMode>
          name="correction"
          label="Correction"
          value={settings.correction ?? 'Sae'}
          options={[
            { value: 'Sae', label: 'SAE' },
            { value: 'Std', label: 'STD' },
            { value: 'Uncorrected', label: 'None' },
          ]}
          onChange={v => onChange({ correction: v })}
        />
        <Segmented<number>
          name="smoothing"
          label="Smoothing"
          value={settings.smoothingBias ?? 0}
          options={[
            { value: -1, label: 'Less' },
            { value: 0, label: 'Automatic' },
            { value: 1, label: 'More' },
          ]}
          onChange={v => onChange({ smoothingBias: v })}
        />
      </div>

      {result && (
        <div className="group">
          <h2>{openRunName ? 'This run' : 'Save'}</h2>
          <div className="field">
            <label htmlFor="run-name">Name</label>
            <input
              id="run-name"
              className="input"
              value={name}
              placeholder={fileName?.replace(/\.[^.]+$/, '') ?? 'Run name'}
              onChange={e => setName(e.target.value)}
            />
          </div>
          {!openRunName && (
            <div className="field">
              <label htmlFor="run-notes">Notes</label>
              <textarea
                id="run-notes"
                className="input"
                rows={2}
                value={notes}
                placeholder="Fuel, mods, weather"
                onChange={e => setNotes(e.target.value)}
              />
            </div>
          )}
          <div className="save-row">
            {openRunName ? (
              <>
                <button className="button" disabled={saving} onClick={() => props.onUpdate(name)}>
                  Save changes
                </button>
                <button className="button quiet" disabled={saving} onClick={() => props.onSave(name, notes)}>
                  Save as new run
                </button>
              </>
            ) : (
              <button className="button" disabled={saving} onClick={() => props.onSave(name, notes)}>
                Save run
              </button>
            )}
          </div>
          {savedNote && <div className="saved-note">{savedNote}</div>}
        </div>
      )}
    </aside>
  );
}
