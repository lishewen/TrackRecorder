using System.Collections.ObjectModel;
using TrackRecorder.Models;

namespace TrackRecorder.Pages;

public partial class FileManagementPage : ContentPage
{
    private ObservableCollection<FileItem> _files = [];

    public FileManagementPage()
    {
        InitializeComponent();
        FilesCollectionView.ItemsSource = _files;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFilesAsync();
    }

    private async Task LoadFilesAsync()
    {
        try
        {
            _files.Clear();
            var appDataPath = FileSystem.AppDataDirectory;

            if (Directory.Exists(appDataPath))
            {
                var gpxFiles = Directory.GetFiles(appDataPath, "*.gpx");

                foreach (var filePath in gpxFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    _files.Add(new FileItem
                    {
                        FileName = fileInfo.Name,
                        FilePath = filePath,
                        FileSize = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime
                    });
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"加载文件失败: {ex.Message}", "确定");
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadFilesAsync();
    }

    private async void OnOpenFileClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // 读取文件内容
                    string content = await File.ReadAllTextAsync(filePath);

                    // 显示文件内容或提供分享选项
                    var action = await DisplayActionSheetAsync("文件操作", "取消", null,
                        "分享文件", "查看内容");

                    if (action == "分享文件")
                    {
                        var fileName = Path.GetFileName(filePath);
                        var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
                        await File.WriteAllTextAsync(tempPath, content);

                        await Share.Default.RequestAsync(new ShareFileRequest
                        {
                            Title = "分享GPX文件",
                            File = new ShareFile(tempPath)
                        });
                    }
                    else if (action == "查看内容")
                    {
                        await Navigation.PushAsync(new FileContentPage(content));
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("错误", $"打开文件失败: {ex.Message}", "确定");
            }
        }
    }

    private async void OnDeleteFileClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string filePath)
        {
            bool confirm = await DisplayAlertAsync("确认删除",
                $"确定要删除文件 {Path.GetFileName(filePath)} 吗？",
                "是", "否");

            if (confirm)
            {
                try
                {
                    File.Delete(filePath);
                    await LoadFilesAsync();
                    await DisplayAlertAsync("成功", "文件已删除", "确定");
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("错误", $"删除文件失败: {ex.Message}", "确定");
                }
            }
        }
    }
}