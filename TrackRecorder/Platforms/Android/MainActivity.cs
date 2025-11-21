using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using TrackRecorder.Platforms.Android;

namespace TrackRecorder;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // 启动服务
    public void StartLocationService()
    {
        var intent = new Intent(this, typeof(LocationTrackingService));
        intent.SetAction("ACTION_START");

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            StartForegroundService(intent);
        }
        else
        {
            StartService(intent);
        }
    }

    // 停止服务
    public void StopLocationService()
    {
        var intent = new Intent(this, typeof(LocationTrackingService));
        intent.SetAction("ACTION_STOP");
        StartService(intent);
    }
}
