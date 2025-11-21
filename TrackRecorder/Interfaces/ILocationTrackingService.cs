using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Models;

namespace TrackRecorder.Interfaces;

public interface ILocationTrackingService
{
    public event EventHandler<LocationUpdatedEventArgs> LocationUpdated;
    bool IsTracking { get; }
    Task StartTrackingAsync();
    Task StopTrackingAsync();
    List<LocationPoint> GetRecordedTrack();
    void ClearTrack();
}
