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
    private const string GpxNamespace = "http://www.topografix.com/GPX/1/1";
    private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    private const string SchemaLocation = "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd";

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
            // 正确写入XML声明
            writer.WriteStartDocument();

            // 正确处理命名空间
            writer.WriteStartElement("gpx", GpxNamespace);

            // 添加命名空间声明
            writer.WriteAttributeString("version", "1.1");
            writer.WriteAttributeString("creator", "MAUI Track Recorder");
            writer.WriteAttributeString("xmlns", "xsi", null, XsiNamespace);
            writer.WriteAttributeString("xsi", "schemaLocation", null, SchemaLocation);

            // metadata
            writer.WriteStartElement("metadata");
            writer.WriteElementString("name", trackName);
            writer.WriteElementString("time", DateTime.UtcNow.ToString("o"));
            writer.WriteElementString("desc", "Track recorded with .NET MAUI GPS Tracker");
            writer.WriteEndElement(); // metadata

            // trk
            writer.WriteStartElement("trk");
            writer.WriteElementString("name", trackName);
            writer.WriteElementString("desc", $"Recorded on {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");

            // trkseg
            writer.WriteStartElement("trkseg");
            foreach (var point in trackPoints)
            {
                writer.WriteStartElement("trkpt");
                writer.WriteAttributeString("lat", point.Latitude.ToString("F6", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("lon", point.Longitude.ToString("F6", CultureInfo.InvariantCulture));

                if (point.Altitude.HasValue && point.Altitude.Value > 0)
                    writer.WriteElementString("ele", point.Altitude.Value.ToString("F1", CultureInfo.InvariantCulture));

                writer.WriteElementString("time", point.Timestamp.ToString("o"));

                if (point.Speed.HasValue || point.Course.HasValue)
                {
                    writer.WriteStartElement("extensions");
                    if (point.Speed.HasValue)
                        writer.WriteElementString("speed", (point.Speed.Value * 3.6).ToString("F1", CultureInfo.InvariantCulture)); // m/s to km/h
                    if (point.Course.HasValue)
                        writer.WriteElementString("course", point.Course.Value.ToString("F1", CultureInfo.InvariantCulture));
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

    public async Task<bool> SaveGpxFileAsync(string gpxContent, string? fileName = null)
    {
        if (string.IsNullOrEmpty(gpxContent))
            return false;

        try
        {
            fileName ??= $"Track_{DateTime.Now:yyyyMMdd_HHmmss}.gpx";

            // 写入到应用数据目录
            string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
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

    public string ExportToGpxSimple(List<LocationPoint> trackPoints, string trackName = "Track")
    {
        if (trackPoints == null || trackPoints.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<gpx version=\"1.1\" creator=\"MAUI Track Recorder\"");
        sb.AppendLine($"     xmlns=\"http://www.topografix.com/GPX/1/1\"");
        sb.AppendLine($"     xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
        sb.AppendLine($"     xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd\">");

        sb.AppendLine("  <metadata>");
        sb.AppendLine($"    <name>{System.Security.SecurityElement.Escape(trackName)}</name>");
        sb.AppendLine($"    <time>{DateTime.UtcNow.ToString("o")}</time>");
        sb.AppendLine("    <desc>Track recorded with .NET MAUI GPS Tracker</desc>");
        sb.AppendLine("  </metadata>");

        sb.AppendLine("  <trk>");
        sb.AppendLine($"    <name>{System.Security.SecurityElement.Escape(trackName)}</name>");
        sb.AppendLine($"    <desc>Recorded on {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}</desc>");
        sb.AppendLine("    <trkseg>");

        foreach (var point in trackPoints)
        {
            sb.AppendLine($"      <trkpt lat=\"{point.Latitude:F6}\" lon=\"{point.Longitude:F6}\">");

            if (point.Altitude.HasValue && point.Altitude.Value > 0)
                sb.AppendLine($"        <ele>{point.Altitude.Value:F1}</ele>");

            sb.AppendLine($"        <time>{point.Timestamp.ToString("o")}</time>");

            if (point.Speed.HasValue || point.Course.HasValue)
            {
                sb.AppendLine("        <extensions>");
                if (point.Speed.HasValue)
                    sb.AppendLine($"          <speed>{(point.Speed.Value * 3.6):F1}</speed>"); // m/s to km/h
                if (point.Course.HasValue)
                    sb.AppendLine($"          <course>{point.Course.Value:F1}</course>");
                sb.AppendLine("        </extensions>");
            }

            sb.AppendLine("      </trkpt>");
        }

        sb.AppendLine("    </trkseg>");
        sb.AppendLine("  </trk>");
        sb.AppendLine("</gpx>");

        return sb.ToString();
    }
}
