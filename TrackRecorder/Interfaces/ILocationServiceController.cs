using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Models;
#if ANDROID
using TrackRecorder.Platforms.Android;
#endif

namespace TrackRecorder.Interfaces;

public interface ILocationServiceController
{
    event EventHandler<ServiceStatusChangedEventArgs> ServiceStatusChanged;
    Task<bool> StartServiceAsync();
    Task<bool> PauseServiceAsync();
    Task<bool> StopServiceAsync();
    Task<bool> IsServiceRunningAsync();
    Task<bool> CheckPermissionsAsync();
    Task<bool> CheckLocationServicesAsync();
#if ANDROID
    void SetMainActivity(MainActivity mainActivity);
#endif
}
