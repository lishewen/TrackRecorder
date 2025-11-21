using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Platforms.Android;

[Service(ForegroundServiceType = ForegroundService.TypeLocation)]
public class LocationTrackingService : Service
{
    private const int NotificationId = 1000;
    private NotificationCompat.Builder _notificationBuilder = null!;

    public override void OnCreate()
    {
        base.OnCreate();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        StartForegroundService();
        return StartCommandResult.Sticky;
    }

    private void StartForegroundService()
    {
        _notificationBuilder = new NotificationCompat.Builder(this, "location_channel")!
            .SetContentTitle("轨迹记录中")!
            .SetContentText("正在后台记录您的位置轨迹")!
            .SetSmallIcon(Resource.Drawable.notification_icon)!
            .SetOngoing(true)!
            .SetPriority(NotificationCompat.PriorityHigh)!;

        var notification = _notificationBuilder.Build();
        StartForeground(NotificationId, notification);
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
            Description = "用于后台位置跟踪"
        };

        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        notificationManager?.CreateNotificationChannel(channel);
    }

    public override IBinder OnBind(Intent? intent)
    {
        return null!;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        StopForeground(true);
    }
}
