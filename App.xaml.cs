using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

using WeatherApp.Services;
using WeatherApp.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WeatherApp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; } = null;
        public static IServiceProvider Services { get; private set; } = null;

        public App()
        {
            InitializeComponent();

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<WeatherCacheService>();
            services.AddTransient<WeatherViewModel>();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}