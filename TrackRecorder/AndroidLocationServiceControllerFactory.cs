using System;
using System.Collections.Generic;
using System.Text;
using TrackRecorder.Interfaces;
#if ANDROID
using TrackRecorder.Platforms.Android;

namespace TrackRecorder;

public class AndroidLocationServiceControllerFactory : ILocationServiceControllerFactory
{
    private WeakReference<MainActivity> _mainActivityRef = null!;
    private ILocationServiceController _controller = null!;

    public void SetMainActivity(MainActivity mainActivity)
    {
        _mainActivityRef = new WeakReference<MainActivity>(mainActivity);
    }

    public ILocationServiceController CreateController()
    {
        if (_controller != null)
            return _controller;

        if (_mainActivityRef == null || !_mainActivityRef.TryGetTarget(out var mainActivity))
        {
            throw new InvalidOperationException("MainActivity is not available. Call SetMainActivity first.");
        }

        _controller = new AndroidLocationServiceController();
        _controller.SetMainActivity(mainActivity);
        return _controller;
    }
}
#endif