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

public class AndroidLocationServiceController : Java.Lang.Object, ILocationServiceController, IDisposable
{
    private WeakReference<MainActivity> _mainActivityRef = null!;
    private readonly ServiceStatusReceiver _serviceStatusReceiver;
    private readonly Timer _serviceHealthCheckTimer;
    private bool _isDisposed;
    private bool _isServiceRunning;
    private bool _isInitialized;

    public event EventHandler<ServiceStatusChangedEventArgs> ServiceStatusChanged = null!;

    public AndroidLocationServiceController()
    {
        _serviceStatusReceiver = new ServiceStatusReceiver(this);

        // 每30秒检查一次服务健康状态
        _serviceHealthCheckTimer = new Timer(30000);
        _serviceHealthCheckTimer.Elapsed += OnServiceHealthCheck;
        _serviceHealthCheckTimer.AutoReset = true;
    }

    public void SetMainActivity(MainActivity mainActivity)
    {
        ArgumentNullException.ThrowIfNull(mainActivity);

        _mainActivityRef = new WeakReference<MainActivity>(mainActivity);

        if (!_isInitialized)
        {
            Initialize();
        }
    }

    private MainActivity GetMainActivity()
    {
        if (_mainActivityRef == null || !_mainActivityRef.TryGetTarget(out var mainActivity) || mainActivity.IsDestroyed)
        {
            throw new InvalidOperationException("MainActivity is not available or has been destroyed");
        }
        return mainActivity;
    }

    private void Initialize()
    {
        if (_isInitialized || _mainActivityRef == null || !_mainActivityRef.TryGetTarget(out var mainActivity))
            return;

        try
        {
            // 注册服务状态广播接收器
            var filter = new IntentFilter("com.lishewen.trackrecorder.SERVICE_STATUS");
            filter.AddCategory(Intent.CategoryDefault);

            mainActivity.RegisterReceiver(_serviceStatusReceiver, filter);

            // 启动健康检查
            _serviceHealthCheckTimer.Start();

            _isInitialized = true;
            Log.Debug("LocationService", "Service controller initialized");
        }
        catch (Exception ex)
        {
            Log.Error("LocationService", $"Service controller initialization failed: {ex.Message}");
            OnServiceError($"初始化失败: {ex.Message}");
        }
    }

    private void OnServiceHealthCheck(object? sender, ElapsedEventArgs e)
    {
        CheckServiceHealthAsync().FireAndForgetSafe();
    }

    private async Task CheckServiceHealthAsync()
    {
        if (_isDisposed) return;

        try
        {
            bool isRunning = await IsServiceRunningAsync();
            if (isRunning != _isServiceRunning)
            {
                _isServiceRunning = isRunning;

                if (isRunning)
                {
                    OnServiceStatusChanged(ServiceStatus.Running, "服务运行正常");
                }
                else
                {
                    OnServiceStatusChanged(ServiceStatus.Stopped, "服务已停止");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service health check failed: {ex.Message}");
        }
    }

    private void OnServiceStatusChanged(ServiceStatus status, string? message = null)
    {
        var mainActivity = GetMainActivity();
        // 确保在主线程更新UI
        if (mainActivity.MainLooper.Thread.Id == Environment.CurrentManagedThreadId)
        {
            ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(status, message));
        }
        else
        {
            mainActivity.RunOnUiThread(() =>
            {
                ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(status, message));
            });
        }
    }

    private void OnServiceError(string message)
    {
        OnServiceStatusChanged(ServiceStatus.Error, message);
    }

    public async Task<bool> StartServiceAsync()
    {
        if (_isDisposed) return false;

        try
        {
            OnServiceStatusChanged(ServiceStatus.Starting, "正在启动后台位置服务...");

            // 检查权限
            bool hasPermissions = await CheckPermissionsAsync();
            if (!hasPermissions)
            {
                OnServiceError("缺少必要位置权限");
                return false;
            }

            // 检查位置服务
            bool locationEnabled = await CheckLocationServicesAsync();
            if (!locationEnabled)
            {
                OnServiceError("位置服务未启用");
                return false;
            }

            var mainActivity = GetMainActivity();

            // 启动服务
            await Task.Run(() =>
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
            });

            // 等待服务启动
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                bool isRunning = await IsServiceRunningAsync();
                if (isRunning)
                {
                    _isServiceRunning = true;
                    OnServiceStatusChanged(ServiceStatus.Running, "后台位置服务已启动");
                    return true;
                }
            }

            OnServiceError("服务启动超时");
            return false;
        }
        catch (Exception ex)
        {
            OnServiceError($"启动失败: {ex.Message}");
            Console.WriteLine($"Start service error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PauseServiceAsync()
    {
        if (_isDisposed) return false;

        try
        {
            OnServiceStatusChanged(ServiceStatus.Paused, "正在暂停后台位置服务...");

            var mainActivity = GetMainActivity();

            await Task.Run(() =>
            {
                var serviceIntent = new Intent(mainActivity, typeof(LocationTrackingService));
                serviceIntent.SetAction("com.lishewen.trackrecorder.action.PAUSE");
                mainActivity.StartService(serviceIntent);
            });

            _isServiceRunning = false;
            OnServiceStatusChanged(ServiceStatus.Paused, "后台位置服务已暂停");
            return true;
        }
        catch (Exception ex)
        {
            OnServiceError($"暂停失败: {ex.Message}");
            Console.WriteLine($"Pause service error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StopServiceAsync()
    {
        if (_isDisposed) return false;

        try
        {
            OnServiceStatusChanged(ServiceStatus.Stopping, "正在停止后台位置服务...");

            var mainActivity = GetMainActivity();

            await Task.Run(() =>
            {
                var serviceIntent = new Intent(mainActivity, typeof(LocationTrackingService));
                serviceIntent.SetAction("com.lishewen.trackrecorder.action.STOP");
                mainActivity.StartService(serviceIntent);
            });

            // 等待服务停止
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                bool isRunning = await IsServiceRunningAsync();
                if (!isRunning)
                {
                    _isServiceRunning = false;
                    OnServiceStatusChanged(ServiceStatus.Stopped, "后台位置服务已停止");
                    return true;
                }
            }

            OnServiceError("服务停止超时");
            return false;
        }
        catch (Exception ex)
        {
            OnServiceError($"停止失败: {ex.Message}");
            Console.WriteLine($"Stop service error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsServiceRunningAsync()
    {
        if (_isDisposed) return false;

        try
        {
            var mainActivity = GetMainActivity();
            var activityManager = (ActivityManager)mainActivity.GetSystemService(Context.ActivityService)!;
            var services = activityManager.GetRunningServices(int.MaxValue)!;

            foreach (var service in services)
            {
                if (service.Service!.ClassName.Contains("LocationTrackingService"))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Check service running error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CheckPermissionsAsync()
    {
        if (_isDisposed) return false;

        try
        {
            var mainActivity = GetMainActivity();
            return await mainActivity.RequestLocationPermissionsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Check permissions error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CheckLocationServicesAsync()
    {
        if (_isDisposed) return false;

        try
        {
            var mainActivity = GetMainActivity();
            return await mainActivity.CheckLocationServicesEnabledAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Check location services error: {ex.Message}");
            return false;
        }
    }

    // 内部方法：处理广播接收
    internal void HandleServiceStatusBroadcast(Intent intent)
    {
        if (_isDisposed) return;

        try
        {
            var statusStr = intent.GetStringExtra("status");
            var message = intent.GetStringExtra("message");
            var timestamp = intent.GetLongExtra("timestamp", DateTime.Now.Ticks);

            Console.WriteLine($"Service status broadcast received: {statusStr}, {message}");

            if (Enum.TryParse<ServiceStatus>(statusStr, out var status))
            {
                OnServiceStatusChanged(status, message);

                // 更新内部状态
                switch (status)
                {
                    case ServiceStatus.Running:
                        _isServiceRunning = true;
                        break;
                    case ServiceStatus.Stopped:
                    case ServiceStatus.Paused:
                        _isServiceRunning = false;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Handle service status broadcast error: {ex.Message}");
        }
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
                // 停止健康检查
                if (_serviceHealthCheckTimer != null)
                {
                    _serviceHealthCheckTimer.Stop();
                    _serviceHealthCheckTimer.Dispose();
                }

                // 注销广播接收器
                try
                {
                    var mainActivity = GetMainActivity();
                    if (_serviceStatusReceiver != null && mainActivity != null)
                    {
                        mainActivity.UnregisterReceiver(_serviceStatusReceiver);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unregister receiver error: {ex.Message}");
                }

                // 停止服务（如果正在运行）
                if (_isServiceRunning)
                {
                    StopServiceAsync().FireAndForgetSafe();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service controller dispose error: {ex.Message}");
            }

            _isDisposed = true;
            _isInitialized = false;
        }
    }

    ~AndroidLocationServiceController()
    {
        Dispose(false);
    }

    // 广播接收器实现
    private class ServiceStatusReceiver(AndroidLocationServiceController controller) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == "com.lishewen.trackrecorder.SERVICE_STATUS")
            {
                controller.HandleServiceStatusBroadcast(intent);
            }
        }
    }
}

// 扩展方法用于安全的异步调用
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
            Console.WriteLine($"FireAndForgetSafe error: {ex.Message}");
        }
    }
}