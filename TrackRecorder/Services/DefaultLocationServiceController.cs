using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Interfaces;
using TrackRecorder.Models;
#if ANDROID
using TrackRecorder.Platforms.Android;
#endif
namespace TrackRecorder.Services;

public class DefaultLocationServiceController : ILocationServiceController
{
    public event EventHandler<ServiceStatusChangedEventArgs> ServiceStatusChanged = null!;

    public Task<bool> CheckLocationServicesAsync() => Task.FromResult(true);
    public Task<bool> CheckPermissionsAsync() => Task.FromResult(true);
    public Task<bool> IsServiceRunningAsync() => Task.FromResult(false);

    public Task<bool> StartServiceAsync()
    {
        ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(ServiceStatus.Running, "服务在模拟模式下运行"));
        return Task.FromResult(true);
    }

    public Task<bool> PauseServiceAsync()
    {
        ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(ServiceStatus.Paused, "服务已暂停"));
        return Task.FromResult(true);
    }

    public Task<bool> StopServiceAsync()
    {
        ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(ServiceStatus.Stopped, "服务已停止"));
        return Task.FromResult(true);
    }
#if ANDROID
    public void SetMainActivity(MainActivity mainActivity)
    {
        throw new NotImplementedException();
    }
#endif
}
