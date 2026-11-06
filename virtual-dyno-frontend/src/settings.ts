import type { DynoSettings } from './types';

/**
 * Settings that describe the user's world rather than one log: altitude, humidity, correction, and
 * the scale weight of each car. Remembered in this browser so they're entered once.
 */
const KEY = 'virtual-dyno.settings.v1';

interface Stored {
  altitudeFt: number;
  humidityPct: number;
  correction: DynoSettings['correction'];
  weights: Record<string, number>;
}

const defaults: Stored = { altitudeFt: 4300, humidityPct: 25, correction: 'Sae', weights: {} };

export function loadStored(): Stored {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? { ...defaults, ...JSON.parse(raw) } : defaults;
  } catch {
    return defaults;
  }
}

export function saveStored(patch: Partial<Stored>): void {
  try {
    localStorage.setItem(KEY, JSON.stringify({ ...loadStored(), ...patch }));
  } catch {
    /* storage unavailable: settings just won't persist */
  }
}

export function rememberWeight(vehicleKey: string, weightLb: number): void {
  const stored = loadStored();
  saveStored({ weights: { ...stored.weights, [vehicleKey]: weightLb } });
}
