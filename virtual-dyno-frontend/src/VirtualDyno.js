import React, { useState, useRef } from 'react';
import './VirtualDyno.css';

// Virtual Dyno Room that processes REAL user CSV files
const VirtualDyno = () => {
  const [csvFile, setCsvFile] = useState(null);
  const [csvData, setCsvData] = useState([]);
  const [dynoResults, setDynoResults] = useState(null);
  const [isRunning, setIsRunning] = useState(false);
  const [currentDataPoint, setCurrentDataPoint] = useState(0);
  const [selectedCar, setSelectedCar] = useState('mazdaspeed3');
  const [virtualDynoSettings, setVirtualDynoSettings] = useState({
    dynoType: 'mustang_md250',
    temperature: 75,
    humidity: 45,
    altitude: 500
  });
  const fileInputRef = useRef(null);

  // Dyno characteristics (no heat soak)
  const dynoTypes = {
    mustang_md250: {
      name: "Mustang MD250 (Load Bearing)",
      correction: 1.0,
      variance: 0.02
    },
    dynojet_248c: {
      name: "DynoJet 248C (Inertial)", 
      correction: 1.15,
      variance: 0.03
    },
    awd_dyno: {
      name: "AWD Dyno (All-Wheel)",
      correction: 0.95,
      variance: 0.04
    }
  };

  // Parse uploaded CSV file
  const handleFileUpload = (event) => {
    const file = event.target.files[0];
    if (!file) return;
    
    setCsvFile(file);
    
    const reader = new FileReader();
    reader.onload = (e) => {
      const csv = e.target.result;
      const lines = csv.split('\n');
      const headers = lines[0].split(',').map(h => h.trim());
      
      const data = lines.slice(1)
        .filter(line => line.trim().length > 0)
        .map(line => {
          const values = line.split(',');
          const row = {};
          headers.forEach((header, index) => {
            row[header] = values[index]?.trim();
          });
          return row;
        })
        .filter(row => {
          // Filter for valid dyno data (like your C# backend does)
          const rpm = parseInt(row['RPM (RPM)'] || row['RPM'] || row['Engine Speed']);
          const maf = parseFloat(row['Mass Airflow (g/s)'] || row['MAF'] || row['Mass Airflow']);
          const load = parseFloat(row['Calculated Load (Load)'] || row['Load'] || row['Engine Load']);
          
          return rpm > 2000 && maf > 5 && load > 0.15;
        });
      
      setCsvData(data);
      console.log(`Loaded ${data.length} valid data points from CSV`);
    };
    
    reader.readAsText(file);
  };

  // Send to your C# backend and return results
  const sendToBackend = async () => {
    if (!csvFile) return null;
    
    try {
      const formData = new FormData();
      formData.append('File', csvFile);
      formData.append('CarPresetKey', selectedCar);
      formData.append('Weight', '3200');
      formData.append('Gear', '4');
      formData.append('Notes', `Virtual dyno: ${virtualDynoSettings.dynoType}, ${virtualDynoSettings.temperature}°F`);
      formData.append('IsPublic', 'false');
      
      console.log('Sending CSV to C# backend...');
      const response = await fetch('http://localhost:5038/api/dyno/upload', {
        method: 'POST',
        body: formData
      });
      
      if (response.ok) {
        const backendResults = await response.json();
        console.log('✅ C# Backend Results:', backendResults);
        console.log('Backend peaks:', backendResults.peaks);
        console.log('Backend dataPointCount:', backendResults.dataPointCount);
        return backendResults;
      } else {
        console.error('Backend API error:', response.status, response.statusText);
        const errorText = await response.text();
        console.error('Error response:', errorText);
        return null;
      }
    } catch (error) {
      console.error('Failed to connect to C# backend:', error);
      return null;
    }
  };

  // Run virtual dyno using YOUR C# backend for calculations
  const runVirtualDyno = async () => {
    if (!csvData.length) {
      alert('Please upload a CSV file first!');
      return;
    }
    
    setIsRunning(true);
    setCurrentDataPoint(0);
    
    try {
      // 1. Send real CSV to YOUR C# backend for processing
      const backendResults = await sendToBackend();
      
      if (!backendResults) {
        alert('Could not connect to backend API. Make sure your C# API is running on localhost:5038');
        setIsRunning(false);
        return;
      }
      
      console.log('Backend response structure:', backendResults);
      
      // 2. Get the official datapoints from YOUR backend
      const detailResponse = await fetch(`http://localhost:5038/api/dyno/runs/${backendResults.id}`);
      
      if (!detailResponse.ok) {
        console.error('Failed to get detailed run data:', detailResponse.status);
        setIsRunning(false);
        return;
      }
      
      const detailData = await detailResponse.json();
      console.log('🔍 Detail data structure:', detailData);
      console.log('🔍 First data point:', detailData.dataPoints?.[0]);
      console.log('🔍 Data point properties:', Object.keys(detailData.dataPoints?.[0] || {}));
      
      // 3. Apply virtual dyno characteristics to YOUR calculated results
      const processedData = (detailData.dataPoints || []).map((point, index) => {
        // Debug logging for first few points
        if (index < 3) {
          console.log(`🔍 Point ${index}:`, point);
          console.log(`🔍 Available properties:`, Object.keys(point));
          console.log(`🔍 MAF check: massAirflow=${point.massAirflow}, MassAirflow=${point.MassAirflow}`);
          console.log(`🔍 Boost check: boost=${point.boost}, Boost=${point.Boost}`);
        }
        
        // Use YOUR backend's calculations as base, apply dyno characteristics
        const dyno = dynoTypes[virtualDynoSettings.dynoType];
        
        // Environmental effects
        const tempCorrection = Math.sqrt(537.67 / (virtualDynoSettings.temperature + 459.67));
        const humidityCorrection = 1 - (virtualDynoSettings.humidity / 100 * 0.047);
        const altitudeCorrection = 1 - (virtualDynoSettings.altitude * 0.000035);
        const envCorrection = tempCorrection * humidityCorrection * altitudeCorrection;
        
        // Dyno type correction and variance
        const variance = 1 + (Math.random() - 0.5) * 2 * dyno.variance;
        
        return {
          rpm: point.rpm || point.Rpm || 0,
          hp: (point.horsepower || point.Horsepower || 0) * envCorrection * dyno.correction * variance,
          torque: (point.torque || point.Torque || 0) * envCorrection * variance,
          boost: point.boost || point.Boost || 0,
          maf: point.massAirflow || point.MassAirflow || 0,
          load: point.load || point.Load || 0,
          backendHP: point.horsepower || point.Horsepower || 0,
          backendTorque: point.torque || point.Torque || 0
        };
      });
      
      // 4. Animate the virtual dyno run
      for (let i = 0; i < processedData.length; i++) {
        setCurrentDataPoint(i);
        // Show current point during animation
        if (i < processedData.length) {
          setDynoResults(prev => ({
            ...prev,
            currentPoint: processedData[i]
          }));
        }
        await new Promise(resolve => setTimeout(resolve, 150));
      }
      
      // 5. Calculate final peaks from virtual dyno
      const peaks = {
        maxHP: processedData.length > 0 ? Math.max(...processedData.map(d => d.hp || 0)) : 0,
        maxTorque: processedData.length > 0 ? Math.max(...processedData.map(d => d.torque || 0)) : 0,
        maxHPRpm: processedData.find(d => d.hp === Math.max(...processedData.map(p => p.hp || 0)))?.rpm || 0,
        maxTorqueRpm: processedData.find(d => d.torque === Math.max(...processedData.map(p => p.torque || 0)))?.rpm || 0,
        // Also include YOUR backend's original peaks for comparison
        backendMaxHP: backendResults.peaks?.maxHorsepower || 0,
        backendMaxTorque: backendResults.peaks?.maxTorque || 0,
        backendMaxHPRpm: backendResults.peaks?.maxHorsepowerRpm || 0,
        backendMaxTorqueRpm: backendResults.peaks?.maxTorqueRpm || 0
      };
      
      setDynoResults({
        processedData,
        peaks,
        settings: virtualDynoSettings,
        fileName: csvFile.name,
        backendResults // Store original backend results
      });
      
    } catch (error) {
      console.error('Error running virtual dyno:', error);
      alert('Error connecting to backend API. Make sure your C# API is running.');
    } finally {
      setIsRunning(false);
    }
  };

  // Reset dyno results
  const resetDyno = () => {
    setDynoResults(null);
    setCurrentDataPoint(0);
  };

  const currentData = dynoResults?.currentPoint || (dynoResults?.processedData && dynoResults.processedData[currentDataPoint]);

  return (
    <div className="virtual-dyno-container">
      <h1 className="virtual-dyno-title">🏁 Virtual Dyno Experience - Powered by Your C# Backend</h1>
      
      <div className="backend-status-alert">
        <strong>⚙️ Make sure your C# backend is running:</strong> <code>cd VirtualDyno.API && dotnet run</code>
      </div>
      
      <div className="dyno-main-grid">
        
        {/* File Upload */}
        <div className={`file-upload-section ${csvFile ? 'has-file' : ''}`}>
          <h3 className="file-upload-title">📁 Upload Your Datalog</h3>
          <input 
            type="file" 
            accept=".csv"
            onChange={handleFileUpload}
            ref={fileInputRef}
            className="hidden"
          />
          <button 
            onClick={() => fileInputRef.current.click()}
            className="file-upload-button"
          >
            {csvFile ? '✅ Change File' : '📂 Select CSV File'}
          </button>
          
          {csvFile && (
            <div className="file-info">
              <p><strong>File:</strong> {csvFile.name}</p>
              <p><strong>Data Points:</strong> {csvData.length}</p>
            </div>
          )}
        </div>

        {/* Virtual Dyno Settings */}
        <div className="settings-panel">
          <h3 className="settings-title">🚗 Vehicle Selection</h3>
          
          <label className="settings-label">Select Car:</label>
          <select 
            value={selectedCar}
            onChange={(e) => {
              setSelectedCar(e.target.value);
              setDynoResults(null); // Clear previous results
              resetDyno(); // Reset session
            }}
            className="settings-select"
          >
            <option value="mazdaspeed3">Mazdaspeed3</option>
            <option value="wrx">Subaru WRX</option>
            <option value="gti">VW GTI</option>
            <option value="focus_st">Ford Focus ST</option>
          </select>

          <h3 className="settings-title">🔧 Dyno Settings</h3>
          
          <label className="settings-label">Dyno Type:</label>
          <select 
            value={virtualDynoSettings.dynoType}
            onChange={(e) => setVirtualDynoSettings(prev => ({
              ...prev, dynoType: e.target.value
            }))}
            className="settings-select"
          >
            {Object.entries(dynoTypes).map(([key, dyno]) => (
              <option key={key} value={key}>{dyno.name}</option>
            ))}
          </select>
          
          <label className="settings-label">Temperature: {virtualDynoSettings.temperature}°F</label>
          <input 
            type="range" 
            min="50" 
            max="110" 
            value={virtualDynoSettings.temperature}
            onChange={(e) => setVirtualDynoSettings(prev => ({
              ...prev, temperature: parseInt(e.target.value)
            }))}
            className="settings-range"
          />
          
          <label className="settings-label">Humidity: {virtualDynoSettings.humidity}%</label>
          <input 
            type="range" 
            min="10" 
            max="90" 
            value={virtualDynoSettings.humidity}
            onChange={(e) => setVirtualDynoSettings(prev => ({
              ...prev, humidity: parseInt(e.target.value)
            }))}
            className="settings-range"
          />
          
          <p className="settings-info"><strong>Selected Car:</strong> {selectedCar}</p>
          <p className="settings-info"><strong>Dyno Correction:</strong> {dynoTypes[virtualDynoSettings.dynoType].correction}x</p>
        </div>

        {/* Live Dyno Display */}
        <div className={`dyno-display-panel ${isRunning ? 'running' : ''}`}>
          <h3 className="dyno-display-title">{isRunning ? 'PROCESSING...' : 'Virtual Dyno'}</h3>
          
          {isRunning && currentData && (
            <div className="live-dyno-reading">
              <div className="live-rpm">
                {currentData.rpm || 0} RPM
              </div>
              <div className="live-power">
                {(currentData.hp || 0).toFixed(1)} HP | {(currentData.torque || 0).toFixed(1)} lb-ft
              </div>
              <div className="live-details">
                Boost: {(currentData.boost || 0).toFixed(1)} PSI | MAF: {(currentData.maf || 0).toFixed(1)} g/s
              </div>
              <div className="progress-bar-container">
                <div className="progress-bar" style={{ 
                  width: `${csvData.length > 0 ? (currentDataPoint / csvData.length) * 100 : 0}%`
                }}></div>
              </div>
            </div>
          )}
          
          {dynoResults && !isRunning && (
            <div className="results-display">
              <h4 className="results-title">🏆 Virtual Dyno Results</h4>
              <div className="virtual-results">
                <p><strong>{(dynoResults.peaks?.maxHP || 0).toFixed(1)} HP</strong> @ {dynoResults.peaks?.maxHPRpm || 'N/A'} RPM</p>
                <p><strong>{(dynoResults.peaks?.maxTorque || 0).toFixed(1)} lb-ft</strong> @ {dynoResults.peaks?.maxTorqueRpm || 'N/A'} RPM</p>
                <p className="results-meta">
                  Dyno: {dynoTypes[dynoResults.settings?.dynoType]?.name || 'Unknown'}<br/>
                  Temp: {dynoResults.settings?.temperature || 'N/A'}°F, Humidity: {dynoResults.settings?.humidity || 'N/A'}%<br/>
                  Altitude: {dynoResults.settings?.altitude || 'N/A'}ft
                </p>
              </div>
              
              {dynoResults.peaks?.backendMaxHP && (
                <div className="backend-results">
                  <strong>🧮 Your C# Backend Results:</strong><br/>
                  {(dynoResults.peaks.backendMaxHP || 0).toFixed(1)} HP @ {dynoResults.peaks?.backendMaxHPRpm || 'N/A'} RPM<br/>
                  {(dynoResults.peaks.backendMaxTorque || 0).toFixed(1)} lb-ft @ {dynoResults.peaks?.backendMaxTorqueRpm || 'N/A'} RPM<br/>
                  <em>(Before virtual dyno effects)</em>
                </div>
              )}
            </div>
          )}

          {!isRunning && !dynoResults && csvData.length > 0 && (
            <div className="status-ready">
              <p><strong>Ready to Run!</strong></p>
              <p>CSV loaded with {csvData.length} data points</p>
              <p>Click "START VIRTUAL DYNO" to process with your C# backend</p>
            </div>
          )}
          
          {!isRunning && !dynoResults && csvData.length === 0 && (
            <div className="status-waiting">
              <p>Upload a CSV file to get started</p>
            </div>
          )}
        </div>
      </div>

      {/* Control Buttons */}
      <div className="controls-section">
        <button 
          onClick={runVirtualDyno}
          disabled={!csvData.length || isRunning}
          className="btn-primary"
        >
          {isRunning ? '🏃 PROCESSING...' : `🚀 START VIRTUAL DYNO`}
        </button>
        
        {dynoResults && !isRunning && (
          <button 
            onClick={resetDyno}
            className="btn-tertiary"
          >
            🔄 Reset
          </button>
        )}
      </div>

      {/* Instructions */}
      <div className="instructions-panel">
        <h4 className="instructions-title">🎮 How to Use Your Virtual Dyno:</h4>
        <ol className="instructions-list">
          <li><strong>Upload your real CSV datalog</strong> (AccessPort, Cobb, MHD, etc.)</li>
          <li><strong>Choose dyno type</strong> - Different dynos read differently</li>
          <li><strong>Set environmental conditions</strong> - Hot weather = less power</li>
          <li><strong>Watch your data process through the virtual dyno</strong> - Real-time simulation</li>
          <li><strong>Compare results</strong> - See how different dynos would read your run</li>
        </ol>
        
        <p><strong>🎯 This uses YOUR C# backend calculations</strong> with virtual dyno simulation!</p>
        
        <div className="tech-flow">
          <strong>🔧 Technical Flow:</strong><br/>
          1. Upload real CSV → 2. Send to C# backend → 3. Get your calculations → 4. Apply virtual dyno effects → 5. Immersive experience
        </div>
      </div>
    </div>
  );
};

export default VirtualDyno;