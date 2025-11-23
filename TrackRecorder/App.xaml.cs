using Microsoft.Extensions.DependencyInjection;

namespace TrackRecorder
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; set; } = null!;
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}