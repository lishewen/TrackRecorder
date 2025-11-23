using System;
using System.Collections.Generic;
using System.Text;

namespace TrackRecorder.Interfaces;

public interface ILocationServiceControllerFactory
{
    ILocationServiceController CreateController();
}
