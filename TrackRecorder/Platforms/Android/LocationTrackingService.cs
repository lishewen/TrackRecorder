using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Models;
using MA = global::Android;

namespace TrackRecorder.Platforms.Android;

[Service(ForegroundServiceType = ForegroundService.TypeLocation, Name = "com.lishewen.trackrecorder.LocationTrackingService")]
[IntentFilter([
    "com.lishewen.trackrecorder.action.START",
    "com.lishewen.trackrecorder.action.PAUSE",
    "com.lishewen.trackrecorder.action.STOP"
])]
public class LocationTrackingService : Service, ILocationListener
{
    private const int NotificationId = 10001;
    private const long MIN_TIME_BETWEEN_UPDATES = 2000; // 2 seconds
    private const float MIN_DISTANCE_CHANGE_FOR_UPDATES = 5.0f; // 5 meters

    private LocationManager _locationManager = null!;
    private NotificationCompat.Builder _notificationBuilder = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;
    private bool _isTracking;
    private DateTime _lastLocationTime;
    private Handler _handler = null!;
    private PowerManager.WakeLock _wakeLock = null!;
    private string _bestProvider = null!;

    // 位置更新回调
    public event EventHandler<LocationUpdatedEventArgs> LocationUpdated;
    public event EventHandler<ServiceStoppedEventArgs> ServiceStopped;

    public override void OnCreate()
    {
        base.OnCreate();

        _cancellationTokenSource = new CancellationTokenSource();
        _handler = new Handler(Looper.MainLooper!);
        _locationManager = (LocationManager)GetSystemService(LocationService)!;

        GetWakeLock();
        DetermineBestProvider();

        Console.WriteLine("LocationTrackingService created (Native Android LocationManager)");
    }

    private void DetermineBestProvider()
    {
        try
        {
            var criteria = new Criteria
            {
                Accuracy = Accuracy.Fine,
                PowerRequirement = Power.Medium,
                AltitudeRequired = false,
                SpeedRequired = true,
                BearingRequired = false,
                CostAllowed = false
            };

            _bestProvider = _locationManager.GetBestProvider(criteria, true);

            if (string.IsNullOrEmpty(_bestProvider))
            {
                // 回退到 GPS 提供者
                if (_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
                {
                    _bestProvider = LocationManager.GpsProvider;
                }
                else if (_locationManager.IsProviderEnabled(LocationManager.NetworkProvider))
                {
                    _bestProvider = LocationManager.NetworkProvider;
                }
                else
                {
                    _bestProvider = LocationManager.PassiveProvider;
                }
            }

            Console.WriteLine($"Best location provider: {_bestProvider}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Provider determination failed: {ex.Message}");
            _bestProvider = LocationManager.GpsProvider; // 默认使用 GPS
        }
    }

    private void GetWakeLock()
    {
        try
        {
            var powerManager = (PowerManager)GetSystemService(PowerService)!;
            _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "TrackRecorder::LocationWakeLock")!;
            _wakeLock.Acquire(10 * 60 * 1000L /*10 minutes*/);
            Console.WriteLine("WakeLock acquired");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WakeLock error: {ex.Message}");
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent == null)
        {
            Console.WriteLine("Null intent received");
            return StartCommandResult.Sticky;
        }

        Console.WriteLine($"Service started with action: {intent.Action}");

        if (intent.Action == "com.lishewen.trackrecorder.action.START")
        {
            StartTracking();
        }
        else if (intent.Action == "com.lishewen.trackrecorder.action.PAUSE")
        {
            PauseTracking();
        }
        else if (intent.Action == "com.lishewen.trackrecorder.action.STOP")
        {
            StopTracking();
            return StartCommandResult.NotSticky;
        }

        return StartCommandResult.Sticky;
    }

    private void StartTracking()
    {
        if (_isTracking) return;

        CreateNotificationChannel();
        ShowForegroundNotification();

        _isTracking = true;
        _lastLocationTime = DateTime.Now;

        RequestLocationUpdates();
    }

    private void RequestLocationUpdates()
    {
        if (!_isTracking) return;

        try
        {
            // 检查权限
            if (ContextCompat.CheckSelfPermission(this, MA.Manifest.Permission.AccessFineLocation) != Permission.Granted &&
                ContextCompat.CheckSelfPermission(this, MA.Manifest.Permission.AccessCoarseLocation) != Permission.Granted)
            {
                ShowErrorNotification("权限不足", "请授予权限以继续跟踪");
                return;
            }

            // 检查位置服务是否启用
            if (!_locationManager.IsProviderEnabled(_bestProvider))
            {
                ShowErrorNotification("位置服务未启用", $"请启用{_bestProvider}提供者");
                return;
            }

            Console.WriteLine($"Requesting location updates from provider: {_bestProvider}");

            // 移除现有的位置更新
            _locationManager.RemoveUpdates(this);

            // 请求位置更新
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                // Android 6.0+ 使用新的API
                _locationManager.RequestLocationUpdates(
                    _bestProvider,
                    MIN_TIME_BETWEEN_UPDATES,
                    MIN_DISTANCE_CHANGE_FOR_UPDATES,
                    this,
                    Looper.MainLooper
                );
            }
            else
            {
                // 旧版本 Android
                _locationManager.RequestLocationUpdates(
                    _bestProvider,
                    MIN_TIME_BETWEEN_UPDATES,
                    MIN_DISTANCE_CHANGE_FOR_UPDATES,
                    this
                );
            }

            Console.WriteLine("Location updates requested successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Location updates request failed: {ex.Message}");
            ShowErrorNotification("位置跟踪错误", $"启动失败: {ex.Message}");
        }
    }

    private void PauseTracking()
    {
        if (!_isTracking) return;

        try
        {
            _locationManager.RemoveUpdates(this);
            UpdateNotification("轨迹记录 - 已暂停", "记录已暂停，点击恢复");
            _isTracking = false;
            Console.WriteLine("Tracking paused");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pause tracking failed: {ex.Message}");
        }
    }

    private void StopTracking()
    {
        if (!_isTracking && _wakeLock == null) return;

        try
        {
            // 移除位置更新
            _locationManager.RemoveUpdates(this);

            // 释放唤醒锁
            if (_wakeLock != null && _wakeLock.IsHeld)
            {
                _wakeLock.Release();
                _wakeLock.Dispose();
                _wakeLock = null!;
            }

            // 取消通知
            StopForeground(true);

            // 停止服务
            StopSelf();

            _isTracking = false;
            _cancellationTokenSource?.Cancel();

            Console.WriteLine("Tracking service stopped completely");

            // 通知主页面服务已停止
            ServiceStopped?.Invoke(this, new ServiceStoppedEventArgs());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stop tracking failed: {ex.Message}");
        }
    }

    private void ShowForegroundNotification()
    {
        try
        {
            var notification = CreateNotification();
            StartForeground(NotificationId, notification);
            Console.WriteLine("Foreground notification started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Notification error: {ex.Message}");
            ShowErrorNotification("通知错误", "后台服务可能受限");
        }
    }

    private Notification CreateNotification()
    {
        string channelId = "location_channel";
        CreateNotificationChannel();

        // 使用系统默认的图标
        int iconId = MA.Resource.Drawable.IcMenuMyLocation;

        // 创建返回主界面的意图
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable);

        // 服务控制意图
        var pauseIntent = new Intent(this, typeof(LocationTrackingService));
        pauseIntent.SetAction("com.lishewen.trackrecorder.action.PAUSE");
        var pausePendingIntent = PendingIntent.GetService(this, 1, pauseIntent, PendingIntentFlags.Immutable);

        var stopIntent = new Intent(this, typeof(LocationTrackingService));
        stopIntent.SetAction("com.lishewen.trackrecorder.action.STOP");
        var stopPendingIntent = PendingIntent.GetService(this, 2, stopIntent, PendingIntentFlags.Immutable);

        // 构建通知
        _notificationBuilder = new NotificationCompat.Builder(this, channelId)
            .SetContentTitle("轨迹记录中 📍")!
            .SetContentText($"使用 {_bestProvider} 提供者")!
            .SetSmallIcon(iconId)!
            .SetContentIntent(pendingIntent)!
            .SetOngoing(true)!
            .SetPriority(NotificationCompat.PriorityHigh)!
            .SetCategory(NotificationCompat.CategoryService)!
            .SetVisibility(NotificationCompat.VisibilityPublic)!
            .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())!
            .SetShowWhen(true)!
            .SetAutoCancel(false)!;

        // 添加控制按钮
        _notificationBuilder
            .AddAction(MA.Resource.Drawable.IcMediaPause, "暂停", pausePendingIntent)!
            .AddAction(MA.Resource.Drawable.IcMenuCloseClearCancel, "停止", stopPendingIntent);

        return _notificationBuilder.Build()!;
    }

    private void UpdateNotification(string title, string text)
    {
        try
        {
            _notificationBuilder.SetContentTitle(title)!.SetContentText(text);
            if (GetSystemService(NotificationService) is NotificationManager notificationManager)
            {
                notificationManager.Notify(NotificationId, _notificationBuilder.Build());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Notification update failed: {ex.Message}");
        }
    }

    private void ShowErrorNotification(string title, string text)
    {
        try
        {
            if (GetSystemService(NotificationService) is not NotificationManager notificationManager) return;

            var channelId = "error_channel";
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(channelId, "错误通知", NotificationImportance.High)
                {
                    Description = "应用错误和警告通知"
                };
                notificationManager.CreateNotificationChannel(channel);
            }

            var notification = new NotificationCompat.Builder(this, channelId)
                .SetContentTitle(title)!
                .SetContentText(text)!
                .SetSmallIcon(MA.Resource.Drawable.IcDialogAlert)!
                .SetPriority(NotificationCompat.PriorityHigh)!
                .SetAutoCancel(true)!
                .Build();

            notificationManager.Notify(9999, notification);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error notification failed: {ex.Message}");
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        try
        {
            if (GetSystemService(NotificationService) is not NotificationManager notificationManager) return;

            // 位置跟踪通道
            var locationChannel = new NotificationChannel(
                "location_channel",
                "位置跟踪服务",
                NotificationImportance.High)
            {
                Description = "用于后台持续记录位置轨迹",
                LockscreenVisibility = NotificationVisibility.Public
            };
            locationChannel.SetSound(null, null); // 无声音

            notificationManager.CreateNotificationChannel(locationChannel);

            Console.WriteLine("Notification channels created");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Notification channel creation failed: {ex.Message}");
        }
    }

    // ILocationListener implementation
    public void OnLocationChanged(MA.Locations.Location location)
    {
        if (location == null) return;

        _lastLocationTime = DateTime.Now;

        Console.WriteLine($"Location update: Lat={location.Latitude}, Lng={location.Longitude}, " +
                         $"Accuracy={location.Accuracy}, Speed={location.Speed}, Provider={location.Provider}");

        // 创建位置点
        var locationPoint = new LocationPoint
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Altitude = location.HasAltitude ? location.Altitude : null,
            Speed = location.HasSpeed ? location.Speed : null,
            Course = location.HasBearing ? location.Bearing : null,
            Accuracy = location.Accuracy,
            Timestamp = DateTime.UtcNow
        };

        // 触发事件
        LocationUpdated?.Invoke(this, new LocationUpdatedEventArgs(locationPoint));
    }

    public void OnProviderDisabled(string provider)
    {
        Console.WriteLine($"Provider disabled: {provider}");
        if (provider == _bestProvider && _isTracking)
        {
            ShowErrorNotification("位置提供者已禁用", $"{provider} 已被禁用");
            PauseTracking();
        }
    }

    public void OnProviderEnabled(string provider)
    {
        Console.WriteLine($"Provider enabled: {provider}");
        if (provider == _bestProvider && !_isTracking)
        {
            // 可以选择自动恢复跟踪
            // StartTracking();
        }
    }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
    {
        Console.WriteLine($"Provider status changed: {provider}, Status: {status}");
    }

    public class LocationUpdatedEventArgs(LocationPoint location) : EventArgs
    {
        public LocationPoint Location { get; } = location;
    }

    public class ServiceStoppedEventArgs : EventArgs { }

    public override IBinder OnBind(Intent? intent)
    {
        return null!;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopTracking();

            if (_locationManager != null)
            {
                _locationManager.RemoveUpdates(this);
                _locationManager = null!;
            }

            if (_wakeLock != null && _wakeLock.IsHeld)
            {
                _wakeLock.Release();
                _wakeLock.Dispose();
                _wakeLock = null!;
            }

            _cancellationTokenSource?.Dispose();
            _handler?.Dispose();
        }

        base.Dispose(disposing);
    }
}
