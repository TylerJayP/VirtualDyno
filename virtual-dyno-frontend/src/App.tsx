import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent } from 'react';
import { api, ApiError } from './api';
import { ChecksPanel } from './components/ChecksPanel';
import { DynoSheet } from './components/DynoSheet';
import { RunsPanel } from './components/RunsPanel';
import { SetupPanel } from './components/SetupPanel';
import { buildRows, CURRENT_COLOR, OVERLAY_COLORS, type Series } from './series';
import { loadStored, rememberWeight, saveStored } from './settings';
import type { DynoResult, DynoSettings, RunDetail, RunSummary, Vehicle } from './types';

interface Problem {
  title: string;
  message: string;
}

const asProblem = (e: unknown): Problem =>
  e instanceof ApiError ? { title: e.title, message: e.message } : { title: 'Something went wrong', message: String(e) };

export default function App() {
  const stored = useMemo(loadStored, []);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [runs, setRuns] = useState<RunSummary[]>([]);
  const [log, setLog] = useState<{ file: Blob; fileName: string } | null>(null);
  const [openRun, setOpenRun] = useState<{ id: number; name: string } | null>(null);
  const [settings, setSettings] = useState<DynoSettings>({
    altitudeFt: stored.altitudeFt,
    humidityPct: stored.humidityPct,
    correction: stored.correction,
    smoothingBias: 0,
    roadGradePct: 0,
  });
  const [result, setResult] = useState<DynoResult | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  // A newly opened log or run plays its pull back once its first analysis arrives.
  const pendingReplay = useRef(false);
  const [replayToken, setReplayToken] = useState(0);
  const [overlayIds, setOverlayIds] = useState<number[]>([]);
  const [overlays, setOverlays] = useState<Record<number, RunDetail>>({});
  const [saving, setSaving] = useState(false);
  const [savedNote, setSavedNote] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const refreshRuns = useCallback(() => api.runs().then(setRuns).catch(e => setProblem(asProblem(e))), []);

  useEffect(() => {
    api.vehicles().then(setVehicles).catch(e => setProblem(asProblem(e)));
    refreshRuns();
  }, [refreshRuns]);

  // The scale weight is remembered per car, so it fills in once the car is known (picked or identified).
  const knownKey = settings.vehicleKey ?? result?.vehicle.key ?? null;
  const rememberedWeight = knownKey ? loadStored().weights[knownKey] ?? null : null;
  const effective = useMemo<DynoSettings>(
    () => ({ ...settings, weightLb: settings.weightLb ?? rememberedWeight }),
    [settings, rememberedWeight],
  );

  // Re-run the dyno whenever the log or any setting changes.
  useEffect(() => {
    if (!log) return;
    const controller = new AbortController();
    setBusy(true);
    const timer = setTimeout(() => {
      api
        .analyze(log.file, log.fileName, effective, controller.signal)
        .then(r => {
          setResult(r);
          setProblem(null);
          if (pendingReplay.current) {
            pendingReplay.current = false;
            setReplayToken(n => n + 1);
          }
        })
        .catch(e => {
          if (controller.signal.aborted) return;
          pendingReplay.current = false;
          setProblem(asProblem(e));
          setResult(null);
        })
        .finally(() => !controller.signal.aborted && setBusy(false));
    }, 150);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [log, effective]);

  // Overlays are recomputed with the same correction and smoothing as the open run.
  const { correction, smoothingBias } = settings;
  useEffect(() => {
    let cancelled = false;
    Promise.all(overlayIds.map(id => api.run(id, { correction, smoothing: smoothingBias }).then(d => [id, d] as const)))
      .then(entries => !cancelled && setOverlays(Object.fromEntries(entries)))
      .catch(e => !cancelled && setProblem(asProblem(e)));
    return () => {
      cancelled = true;
    };
  }, [overlayIds, correction, smoothingBias]);

  const colors = useMemo(
    () => Object.fromEntries(overlayIds.map((id, i) => [id, OVERLAY_COLORS[i % OVERLAY_COLORS.length]])) as Record<number, string>,
    [overlayIds],
  );

  const series = useMemo<Series[]>(() => {
    if (!result) return [];
    const current: Series = { id: 'cur', name: openRun?.name ?? 'This log', color: CURRENT_COLOR, points: result.points };
    const others = overlayIds
      .filter(id => overlays[id])
      .map(id => ({ id: String(id), name: overlays[id].run.name, color: colors[id], points: overlays[id].analysis.points }));
    return [current, ...others];
  }, [result, openRun, overlayIds, overlays, colors]);

  const { rows, minRpm, maxRpm } = useMemo(() => buildRows(series, result?.points ?? null), [series, result]);

  // ---- actions -----------------------------------------------------------------------------

  const loadFile = (file: File) => {
    setLog({ file, fileName: file.name });
    setOpenRun(null);
    setSavedNote(null);
    pendingReplay.current = true;
    setSettings(s => ({ ...s, pullIndex: null, weightLb: null }));
  };

  const changeSettings = (patch: Partial<DynoSettings>) => {
    setSavedNote(null);
    setSettings(s => {
      const next = { ...s, ...patch };
      if ('vehicleKey' in patch && patch.vehicleKey !== s.vehicleKey) next.weightLb = null;
      return next;
    });
    const persist: Parameters<typeof saveStored>[0] = {};
    if (patch.altitudeFt != null) persist.altitudeFt = patch.altitudeFt;
    if (patch.humidityPct != null) persist.humidityPct = patch.humidityPct;
    if (patch.correction) persist.correction = patch.correction;
    if (Object.keys(persist).length) saveStored(persist);
  };

  const changeWeight = (weightLb: number | null) => {
    setSavedNote(null);
    setSettings(s => ({ ...s, weightLb }));
    if (weightLb && knownKey) rememberWeight(knownKey, weightLb);
  };

  const openSavedRun = async (id: number) => {
    try {
      const [detail, file] = await Promise.all([api.run(id), api.runLog(id)]);
      setSettings({ ...detail.settings, pullIndex: detail.settings.pullIndex ?? null });
      setLog({ file, fileName: detail.run.fileName });
      setOpenRun({ id, name: detail.run.name });
      setOverlayIds(ids => ids.filter(x => x !== id));
      setSavedNote(null);
      pendingReplay.current = true;
    } catch (e) {
      setProblem(asProblem(e));
    }
  };

  const saveRun = async (name: string, notes: string) => {
    if (!log) return;
    setSaving(true);
    try {
      const saved = await api.saveRun(log.file, log.fileName, effective, name, notes);
      setOpenRun({ id: saved.run.id, name: saved.run.name });
      setSavedNote(`Saved as “${saved.run.name}”.`);
      await refreshRuns();
    } catch (e) {
      setProblem(asProblem(e));
    } finally {
      setSaving(false);
    }
  };

  const updateRun = async (name: string) => {
    if (!openRun) return;
    setSaving(true);
    try {
      const saved = await api.updateRun(openRun.id, { name: name || undefined, settings: effective });
      setOpenRun({ id: saved.run.id, name: saved.run.name });
      setSavedNote('Changes saved.');
      await refreshRuns();
    } catch (e) {
      setProblem(asProblem(e));
    } finally {
      setSaving(false);
    }
  };

  const deleteRun = async (run: RunSummary) => {
    if (!window.confirm(`Delete “${run.name}”? This can’t be undone.`)) return;
    try {
      await api.deleteRun(run.id);
      setOverlayIds(ids => ids.filter(x => x !== run.id));
      if (openRun?.id === run.id) setOpenRun(null);
      await refreshRuns();
    } catch (e) {
      setProblem(asProblem(e));
    }
  };

  const toggleOverlay = (id: number) =>
    setOverlayIds(ids => (ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]));

  const onDrop = (e: DragEvent) => {
    e.preventDefault();
    setDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) loadFile(file);
  };

  return (
    <div
      className="app"
      onDragOver={e => {
        e.preventDefault();
        setDragging(true);
      }}
      onDragLeave={e => e.currentTarget === e.target && setDragging(false)}
      onDrop={onDrop}
    >
      <input
        ref={fileInput}
        type="file"
        accept=".csv,.txt,.log"
        hidden
        onChange={e => {
          const f = e.target.files?.[0];
          if (f) loadFile(f);
          e.target.value = '';
        }}
      />

      <header className="brand">
        <svg width="28" height="28" viewBox="0 0 32 32" aria-hidden="true">
          <rect width="32" height="32" rx="6" fill="#15233A" />
          <path d="M5 25 C10 24 12 11 17 9 C21 8 24 12 27 19" fill="none" stroke="#D7263D" strokeWidth="3" strokeLinecap="round" />
          <path d="M5 27 C9 14 13 12 17 12 C21 12 24 17 27 25" fill="none" stroke="#9FB3C8" strokeWidth="2" strokeDasharray="3 2.5" strokeLinecap="round" />
        </svg>
        <h1>Virtual Dyno</h1>
      </header>

      <SetupPanel
        vehicles={vehicles}
        settings={effective}
        result={result}
        fileName={log?.fileName ?? null}
        openRunName={openRun?.name ?? null}
        saving={saving}
        savedNote={savedNote}
        onChange={changeSettings}
        onWeightChange={changeWeight}
        onChooseFile={() => fileInput.current?.click()}
        onSave={saveRun}
        onUpdate={updateRun}
      />

      <main className="sheet paper" aria-busy={busy}>
        {result ? (
          <DynoSheet
            result={result}
            series={series}
            rows={rows}
            minRpm={minRpm}
            maxRpm={maxRpm}
            busy={busy}
            notice={problem && <ProblemNote problem={problem} />}
            replayToken={replayToken}
          />
        ) : (
          <>
            <div className="sheet-top">{problem && <ProblemNote problem={problem} />}</div>
            <div className={`drop${dragging ? ' active' : ''}`}>
              <div className="inner">
                <h2>{busy ? 'Reading the log…' : 'Drop a log to run the dyno'}</h2>
                <p>
                  Any AccessPort CSV with one full-throttle pull in a single gear. 3rd or 4th gear gives the cleanest
                  numbers.
                </p>
                <button className="button" onClick={() => fileInput.current?.click()}>
                  Choose a log
                </button>
              </div>
            </div>
          </>
        )}
      </main>

      <div className="side">
        {result && <ChecksPanel result={result} />}
        <RunsPanel
          runs={runs}
          overlayIds={overlayIds}
          colors={colors}
          openRunId={openRun?.id ?? null}
          currentGear={result?.gearing.detectedGear ?? null}
          onToggleOverlay={toggleOverlay}
          onOpen={openSavedRun}
          onDelete={deleteRun}
        />
      </div>
    </div>
  );
}

function ProblemNote({ problem }: { problem: Problem }) {
  return (
    <div className="problem" role="alert">
      <strong>{problem.title}</strong>
      {problem.message}
    </div>
  );
}
