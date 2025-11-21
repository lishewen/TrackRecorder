using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Models;

public record LocationPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? Speed { get; set; }
    public double? Bearing { get; set; }
    public DateTime Timestamp { get; set; }
}
