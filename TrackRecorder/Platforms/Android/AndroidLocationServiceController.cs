using Android.App;
using Android.Content;
using Android.Util;
using AndroidX.Core.Content;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using TrackRecorder.Interfaces;
using TrackRecorder.Models;
using Timer = System.Timers.Timer;

namespace TrackRecorder.Platforms.Android;

public class AndroidLocationServiceController : Object, ILocationTrackingService, IDisposable
{
    private readonly WeakReference<MainActivity> _mainActivityRef;
    private bool _isTracking;
    private List<LocationPoint> _trackPoints = [];
    private EventHandler<LocationUpdatedEventArgs> _locationHandler;
    private bool _isDisposed;

    public event EventHandler<LocationUpdatedEventArgs> LocationUpdated=null!;

    public bool IsTracking => _isTracking;

    public AndroidLocationServiceController(MainActivity mainActivity)
    {
        ArgumentNullException.ThrowIfNull(mainActivity);

        _mainActivityRef = new WeakReference<MainActivity>(mainActivity);

        // 订阅后台服务的位置更新
        _locationHandler = (sender, args) =>
        {
            try
            {
                if (_isDisposed) return;

                // 在主线程更新UI
                if (GetMainActivity() is MainActivity mainActivity && !mainActivity.IsDestroyed)
                {
                    mainActivity.RunOnUiThread(() =>
                    {
                        if (!_isDisposed && _isTracking)
                        {
                            _trackPoints.Add(args.Location);
                            LocationUpdated?.Invoke(this, args);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error("LocationController", $"Location handler error: {ex.Message}");
            }
        };

        LocationTrackingService.LocationUpdated += _locationHandler;
        AndroidContext.SetLocationService(this);

        Log.Debug("LocationController", "AndroidLocationServiceController created");
    }

    private MainActivity GetMainActivity()
    {
        if (_mainActivityRef == null || !_mainActivityRef.TryGetTarget(out var mainActivity) || mainActivity.IsDestroyed)
        {
            Log.Warn("LocationController", "MainActivity is not available or destroyed");
            return null!;
        }
        return mainActivity;
    }

    public async Task StartTrackingAsync()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AndroidLocationServiceController));
        if (_isTracking) return;

        var mainActivity = GetMainActivity() ?? throw new InvalidOperationException("MainActivity is not available");
        try
        {
            // 检查权限
            bool hasPermissions = await mainActivity.RequestLocationPermissionsAsync();
            if (!hasPermissions)
            {
                throw new PermissionException("Location permissions not granted");
            }

            // 检查位置服务
            bool locationEnabled = await mainActivity.CheckLocationServicesEnabledAsync();
            if (!locationEnabled)
            {
                throw new InvalidOperationException("Location services are not enabled");
            }

            // 启动 Android 后台服务
            await StartBackgroundServiceAsync(mainActivity);

            _isTracking = true;
            _trackPoints.Clear();

            Log.Info("LocationController", "Tracking started successfully");
        }
        catch (Exception ex)
        {
            Log.Error("LocationController", $"Start tracking failed: {ex.Message}");
            throw;
        }
    }

    public async Task StopTrackingAsync()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AndroidLocationServiceController));
        if (!_isTracking) return;

        var mainActivity = GetMainActivity();
        if (mainActivity == null) return;

        try
        {
            await StopBackgroundServiceAsync(mainActivity);
            _isTracking = false;
            Log.Info("LocationController", "Tracking stopped successfully");
        }
        catch (Exception ex)
        {
            Log.Error("LocationController", $"Stop tracking failed: {ex.Message}");
        }
    }

    private async Task StartBackgroundServiceAsync(MainActivity mainActivity)
    {
        try
        {
            var serviceIntent = new Intent(mainActivity, typeof(LocationTrackingService));
            serviceIntent.SetAction("com.lishewen.trackrecorder.action.START");

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                ContextCompat.StartForegroundService(mainActivity, serviceIntent);
            }
            else
            {
                mainActivity.StartService(serviceIntent);
            }

            // 等待服务启动
            await Task.Delay(2000);

            // 验证服务是否运行
            bool isRunning = await IsBackgroundServiceRunningAsync(mainActivity);
            if (!isRunning)
            {
                throw new InvalidOperationException("Background service failed to start");
            }
        }
        catch (Exception ex)
        {
            Log.Error("LocationController", $"Start background service failed: {ex.Message}");
            throw;
        }
    }

    private static async Task StopBackgroundServiceAsync(MainActivity mainActivity)
    {
        try
        {
            var serviceIntent = new Intent(mainActivity, typeof(LocationTrackingService));
            serviceIntent.SetAction("com.lishewen.trackrecorder.action.STOP");
            mainActivity.StartService(serviceIntent);

            // 等待服务停止
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            Log.Error("LocationController", $"Stop background service failed: {ex.Message}");
        }
    }

    private static async Task<bool> IsBackgroundServiceRunningAsync(MainActivity mainActivity)
    {
        try
        {
            var activityManager = (ActivityManager)mainActivity.GetSystemService(Context.ActivityService)!;
            var services = activityManager.GetRunningServices(int.MaxValue);

            foreach (var service in services!)
            {
                if (service.Service!.ClassName.Contains("LocationTrackingService"))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("LocationController", $"Check service running failed: {ex.Message}");
        }
        return false;
    }

    public List<LocationPoint> GetRecordedTrack()
    {
        return _isDisposed ? throw new ObjectDisposedException(nameof(AndroidLocationServiceController)) : [.. _trackPoints];
    }

    public void ClearTrack()
    {
        if (_isDisposed) return;
        _trackPoints.Clear();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            try
            {
                // 停止跟踪
                if (_isTracking)
                {
                    var mainActivity = GetMainActivity();
                    if (mainActivity != null)
                    {
                        StopBackgroundServiceAsync(mainActivity).FireAndForgetSafe();
                    }
                    _isTracking = false;
                }

                // 清理事件订阅
                LocationTrackingService.LocationUpdated -= _locationHandler;
                _locationHandler = null!;

                // 清理数据
                _trackPoints?.Clear();
                _trackPoints = null!;

                // 从上下文中移除
                AndroidContext.SetLocationService(null!);
            }
            catch (Exception ex)
            {
                Log.Error("LocationController", $"Dispose error: {ex.Message}");
            }
        }

        _isDisposed = true;
    }

    ~AndroidLocationServiceController()
    {
        Dispose(false);
    }
}

// 异常类
public class PermissionException(string message) : Exception(message)
{
}

// 扩展方法
public static class TaskExtensions
{
    public static async void FireAndForgetSafe(this Task task, Action<Exception> errorAction = null!)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            errorAction?.Invoke(ex);
            Log.Error("TaskExtensions", $"FireAndForgetSafe error: {ex.Message}");
        }
    }
}