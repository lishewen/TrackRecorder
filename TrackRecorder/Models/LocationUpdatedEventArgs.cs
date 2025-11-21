using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Models;

public class LocationUpdatedEventArgs(LocationPoint location) : EventArgs
{
    public LocationPoint Location { get; } = location;
}
