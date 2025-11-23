using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Platforms.Android;

public class AndroidPermissionsService
{
    private readonly MainActivity _activity;

    public AndroidPermissionsService(MainActivity activity)
    {
        _activity = activity;
    }

    public async Task<bool> RequestBackgroundLocationPermissionAsync()
    {
        return await _activity.RequestLocationPermissionsAsync();
    }

    public async Task<bool> CheckLocationServicesEnabledAsync()
    {
        return await _activity.CheckLocationServicesEnabledAsync();
    }

    public async Task EnableLocationServicesAsync()
    {
        await _activity.EnableLocationServicesAsync();
    }

    public async Task OpenAppSettingsAsync()
    {
        await _activity.OpenAppSettingsAsync();
    }
}
