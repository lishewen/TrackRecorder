using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Interfaces;
using TrackRecorder.Services;

namespace TrackRecorder;

public class DefaultLocationServiceControllerFactory : ILocationServiceControllerFactory
{
    private ILocationServiceController _controller = null!;

    public ILocationServiceController CreateController()
    {
        return _controller ??= new DefaultLocationServiceController();
    }
}
