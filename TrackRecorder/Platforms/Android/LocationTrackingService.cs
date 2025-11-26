using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using System.Threading;
using TrackRecorder.Models;
using MA = global::Android;

namespace TrackRecorder.Platforms.Android;

[Service(ForegroundServiceType = ForegroundService.TypeLocation,
         Name = "com.lishewen.trackrecorder.LocationTrackingService",
         Exported = false)]
[IntentFilter([
    "com.lishewen.trackrecorder.action.START",
    "com.lishewen.trackrecorder.action.PAUSE",
    "com.lishewen.trackrecorder.action.STOP"
])]
public class LocationTrackingService : Service, ILocationListener
{
    private const int NotificationId = 10001;
    private const long MIN_TIME_BETWEEN_UPDATES = 2000;
    private const float MIN_DISTANCE_CHANGE_FOR_UPDATES = 5.0f;

    private NotificationCompat.Builder _notificationBuilder = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;

    private LocationManager _locationManager = null!;
    private string? _bestProvider;
    private bool _isTracking;
    private DateTime _lastLocationTime;
    private PowerManager.WakeLock _wakeLock = null!;

    // 仅用于通知跨平台层有位置更新
    public static event EventHandler<LocationUpdatedEventArgs> LocationUpdated = null!;
    public event EventHandler<ServiceStoppedEventArgs> ServiceStopped = null!;

    public override void OnCreate()
    {
        base.OnCreate();
        _locationManager = (LocationManager)GetSystemService(LocationService)!;
        DetermineBestProvider();
        GetWakeLock();

        Log.Debug("BackgroundService", "LocationTrackingService created");
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
                _bestProvider = _locationManager.IsProviderEnabled(LocationManager.GpsProvider)
                    ? LocationManager.GpsProvider
                    : LocationManager.NetworkProvider;
            }

            Log.Debug("BackgroundService", $"Best provider: {_bestProvider}");
        }
        catch (Exception ex)
        {
            Log.Error("BackgroundService", $"Provider determination failed: {ex.Message}");
            _bestProvider = LocationManager.GpsProvider;
        }
    }

    private void GetWakeLock()
    {
        try
        {
            var powerManager = (PowerManager)GetSystemService(PowerService)!;
            _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "TrackRecorder::LocationWakeLock")!;
            _wakeLock.Acquire(10 * 60 * 1000L);
            Log.Debug("BackgroundService", "WakeLock acquired");
        }
        catch (Exception ex)
        {
            Log.Error("BackgroundService", $"WakeLock error: {ex.Message}");
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent == null) return StartCommandResult.Sticky;

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
        Log.Debug("BackgroundService", "Tracking started");
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

    private void RequestLocationUpdates()
    {
        try
        {
            _locationManager.RemoveUpdates(this);

            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                _locationManager.RequestLocationUpdates(
                    _bestProvider!,
                    MIN_TIME_BETWEEN_UPDATES,
                    MIN_DISTANCE_CHANGE_FOR_UPDATES,
                    this,
                    Looper.MainLooper
                );
            }
            else
            {
                _locationManager.RequestLocationUpdates(
                    _bestProvider!,
                    MIN_TIME_BETWEEN_UPDATES,
                    MIN_DISTANCE_CHANGE_FOR_UPDATES,
                    this
                );
            }

            Log.Debug("BackgroundService", $"Location updates requested from {_bestProvider}");
        }
        catch (Exception ex)
        {
            Log.Error("BackgroundService", $"Location updates failed: {ex.Message}");
        }
    }

    public void OnLocationChanged(MA.Locations.Location location)
    {
        if (location == null) return;

        _lastLocationTime = DateTime.Now;

        Log.Debug("BackgroundService", $"Raw location: Lat={location.Latitude:F6}, Lng={location.Longitude:F6}, " +
                                      $"Accuracy={location.Accuracy:F1}m, Speed={location.Speed:F1}m/s");

        // 将 Android Location 转换为跨平台 LocationPoint
        var locationPoint = new LocationPoint
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Altitude = location.HasAltitude ? location.Altitude : null,
            Speed = location.HasSpeed ? location.Speed : null,
            Course = location.HasBearing ? location.Bearing : null,
            Accuracy = location.Accuracy,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(location.Time).UtcDateTime
        };

        // 仅通知跨平台层，不处理业务逻辑
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
            StartTracking();
        }
    }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
    {
        Console.WriteLine($"Provider status changed: {provider}, Status: {status}");
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
            _locationManager?.RemoveUpdates(this);
            if (_wakeLock != null && _wakeLock.IsHeld) _wakeLock.Release();
        }
        base.Dispose(disposing);
    }
}
