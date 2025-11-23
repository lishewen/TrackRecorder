using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.LifecycleEvents;
using TrackRecorder.Interfaces;
using TrackRecorder.Pages;
#if ANDROID
using TrackRecorder.Platforms.Android;
#endif
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
                })
                .ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    events.AddAndroid(android => android
                        .OnCreate((activity, savedInstanceState) =>
                        {
                            // 在Android活动创建时注册服务
                            RegisterAndroidServices(activity);
                        })
                    );
#endif
                });

            // 注册服务

            // 平台特定服务
#if ANDROID
            builder.Services.AddSingleton<ILocationTrackingService, Platforms.Android.LocationTrackingService>();
#else
            // iOS和其他平台的默认实现
            builder.Services.AddSingleton<ILocationTrackingService, Services.LocationTrackingService>();
            builder.Services.AddSingleton<ILocationServiceController, DefaultLocationServiceController>();
#endif

            builder.Services.AddSingleton<IGpxExporter, GpxExporter>();

            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<FileManagementPage>();

            // 注册平台特定的服务工厂
#if ANDROID
            builder.Services.AddSingleton<ILocationServiceControllerFactory, AndroidLocationServiceControllerFactory>();
#else
            builder.Services.AddSingleton<ILocationServiceControllerFactory, DefaultLocationServiceControllerFactory>();
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // 保存服务提供者到静态属性
            App.ServiceProvider = app.Services;

            return app;
        }
#if ANDROID
        private static void RegisterAndroidServices(Android.App.Activity activity)
        {
            if (activity is MainActivity mainActivity)
            {
                // 获取服务提供者
                var services = App.ServiceProvider;

                // 获取工厂
                var factory = services.GetService<ILocationServiceControllerFactory>();
                if (factory is AndroidLocationServiceControllerFactory androidFactory)
                {
                    // 设置MainActivity
                    androidFactory.SetMainActivity(mainActivity);
                }
            }
        }
#endif
    }
}
