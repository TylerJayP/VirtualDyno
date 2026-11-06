// Shapes returned by the Virtual Dyno API (camelCase JSON, enums as strings).

export type CorrectionMode = 'Uncorrected' | 'Sae' | 'Std';
export type ValueSource = 'User' | 'Log' | 'Catalog' | 'Default' | 'Measured';
export type Drivetrain = 'Fwd' | 'Rwd' | 'Awd';

export interface DynoSettings {
  vehicleKey?: string | null;
  weightLb?: number | null;
  dragAreaM2?: number | null;
  rollingResistance?: number;
  rotationalMassFactor?: number;
  roadGradePct?: number;
  altitudeFt?: number | null;
  baroInHg?: number | null;
  ambientTempF?: number | null;
  humidityPct?: number;
  correction?: CorrectionMode;
  smoothingBias?: number;
  pullIndex?: number | null;
  speedometerErrorPct?: number;
  gear?: number | null;
  overallRatio?: number | null;
  tireSize?: string | null;
}

export interface Vehicle {
  key: string;
  name: string;
  make: string;
  model: string;
  yearFrom: number;
  yearTo: number;
  curbWeightLb: number;
  drive: Drivetrain;
  turbo: boolean;
  dragCoefficient: number;
  frontalAreaM2: number;
  dragAreaM2: number;
  gearCount: number;
  stockTire: string | null;
  specNotes: string | null;
}

export interface DynoPoint {
  timeSec: number;
  rpm: number;
  whp: number;
  wtq: number;
  mph: number;
  boostPsi: number | null;
  afr: number | null;
  sparkAdvance: number | null;
  knockRetard: number | null;
  intakeTempF: number | null;
  chargeTempF: number | null;
  slip: boolean;
}

export interface Pull {
  index: number;
  startTime: number;
  endTime: number;
  startRpm: number;
  endRpm: number;
  duration: number;
}

export interface Warning {
  code: string;
  severity: 'Info' | 'Warning';
  message: string;
}

export interface ChannelMatch {
  channel: string;
  column: string;
  sourceUnit: string | null;
  conversion: string | null;
}

export interface DynoResult {
  log: { device: string | null; firmware: string | null; vehicle: string | null; map: string | null; rows: number; channels: ChannelMatch[] };
  vehicle: {
    key: string | null;
    name: string;
    autoDetected: boolean;
    weightLb: number;
    weightSource: ValueSource;
    dragAreaM2: number;
    dragAreaSource: ValueSource;
    drive: Drivetrain;
    turbo: boolean;
  };
  pulls: Pull[];
  selectedPull: number;
  gearing: {
    rpmPerMph: number;
    source: ValueSource;
    detectedGear: number | null;
    overallRatio: number | null;
    tireSize: string | null;
    slipPoints: number;
    slipFromRpm: number | null;
    slipToRpm: number | null;
  };
  environment: {
    baroInHg: number;
    baroSource: ValueSource;
    ambientTempF: number;
    ambientSource: ValueSource;
    humidityPct: number;
    airDensityKgM3: number;
    correction: CorrectionMode;
    correctionFactor: number;
  };
  smoothing: { bias: number; lambda: number; effectiveDegreesOfFreedom: number; sampleRateHz: number };
  peaks: {
    whp: number;
    whpRpm: number;
    wtq: number;
    wtqRpm: number;
    boostPsi: number | null;
    uncorrectedWhp: number;
    uncorrectedWtq: number;
  };
  breakdown: { accelerationHp: number; aeroHp: number; rollingHp: number; gradeHp: number };
  crossCheck: {
    mafGs: number;
    afr: number;
    afrAssumed: boolean;
    fuelLbPerHr: number;
    impliedBsfc: number;
    expectedBsfcLow: number;
    expectedBsfcHigh: number;
    estimatedWhpLow: number;
    estimatedWhpHigh: number;
    agrees: boolean;
  } | null;
  points: DynoPoint[];
  warnings: Warning[];
}

export interface RunSummary {
  id: number;
  name: string;
  fileName: string;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  vehicleKey: string | null;
  vehicleName: string;
  gear: number | null;
  weightLb: number;
  correction: CorrectionMode;
  peakWhp: number;
  peakWhpRpm: number;
  peakWtq: number;
  peakWtqRpm: number;
  peakBoostPsi: number | null;
}

export interface RunDetail {
  run: RunSummary;
  settings: DynoSettings;
  analysis: DynoResult;
}
