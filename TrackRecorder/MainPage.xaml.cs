using TrackRecorder.Interfaces;
using TrackRecorder.Models;

namespace TrackRecorder;

public partial class MainPage : ContentPage
{
    private readonly ILocationTrackingService _locationService;
    private readonly IGpxExporter _gpxExporter;
    private double _totalDistance;

    public MainPage(ILocationTrackingService locationService, IGpxExporter gpxExporter)
    {
        InitializeComponent();
        _locationService = locationService;
        _gpxExporter = gpxExporter;

        _locationService.LocationUpdated += OnLocationUpdated;
    }

    private void OnLocationUpdated(object? sender, LocationUpdatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateLocationDisplay(e.Location);
            CalculateDistance();
            PointCountLabel.Text = $"记录点数: {_locationService.GetRecordedTrack().Count}";
        });
    }

    private void UpdateLocationDisplay(LocationPoint location)
    {
        LocationLabel.Text = $"纬度: {location.Latitude:F6}\n经度: {location.Longitude:F6}";
        SpeedLabel.Text = location.Speed.HasValue
            ? $"速度: {location.Speed.Value * 3.6:F1} km/h"
            : "速度: 0.0 km/h";
        AltitudeLabel.Text = location.Altitude.HasValue
            ? $"海拔: {location.Altitude.Value:F1} m"
            : "海拔: 0.0 m";
    }

    private void CalculateDistance()
    {
        var track = _locationService.GetRecordedTrack();
        _totalDistance = CalculateTotalDistance(track);
        DistanceLabel.Text = $"距离: {_totalDistance:F2} km";
    }

    private double CalculateTotalDistance(List<LocationPoint> track)
    {
        if (track.Count < 2) return 0;

        double total = 0;
        for (int i = 1; i < track.Count; i++)
        {
            total += CalculateDistanceBetweenPoints(
                track[i - 1].Latitude, track[i - 1].Longitude,
                track[i].Latitude, track[i].Longitude);
        }
        return total;
    }

    private double CalculateDistanceBetweenPoints(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // 地球半径（公里）

        var lat1Rad = lat1 * Math.PI / 180;
        var lat2Rad = lat2 * Math.PI / 180;
        var deltaLat = (lat2 - lat1) * Math.PI / 180;
        var deltaLon = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        try
        {
            await _locationService.StartTrackingAsync();
            UpdateUIForTrackingState(true);
            StatusLabel.Text = "正在记录轨迹...";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"开始记录失败: {ex.Message}", "确定");
        }
    }

    private async void OnPauseClicked(object sender, EventArgs e)
    {
        await _locationService.StopTrackingAsync();
        UpdateUIForTrackingState(false);
        StatusLabel.Text = "已暂停记录";
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        await _locationService.StopTrackingAsync();
        UpdateUIForTrackingState(false);
        StatusLabel.Text = "记录已停止";
        ExportButton.IsEnabled = _locationService.GetRecordedTrack().Count > 0;
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var track = _locationService.GetRecordedTrack();
            if (track.Count == 0)
            {
                await DisplayAlertAsync("提示", "没有可导出的轨迹数据", "确定");
                return;
            }

            // 显示导出选项
            var action = await DisplayActionSheetAsync("选择导出方式", "取消", null,
                "保存到应用目录", "分享文件");

            if (action == "取消")
                return;

            var gpxContent = _gpxExporter.ExportToGpx(track, $"Track_{DateTime.Now:yyyyMMdd_HHmmss}");

            bool success = false;
            if (action == "保存到应用目录")
            {
                success = await _gpxExporter.SaveGpxFileAsync(gpxContent);
                if (success)
                {
                    string filePath = Path.Combine(FileSystem.AppDataDirectory, $"Track_{DateTime.Now:yyyyMMdd_HHmmss}.gpx");
                    await DisplayAlertAsync("成功", $"GPX文件已保存到:\n{filePath}", "确定");
                }
            }
            else if (action == "分享文件")
            {
                success = await _gpxExporter.ShareGpxFileAsync(gpxContent);
                if (success)
                {
                    await DisplayAlertAsync("成功", "GPX文件已准备分享", "确定");
                }
            }

            if (!success)
            {
                await DisplayAlertAsync("失败", "GPX文件导出失败", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"导出失败: {ex.Message}", "确定");
        }
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        if (_locationService.IsTracking)
        {
            await DisplayAlertAsync("提示", "请先停止记录再清除轨迹", "确定");
            return;
        }

        bool confirm = await DisplayAlertAsync("确认", "确定要清除所有轨迹数据吗？", "是", "否");
        if (confirm)
        {
            _locationService.ClearTrack();
            _totalDistance = 0;
            UpdateUIForTrackingState(false);
            StatusLabel.Text = "未开始记录";
            PointCountLabel.Text = "记录点数: 0";
            DistanceLabel.Text = "距离: 0.00 km";
            ExportButton.IsEnabled = false;
        }
    }

    private void UpdateUIForTrackingState(bool isTracking)
    {
        StartButton.IsEnabled = !isTracking;
        PauseButton.IsEnabled = isTracking;
        StopButton.IsEnabled = isTracking;
        ExportButton.IsEnabled = !isTracking && _locationService.GetRecordedTrack().Count > 0;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // 页面离开时停止跟踪以节省电量
        if (_locationService.IsTracking)
        {
            _locationService.StopTrackingAsync();
        }
    }
}
