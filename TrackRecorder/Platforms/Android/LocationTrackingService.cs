using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using MA = global::Android;
using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Platforms.Android;

[Service(ForegroundServiceType = ForegroundService.TypeLocation)]
public class LocationTrackingService : Service
{
    private const int NotificationId = 109915; // 使用更大的ID避免冲突
    private NotificationCompat.Builder _notificationBuilder = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;
    private bool _isTracking;

    public override void OnCreate()
    {
        base.OnCreate();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "ACTION_START")
        {
            StartTracking();
        }
        else if (intent?.Action == "ACTION_PAUSE")
        {
            PauseTracking();
        }
        else if (intent?.Action == "ACTION_STOP")
        {
            StopTracking();
            return StartCommandResult.NotSticky;
        }

        return StartCommandResult.Sticky;
    }

    private void StartTracking()
    {
        CreateNotificationChannel();
        ShowForegroundNotification();
        _isTracking = true;

        // 这里可以启动位置跟踪逻辑
        _ = TrackLocationAsync(_cancellationTokenSource.Token);
    }

    private void PauseTracking()
    {
        _isTracking = false;
        // 更新通知状态
        UpdateNotification("轨迹记录 - 已暂停", "记录已暂停，点击恢复");
    }

    private void StopTracking()
    {
        _isTracking = false;
        _cancellationTokenSource?.Cancel();
        StopForeground(true);
        StopSelf();
    }

    private void ShowForegroundNotification()
    {
        var notification = CreateNotification();
        StartForeground(NotificationId, notification);
    }

    private Notification CreateNotification()
    {
        string channelName = "位置跟踪服务";
        CreateNotificationChannel();

        int iconId = GetNotificationIconId();

        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable);

        _notificationBuilder = new NotificationCompat.Builder(this, "location_channel")
            .SetContentTitle("轨迹记录中")!
            .SetContentText("正在后台记录您的位置轨迹")!
            .SetSmallIcon(iconId)!
            .SetContentIntent(pendingIntent)!
            .SetOngoing(true)!
            .SetPriority(NotificationCompat.PriorityHigh)!
            .SetCategory(NotificationCompat.CategoryService)!
            .SetVisibility(NotificationCompat.VisibilityPublic)!
            .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())!
            .SetShowWhen(true)!;

        // 添加控制按钮
        AddNotificationActions();

        return _notificationBuilder.Build()!;
    }

    private void AddNotificationActions()
    {
        // 暂停按钮
        var pauseIntent = new Intent(this, typeof(LocationTrackingService));
        pauseIntent.SetAction("ACTION_PAUSE");
        var pausePendingIntent = PendingIntent.GetService(this, 1, pauseIntent, PendingIntentFlags.Immutable);
        _notificationBuilder.AddAction(MA.Resource.Drawable.IcMediaPause, "暂停", pausePendingIntent);

        // 停止按钮
        var stopIntent = new Intent(this, typeof(LocationTrackingService));
        stopIntent.SetAction("ACTION_STOP");
        var stopPendingIntent = PendingIntent.GetService(this, 2, stopIntent, PendingIntentFlags.Immutable);
        _notificationBuilder.AddAction(MA.Resource.Drawable.IcMenuCloseClearCancel, "停止", stopPendingIntent);
    }

    private void UpdateNotification(string title, string text)
    {
        _notificationBuilder?.SetContentTitle(title)?.SetContentText(text);
        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        notificationManager?.Notify(NotificationId, _notificationBuilder?.Build());
    }

    private int GetNotificationIconId()
    {
        try
        {
            // 尝试获取自定义图标
            int iconId = Resources!.GetIdentifier("notification_icon", "drawable", PackageName);
            if (iconId != 0 && iconId != MA.Resource.Drawable.IcDialogInfo)
            {
                return iconId;
            }
        }
        catch
        {
            // 忽略异常
        }

        // 使用系统位置图标
        return MA.Resource.Drawable.IcMenuMyLocation;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(
            "location_channel",
            "位置跟踪服务",
            NotificationImportance.High)
        {
            Description = "用于后台持续记录位置轨迹",
            LockscreenVisibility = NotificationVisibility.Public,
        };

        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        notificationManager?.CreateNotificationChannel(channel);
    }

    private async Task TrackLocationAsync(CancellationToken cancellationToken)
    {
        while (_isTracking && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 这里添加您的位置跟踪逻辑
                await Task.Delay(5000, cancellationToken);
            }
            catch (MA.OS.OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Location tracking error: {ex.Message}");
                await Task.Delay(10000, cancellationToken);
            }
        }
    }

    public override IBinder OnBind(Intent? intent)
    {
        return null!;
    }

    public override void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        base.OnDestroy();
    }
}
