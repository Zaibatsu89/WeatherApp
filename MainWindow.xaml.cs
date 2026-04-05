using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

using WeatherApp.ViewModels;

// To learn more about WinUI, the WinUI project structure, and more about our project templates, see the
// following URL: https://aka.ms/winui-project-info.

namespace WeatherApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // In WinUI 3, Window doesn't have a DataContext property directly.
            // We usually set it on the root element of the window's content.
            if (this.Content is FrameworkElement rootElement)
            {
	            rootElement.DataContext = App.Services.GetRequiredService<WeatherViewModel>();
	        }
	    }
	}
}