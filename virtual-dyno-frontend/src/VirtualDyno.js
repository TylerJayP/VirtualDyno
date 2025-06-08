import React, { useState, useRef, useEffect } from 'react';

const VirtualDyno = () => {
  // Core state
  const [csvFile, setCsvFile] = useState(null);
  const [csvData, setCsvData] = useState([]);
  const [dynoResults, setDynoResults] = useState(null);
  const [isRunning, setIsRunning] = useState(false);
  const [currentDataPoint, setCurrentDataPoint] = useState(0);
  const [liveGraphData, setLiveGraphData] = useState([]);
  
  // Live stats during run
  const [currentPeaks, setCurrentPeaks] = useState({
    maxHP: 0,
    maxTorque: 0,
    maxBoost: 0,
    currentHP: 0,
    currentTorque: 0,
    currentBoost: 0
  });
  
  // Dyno settings
  const [dynoSettings, setDynoSettings] = useState({
    selectedCar: 'mazdaspeed3',
    gear: 4,
    dynoType: 'mustang_md250',
    weight: 3200,
    temperature: 75,
    humidity: 45
  });

  // Smoothing state
  const [smoothingLevel, setSmoothingLevel] = useState(0);
  const [smoothedData, setSmoothedData] = useState([]);
  const [smoothedPeaks, setSmoothedPeaks] = useState(null);

  // Debug state
  const [debugInfo, setDebugInfo] = useState({
    lastSmoothingCall: null,
    apiResponse: null,
    dataProcessing: null,
    errors: []
  });

  // Refs
  const fileInputRef = useRef(null);
  const canvasRef = useRef(null);

  // Configuration data
  const carOptions = [
    { value: 'mazdaspeed3', label: 'Mazdaspeed3' },
    { value: 'wrx', label: 'Subaru WRX' },
    { value: 'sti', label: 'Subaru STI' },
    { value: 'evo', label: 'Mitsubishi Evo' },
    { value: 'gti', label: 'VW Golf GTI' },
    { value: 'focus_st', label: 'Ford Focus ST' }
  ];

  const dynoTypes = {
    mustang_md250: { name: "Mustang MD250", correction: 1.0, variance: 0.02 },
    dynojet_248c: { name: "DynoJet 248C", correction: 1.15, variance: 0.03 },
    awd_dyno: { name: "AWD Dyno", correction: 0.95, variance: 0.04 }
  };

  const smoothingOptions = [
    { value: 0, label: 'Raw Data (0)' },
    { value: 1, label: 'Light (1)' },
    { value: 2, label: 'Moderate (2)' },
    { value: 3, label: 'Smooth (3)' },
    { value: 4, label: 'Heavy (4)' },
    { value: 5, label: 'Maximum (5)' }
  ];

  // Helper functions
  const calculatePeaksFromData = (data) => {
    if (!data || data.length === 0) return null;
    
    const maxHP = Math.max(...data.map(d => d.horsepower));
    const maxTorque = Math.max(...data.map(d => d.torque));
    const maxBoost = Math.max(...data.map(d => d.boost));
    const maxHPPoint = data.find(d => d.horsepower === maxHP);
    const maxTorquePoint = data.find(d => d.torque === maxTorque);
    
    return {
      maxHP,
      maxTorque,
      maxBoost,
      maxHPRpm: maxHPPoint?.rpm || 0,
      maxTorqueRpm: maxTorquePoint?.rpm || 0
    };
  };

  const addError = (error) => {
    console.error('VirtualDyno Error:', error);
    setDebugInfo(prev => ({
      ...prev,
      errors: [...prev.errors.slice(-4), { // Keep last 5 errors
        timestamp: new Date().toLocaleTimeString(),
        message: error.toString(),
        stack: error.stack
      }]
    }));
  };

  // File upload handler
  const handleFileUpload = (event) => {
    try {
      const file = event.target.files[0];
      if (!file) return;
    
      setCsvFile(file);
      setDynoResults(null);
      setLiveGraphData([]);
      setSmoothedData([]);
      setSmoothingLevel(0);
      
      const reader = new FileReader();
      reader.onload = (e) => {
        try {
          const csv = e.target.result;
          const lines = csv.split('\n');
          const headers = lines[0].split(',').map(h => h.trim());
          
          console.log('📄 CSV headers found:', headers);
          
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
              const rpm = parseInt(row['RPM (RPM)'] || row['RPM'] || row['Engine Speed'] || 0);
              const maf = parseFloat(row['Mass Airflow (g/s)'] || row['MAF'] || row['Mass Airflow'] || 0);
              const load = parseFloat(row['Calculated Load (Load)'] || row['Load'] || row['Engine Load'] || 0);
              
              // More lenient filtering for broader compatibility
              return rpm > 1500 && (maf > 3 || load > 0.10);
            });
          
          setCsvData(data);
          console.log(`✅ Loaded ${data.length} valid data points from CSV`);
          
        } catch (error) {
          addError(new Error(`CSV parsing failed: ${error.message}`));
          alert('Error parsing CSV file. Please check the file format.');
        }
      };
      
      reader.onerror = () => {
        addError(new Error('File reading failed'));
        alert('Error reading file.');
      };
      
      reader.readAsText(file);
      
    } catch (error) {
      addError(error);
      alert('Error handling file upload.');
    }
  };

  // Fixed backend communication
  const sendToBackend = async () => {
    if (!csvFile) {
      throw new Error('No CSV file selected');
    }
    
    try {
      console.log('🚀 Sending data to backend...');
      
      const formData = new FormData();
      formData.append('File', csvFile);
      formData.append('CarPresetKey', dynoSettings.selectedCar);
      formData.append('Weight', dynoSettings.weight.toString());
      formData.append('Gear', dynoSettings.gear.toString());
      formData.append('Notes', `${dynoSettings.dynoType}, ${dynoSettings.temperature}°F, ${dynoSettings.humidity}% humidity`);
      formData.append('IsPublic', 'false');
      
      console.log('📤 FormData contents:', {
        file: csvFile.name,
        carPresetKey: dynoSettings.selectedCar,
        weight: dynoSettings.weight,
        gear: dynoSettings.gear
      });
      
      const response = await fetch('http://localhost:5038/api/dyno/upload', {
        method: 'POST',
        body: formData,
        // Don't set Content-Type header - let browser set it with boundary
      });
      
      console.log('📨 Backend response status:', response.status, response.statusText);
      
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Backend API error (${response.status}): ${errorText}`);
      }
      
      const backendResults = await response.json();
      console.log('✅ Backend results received:', backendResults);
      
      return backendResults;
      
    } catch (error) {
      if (error.name === 'TypeError' && error.message.includes('fetch')) {
        throw new Error('Cannot connect to backend API. Make sure your C# API is running on localhost:5038');
      }
      throw error;
    }
  };

  // Get detailed run data from backend
  const getDetailedRunData = async (runId) => {
    try {
      console.log(`📊 Fetching detailed data for run ${runId}...`);
      
      const response = await fetch(`http://localhost:5038/api/dyno/runs/${runId}`);
      
      if (!response.ok) {
        throw new Error(`Failed to get detailed run data: ${response.status} ${response.statusText}`);
      }
      
      const detailData = await response.json();
      console.log(`✅ Got ${detailData.dataPoints?.length || 0} detailed data points`);
      
      return detailData;
      
    } catch (error) {
      throw new Error(`Error fetching detailed data: ${error.message}`);
    }
  };

  // Smoothing handler
  const handleSmoothingChange = async (newLevel) => {
    if (!dynoResults || !dynoResults.processedData) {
      console.warn('⚠️ No dyno results available for smoothing');
      return;
    }
    
    const debugStart = Date.now();
    console.log(`🔧 Applying smoothing level ${newLevel}...`);
    
    setDebugInfo(prev => ({
      ...prev,
      lastSmoothingCall: { 
        level: newLevel, 
        timestamp: new Date().toLocaleTimeString() 
      }
    }));
    
    setSmoothingLevel(newLevel);
    
    try {
      if (newLevel === 0) {
        // Raw data - use original processed data
        const dataToProcess = dynoResults.processedData.map(d => ({
          rpm: d.rpm,
          horsepower: Math.round(d.hp),
          torque: Math.round(d.torque),
          boost: Math.round(d.boost * 10) / 10
        }));
        
        console.log(`📊 Using raw data: ${dataToProcess.length} points`);
        
        setSmoothedData(dataToProcess);
        const rawPeaks = calculatePeaksFromData(dataToProcess);
        setSmoothedPeaks(rawPeaks);
        
        setDebugInfo(prev => ({
          ...prev,
          dataProcessing: { 
            source: 'raw', 
            points: dataToProcess.length, 
            peaks: rawPeaks,
            processingTime: Date.now() - debugStart
          }
        }));
        
      } else {
        // Apply smoothing via backend API
        const apiUrl = `http://localhost:5038/api/dyno/runs/${dynoResults.backendResults.id}/smooth/${newLevel}`;
        console.log(`🌐 Fetching smoothed data: ${apiUrl}`);
        
        const response = await fetch(apiUrl);
        
        setDebugInfo(prev => ({
          ...prev,
          apiResponse: { 
            url: apiUrl, 
            status: response.status, 
            ok: response.ok,
            timestamp: new Date().toLocaleTimeString()
          }
        }));
        
        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(`Smoothing API error (${response.status}): ${errorText}`);
        }
        
        const smoothed = await response.json();
        console.log(`✅ Received ${smoothed.length} smoothed points`);
        
        const dataToProcess = smoothed.map(d => ({
          rpm: d.rpm,
          horsepower: Math.round(d.horsepower),
          torque: Math.round(d.torque),
          boost: Math.round(d.boost * 10) / 10
        }));
        
        setSmoothedData(dataToProcess);
        const smoothedPeaksCalc = calculatePeaksFromData(dataToProcess);
        setSmoothedPeaks(smoothedPeaksCalc);
        
        // Calculate changes from original
        const originalPeaks = dynoResults.peaks;
        const changes = {
          hp: smoothedPeaksCalc.maxHP - originalPeaks.maxHP,
          tq: smoothedPeaksCalc.maxTorque - originalPeaks.maxTorque,
          boost: smoothedPeaksCalc.maxBoost - (originalPeaks.maxBoost || 0)
        };
        
        setDebugInfo(prev => ({
          ...prev,
          dataProcessing: { 
            source: 'smoothed', 
            points: dataToProcess.length, 
            peaks: smoothedPeaksCalc,
            processingTime: Date.now() - debugStart,
            changes
          }
        }));
        
        console.log(`🏁 Smoothing results for level ${newLevel}:`);
        console.log(`  HP: ${originalPeaks.maxHP} → ${smoothedPeaksCalc.maxHP} (${changes.hp >= 0 ? '+' : ''}${changes.hp})`);
        console.log(`  TQ: ${originalPeaks.maxTorque} → ${smoothedPeaksCalc.maxTorque} (${changes.tq >= 0 ? '+' : ''}${changes.tq})`);
        console.log(`  Boost: ${originalPeaks.maxBoost || 0} → ${smoothedPeaksCalc.maxBoost} (should be same)`);
      }
      
    } catch (error) {
      addError(error);
      alert(`Error applying smoothing: ${error.message}`);
    }
  };

  // Main dyno run function
  const runVirtualDyno = async () => {
    if (!csvData.length) {
      alert('Please upload a CSV file first!');
      return;
    }
    
    console.log('🏁 Starting virtual dyno run...');
    
    // Reset state
    setIsRunning(true);
    setCurrentDataPoint(0);
    setLiveGraphData([]);
    setSmoothedData([]);
    setSmoothingLevel(0);
    setCurrentPeaks({
      maxHP: 0,
      maxTorque: 0,
      maxBoost: 0,
      currentHP: 0,
      currentTorque: 0,
      currentBoost: 0
    });
    
    try {
      // Step 1: Send data to backend
      const backendResults = await sendToBackend();
      
      // Step 2: Get detailed data points
      const detailData = await getDetailedRunData(backendResults.id);
      
      // Step 3: Process data with virtual dyno characteristics
      const dyno = dynoTypes[dynoSettings.dynoType];
      const tempCorrection = Math.sqrt(537.67 / (dynoSettings.temperature + 459.67));
      const humidityCorrection = 1 - (dynoSettings.humidity / 100 * 0.047);
      const envCorrection = tempCorrection * humidityCorrection;
      
      const processedData = (detailData.dataPoints || []).map(point => {
        const variance = 1 + (Math.random() - 0.5) * 2 * dyno.variance;
        
        return {
          rpm: point.rpm,
          hp: point.horsepower * envCorrection * dyno.correction * variance,
          torque: point.torque * envCorrection * variance,
          boost: point.boost,
          maf: point.massAirflow || 0,
          load: point.load || 0
        };
      });
      
      // Sort by RPM for proper animation
      processedData.sort((a, b) => a.rpm - b.rpm);
      
      console.log(`🎬 Starting animation with ${processedData.length} points`);
      
      // Step 4: Animate the dyno run
      for (let i = 0; i < processedData.length; i++) {
        const currentPoint = processedData[i];
        
        // Add point to live graph
        setLiveGraphData(prev => {
          const newData = [...prev, {
            rpm: currentPoint.rpm,
            horsepower: Math.round(currentPoint.hp),
            torque: Math.round(currentPoint.torque),
            boost: Math.round(currentPoint.boost * 10) / 10
          }];
          return newData.sort((a, b) => a.rpm - b.rpm);
        });
        
        // Update live peaks
        setCurrentPeaks(prev => {
          const maxHP = Math.max(prev.maxHP, currentPoint.hp);
          const maxTorque = Math.max(prev.maxTorque, currentPoint.torque);
          const maxBoost = Math.max(prev.maxBoost, currentPoint.boost);
          
          return {
            maxHP: Math.round(maxHP),
            maxTorque: Math.round(maxTorque),
            maxBoost: Math.round(maxBoost * 10) / 10,
            currentHP: Math.round(currentPoint.hp),
            currentTorque: Math.round(currentPoint.torque),
            currentBoost: Math.round(currentPoint.boost * 10) / 10
          };
        });
        
        setCurrentDataPoint(i);
        
        // Variable delay based on RPM
        const delay = currentPoint.rpm < 3000 ? 200 : 
                     currentPoint.rpm < 4000 ? 150 : 
                     currentPoint.rpm < 5000 ? 120 : 100;
        
        await new Promise(resolve => setTimeout(resolve, delay));
      }
      
      // Step 5: Calculate final results
      const peaks = {
        maxHP: Math.round(Math.max(...processedData.map(d => d.hp))),
        maxTorque: Math.round(Math.max(...processedData.map(d => d.torque))),
        maxBoost: Math.round(Math.max(...processedData.map(d => d.boost)) * 10) / 10,
        maxHPRpm: processedData.find(d => d.hp === Math.max(...processedData.map(p => p.hp)))?.rpm || 0,
        maxTorqueRpm: processedData.find(d => d.torque === Math.max(...processedData.map(p => p.torque)))?.rpm || 0
      };
      
      const finalResults = {
        processedData,
        peaks,
        settings: dynoSettings,
        fileName: csvFile.name,
        backendResults
      };
      
      setDynoResults(finalResults);
      
      // Initialize with raw data
      handleSmoothingChange(0);
      
      console.log('🏆 Dyno run completed successfully!');
      console.log('Peak Results:', peaks);
      
    } catch (error) {
      addError(error);
      alert(`Dyno run failed: ${error.message}`);
    } finally {
      setIsRunning(false);
    }
  };

  // Canvas drawing effect
  useEffect(() => {
    if (!canvasRef.current) return;

    // Determine data source
    let dataToDisplay;
    let dataSource;
    
    if (isRunning && liveGraphData.length > 0) {
      dataToDisplay = liveGraphData;
      dataSource = "live";
    } else if (smoothedData.length > 0) {
      dataToDisplay = smoothedData;
      dataSource = `smoothed-L${smoothingLevel}`;
    } else if (dynoResults?.processedData) {
      dataToDisplay = dynoResults.processedData.map(d => ({
        rpm: d.rpm,
        horsepower: Math.round(d.hp),
        torque: Math.round(d.torque),
        boost: Math.round(d.boost * 10) / 10
      }));
      dataSource = "processed";
    } else {
      return;
    }

    console.log(`🎨 Drawing ${dataToDisplay.length} points from ${dataSource}`);

    const canvas = canvasRef.current;
    const ctx = canvas.getContext('2d');
    
    // Setup canvas
    const rect = canvas.getBoundingClientRect();
    const devicePixelRatio = window.devicePixelRatio || 1;
    canvas.width = rect.width * devicePixelRatio;
    canvas.height = rect.height * devicePixelRatio;
    ctx.scale(devicePixelRatio, devicePixelRatio);
    
    const width = rect.width;
    const height = rect.height;

    // Clear canvas
    ctx.fillStyle = '#1a1a1a';
    ctx.fillRect(0, 0, width, height);

    // Graph dimensions
    const leftPadding = 70;
    const rightPadding = 40;
    const topPadding = 40;
    const bottomPadding = 60;
    const graphWidth = width - leftPadding - rightPadding;
    const graphHeight = height - topPadding - bottomPadding;

    // Data ranges
    const maxRpm = Math.max(...dataToDisplay.map(d => d.rpm), 7000);
    const minRpm = Math.min(...dataToDisplay.map(d => d.rpm), 2000);
    const maxHP = Math.max(...dataToDisplay.map(d => d.horsepower), 100);
    const maxTorque = Math.max(...dataToDisplay.map(d => d.torque), 100);
    const maxPower = Math.max(maxHP, maxTorque);

    // Draw grid
    ctx.strokeStyle = '#333';
    ctx.lineWidth = 1;
    
    // Vertical grid lines
    for (let rpm = Math.ceil(minRpm / 500) * 500; rpm <= maxRpm; rpm += 500) {
      const x = leftPadding + ((rpm - minRpm) / (maxRpm - minRpm)) * graphWidth;
      ctx.beginPath();
      ctx.moveTo(x, topPadding);
      ctx.lineTo(x, height - bottomPadding);
      ctx.stroke();
      
      ctx.fillStyle = '#666';
      ctx.font = `${Math.max(8, width * 0.010)}px Arial`;
      ctx.textAlign = 'center';
      ctx.fillText(rpm.toString(), x, height - bottomPadding + 20);
    }

    // Horizontal grid lines
    for (let power = 0; power <= maxPower; power += 50) {
      const y = height - bottomPadding - (power / maxPower) * graphHeight;
      ctx.beginPath();
      ctx.moveTo(leftPadding, y);
      ctx.lineTo(width - rightPadding, y);
      ctx.stroke();
      
      ctx.fillStyle = '#666';
      ctx.font = `${Math.max(8, width * 0.010)}px Arial`;
      ctx.textAlign = 'right';
      ctx.fillText(power.toString(), leftPadding - 10, y + 5);
    }

    // Draw axes
    ctx.strokeStyle = '#666';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(leftPadding, topPadding);
    ctx.lineTo(leftPadding, height - bottomPadding);
    ctx.lineTo(width - rightPadding, height - bottomPadding);
    ctx.stroke();

    // Draw HP line
    if (dataToDisplay.length > 1) {
      ctx.strokeStyle = '#ff4444';
      ctx.lineWidth = dataSource.includes('smoothed') ? 4 : 3;
      ctx.beginPath();
      
      dataToDisplay.forEach((point, index) => {
        const x = leftPadding + ((point.rpm - minRpm) / (maxRpm - minRpm)) * graphWidth;
        const y = height - bottomPadding - (point.horsepower / maxPower) * graphHeight;
        
        if (index === 0) {
          ctx.moveTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }
      });
      ctx.stroke();
    }

    // Draw Torque line
    if (dataToDisplay.length > 1) {
      ctx.strokeStyle = '#44ff44';
      ctx.lineWidth = dataSource.includes('smoothed') ? 4 : 3;
      ctx.beginPath();
      
      dataToDisplay.forEach((point, index) => {
        const x = leftPadding + ((point.rpm - minRpm) / (maxRpm - minRpm)) * graphWidth;
        const y = height - bottomPadding - (point.torque / maxPower) * graphHeight;
        
        if (index === 0) {
          ctx.moveTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }
      });
      ctx.stroke();
    }

    // Current point indicator during live run
    if (isRunning && liveGraphData.length > 0) {
      const currentPoint = liveGraphData[liveGraphData.length - 1];
      const x = leftPadding + ((currentPoint.rpm - minRpm) / (maxRpm - minRpm)) * graphWidth;
      const yHP = height - bottomPadding - (currentPoint.horsepower / maxPower) * graphHeight;
      const yTQ = height - bottomPadding - (currentPoint.torque / maxPower) * graphHeight;
      
      const indicatorSize = 5;
      
      ctx.fillStyle = '#ff4444';
      ctx.beginPath();
      ctx.arc(x, yHP, indicatorSize, 0, 2 * Math.PI);
      ctx.fill();
      
      ctx.fillStyle = '#44ff44';
      ctx.beginPath();
      ctx.arc(x, yTQ, indicatorSize, 0, 2 * Math.PI);
      ctx.fill();
    }

    // Legend
    const legendFontSize = Math.max(8, width * 0.010);
    ctx.fillStyle = '#ff4444';
    ctx.font = `bold ${legendFontSize}px Arial`;
    ctx.textAlign = 'left';
    ctx.fillText('■ Horsepower', width - 160, 25);
    
    ctx.fillStyle = '#44ff44';
    ctx.fillText('■ Torque', width - 160, 25 + legendFontSize + 6);
    
    ctx.fillStyle = '#a0aec0';
    ctx.font = `${Math.max(6, width * 0.008)}px Arial`;
    ctx.fillText(`Data: ${dataSource}`, width - 160, 25 + (legendFontSize + 6) * 2);

    // Axis labels
    const axisLabelSize = Math.max(8, width * 0.010);
    ctx.fillStyle = '#ccc';
    ctx.font = `bold ${axisLabelSize}px Arial`;
    ctx.textAlign = 'center';
    ctx.fillText('RPM', width / 2, height - 15);
    
    ctx.save();
    ctx.translate(20, height / 2);
    ctx.rotate(-Math.PI / 2);
    ctx.textAlign = 'center';
    ctx.fillText('HP / TQ', 0, 0);
    ctx.restore();

  }, [liveGraphData, smoothedData, smoothingLevel, isRunning, dynoResults]);

  // Styles
  const inputStyle = {
    width: '95%',
    padding: '8px 12px',
    border: '1px solid #4a5568',
    borderRadius: '4px',
    fontSize: '14px',
    backgroundColor: '#2d3748',
    color: '#e2e8f0'
  };

  const labelStyle = {
    display: 'block',
    fontSize: '14px',
    fontWeight: '600',
    color: '#e2e8f0',
    marginBottom: '6px'
  };

  const sectionStyle = {
    marginBottom: '20px',
    padding: '15px',
    backgroundColor: '#2d3748',
    borderRadius: '6px',
    border: '1px solid #4a5568'
  };

  return (
    <div style={{ 
      minHeight: '100vh',
      backgroundColor: '#0f1419',
      fontFamily: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
    }}>
      
      {/* Debug Panel */}
      {debugInfo.lastSmoothingCall && (
        <div style={{
          position: 'fixed',
          top: '10px',
          right: '10px',
          background: 'rgba(0,0,0,0.9)',
          color: 'white',
          padding: '12px',
          borderRadius: '6px',
          fontSize: '10px',
          fontFamily: 'monospace',
          zIndex: 1000,
          maxWidth: '320px',
          border: '1px solid #4a5568'
        }}>
          <div style={{ color: '#68d391', fontWeight: 'bold', marginBottom: '6px' }}>
            🔧 Debug Panel
          </div>
          
          <div><strong>Smoothing:</strong> L{debugInfo.lastSmoothingCall.level} at {debugInfo.lastSmoothingCall.timestamp}</div>
          
          {debugInfo.apiResponse && (
            <div><strong>API:</strong> {debugInfo.apiResponse.status} {debugInfo.apiResponse.ok ? '✅' : '❌'}</div>
          )}
          
          {debugInfo.dataProcessing && (
            <div>
              <div><strong>Source:</strong> {debugInfo.dataProcessing.source}</div>
              <div><strong>Points:</strong> {debugInfo.dataProcessing.points}</div>
              {debugInfo.dataProcessing.peaks && (
                <div><strong>Peaks:</strong> HP={debugInfo.dataProcessing.peaks.maxHP}, TQ={debugInfo.dataProcessing.peaks.maxTorque}</div>
              )}
              {debugInfo.dataProcessing.changes && (
                <div style={{ color: '#68d391' }}>
                  <strong>Changes:</strong> HP{debugInfo.dataProcessing.changes.hp >= 0 ? '+' : ''}{debugInfo.dataProcessing.changes.hp}, 
                  TQ{debugInfo.dataProcessing.changes.tq >= 0 ? '+' : ''}{debugInfo.dataProcessing.changes.tq}
                </div>
              )}
              <div><strong>Time:</strong> {debugInfo.dataProcessing.processingTime}ms</div>
            </div>
          )}
          
          <div style={{ marginTop: '6px' }}>
            <div><strong>State:</strong> Level={smoothingLevel}, Data={smoothedData.length}, Peaks={smoothedPeaks ? 'yes' : 'no'}</div>
          </div>
          
          {debugInfo.errors.length > 0 && (
            <div style={{ marginTop: '6px', color: '#ff6b6b' }}>
              <div><strong>Last Error:</strong></div>
              <div style={{ fontSize: '9px' }}>{debugInfo.errors[debugInfo.errors.length - 1]?.message}</div>
            </div>
          )}
        </div>
      )}

      {/* Header */}
      <div style={{
        backgroundColor: '#1a2332',
        color: 'white',
        padding: '16px 0',
        textAlign: 'center',
        boxShadow: '0 2px 10px rgba(0,0,0,0.3)',
        borderBottom: '1px solid #2d3748'
      }}>
        <h1 style={{ 
          margin: 0, 
          fontSize: '24px', 
          fontWeight: '700',
          letterSpacing: '-0.5px',
          background: 'linear-gradient(135deg, #ff4444, #44ff44, #4488ff)',
          WebkitBackgroundClip: 'text',
          WebkitTextFillColor: 'transparent',
          backgroundClip: 'text'
        }}>
          Tyler's Virtual Dyno
        </h1>
        <p style={{ 
          margin: '6px 0 0 0', 
          fontSize: '14px', 
          opacity: 0.8,
          fontWeight: '400',
          color: '#a0aec0'
        }}>
          Personal Dyno Analysis & Simulation
        </p>
      </div>

      {/* Main Content Grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '260px 1fr',
        height: 'calc(100vh - 100px)',
        gap: '0'
      }}>
        
        {/* Left Panel - Settings */}
        <div style={{
          backgroundColor: '#1a202c',
          borderRight: '1px solid #2d3748',
          padding: '40px 10px',
          overflowY: 'auto'
        }}>
          
          {/* File Upload */}
          <div style={sectionStyle}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '12px' }}>
              <label style={{ ...labelStyle, margin: 0, flex: 1 }}>Upload Datalog</label>
              <button 
                onClick={() => fileInputRef.current?.click()}
                style={{
                  padding: '8px 16px',
                  backgroundColor: csvFile ? '#38a169' : '#3182ce',
                  color: 'white',
                  border: 'none',
                  borderRadius: '4px',
                  cursor: 'pointer',
                  fontSize: '14px',
                  fontWeight: '500',
                  transition: 'all 0.2s ease'
                }}
              >
                {csvFile ? '✅ Change' : '📂 Select'}
              </button>
              <input 
                type="file" 
                accept=".csv"
                onChange={handleFileUpload}
                ref={fileInputRef}
                style={{ display: 'none' }}
              />
            </div>
            
            {csvFile && (
              <div style={{
                fontSize: '12px',
                color: '#a0aec0',
                backgroundColor: '#2d3748',
                padding: '8px',
                borderRadius: '4px',
                border: '1px solid #4a5568',
                wordBreak: 'break-all'
              }}>
                📄 {csvFile.name} ({csvData.length} valid points)
              </div>
            )}
          </div>

          {/* Vehicle Settings */}
          <div style={sectionStyle}>
            <h3 style={{ margin: '0 0 14px 0', fontSize: '15px', color: '#68d391' }}>Vehicle Setup</h3>
            
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
              <label style={{ ...labelStyle, margin: 0, minWidth: '70px', fontSize: '13px' }}>Car:</label>
              <select 
                value={dynoSettings.selectedCar}
                onChange={(e) => setDynoSettings(prev => ({ ...prev, selectedCar: e.target.value }))}
                style={{ ...inputStyle, flex: 1, fontSize: '13px' }}
              >
                {carOptions.map(car => (
                  <option key={car.value} value={car.value}>{car.label}</option>
                ))}
              </select>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ ...labelStyle, fontSize: '13px' }}>Gear Used:</label>
              <div style={{ display: 'flex', gap: '6px' }}>
                {[3, 4, 5].map(gear => (
                  <button
                    key={gear}
                    onClick={() => setDynoSettings(prev => ({ ...prev, gear }))}
                    style={{
                      flex: 1,
                      padding: '8px',
                      border: dynoSettings.gear === gear ? '2px solid #3182ce' : '1px solid #4a5568',
                      borderRadius: '4px',
                      backgroundColor: dynoSettings.gear === gear ? '#2c5282' : '#2d3748',
                      color: dynoSettings.gear === gear ? 'white' : '#a0aec0',
                      cursor: 'pointer',
                      fontSize: '13px',
                      fontWeight: dynoSettings.gear === gear ? '600' : '400',
                      transition: 'all 0.2s ease'
                    }}
                  >
                    {gear}rd
                  </button>
                ))}
              </div>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ ...labelStyle, fontSize: '13px' }}>Vehicle Weight (lbs):</label>
              <input
                type="number"
                value={dynoSettings.weight}
                onChange={(e) => setDynoSettings(prev => ({ ...prev, weight: parseInt(e.target.value) || 0 }))}
                style={{ ...inputStyle, fontSize: '13px', padding: '6px 8px' }}
                min="1000"
                max="10000"
                step="50"
              />
            </div>
          </div>

          {/* Dyno Settings */}
          <div style={sectionStyle}>
            <h3 style={{ margin: '0 0 14px 0', fontSize: '15px', color: '#68d391' }}>Dyno Type</h3>
            
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
              <label style={{ ...labelStyle, margin: 0, minWidth: '70px', fontSize: '13px' }}>Type:</label>
              <select 
                value={dynoSettings.dynoType}
                onChange={(e) => setDynoSettings(prev => ({ ...prev, dynoType: e.target.value }))}
                style={{ ...inputStyle, flex: 1, fontSize: '13px' }}
              >
                {Object.entries(dynoTypes).map(([key, dyno]) => (
                  <option key={key} value={key}>{dyno.name}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Environmental */}
          <div style={sectionStyle}>
            <h3 style={{ margin: '0 0 14px 0', fontSize: '15px', color: '#68d391' }}>Environment</h3>
            
            <div style={{ marginBottom: '14px' }}>
              <label style={{ ...labelStyle, fontSize: '13px' }}>Temperature: {dynoSettings.temperature}°F</label>
              <input 
                type="range" 
                min="50" 
                max="110" 
                value={dynoSettings.temperature}
                onChange={(e) => setDynoSettings(prev => ({ ...prev, temperature: parseInt(e.target.value) }))}
                style={{ width: '100%', margin: '4px 0' }}
              />
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '11px', color: '#718096' }}>
                <span>50°F</span><span>110°F</span>
              </div>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ ...labelStyle, fontSize: '13px' }}>Humidity: {dynoSettings.humidity}%</label>
              <input 
                type="range" 
                min="10" 
                max="90" 
                value={dynoSettings.humidity}
                onChange={(e) => setDynoSettings(prev => ({ ...prev, humidity: parseInt(e.target.value) }))}
                style={{ width: '100%', margin: '4px 0' }}
              />
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '11px', color: '#718096' }}>
                <span>10%</span><span>90%</span>
              </div>
            </div>
          </div>
        </div>

        {/* Right Panel - Graph Area */}
        <div style={{
          backgroundColor: '#1a202c',
          padding: '20px',
          display: 'flex',
          flexDirection: 'column',
          position: 'relative'
        }}>
          
          {(isRunning || dynoResults) ? (
            <div>
              {/* Stats Display */}
              <div style={{
                backgroundColor: '#1a1a1a',
                color: 'white',
                padding: '16px',
                borderRadius: '8px',
                marginBottom: '16px',
                display: 'grid',
                gridTemplateColumns: '1fr 1fr 1fr',
                gap: '16px',
                textAlign: 'center'
              }}>
                {/* HP */}
                <div>
                  <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#ff4444' }}>
                    {(() => {
                      if (isRunning) return currentPeaks.currentHP;
                      if (smoothingLevel > 0 && smoothedPeaks) return smoothedPeaks.maxHP;
                      return dynoResults?.peaks?.maxHP || 0;
                    })()}
                  </div>
                  <div style={{ fontSize: '11px', color: '#ccc', marginBottom: '3px' }}>
                    {isRunning ? 'CURRENT' : 'PEAK'} HP
                    {!isRunning && smoothingLevel > 0 && (
                      <span style={{ color: '#68d391', marginLeft: '4px' }}>(S{smoothingLevel})</span>
                    )}
                  </div>
                  {!isRunning && (
                    <div style={{ fontSize: '9px', color: '#888' }}>
                      @ {smoothingLevel > 0 && smoothedPeaks ? smoothedPeaks.maxHPRpm : (dynoResults?.peaks?.maxHPRpm || 0)} RPM
                      {smoothingLevel > 0 && smoothedPeaks && dynoResults?.peaks && (
                        <div style={{ color: smoothedPeaks.maxHP > dynoResults.peaks.maxHP ? '#68d391' : '#ff6b6b' }}>
                          {smoothedPeaks.maxHP > dynoResults.peaks.maxHP ? '+' : ''}{smoothedPeaks.maxHP - dynoResults.peaks.maxHP} HP
                        </div>
                      )}
                    </div>
                  )}
                </div>
                
                {/* Torque */}
                <div>
                  <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#44ff44' }}>
                    {(() => {
                      if (isRunning) return currentPeaks.currentTorque;
                      if (smoothingLevel > 0 && smoothedPeaks) return smoothedPeaks.maxTorque;
                      return dynoResults?.peaks?.maxTorque || 0;
                    })()}
                  </div>
                  <div style={{ fontSize: '11px', color: '#ccc', marginBottom: '3px' }}>
                    {isRunning ? 'CURRENT' : 'PEAK'} LB-FT
                    {!isRunning && smoothingLevel > 0 && (
                      <span style={{ color: '#68d391', marginLeft: '4px' }}>(S{smoothingLevel})</span>
                    )}
                  </div>
                  {!isRunning && (
                    <div style={{ fontSize: '9px', color: '#888' }}>
                      @ {smoothingLevel > 0 && smoothedPeaks ? smoothedPeaks.maxTorqueRpm : (dynoResults?.peaks?.maxTorqueRpm || 0)} RPM
                      {smoothingLevel > 0 && smoothedPeaks && dynoResults?.peaks && (
                        <div style={{ color: smoothedPeaks.maxTorque > dynoResults.peaks.maxTorque ? '#68d391' : '#ff6b6b' }}>
                          {smoothedPeaks.maxTorque > dynoResults.peaks.maxTorque ? '+' : ''}{smoothedPeaks.maxTorque - dynoResults.peaks.maxTorque} TQ
                        </div>
                      )}
                    </div>
                  )}
                </div>
                
                {/* Boost */}
                <div>
                  <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#4488ff' }}>
                    {(() => {
                      if (isRunning) return currentPeaks.maxBoost;
                      // Always show original boost (never smoothed)
                      const originalBoost = Math.max(...(dynoResults?.processedData?.map(d => d.boost) || [0]));
                      return Math.round(originalBoost * 10) / 10;
                    })()}
                  </div>
                  <div style={{ fontSize: '11px', color: '#ccc', marginBottom: '3px' }}>
                    PEAK BOOST PSI
                    {!isRunning && smoothingLevel > 0 && (
                      <span style={{ color: '#a0aec0', marginLeft: '4px' }}>(Raw)</span>
                    )}
                  </div>
                  <div style={{ fontSize: '9px', color: '#888' }}>
                    {isRunning ? 'LIVE' : 'SENSOR DATA'}
                  </div>
                </div>
              </div>

              {/* Graph Area */}
              <div style={{ 
                flex: 1, 
                position: 'relative',
                height: 'calc(100vh - 280px)',
                minHeight: '400px',
                maxHeight: '600px'
              }}>
                {/* Smoothing Control */}
                {dynoResults && !isRunning && (
                  <div style={{
                    position: 'absolute',
                    top: '8px',
                    left: '8px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    backgroundColor: 'rgba(26, 32, 44, 0.9)',
                    padding: '6px 10px',
                    borderRadius: '6px',
                    border: '1px solid #4a5568',
                    zIndex: 10
                  }}>
                    <label style={{ fontSize: '12px', color: '#a0aec0', fontWeight: '500' }}>
                      Smoothing:
                    </label>
                    <select
                      value={smoothingLevel}
                      onChange={(e) => {
                        const newLevel = parseInt(e.target.value);
                        console.log(`🎚️ User selected smoothing level: ${newLevel}`);
                        handleSmoothingChange(newLevel);
                      }}
                      style={{
                        padding: '4px 8px',
                        border: '1px solid #4a5568',
                        borderRadius: '4px',
                        fontSize: '12px',
                        backgroundColor: '#1a202c',
                        color: '#e2e8f0'
                      }}
                    >
                      {smoothingOptions.map(option => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </div>
                )}
                
                <canvas
                  ref={canvasRef}
                  style={{
                    width: '100%',
                    height: '100%',
                    border: '1px solid #4a5568',
                    borderRadius: '6px',
                    backgroundColor: '#1a1a1a'
                  }}
                />
              </div>
            </div>
          ) : (
            <div style={{
              flex: 1,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              backgroundColor: '#2d3748',
              borderRadius: '8px',
              border: '2px dashed #4a5568',
              minHeight: '400px'
            }}>
              <div style={{ textAlign: 'center', color: '#a0aec0' }}>
                <div style={{ fontSize: '48px', marginBottom: '16px' }}>📊</div>
                <h3 style={{ margin: '0 0 8px 0', fontSize: '18px', color: '#68d391' }}>
                  Ready for Dyno Run
                </h3>
                <p style={{ margin: '0 0 20px 0', fontSize: '14px' }}>
                  Upload a datalog file and configure your settings to begin
                </p>
                
                <button 
                  onClick={runVirtualDyno}
                  disabled={!csvData.length || isRunning}
                  style={{
                    padding: '16px 32px',
                    backgroundColor: isRunning ? '#4a5568' : (!csvData.length ? '#4a5568' : '#e53e3e'),
                    color: 'white',
                    border: 'none',
                    borderRadius: '8px',
                    fontSize: '16px',
                    fontWeight: '700',
                    cursor: (!csvData.length || isRunning) ? 'not-allowed' : 'pointer',
                    transition: 'all 0.2s ease',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
                    minWidth: '200px'
                  }}
                >
                  {isRunning ? '🏃 PROCESSING...' : (!csvData.length ? 'Upload File First' : 'START VIRTUAL DYNO')}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default VirtualDyno;