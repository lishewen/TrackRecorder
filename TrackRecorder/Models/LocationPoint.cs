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
    public double? Course { get; set; }
    public double? Accuracy { get; set; }
    public DateTime Timestamp { get; set; }
}
