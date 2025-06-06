import React, { useState, useRef, useEffect } from 'react';

const VirtualDyno = () => {
  const [csvFile, setCsvFile] = useState(null);
  const [csvData, setCsvData] = useState([]);
  const [dynoResults, setDynoResults] = useState(null);
  const [isRunning, setIsRunning] = useState(false);
  const [currentDataPoint, setCurrentDataPoint] = useState(0);
  const [liveGraphData, setLiveGraphData] = useState([]);
  const [currentPeaks, setCurrentPeaks] = useState({
    maxHP: 0,
    maxTorque: 0,
    maxBoost: 0,
    currentHP: 0,
    currentTorque: 0,
    currentBoost: 0
  });
  
  // Dyno Settings State
  const [dynoSettings, setDynoSettings] = useState({
    selectedCar: 'mazdaspeed3',
    gear: 4,
    dynoType: 'mustang_md250',
    weight: 3200,
    temperature: 75,
    humidity: 45
  });

  // Post-run smoothing control
  const [smoothingLevel, setSmoothingLevel] = useState(0);
  const [smoothedData, setSmoothedData] = useState([]);
  const [smoothedPeaks, setSmoothedPeaks] = useState(null);

  const fileInputRef = useRef(null);
  const canvasRef = useRef(null);

  // Car options
  const carOptions = [
    { value: 'mazdaspeed3', label: 'Mazdaspeed3' },
    { value: 'wrx', label: 'Subaru WRX' },
    { value: 'sti', label: 'Subaru STI' },
    { value: 'evo', label: 'Mitsubishi Evo' },
    { value: 'gti', label: 'VW Golf GTI' },
    { value: 'focus_st', label: 'Ford Focus ST' }
  ];

  // Dyno types
  const dynoTypes = {
    mustang_md250: { name: "Mustang MD250", correction: 1.0, variance: 0.02 },
    dynojet_248c: { name: "DynoJet 248C", correction: 1.15, variance: 0.03 },
    awd_dyno: { name: "AWD Dyno", correction: 0.95, variance: 0.04 }
  };

  // Smoothing options
  const smoothingOptions = [
    { value: 0, label: 'Raw Data (0)' },
    { value: 1, label: 'Light (1)' },
    { value: 2, label: 'Moderate (2)' },
    { value: 3, label: 'Smooth (3)' },
    { value: 4, label: 'Heavy (4)' },
    { value: 5, label: 'Maximum (5)' }
  ];

  // Draw graph on canvas with improved sizing
  useEffect(() => {
    if (!canvasRef.current) return;

    const dataToDisplay = smoothedData.length > 0 ? smoothedData : liveGraphData;
    if (dataToDisplay.length === 0) return;

    const canvas = canvasRef.current;
    const ctx = canvas.getContext('2d');
    
    // Get the actual display size
    const rect = canvas.getBoundingClientRect();
    const displayWidth = rect.width;
    const displayHeight = rect.height;
    
    // Set the internal size to match the display size for crisp rendering
    const devicePixelRatio = window.devicePixelRatio || 1;
    canvas.width = displayWidth * devicePixelRatio;
    canvas.height = displayHeight * devicePixelRatio;
    
    // Scale the drawing context so everything draws at the correct size
    ctx.scale(devicePixelRatio, devicePixelRatio);
    
    // Use display size for calculations
    const width = displayWidth;
    const height = displayHeight;

    // Clear canvas
    ctx.fillStyle = '#1a1a1a';
    ctx.fillRect(0, 0, width, height);

    // Setup graph dimensions with much better proportions
    const leftPadding = 70; // Fixed larger left padding for Y-axis label
    const rightPadding = 40; // Smaller right padding
    const topPadding = 40; // Smaller top padding
    const bottomPadding = 60; // Fixed bottom padding for X-axis label
    const graphWidth = width - leftPadding - rightPadding;
    const graphHeight = height - topPadding - bottomPadding;

    // Find data ranges
    const maxRpm = Math.max(...dataToDisplay.map(d => d.rpm), 7000);
    const minRpm = Math.min(...dataToDisplay.map(d => d.rpm), 2000);
    const maxHP = Math.max(...dataToDisplay.map(d => d.horsepower), 100);
    const maxTorque = Math.max(...dataToDisplay.map(d => d.torque), 100);
    const maxPower = Math.max(maxHP, maxTorque);

    // Draw grid
    ctx.strokeStyle = '#333';
    ctx.lineWidth = 1;
    
    // Vertical grid lines (RPM)
    for (let rpm = Math.ceil(minRpm / 500) * 500; rpm <= maxRpm; rpm += 500) {
      const x = leftPadding + ((rpm - minRpm) / (maxRpm - minRpm)) * graphWidth;
      ctx.beginPath();
      ctx.moveTo(x, topPadding);
      ctx.lineTo(x, height - bottomPadding);
      ctx.stroke();
      
      // RPM labels
      ctx.fillStyle = '#666';
      ctx.font = `${Math.max(8, width * 0.010)}px Arial`;
      ctx.textAlign = 'center';
      ctx.fillText(rpm.toString(), x, height - bottomPadding + 20);
    }

    // Horizontal grid lines (Power)
    for (let power = 0; power <= maxPower; power += 50) {
      const y = height - bottomPadding - (power / maxPower) * graphHeight;
      ctx.beginPath();
      ctx.moveTo(leftPadding, y);
      ctx.lineTo(width - rightPadding, y);
      ctx.stroke();
      
      // Power labels
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

    // Draw HP line (red)
    if (dataToDisplay.length > 1) {
      ctx.strokeStyle = '#ff4444';
      ctx.lineWidth = Math.max(3, width * 0.002);
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

    // Draw Torque line (green)
    if (dataToDisplay.length > 1) {
      ctx.strokeStyle = '#44ff44';
      ctx.lineWidth = Math.max(3, width * 0.002);
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

    // Draw current point indicator (only during live run)
    if (isRunning && liveGraphData.length > 0) {
      const currentPoint = liveGraphData[liveGraphData.length - 1];
      const x = leftPadding + ((currentPoint.rpm - minRpm) / (maxRpm - minRpm)) * graphWidth;
      const yHP = height - bottomPadding - (currentPoint.horsepower / maxPower) * graphHeight;
      const yTQ = height - bottomPadding - (currentPoint.torque / maxPower) * graphHeight;
      
      const indicatorSize = Math.max(4, width * 0.008);
      
      // HP indicator
      ctx.fillStyle = '#ff4444';
      ctx.beginPath();
      ctx.arc(x, yHP, indicatorSize, 0, 2 * Math.PI);
      ctx.fill();
      
      // Torque indicator
      ctx.fillStyle = '#44ff44';
      ctx.beginPath();
      ctx.arc(x, yTQ, indicatorSize, 0, 2 * Math.PI);
      ctx.fill();
    }

    // Draw legend with responsive sizing
    const legendFontSize = Math.max(8, width * 0.010);
    ctx.fillStyle = '#ff4444';
    ctx.font = `bold ${legendFontSize}px Arial`;
    ctx.textAlign = 'left';
    ctx.fillText('■ Horsepower', width - 160, 25);
    
    ctx.fillStyle = '#44ff44';
    ctx.fillText('■ Torque', width - 160, 25 + legendFontSize + 6);

    // Axis labels with responsive sizing and better positioning
    const axisLabelSize = Math.max(8, width * 0.010);
    ctx.fillStyle = '#ccc';
    ctx.font = `bold ${axisLabelSize}px Arial`;
    ctx.textAlign = 'center';
    ctx.fillText('RPM', width / 2, height - 15);
    
    // Y-axis label with proper positioning
    ctx.save();
    ctx.translate(20, height / 2);
    ctx.rotate(-Math.PI / 2);
    ctx.textAlign = 'center';
    ctx.fillText('HP / TQ', 0, 0);
    ctx.restore();

  }, [liveGraphData, smoothedData, isRunning]);

  // Handle smoothing change
  const handleSmoothingChange = async (newLevel) => {
    if (!dynoResults || !dynoResults.processedData) return;
    
    setSmoothingLevel(newLevel);
    
    let dataToProcess;
    
    if (newLevel === 0) {
      // Raw data
      dataToProcess = dynoResults.processedData.map(d => ({
        rpm: d.rpm,
        horsepower: Math.round(d.hp),
        torque: Math.round(d.torque),
        boost: Math.round(d.boost * 10) / 10
      }));
      setSmoothedData(dataToProcess);
    } else {
      // Apply smoothing via backend
      try {
        const response = await fetch(`http://localhost:5038/api/dyno/runs/${dynoResults.backendResults.id}/smooth/${newLevel}`);
        if (response.ok) {
          const smoothed = await response.json();
          dataToProcess = smoothed.map(d => ({
            rpm: d.rpm,
            horsepower: Math.round(d.horsepower),
            torque: Math.round(d.torque),
            boost: Math.round(d.boost * 10) / 10
          }));
          setSmoothedData(dataToProcess);
        }
      } catch (error) {
        console.error('Error applying smoothing:', error);
        return;
      }
    }
    
    // Calculate new peaks from smoothed data
    if (dataToProcess && dataToProcess.length > 0) {
      const maxHP = Math.max(...dataToProcess.map(d => d.horsepower));
      const maxTorque = Math.max(...dataToProcess.map(d => d.torque));
      const maxBoost = Math.max(...dataToProcess.map(d => d.boost));
      const maxHPPoint = dataToProcess.find(d => d.horsepower === maxHP);
      const maxTorquePoint = dataToProcess.find(d => d.torque === maxTorque);
      
      setSmoothedPeaks({
        maxHP,
        maxTorque,
        maxBoost,
        maxHPRpm: maxHPPoint?.rpm || 0,
        maxTorqueRpm: maxTorquePoint?.rpm || 0
      });
    }
  };

  // Parse uploaded CSV file
  const handleFileUpload = (event) => {
    const file = event.target.files[0];
    if (!file) return;
    
    setCsvFile(file);
    setDynoResults(null);
    setLiveGraphData([]);
    setSmoothedData([]);
    
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

  // Send to C# backend
  const sendToBackend = async () => {
    if (!csvFile) return null;
    
    try {
      const formData = new FormData();
      formData.append('File', csvFile);
      formData.append('CarPresetKey', dynoSettings.selectedCar);
      formData.append('Weight', dynoSettings.weight.toString());
      formData.append('Gear', dynoSettings.gear.toString());
      formData.append('Notes', `${dynoSettings.dynoType}, ${dynoSettings.temperature}°F`);
      formData.append('IsPublic', 'false');
      
      console.log('Sending CSV to C# backend...');
      const response = await fetch('http://localhost:5038/api/dyno/upload', {
        method: 'POST',
        body: formData
      });
      
      if (response.ok) {
        const backendResults = await response.json();
        console.log('✅ C# Backend Results:', backendResults);
        return backendResults;
      } else {
        console.error('Backend API error:', response.status, response.statusText);
        return null;
      }
    } catch (error) {
      console.error('Failed to connect to C# backend:', error);
      return null;
    }
  };

  // Run virtual dyno
  const runVirtualDyno = async () => {
    if (!csvData.length) {
      alert('Please upload a CSV file first!');
      return;
    }
    
    setIsRunning(true);
    setCurrentDataPoint(0);
    setLiveGraphData([]);
    setSmoothedData([]);
    setCurrentPeaks({
      maxHP: 0,
      maxTorque: 0,
      maxBoost: 0,
      currentHP: 0,
      currentTorque: 0,
      currentBoost: 0
    });
    
    try {
      // Get backend results
      const backendResults = await sendToBackend();
      
      if (!backendResults) {
        alert('Could not connect to backend API. Make sure your C# API is running on localhost:5038');
        setIsRunning(false);
        return;
      }
      
      // Get detailed data points
      const detailResponse = await fetch(`http://localhost:5038/api/dyno/runs/${backendResults.id}`);
      
      if (!detailResponse.ok) {
        console.error('Failed to get detailed run data:', detailResponse.status);
        setIsRunning(false);
        return;
      }
      
      const detailData = await detailResponse.json();
      
      // Process data with virtual dyno characteristics
      const processedData = (detailData.dataPoints || []).map((point, index) => {
        const dyno = dynoTypes[dynoSettings.dynoType];
        
        // Environmental effects
        const tempCorrection = Math.sqrt(537.67 / (dynoSettings.temperature + 459.67));
        const humidityCorrection = 1 - (dynoSettings.humidity / 100 * 0.047);
        const envCorrection = tempCorrection * humidityCorrection;
        
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
      
      // Sort by RPM for proper graph progression
      processedData.sort((a, b) => a.rpm - b.rpm);
      
      // Animate the dyno run with live graph updates
      for (let i = 0; i < processedData.length; i++) {
        const currentPoint = processedData[i];
        
        // Update live graph data (add current point)
        setLiveGraphData(prev => {
          const newData = [...prev, {
            rpm: currentPoint.rpm,
            horsepower: Math.round(currentPoint.hp),
            torque: Math.round(currentPoint.torque),
            boost: Math.round(currentPoint.boost * 10) / 10
          }];
          
          return newData.sort((a, b) => a.rpm - b.rpm);
        });
        
        // Update current peaks and live readings
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
        
        // Vary the delay based on RPM
        const delay = currentPoint.rpm < 3000 ? 200 : 
                     currentPoint.rpm < 4000 ? 150 : 
                     currentPoint.rpm < 5000 ? 120 : 100;
        
        await new Promise(resolve => setTimeout(resolve, delay));
      }
      
      // Calculate final peaks
      const peaks = {
        maxHP: processedData.length > 0 ? Math.round(Math.max(...processedData.map(d => d.hp || 0))) : 0,
        maxTorque: processedData.length > 0 ? Math.round(Math.max(...processedData.map(d => d.torque || 0))) : 0,
        maxHPRpm: processedData.find(d => d.hp === Math.max(...processedData.map(p => p.hp || 0)))?.rpm || 0,
        maxTorqueRpm: processedData.find(d => d.torque === Math.max(...processedData.map(p => p.torque || 0)))?.rpm || 0,
        backendMaxHP: Math.round(backendResults.peaks?.maxHorsepower || 0),
        backendMaxTorque: Math.round(backendResults.peaks?.maxTorque || 0),
        backendMaxHPRpm: backendResults.peaks?.maxHorsepowerRpm || 0,
        backendMaxTorqueRpm: backendResults.peaks?.maxTorqueRpm || 0
      };
      
      setDynoResults({
        processedData,
        peaks,
        settings: dynoSettings,
        fileName: csvFile.name,
        backendResults
      });
      
      // Set initial smoothing level to 0 (raw data)
      setSmoothingLevel(0);
      handleSmoothingChange(0);
      
    } catch (error) {
      console.error('Error running virtual dyno:', error);
      alert('Error connecting to backend API. Make sure your C# API is running.');
    } finally {
      setIsRunning(false);
    }
  };

  // Reset dyno
  const resetDyno = () => {
    setDynoResults(null);
    setCurrentDataPoint(0);
    setLiveGraphData([]);
    setSmoothedData([]);
    setSmoothedPeaks(null);
    setSmoothingLevel(0);
    setCurrentPeaks({
      maxHP: 0,
      maxTorque: 0,
      maxBoost: 0,
      currentHP: 0,
      currentTorque: 0,
      currentBoost: 0
    });
  };

  const inputStyle = {
    width: '100%',
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
          🏁 Virtual Dyno Pro
        </h1>
        <p style={{ 
          margin: '6px 0 0 0', 
          fontSize: '14px', 
          opacity: 0.8,
          fontWeight: '400',
          color: '#a0aec0'
        }}>
          Professional Dyno Analysis & Simulation
        </p>
      </div>

      {/* Main Content */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '360px 1fr',
        height: 'calc(100vh - 100px)', // Increased available height
        gap: '0'
      }}>
        
        {/* Left Panel - Settings */}
        <div style={{
          backgroundColor: '#1a202c',
          borderRight: '1px solid #2d3748',
          padding: '20px',
          overflowY: 'auto'
        }}>
          
          {/* File Upload Section */}
          <div style={sectionStyle}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '12px' }}>
              <label style={{ ...labelStyle, margin: 0, flex: 1 }}>📁 Upload Datalog</label>
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
                onMouseOver={(e) => e.target.style.backgroundColor = csvFile ? '#2f855a' : '#2c5282'}
                onMouseOut={(e) => e.target.style.backgroundColor = csvFile ? '#38a169' : '#3182ce'}
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
                📄 {csvFile.name}
              </div>
            )}
          </div>

          {/* Vehicle Settings */}
          <div style={sectionStyle}>
            <h3 style={{ margin: '0 0 14px 0', fontSize: '15px', color: '#68d391' }}>🚗 Vehicle Setup</h3>
            
            {/* Car Selection */}
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

            {/* Gear Selection */}
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

            {/* Weight */}
            <div style={{ marginBottom: '14px' }}>
              <label style={{ ...labelStyle, fontSize: '13px' }}>Vehicle Weight (lbs):</label>
              <input
                type="number"
                value={dynoSettings.weight}
                onChange={(e) => setDynoSettings(prev => ({ ...prev, weight: parseInt(e.target.value) || 0 }))}
                style={{ ...inputStyle, fontSize: '13px' }}
                min="1000"
                max="10000"
                step="50"
              />
            </div>
          </div>

          {/* Dyno Settings */}
          <div style={sectionStyle}>
            <h3 style={{ margin: '0 0 14px 0', fontSize: '15px', color: '#68d391' }}>⚙️ Dyno Configuration</h3>
            
            {/* Dyno Type */}
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

          {/* Environmental Settings */}
          <div style={sectionStyle}>
            <h3 style={{ margin: '0 0 14px 0', fontSize: '15px', color: '#68d391' }}>🌡️ Environmental</h3>
            
            {/* Temperature */}
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

            {/* Humidity */}
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

          {/* Run Button */}
          <button 
            onClick={runVirtualDyno}
            disabled={!csvData.length || isRunning}
            style={{
              width: '100%',
              padding: '14px',
              backgroundColor: isRunning ? '#4a5568' : '#e53e3e',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              fontSize: '15px',
              fontWeight: '700',
              cursor: isRunning ? 'not-allowed' : 'pointer',
              transition: 'all 0.2s ease',
              boxShadow: '0 2px 4px rgba(0,0,0,0.3)'
            }}
          >
            {isRunning ? '🏃 PROCESSING...' : '🚀 START VIRTUAL DYNO'}
          </button>
        </div>

        {/* Right Panel - Graph */}
        <div style={{
          backgroundColor: '#1a202c',
          padding: '20px',
          display: 'flex',
          flexDirection: 'column',
          position: 'relative'
        }}>
          
          {/* Stats Cards or Placeholder */}
          {(isRunning || dynoResults) ? (
            <div>
              {/* Smoothing Control - Only show after run */}
              {dynoResults && !isRunning && (
                <div style={{
                  position: 'absolute',
                  top: '24px',
                  right: '24px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: '#2d3748',
                  padding: '6px 10px',
                  borderRadius: '6px',
                  border: '1px solid #4a5568',
                  zIndex: 10
                }}>
                  <label style={{ fontSize: '11px', color: '#a0aec0', fontWeight: '500' }}>
                    Smoothing:
                  </label>
                  <select
                    value={smoothingLevel}
                    onChange={(e) => handleSmoothingChange(parseInt(e.target.value))}
                    style={{
                      padding: '3px 6px',
                      border: '1px solid #4a5568',
                      borderRadius: '4px',
                      fontSize: '11px',
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

              {/* Peak Stats Card */}
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
                <div>
                  <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#ff4444' }}>
                    {isRunning ? currentPeaks.currentHP : (smoothedPeaks ? smoothedPeaks.maxHP : (dynoResults?.peaks?.maxHP || 0))}
                  </div>
                  <div style={{ fontSize: '11px', color: '#ccc', marginBottom: '3px' }}>
                    {isRunning ? 'CURRENT' : 'PEAK'} HP
                  </div>
                  {!isRunning && (dynoResults || smoothedPeaks) && (
                    <div style={{ fontSize: '9px', color: '#888' }}>
                      @ {smoothedPeaks ? smoothedPeaks.maxHPRpm : (dynoResults?.peaks?.maxHPRpm || 0)} RPM
                    </div>
                  )}
                </div>
                
                <div>
                  <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#44ff44' }}>
                    {isRunning ? currentPeaks.currentTorque : (smoothedPeaks ? smoothedPeaks.maxTorque : (dynoResults?.peaks?.maxTorque || 0))}
                  </div>
                  <div style={{ fontSize: '11px', color: '#ccc', marginBottom: '3px' }}>
                    {isRunning ? 'CURRENT' : 'PEAK'} LB-FT
                  </div>
                  {!isRunning && (dynoResults || smoothedPeaks) && (
                    <div style={{ fontSize: '9px', color: '#888' }}>
                      @ {smoothedPeaks ? smoothedPeaks.maxTorqueRpm : (dynoResults?.peaks?.maxTorqueRpm || 0)} RPM
                    </div>
                  )}
                </div>
                
                <div>
                  <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#4488ff' }}>
                    {isRunning ? currentPeaks.maxBoost : (smoothedPeaks ? smoothedPeaks.maxBoost : (Math.max(...(liveGraphData.map(d => d.boost) || [0])) || (smoothedData.length > 0 ? Math.max(...smoothedData.map(d => d.boost)) : 0)))}
                  </div>
                  <div style={{ fontSize: '11px', color: '#ccc', marginBottom: '3px' }}>
                    PEAK BOOST PSI
                  </div>
                  <div style={{ fontSize: '9px', color: '#888' }}>
                    {isRunning ? 'LIVE' : 'RECORDED'}
                  </div>
                </div>
              </div>

              {/* Canvas Graph with much improved sizing */}
              <div style={{ 
                flex: 1, 
                position: 'relative',
                height: 'calc(100vh - 280px)', // Much larger height
                minHeight: '400px', // Increased minimum
                maxHeight: '600px' // Increased maximum
              }}>
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
                
                {/* Save Dyno Button - Only show after completion */}
                {dynoResults && !isRunning && (
                  <button 
                    onClick={() => {
                      // TODO: Implement save functionality
                      alert('Save functionality will be implemented soon!');
                    }}
                    style={{
                      position: 'absolute',
                      bottom: '16px',
                      right: '16px',
                      padding: '8px 14px',
                      backgroundColor: '#38a169',
                      color: 'white',
                      border: 'none',
                      borderRadius: '6px',
                      cursor: 'pointer',
                      fontSize: '13px',
                      fontWeight: '600',
                      transition: 'all 0.2s ease',
                      boxShadow: '0 2px 8px rgba(56, 161, 105, 0.3)',
                      zIndex: 10
                    }}
                    onMouseOver={(e) => e.target.style.backgroundColor = '#2f855a'}
                    onMouseOut={(e) => e.target.style.backgroundColor = '#38a169'}
                  >
                    💾 Save Dyno
                  </button>
                )}
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
              minHeight: '400px' // Increased to match new canvas size
            }}>
              <div style={{
                textAlign: 'center',
                color: '#a0aec0'
              }}>
                <div style={{ fontSize: '48px', marginBottom: '16px' }}>📊</div>
                <h3 style={{ margin: '0 0 8px 0', fontSize: '18px', color: '#68d391' }}>Ready for Dyno Run</h3>
                <p style={{ margin: 0, fontSize: '14px' }}>
                  Upload a datalog file and configure your settings to begin
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default VirtualDyno;