using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using TrackRecorder.Interfaces;
using TrackRecorder.Models;

namespace TrackRecorder.Services;

public class GpxExporter : IGpxExporter
{
    public string ExportToGpx(List<LocationPoint> trackPoints, string trackName = "Track")
    {
        if (trackPoints == null || trackPoints.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            IndentChars = "  ",
            NewLineOnAttributes = false
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("gpx");
            writer.WriteAttributeString("version", "1.1");
            writer.WriteAttributeString("creator", "MAUI Track Recorder");
            writer.WriteAttributeString("xmlns", "http://www.topografix.com/GPX/1/1");
            writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xsi:schemaLocation", "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd");

            writer.WriteStartElement("metadata");
            writer.WriteElementString("name", trackName);
            writer.WriteElementString("time", DateTime.UtcNow.ToString("o"));
            writer.WriteElementString("desc", "Track recorded with .NET MAUI GPS Tracker");
            writer.WriteEndElement(); // metadata

            writer.WriteStartElement("trk");
            writer.WriteElementString("name", trackName);
            writer.WriteElementString("desc", $"Recorded on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            writer.WriteStartElement("trkseg");
            foreach (var point in trackPoints)
            {
                writer.WriteStartElement("trkpt");
                writer.WriteAttributeString("lat", point.Latitude.ToString("F6", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("lon", point.Longitude.ToString("F6", CultureInfo.InvariantCulture));

                if (point.Altitude.HasValue && point.Altitude.Value > 0)
                    writer.WriteElementString("ele", point.Altitude.Value.ToString("F1", CultureInfo.InvariantCulture));

                writer.WriteElementString("time", point.Timestamp.ToString("o"));

                if (point.Speed.HasValue || point.Bearing.HasValue)
                {
                    writer.WriteStartElement("extensions");
                    if (point.Speed.HasValue)
                        writer.WriteElementString("speed", (point.Speed.Value * 3.6).ToString("F1", CultureInfo.InvariantCulture)); // m/s to km/h
                    if (point.Bearing.HasValue)
                        writer.WriteElementString("course", point.Bearing.Value.ToString("F1", CultureInfo.InvariantCulture));
                    writer.WriteEndElement(); // extensions
                }

                writer.WriteEndElement(); // trkpt
            }
            writer.WriteEndElement(); // trkseg
            writer.WriteEndElement(); // trk
            writer.WriteEndElement(); // gpx
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }

    public async Task<string> GetSaveFilePathAsync(string? fileName = null)
    {
        try
        {
            fileName ??= $"Track_{DateTime.Now:yyyyMMdd_HHmmss}.gpx";

            // 根据平台确定保存路径
            string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            return filePath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get save file path failed: {ex.Message}");
            return null!;
        }
    }

    public async Task<bool> SaveGpxFileAsync(string gpxContent, string? fileName = null)
    {
        if (string.IsNullOrEmpty(gpxContent))
            return false;

        try
        {
            // 方法1：直接保存到应用沙盒目录（推荐）
            string filePath = await GetSaveFilePathAsync(fileName);
            if (string.IsNullOrEmpty(filePath))
                return false;

            await File.WriteAllTextAsync(filePath, gpxContent);
            Console.WriteLine($"GPX file saved to: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GPX save failed: {ex.Message}");
            return false;
        }
    }

    // 可选：提供共享文件功能
    public async Task<bool> ShareGpxFileAsync(string gpxContent, string? fileName = null)
    {
        if (string.IsNullOrEmpty(gpxContent))
            return false;

        try
        {
            fileName ??= $"Track_{DateTime.Now:yyyyMMdd_HHmmss}.gpx";

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
}
