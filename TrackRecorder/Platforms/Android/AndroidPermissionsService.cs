using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Platforms.Android;

public class AndroidPermissionsService(MainActivity activity)
{
    private readonly WeakReference<MainActivity> _activityRef = new(activity);

    private MainActivity GetActivity()
    {
        if (!_activityRef.TryGetTarget(out var activity) || activity.IsDestroyed)
        {
            throw new InvalidOperationException("MainActivity is not available");
        }
        return activity;
    }

    public async Task<bool> RequestBackgroundLocationPermissionAsync()
    {
        return await GetActivity().RequestLocationPermissionsAsync();
    }

    public async Task<bool> CheckLocationServicesEnabledAsync()
    {
        return await GetActivity().CheckLocationServicesEnabledAsync();
    }

    public async Task EnableLocationServicesAsync()
    {
        await GetActivity().EnableLocationServicesAsync();
    }

    public async Task OpenAppSettingsAsync()
    {
        await GetActivity().OpenAppSettingsAsync();
    }
}
