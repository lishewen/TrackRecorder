using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Util;
using Android.Widget;
using AndroidX.Core.App;
using TrackRecorder.Interfaces;
using TrackRecorder.Models;
using TrackRecorder.Services;
using MA = Android;

namespace TrackRecorder.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Name = "com.lishewen.trackrecorder.MainActivity",
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                          ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private AndroidPermissionsService _permissionsService = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AndroidContext.SetMainActivity(this);

        _permissionsService = new AndroidPermissionsService(this);

        // 检查电池优化
        CheckBatteryOptimization();

        Log.Debug("MainActivity", "MainActivity created successfully");
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        AndroidContext.SetMainActivity(null!);
        Log.Debug("MainActivity", "MainActivity destroyed");
    }

    public void OnServiceStatusChanged(ServiceStatus status, string message)
    {
        try
        {
            RunOnUiThread(() =>
            {
                Toast.MakeText(this, message, ToastLength.Short)!.Show();
                Log.Debug("MainActivity", $"Service status: {status} - {message}");
            });
        }
        catch (Exception ex)
        {
            Log.Error("MainActivity", $"OnServiceStatusChanged error: {ex.Message}");
        }
    }

    private async void CheckBatteryOptimization()
    {
        try
        {
            var powerManager = (PowerManager)GetSystemService(PowerService)!;
            var packageName = PackageName;

            if (!powerManager.IsIgnoringBatteryOptimizations(packageName))
            {
                var result = await ShowAlertAsync(
                    "电池优化",
                    "为了确保后台位置跟踪正常工作，建议将此应用从电池优化中排除。是否现在设置？",
                    "设置", "稍后");

                if (result)
                {
                    var intent = new Intent(MA.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                    intent.SetData(MA.Net.Uri.Parse($"package:{packageName}"));
                    StartActivity(intent);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Battery optimization check failed: {ex.Message}");
        }
    }

    // 启动位置跟踪服务
    public void StartLocationTracking()
    {
        try
        {
            var serviceIntent = new Intent(this, typeof(LocationTrackingService));
            serviceIntent.SetAction("com.lishewen.trackrecorder.action.START");

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                StartForegroundService(serviceIntent);
            }
            else
            {
                StartService(serviceIntent);
            }

            Console.WriteLine("Location tracking started (Native)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Start tracking failed: {ex.Message}");
        }
    }

    // 停止位置跟踪服务
    public void StopLocationTracking()
    {
        try
        {
            var serviceIntent = new Intent(this, typeof(LocationTrackingService));
            serviceIntent.SetAction("com.lishewen.trackrecorder.action.STOP");
            StartService(serviceIntent);

            Console.WriteLine("Location tracking stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stop tracking failed: {ex.Message}");
        }
    }

    // 暂停位置跟踪
    public void PauseLocationTracking()
    {
        try
        {
            var serviceIntent = new Intent(this, typeof(LocationTrackingService));
            serviceIntent.SetAction("com.lishewen.trackrecorder.action.PAUSE");
            StartService(serviceIntent);

            Console.WriteLine("Location tracking paused");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pause tracking failed: {ex.Message}");
        }
    }

    public async Task<bool> RequestLocationPermissionsAsync()
    {
        try
        {
            // 检查前台位置权限
            var foregroundStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (foregroundStatus != PermissionStatus.Granted)
            {
                foregroundStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (foregroundStatus != PermissionStatus.Granted)
                {
                    return false;
                }
            }

            // 检查后台位置权限 (Android 10+)
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var backgroundStatus = await CheckAndRequestBackgroundPermissionAsync();
                return backgroundStatus == PermissionStatus.Granted;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Permission request failed: {ex.Message}");
            return false;
        }
    }

    private async Task<PermissionStatus> CheckAndRequestBackgroundPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();

            if (status == PermissionStatus.Unknown)
            {
                status = await Permissions.RequestAsync<Permissions.LocationAlways>();
                return status;
            }

            if (status == PermissionStatus.Denied)
            {
                await OpenAppSettingsAsync();
                return PermissionStatus.Denied;
            }

            return status;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Background permission check failed: {ex.Message}");
            return PermissionStatus.Denied;
        }
    }

    public override bool ShouldShowRequestPermissionRationale(string permission)
    {
        try
        {
            return ActivityCompat.ShouldShowRequestPermissionRationale(this, permission);
        }
        catch
        {
            return false;
        }
    }

    public async Task OpenAppSettingsAsync()
    {
        try
        {
            var intent = new Intent(MA.Provider.Settings.ActionApplicationDetailsSettings);
            intent.SetData(MA.Net.Uri.Parse($"package:{PackageName}"));
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Open settings failed: {ex.Message}");
        }
    }

    public async Task<bool> CheckLocationServicesEnabledAsync()
    {
        try
        {
            var locationManager = (LocationManager)GetSystemService(LocationService)!;
            bool gpsEnabled = locationManager.IsProviderEnabled(LocationManager.GpsProvider);
            bool networkEnabled = locationManager.IsProviderEnabled(LocationManager.NetworkProvider);

            return gpsEnabled || networkEnabled;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Location services check failed: {ex.Message}");
            return false;
        }
    }

    public async Task EnableLocationServicesAsync()
    {
        try
        {
            var intent = new Intent(MA.Provider.Settings.ActionLocationSourceSettings);
            StartActivity(intent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Enable location services failed: {ex.Message}");
        }
    }

    public async Task<bool> ShowAlertAsync(string title, string message, string accept, string cancel)
    {
        var tcs = new TaskCompletionSource<bool>();

        RunOnUiThread(() =>
        {
            var builder = new AlertDialog.Builder(this);
            builder.SetTitle(title);
            builder.SetMessage(message);
            builder.SetPositiveButton(accept, (s, e) => tcs.TrySetResult(true));
            builder.SetNegativeButton(cancel, (s, e) => tcs.TrySetResult(false));
            builder.Show();
        });

        return await tcs.Task;
    }
}
