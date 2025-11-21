using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using TrackRecorder.Interfaces;
using TrackRecorder.Services;

namespace TrackRecorder
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                // Initialize the .NET MAUI Community Toolkit by adding the below line of code
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // 注册服务
            builder.Services.AddSingleton<ILocationTrackingService, LocationTrackingService>();
            builder.Services.AddSingleton<IGpxExporter, GpxExporter>();
            builder.Services.AddSingleton<MainPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
