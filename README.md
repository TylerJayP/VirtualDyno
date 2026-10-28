# Virtual Dyno

Turns a full-throttle datalog from a Cobb AccessPort (or most loggers that export CSV) into a wheel horsepower and torque curve — computed from physics, not fudge factors.

Drop in a log and it identifies the car from the log header, finds the pull, works out which gear you were in, and draws the dyno sheet with boost, AFR and timing underneath. Every run is checked against the fuel the engine burned, and anything that would make the number wrong (wheelspin, knock, a guessed weight) is called out.

## How it measures power

A chassis dyno like a Dynojet doesn't measure force directly: it measures how fast your car spins up a drum of known inertia. On the road, **the car itself is the drum** and its weight is the known inertia. At every sample:

```
force at the tires = m_eff · a  +  ½ · ρ · CdA · v²  +  Crr · m · g  +  m · g · grade
wheel power        = force × v
```

| Term | Where it comes from |
|---|---|
| `v`, `a` | Engine RPM over time, divided by the RPM-per-mph ratio **measured from the log's own vehicle speed**. RPM is used instead of speed because it's logged in much finer steps. |
| `m` | Your weight (car + you + fuel). Defaults to factory curb weight + 200 lb until you enter a scale reading. |
| `m_eff` | `m × 1.03` for the wheels and tires spinning up. Engine and gearbox inertia are left out on purpose: a chassis dyno doesn't count them either, which is why lower gears read lower on both. |
| `ρ` | Air density from barometric pressure (altitude), temperature (the log's intake air temperature) and humidity. |
| `CdA`, `Crr` | Drag area from the car's factory spec; rolling resistance 0.012. |

Torque is the dyno-sheet convention: `wheel hp × 5252 / engine rpm`.

### Getting acceleration from a noisy log

Power comes from the *derivative* of RPM, and differentiating amplifies noise — that's why other road dynos make you pick a smoothing level (too little inflates the peak, too much flattens the curve). Here, RPM is fit with a penalized cubic B-spline whose smoothness is **estimated from the log itself by restricted maximum likelihood (REML)**. Noisy logs get more smoothing, clean logs less, and the result doesn't swing with where the pull happens to start. A manual "less / more" nudge is still there.

Before fitting, the pull is trimmed to steady acceleration: from the moment the pedal is flat and the tip-in driveline shudder has settled, to the highest RPM before you lift or shift.

### Checks on every run

- **Fuel check.** MAF airflow ÷ AFR gives fuel burned. Engines live in a narrow brake-specific fuel consumption band, so if the measured power implies an impossible BSFC, something else is off (weight, a hill, slip).
- **Slip.** If engine RPM runs ahead of vehicle speed, the clutch slipped (or the tires did, if the speed signal comes from undriven wheels). That stretch is bridged from both sides, drawn shaded, and left off the peaks.
- **Gear.** Named from the measured ratio against factory gearing. Low gears and high gears each get a note on what they do to the number.
- **Knock, lean AFR, low logging rate, short pull, large altitude correction** — each called out with what to do about it.

### Accuracy

The test suite drives a simulated car with a known power curve down the road and writes the log an AccessPort would record (irregular ~23 Hz samples, timestamp jitter, whole-RPM and whole-mph values). The engine recovers peak power within **±2%** and the whole curve within **1.5% RMS** across random seeds.

What it can't know for you: your real weight (weigh the car — a 1% error is a 1% error), a road grade you didn't notice (run the same stretch both ways), and wind.

## Running it

**Prerequisites:** .NET 10 SDK, Node.js 22+, SQL Server LocalDB (comes with Visual Studio).

```bash
# 1. Create the database migration (first time only)
dotnet tool restore
dotnet ef migrations add InitialCreate --project VirtualDyno.API

# 2. Start the API (http://localhost:5038, Swagger UI at /swagger)
dotnet run --project VirtualDyno.API

# 3. In another terminal, start the app (http://localhost:5173)
cd virtual-dyno-frontend
npm install
npm run dev
```

The API applies migrations on startup. To try it without SQL Server, set `"Storage": { "Provider": "InMemory" }` in `VirtualDyno.API/appsettings.json` (runs are lost on restart).

```bash
dotnet test            # engine, sample-log and API service tests
```

## Getting a good log

- One pull, one gear, pedal flat from about 2,500 rpm to redline. 3rd or 4th gear.
- A flat, straight road. Run it in both directions and compare the two.
- Log as few channels as you need: fewer channels means more samples per second. RPM, vehicle speed, pedal, boost, AFR, MAF, intake temp, timing and knock is plenty.
- Enter your scale weight once; it's remembered for that car.

## Project layout

```
VirtualDyno.Engine/      Physics and signal processing. No dependencies.
  Logs/                  CSV parsing and column → channel mapping with unit conversion
  Analysis/              Pull detection, the dyno solver, settings and results
  Numerics/              P-spline (REML) and a small Cholesky solver
  Vehicles/              Car catalog, AccessPort header matching, gear detection
  Atmosphere.cs          Air density, SAE J1349 and J607 correction
VirtualDyno.API/         ASP.NET Core Web API
  Controllers/           /api/vehicles, /api/analyze, /api/runs
  Services/              DynoService (application layer), repository interface, error handling
  Data/                  EF Core (SQL Server) storage
VirtualDyno.Tests/       xUnit tests, including the simulated-car accuracy test
virtual-dyno-frontend/   React + TypeScript (Vite), Recharts
SampleLogs/              A real 3rd-gear AccessPort log from a 2012 Mazdaspeed3
```

Saved runs store the **original log and the settings**, not the computed curve, so every run is recomputed on demand and picks up any engine improvement.

## API

| Method | Route | |
|---|---|---|
| GET | `/api/vehicles` | Car catalog |
| POST | `/api/analyze` | Analyze a log without saving (`multipart/form-data`: `file`, `settings` JSON) |
| GET | `/api/runs` | Saved runs |
| POST | `/api/runs` | Save a run (`file`, `settings`, `name`, `notes`) |
| GET | `/api/runs/{id}?correction=&smoothing=` | Recompute a run, optionally viewed with another correction or smoothing |
| PUT | `/api/runs/{id}` | Rename, edit notes, or change settings (recomputes) |
| DELETE | `/api/runs/{id}` | Delete a run |
| GET | `/api/runs/{id}/log` | Download the original log |

A log that can't produce a curve returns `422` with a message saying why (no full-throttle pull, no time column, …).

## Car catalog

Specs come from manufacturer spec sheets where available; each entry says what's sourced and what's estimated (`VirtualDyno.Engine/Vehicles/CatalogData.cs`). Only weight and drag area affect the power number. Gear ratios are used to name the gear, so cars without verified gearing still work — the gear just isn't labeled.
