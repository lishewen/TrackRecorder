using Android.App;
using Android.Content;
using Android.Util;
using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Interfaces;
using TrackRecorder.Models;

namespace TrackRecorder.Platforms.Android;

public static class AndroidContext
{
    private static readonly Lock _lock = new();
    private static WeakReference<MainActivity> _mainActivityRef=null!;
    private static ILocationTrackingService _locationService=null!;

    public static void SetMainActivity(Activity activity)
    {
        try
        {
            if (activity is MainActivity mainActivity)
            {
                lock (_lock)
                {
                    _mainActivityRef = new WeakReference<MainActivity>(mainActivity);
                    Log.Debug("AndroidContext", "MainActivity reference set");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("AndroidContext", $"SetMainActivity failed: {ex.Message}");
        }
    }

    public static MainActivity GetMainActivity()
    {
        try
        {
            lock (_lock)
            {
                if (_mainActivityRef != null && _mainActivityRef.TryGetTarget(out var mainActivity) && !mainActivity.IsDestroyed)
                {
                    return mainActivity;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("AndroidContext", $"GetMainActivity failed: {ex.Message}");
        }
        return null!;
    }

    public static void CheckServiceStatus(Activity activity)
    {
        try
        {
            // 检查后台服务状态
            var activityManager = (ActivityManager)activity.GetSystemService(Context.ActivityService)!;
            var services = activityManager.GetRunningServices(int.MaxValue);

            bool isBackgroundServiceRunning = false;
            foreach (var service in services!)
            {
                if (service.Service!.ClassName.Contains("LocationTrackingService"))
                {
                    isBackgroundServiceRunning = true;
                    break;
                }
            }

            Log.Debug("AndroidContext", $"Background service running: {isBackgroundServiceRunning}");

            if (isBackgroundServiceRunning)
            {
                // 通知 UI 服务正在运行
                var mainActivity = GetMainActivity();
                mainActivity?.OnServiceStatusChanged(ServiceStatus.Running, "后台服务正在运行");
            }
        }
        catch (Exception ex)
        {
            Log.Error("AndroidContext", $"CheckServiceStatus failed: {ex.Message}");
        }
    }

    public static void SetLocationService(ILocationTrackingService service)
    {
        lock (_lock)
        {
            _locationService = service;
        }
    }

    public static ILocationTrackingService GetLocationService()
    {
        lock (_lock)
        {
            return _locationService;
        }
    }
}
