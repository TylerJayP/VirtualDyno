namespace VirtualDyno.API.Models.DTOs
{
    public class DynoRunCreateDto
    {
        public required IFormFile File { get; set; }
        public required string CarPresetKey { get; set; }
        public int Weight { get; set; }
        public int Gear { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = false;
    }

    public class DynoRunResponseDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public CarConfigurationDto Car { get; set; } = new();
        public int GearUsed { get; set; }
        public PeakValuesDto? Peaks { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public int DataPointCount { get; set; }
    }

    public class DynoRunDetailDto : DynoRunResponseDto
    {
        public List<DynoDataPointDto> DataPoints { get; set; } = new();
    }

    public class DynoDataPointDto
    {
        public int Rpm { get; set; }
        public double Horsepower { get; set; }
        public double Torque { get; set; }
        public double Boost { get; set; }
    }

    public class PeakValuesDto
    {
        public double MaxHorsepower { get; set; }
        public int MaxHorsepowerRpm { get; set; }
        public double MaxTorque { get; set; }
        public int MaxTorqueRpm { get; set; }
        public double MaxBoost { get; set; }
        public double PowerToWeightRatio { get; set; }
    }

    public class CarConfigurationDto
    {
        public string Name { get; set; } = string.Empty;
        public int Weight { get; set; }
        public double Displacement { get; set; }
        public string DriveType { get; set; } = string.Empty;
    }

    public class CarPresetDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Weight { get; set; }
        public double Displacement { get; set; }
        public string DriveType { get; set; } = string.Empty;
        public Dictionary<int, double> GearRatios { get; set; } = new();
        public double FinalDrive { get; set; }
    }
}