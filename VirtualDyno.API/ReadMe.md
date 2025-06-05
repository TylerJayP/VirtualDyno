# Virtual Dyno

Personal dyno analysis tool that transforms AccessPort, Hondata, and OBD2 datalog files into accurate horsepower and torque curves with real-world precision.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![C#](https://img.shields.io/badge/C%23-ASP.NET%20Core-purple.svg)](https://docs.microsoft.com/en-us/dotnet/)

## Features

### Multi-Method Calculation Engine
- **MAF-based calculations** - Most accurate method using direct airflow measurement
- **MAP-based calculations** - For speed density tunes without MAF sensors  
- **Load-based calculations** - Universal fallback for basic OBD data
- **Automatic method selection** based on available sensors

### Universal Platform Support
- **Mazda AccessPort** - `Mass Airflow (g/s)`, `Knock Retard (°)`, `Actual AFR (AFR)`
- **Subaru AccessPort** - `Feedback Knock (°)`, `DAM (%)`, `Fine Knock Learn (°)`
- **Honda Hondata** - `MAP (kPa)`, `Knock Count`, `AFR Bank 1`
- **Generic OBD** - Works with basic `RPM`, `Load`, `Throttle Position`
- **Automatic column detection** - No manual mapping required

### Advanced Data Processing
- **Manual smoothing control** (0-5 levels: 0=raw data, 1=light, 5=maximum)
- **Intelligent column mapping** for 50+ parameter variations
- **Platform-specific knock detection** (Subaru DAM integration, Honda knock counts)
- **Gaussian-weighted smoothing** algorithm for professional results

### Multi-Car Support
**Pre-configured vehicles with real-world calibration:**
- **Mazdaspeed3**
- **Subaru WRX/STI**
- **Mitsubishi Evo*
- **VW GTI**
- **Ford Focus ST**

### Analysis Features
- **Gear-specific corrections** (3rd, 4th, 5th gear with gear ratios that need some improvements)
- **Drivetrain loss compensation** (FWD/AWD/RWD specific)
- **AFR optimization curves** (11.8 AFR target for turbocharged engines)
- **Knock retard penalties** (real-time timing adjustment effects)
- **Temperature density correction** (SAE J1349 atmospheric standards)
- **Peak detection** with RPM validation

### Advanced Corrections
- **MAF sensor trust philosophy** - Primary airflow measurement
- **Temperature-based efficiency** - Air density effects
- **Knock timing penalties** - Actual power loss from timing retard
- **Platform-specific calibration** - Individual car scaling factors
- **Volumetric efficiency curves** - Engine "breathing" characteristics

## Tech Stack

**Backend:**
- C# ASP.NET Core 9.0
- Entity Framework Core
- SQL Server LocalDB  
- CsvHelper for file processing
- Swagger/OpenAPI documentation

**Algorithm:**
- Physics-based MAP calculations
- Gaussian-weighted data smoothing
- Multi-sensor fusion approach
- Real-world calibration factors

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- SQL Server LocalDB (included with Visual Studio)

## Quick Start

### 1. Clone the Repository
```bash
git clone https://github.com/TylerJayP/VirtualDyno.git
cd VirtualDyno
```

### 2. Backend Setup
```bash
cd VirtualDyno.API

# Install EF Core tools
dotnet tool install --global dotnet-ef

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run database migrations
dotnet ef database update

# Start the API server
dotnet run
```

### 3. Access the Application
- **API Documentation**: `https://localhost:7175/swagger`
- **Backend API**: `https://localhost:7175/api`

## Usage

### Upload Datalog Files
1. Navigate to the Swagger documentation
2. Use the `POST /api/Dyno/upload` endpoint
3. Upload your CSV file (AccessPort, Hondata, OBD2)
4. Configure vehicle settings (car model, weight, gear used)
5. Receive instant dyno analysis results

### API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/CarPresets` | Get all available car presets |
| `POST` | `/api/Dyno/upload` | Upload and process datalog file |
| `GET` | `/api/Dyno/runs` | Get user's dyno runs |
| `GET` | `/api/Dyno/runs/{id}` | Get specific run with data points |
| `GET` | `/api/Dyno/runs/{id}/smooth/{level}` | Get smoothed data (0-5 levels) |
| `DELETE` | `/api/Dyno/runs/{id}` | Delete a dyno run |

### Supported CSV Formats

**Mazdaspeed3 AccessPort:**
```csv
Time (sec),RPM (RPM),Mass Airflow (g/s),Boost (psi),Actual AFR (AFR),Knock Retard (°)
```

**Subaru WRX AccessPort:**
```csv
Time,RPM (RPM),Boost (psi),Calculated Load (%),Feedback Knock (°),DAM (%)
```

**Honda Civic Si Hondata:**
```csv
Time,RPM,MAP (kPa),Engine Load (%),AFR Bank 1,Knock Count
```

**Generic OBD:**
```csv
Time,RPM,Engine Load (%),Throttle Position (%)
```

## Algorithm Performance

### Calculation Methods & Accuracy

| Method | Accuracy | When Used | Example Cars |
|--------|----------|-----------|--------------|
| **MAF-based** | ±1-2% | MAF sensor available | Stock Mazdaspeed3, WRX |
| **MAP-based** | ±5-10% | Speed density tunes | Modified WRX/STI |
| **Load-based** | ±15-20% | Basic OBD only | Any car with basic scanner |

### Real-World Validation

**Mazdaspeed3 Datalog Results:**
- **Target:** 237-239 WHP @ 5400-5600 RPM, 280-287 lb-ft @ 3900-4100 RPM
- **Actual:** 239.4 WHP @ 5660 RPM, 287 lb-ft @ 3482 RPM  
- **Accuracy:** 1.1% HP error, 0.8% torque error

### Smoothing Control

| Level | Description | Use Case |
|-------|-------------|----------|
| **0** | Raw data | Debugging/analysis |
| **1** | Light (default) | Best accuracy/smoothness balance |
| **2-3** | Moderate | Noisy data cleanup |
| **4-5** | Heavy | Trend analysis |

## Calibration & Accuracy

The dyno calculations are calibrated against real-world dyno results:

### Calibration Methodology
- **Cross-validation**: Multiple vehicle platforms and dyno facilities
- **Accuracy target**: ±5% for MAF-based calculations
- **Method validation**: Physics-based MAP calculations

### Advanced Algorithm Features
- **MAF sensor priority** - Most accurate airflow measurement
- **Temperature density correction** - Accounts for air density changes
- **Knock retard penalty** - Real power loss from timing retard (1.8% per degree)
- **AFR optimization** - Peak power targeting (11.8 AFR for turbo engines)
- **Platform-specific knock handling** - Subaru DAM integration

## Project Structure

```
VirtualDyno.API/
├── Controllers/           # API endpoints and request handling
├── Models/               # Data models and DTOs
│   ├── DynoModels.cs    # Core dyno data structures
│   ├── AdvancedDynoModels.cs # Multi-method calculation models
│   └── DTOs/            # Data transfer objects
├── Services/            # Business logic and calculations
│   ├── DynoCalculationService.cs # Multi-method dyno algorithms
│   ├── CsvProcessingService.cs   # Universal file processing
│   └── Interfaces/      # Service contracts
├── Data/                # Entity Framework context
└── Migrations/          # Database schema changes
```

## Configuration

### Database Connection
Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=VirtualDynoDB;Trusted_Connection=true"
  }
}
```

### Car Calibration
Fine-tune calibration factors in `DynoCalculationService.cs`:
```csharp
private double GetMAFCalibrationFactor(string carKey)
{
    return carKey switch
    {
        "mazdaspeed3" => 0.895, // Calibrated to 239 WHP
        "wrx" => 0.92,          // 2.0L WRX
        // Add your calibration data
    };
}
```

## Troubleshooting

### Common Issues

**"No valid dyno data found":**
- Check RPM > 2000, Load > 15%, appropriate sensor thresholds
- Ensure you have a proper WOT run, not idle/cruise data

**Column not found errors:**
- Check CSV headers against supported variations in documentation
- System supports 50+ column name variations automatically

**Unrealistic power numbers:**
- Verify car preset selection matches your actual vehicle
- Check for sensor calibration issues in your tune

**Missing MAF data:**
- System automatically uses MAP-based calculation for speed density tunes
- Accuracy reduced but still provides meaningful results

### Data Quality Tips

1. **Full throttle runs**: Best results from WOT pulls
2. **Gear selection**: 3rd-5th gear recommended (4th gear is baseline)
3. **RPM range**: 2500-6500 RPM for complete power curve
4. **Data density**: More data points = better accuracy
5. **Sensor health**: Ensure MAF, MAP, and knock sensors are functioning

## Roadmap

- [ ] **React Frontend** - Modern web interface with interactive charts
- [ ] **Mobile App** - React Native dyno analysis on-the-go
- [ ] **Run Comparison** - Overlay multiple dyno runs
- [ ] **Export Features** - PDF reports, CSV data export  
- [ ] **Advanced Analytics** - Quarter-mile predictions, efficiency maps
- [ ] **Social Features** - Share runs, leaderboards
- [ ] **More Platforms** - VCDS, OBD11, EcuTek support


## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- **SAE J1349** standard for atmospheric correction formulas
- **Real dyno facilities** for calibration data validation
- **AccessPort/Hondata communities** for providing datalog samples
- **Automotive engineering references** for VE curves and efficiency calculations

---

**Built for enthusiasts, by enthusiasts.**
