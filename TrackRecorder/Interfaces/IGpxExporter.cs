using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Models;

namespace TrackRecorder.Interfaces;

public interface IGpxExporter
{
    /// <summary>
    /// 使用XmlWriter导出GPX文件
    /// </summary>
    string ExportToGpx(List<LocationPoint> trackPoints, string trackName = "Track");

    /// <summary>
    /// 使用字符串构建导出GPX文件（备用方案）
    /// </summary>
    string ExportToGpxSimple(List<LocationPoint> trackPoints, string trackName = "Track");

    /// <summary>
    /// 保存GPX文件到应用数据目录
    /// </summary>
    Task<bool> SaveGpxFileAsync(string gpxContent, string? fileName = null);

    /// <summary>
    /// 获取保存文件路径
    /// </summary>
    Task<string> GetSaveFilePathAsync(string? fileName = null);

    /// <summary>
    /// 分享GPX文件
    /// </summary>
    Task<bool> ShareGpxFileAsync(string gpxContent, string? fileName = null);
}
