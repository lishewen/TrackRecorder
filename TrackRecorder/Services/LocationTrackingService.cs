using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Interfaces;
using TrackRecorder.Models;

namespace TrackRecorder.Services;

public class LocationTrackingService : ILocationTrackingService, IDisposable
{
    private CancellationTokenSource _cancellationTokenSource=null!;
    private List<LocationPoint> _trackPoints = [];
    private bool _isTracking;
    private DateTime _lastUpdate;

    public event EventHandler<LocationUpdatedEventArgs> LocationUpdated = null!;

    public bool IsTracking => _isTracking;

    public LocationTrackingService()
    {
        _lastUpdate = DateTime.Now;
    }

    public async Task StartTrackingAsync()
    {
        if (_isTracking) return;

        try
        {
            // 检查位置权限
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                throw new Exception("需要位置权限才能记录轨迹");
            }

            // 检查后台权限（Android需要）
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var backgroundStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                if (backgroundStatus != PermissionStatus.Granted)
                {
                    backgroundStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
                }
            }

            // 检查位置服务是否启用
            if (!await LocationTrackingService.CheckLocationServicesEnabledAsync())
            {
                throw new Exception("请启用设备的位置服务");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _isTracking = true;
            _trackPoints.Clear();

            // 开始位置跟踪
            _ = StartLocationUpdatesAsync(_cancellationTokenSource.Token);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Start tracking failed: {ex.Message}");
            throw;
        }
    }

    private static async Task<bool> CheckLocationServicesEnabledAsync()
    {
        try
        {
            // 尝试获取一次位置来检查服务是否启用
            var location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Best,
                Timeout = TimeSpan.FromSeconds(10)
            });

            return location != null;
        }
        catch (FeatureNotSupportedException)
        {
            return false;
        }
        catch (FeatureNotEnabledException)
        {
            return false;
        }
        catch (PermissionException)
        {
            return false;
        }
        catch
        {
            return true; // 如果出现其他异常，假设服务已启用
        }
    }

    private async Task StartLocationUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_isTracking && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Best,
                        Timeout = TimeSpan.FromSeconds(30)
                    }, cancellationToken);

                    if (location != null)
                    {
                        AddLocationPoint(location);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Location update error: {ex.Message}");
                }

                // 每2秒获取一次位置
                await Task.Delay(2000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tracking loop error: {ex.Message}");
        }
    }

    private void AddLocationPoint(Location location)
    {
        var currentTime = DateTime.Now;

        // 防止过于频繁的更新
        if ((currentTime - _lastUpdate).TotalSeconds < 1) return;

        var locationPoint = new LocationPoint
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Altitude = location.Altitude,
            Speed = location.Speed,
            Bearing = location.Course,
            Timestamp = DateTime.UtcNow
        };

        _trackPoints.Add(locationPoint);
        _lastUpdate = currentTime;
        LocationUpdated?.Invoke(this, new LocationUpdatedEventArgs(locationPoint));
    }

    public async Task StopTrackingAsync()
    {
        if (!_isTracking) return;

        _isTracking = false;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null!;
    }

    public List<LocationPoint> GetRecordedTrack() => _trackPoints.ToList();
    public void ClearTrack() => _trackPoints.Clear();

    public void Dispose()
    {
        _ = StopTrackingAsync();
        _cancellationTokenSource?.Dispose();
    }
}