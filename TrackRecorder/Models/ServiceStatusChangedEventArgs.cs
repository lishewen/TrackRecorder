using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Models;

public class ServiceStatusChangedEventArgs(ServiceStatus status, string? message = null) : EventArgs
{
    public ServiceStatus Status { get; } = status;
    public string? Message { get; } = message;
}

public enum ServiceStatus
{
    Stopped,
    Starting,
    Running,
    Paused,
    Stopping,
    Error
}
