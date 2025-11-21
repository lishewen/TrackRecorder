using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Models;

namespace TrackRecorder.Interfaces;

public interface IGpxExporter
{
    string ExportToGpx(List<LocationPoint> trackPoints, string trackName = "Track");
    Task<bool> SaveGpxFileAsync(string gpxContent, string? fileName = null);
    Task<string> GetSaveFilePathAsync(string? fileName = null);
    Task<bool> ShareGpxFileAsync(string gpxContent, string? fileName = null);
}
