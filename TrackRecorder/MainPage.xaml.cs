using TrackRecorder.Interfaces;
using TrackRecorder.Models;
using TrackRecorder.Pages;
using TrackRecorder.Services;

namespace TrackRecorder;

public partial class MainPage : ContentPage
{
    private readonly ILocationTrackingService _locationService;
    private readonly IGpxExporter _gpxExporter;
    private readonly ILocationServiceController _serviceController;

    private double _totalDistance;
    private DateTime _startTime;
    private double _maxSpeed;
    private bool _isServiceRunning;

    public MainPage(ILocationTrackingService locationService, IGpxExporter gpxExporter, ILocationServiceControllerFactory controllerFactory)
    {
        InitializeComponent();
        _locationService = locationService;
        _gpxExporter = gpxExporter;

        try
        {
            // 从工厂创建控制器
            _serviceController = controllerFactory.CreateController();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Create service controller failed: {ex.Message}");
            _serviceController = new DefaultLocationServiceController();
        }

        _locationService.LocationUpdated += OnLocationUpdated;
        _serviceController.ServiceStatusChanged += OnServiceStatusChanged;

        // 初始化UI状态
        UpdateServiceUIState(false);
        UpdateUIForTrackingState(false);
    }

    private void OnServiceStatusChanged(object? sender, ServiceStatusChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateServiceStatusDisplay(e.Status, e.Message);

            switch (e.Status)
            {
                case ServiceStatus.Running:
                    _isServiceRunning = true;
                    UpdateServiceUIState(true);
                    break;
                case ServiceStatus.Stopped:
                case ServiceStatus.Paused:
                    _isServiceRunning = false;
                    UpdateServiceUIState(false);
                    break;
                case ServiceStatus.Error:
                    // 保持当前状态，但显示错误
                    break;
            }
        });
    }

    private void UpdateServiceStatusDisplay(ServiceStatus status, string? message)
    {
        string statusText = status switch
        {
            ServiceStatus.Stopped => "服务未运行",
            ServiceStatus.Starting => "服务启动中...",
            ServiceStatus.Running => "服务运行中 🟢",
            ServiceStatus.Paused => "服务已暂停 ⏸️",
            ServiceStatus.Stopping => "服务停止中...",
            ServiceStatus.Error => "服务错误 ❌",
            _ => "未知状态"
        };

        string statusColor = status switch
        {
            ServiceStatus.Running => "#4CAF50",
            ServiceStatus.Paused => "#FFA726",
            ServiceStatus.Error => "#F44336",
            _ => "#9E9E9E"
        };

        ServiceStatusLabel.Text = statusText;
        ServiceStatusLabel.TextColor = Color.FromArgb(statusColor);
        ServiceStatusMessage.Text = message ?? "服务状态正常";

        ServiceProgressBar.IsVisible = status == ServiceStatus.Starting || status == ServiceStatus.Stopping;
    }

    private void UpdateServiceUIState(bool isRunning)
    {
        StartServiceButton.IsEnabled = !isRunning;
        PauseServiceButton.IsEnabled = isRunning;
        StopServiceButton.IsEnabled = isRunning;

        // 如果服务没有运行，禁用轨迹记录按钮
        if (!isRunning)
        {
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
            ClearButton.IsEnabled = true;
        }
    }

    private void UpdateTrackingUIState(bool isTracking)
    {
        StartButton.IsEnabled = !isTracking && _isServiceRunning;
        PauseButton.IsEnabled = isTracking;
        StopButton.IsEnabled = isTracking;
        ExportButton.IsEnabled = !isTracking && _locationService.GetRecordedTrack().Count > 0;
        ClearButton.IsEnabled = !isTracking;
    }

    // 服务控制按钮事件
    private async void OnStartServiceClicked(object sender, EventArgs e)
    {
        try
        {
            ServiceProgressBar.IsVisible = true;
            StartServiceButton.IsEnabled = false;

            bool success = await _serviceController.StartServiceAsync();

            if (success)
            {
                await DisplayAlertAsync("成功", "后台服务已启动", "确定");
            }
            else
            {
                await DisplayAlertAsync("失败", "服务启动失败，请检查权限和位置设置", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"启动服务失败: {ex.Message}", "确定");
            UpdateServiceStatusDisplay(ServiceStatus.Error, $"启动失败: {ex.Message}");
        }
        finally
        {
            ServiceProgressBar.IsVisible = false;
            StartServiceButton.IsEnabled = true;
        }
    }

    private async void OnPauseServiceClicked(object sender, EventArgs e)
    {
        try
        {
            bool success = await _serviceController.PauseServiceAsync();
            if (success)
            {
                await DisplayAlertAsync("成功", "服务已暂停", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"暂停服务失败: {ex.Message}", "确定");
        }
    }

    private async void OnStopServiceClicked(object sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlertAsync("确认", "确定要停止后台服务吗？这将停止所有轨迹记录", "是", "否");
            if (!confirm) return;

            ServiceProgressBar.IsVisible = true;
            StopServiceButton.IsEnabled = false;

            bool success = await _serviceController.StopServiceAsync();
            if (success)
            {
                await DisplayAlertAsync("成功", "服务已停止", "确定");
                _locationService.ClearTrack();
                UpdateTrackingUIState(false);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"停止服务失败: {ex.Message}", "确定");
        }
        finally
        {
            ServiceProgressBar.IsVisible = false;
            StopServiceButton.IsEnabled = true;
        }
    }

    private async void OnCheckPermissionsClicked(object sender, EventArgs e)
    {
        try
        {
            ServiceProgressBar.IsVisible = true;
            CheckPermissionsButton.IsEnabled = false;

            bool hasPermissions = await _serviceController.CheckPermissionsAsync();

            if (hasPermissions)
            {
                await DisplayAlertAsync("权限检查", "✅ 所有位置权限已授予", "确定");
            }
            else
            {
                await DisplayAlertAsync("权限检查", "❌ 缺少必要权限，请手动授予权限", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"权限检查失败: {ex.Message}", "确定");
        }
        finally
        {
            ServiceProgressBar.IsVisible = false;
            CheckPermissionsButton.IsEnabled = true;
        }
    }

    private async void OnCheckLocationClicked(object sender, EventArgs e)
    {
        try
        {
            ServiceProgressBar.IsVisible = true;
            CheckLocationButton.IsEnabled = false;

            bool locationEnabled = await _serviceController.CheckLocationServicesAsync();

            if (locationEnabled)
            {
                await DisplayAlertAsync("位置服务", "✅ 位置服务已启用", "确定");
            }
            else
            {
                await DisplayAlertAsync("位置服务", "❌ 位置服务未启用，请启用GPS", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"位置服务检查失败: {ex.Message}", "确定");
        }
        finally
        {
            ServiceProgressBar.IsVisible = false;
            CheckLocationButton.IsEnabled = true;
        }
    }

    private void OnLocationUpdated(object? sender, LocationUpdatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateLocationDisplay(e.Location);
            CalculateDistance();
            UpdateStatistics(e.Location);
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
        AccuracyLabel.Text = location.Accuracy.HasValue
            ? $"精度: {location.Accuracy.Value:F1} m"
            : "精度: 0.0 m";
    }

    private void CalculateDistance()
    {
        var track = _locationService.GetRecordedTrack();
        _totalDistance = MainPage.CalculateTotalDistance(track);
        DistanceLabel.Text = $"距离: {_totalDistance:F2} km";
    }

    private static double CalculateTotalDistance(List<LocationPoint> track)
    {
        if (track.Count < 2) return 0;

        double total = 0;
        for (int i = 1; i < track.Count; i++)
        {
            total += MainPage.CalculateDistanceBetweenPoints(
                track[i - 1].Latitude, track[i - 1].Longitude,
                track[i].Latitude, track[i].Longitude);
        }
        return total;
    }

    private static double CalculateDistanceBetweenPoints(double lat1, double lon1, double lat2, double lon2)
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

    private void UpdateStatistics(LocationPoint location)
    {
        if (_startTime == default)
            _startTime = DateTime.Now;

        var duration = DateTime.Now - _startTime;
        DurationLabel.Text = $"时长: {duration:hh\\:mm\\:ss}";

        if (location.Speed.HasValue)
        {
            double speedKmh = location.Speed.Value * 3.6;
            if (speedKmh > _maxSpeed)
            {
                _maxSpeed = speedKmh;
                MaxSpeedLabel.Text = $"最高速度: {_maxSpeed:F1} km/h";
            }
        }
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        try
        {
            if (!_isServiceRunning)
            {
                await DisplayAlertAsync("提示", "请先启动后台服务", "确定");
                return;
            }

            await _locationService.StartTrackingAsync();
            _startTime = DateTime.Now;
            _maxSpeed = 0;
            UpdateTrackingUIState(true);
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
        UpdateTrackingUIState(false);
        StatusLabel.Text = "已暂停记录";
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        await _locationService.StopTrackingAsync();
        UpdateTrackingUIState(false);
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

            // 提供导出选项
            var action = await DisplayActionSheetAsync("选择导出方式", "取消", null,
                "保存到应用目录", "分享文件", "查看GPX内容");

            if (action == "取消")
                return;

            string gpxContent;

            // 尝试使用XmlWriter方法，如果失败则使用字符串构建方法
            try
            {
                gpxContent = _gpxExporter.ExportToGpx(track, $"Track_{DateTime.Now:yyyyMMdd_HHmmss}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"XmlWriter export failed: {ex.Message}");
                Console.WriteLine("Falling back to simple string method");
                gpxContent = _gpxExporter.ExportToGpxSimple(track, $"Track_{DateTime.Now:yyyyMMdd_HHmmss}");
            }

            bool success = false;
            string fileName = $"Track_{DateTime.Now:yyyyMMdd_HHmmss}.gpx";

            if (action == "保存到应用目录")
            {
                success = await _gpxExporter.SaveGpxFileAsync(gpxContent, fileName);
                if (success)
                {
                    string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
                    await DisplayAlertAsync("成功", $"GPX文件已保存到:\n{filePath}", "确定");
                }
            }
            else if (action == "分享文件")
            {
                success = await MainPage.ShareGpxFileAsync(gpxContent, fileName);
                if (success)
                {
                    await DisplayAlertAsync("成功", "GPX文件已准备分享", "确定");
                }
            }
            else if (action == "查看GPX内容")
            {
                // 显示前1000个字符预览
                string preview = gpxContent.Length > 1000 ? gpxContent[..1000] + "..." : gpxContent;
                await DisplayAlertAsync("GPX内容预览", preview, "确定");
            }

            if (!success && action != "查看GPX内容")
            {
                await DisplayAlertAsync("失败", "GPX文件导出失败", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"导出失败: {ex.Message}", "确定");
        }
    }

    private static async Task<bool> ShareGpxFileAsync(string gpxContent, string fileName)
    {
        try
        {
            // 创建临时文件
            string tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(tempPath, gpxContent);

            // 共享文件
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "分享GPX轨迹文件",
                File = new ShareFile(tempPath)
            });

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GPX share failed: {ex.Message}");
            return false;
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

    // 新增：文件管理按钮点击事件
    private async void OnFileManagementClicked(object sender, EventArgs e)
    {
        try
        {
            // 导航到文件管理页面
            await Navigation.PushAsync(new FileManagementPage());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"打开文件管理失败: {ex.Message}", "确定");
        }
    }

    // 新增：刷新数据按钮
    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            // 刷新UI状态
            var track = _locationService.GetRecordedTrack();
            PointCountLabel.Text = $"记录点数: {track.Count}";
            CalculateDistance();

            if (_locationService.IsTracking)
            {
                StatusLabel.Text = "正在记录轨迹...";
            }
            else
            {
                StatusLabel.Text = track.Count > 0 ? "记录已停止" : "未开始记录";
            }

            await DisplayAlertAsync("成功", "数据已刷新", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"刷新数据失败: {ex.Message}", "确定");
        }
    }

    // 新增：设置按钮
    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("设置", "应用设置功能将在后续版本中实现", "确定");
    }

    // 新增：统计图表按钮
    private async void OnStatisticsClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("统计图表", "统计图表功能将在后续版本中实现", "确定");
    }

    // 新增：同步按钮
    private async void OnSyncClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("在线同步", "在线同步功能将在后续版本中实现", "确定");
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
