using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Models;

public record FileItem
{
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
}
