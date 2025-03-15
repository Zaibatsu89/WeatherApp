using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

using Windows.Devices.Geolocation;

namespace WeatherApp;

public partial class MainWindow : Window
{
    private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";

    public MainWindow()
    {
        InitializeComponent();
        GetLocationAsync().ConfigureAwait(false);
    }

    private async Task GetLocationAsync()
    {
        Geolocator geolocator = new Geolocator();
        Geoposition? geoposition = await geolocator.GetGeopositionAsync();
        if (geoposition != null)
        {
            Geocoordinate coordinate = geoposition.Coordinate;
            double latitude = coordinate.Point.Position.Latitude;
            double longitude = coordinate.Point.Position.Longitude;
            // Update the API URL with the current location, use InvariantCulture to avoid issues with decimal separators
            string apiUrl = $"https://api.met.no/weatherapi/locationforecast/2.0/classic?lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}";

            LoadWeatherData(apiUrl);
        }
        else
        {
            Dispatcher.Invoke(() => ErrorTextBlock.Text = "Failed to get location.");
        }
    }

    private async void LoadWeatherData(string apiUrl)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", _userAgent); // Required by the API

                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode(); // Throw exception if not successful

                string xmlString = await response.Content.ReadAsStringAsync();

                XmlSerializer serializer = new XmlSerializer(typeof(WeatherData));
                using (StringReader reader = new StringReader(xmlString))
                {
                    WeatherData? weatherData = (WeatherData?)serializer.Deserialize(reader);

                    if (weatherData != null && weatherData.Product != null && weatherData.Product.Time?.Length > 0)
                    {
                        // Assuming you want the first time slot's data (current weather)
                        Time currentTimeData = weatherData.Product.Time[0];

                        // Update UI elements
                        Dispatcher.Invoke(() =>
                        {
                            TemperatureTextBlock.Text = $"Temperature: {currentTimeData.Location?.Temperature?.Value}°C";
                            WindSpeedTextBlock.Text = $"Wind Speed: {currentTimeData.Location?.WindSpeed?.Mps} m/s";
                            WindDirectionTextBlock.Text = $"Wind Direction: {currentTimeData.Location?.WindDirection?.Name}";
                            HumidityTextBlock.Text = $"Humidity: {currentTimeData.Location?.Humidity?.Value}%";
                            PressureTextBlock.Text = $"Pressure: {currentTimeData.Location?.Pressure?.Value} hPa";
                            CloudinessTextBlock.Text = $"Cloudiness: {currentTimeData.Location?.Cloudiness?.Percent}%";
                            DewpointTemperatureTextBlock.Text = $"Dewpoint Temperature: {currentTimeData.Location?.DewpointTemperature?.Value}°C";
                            SymbolTextBlock.Text = $"Symbol: {currentTimeData.Location?.Symbol?.Code ?? "N/A"}";
                            PrecipitationTextBlock.Text = $"Precipitation: {((currentTimeData.Location?.Precipitation != null) ? currentTimeData.Location?.Precipitation?.Value : "N/A")} mm";
                            MinTemperatureTextBlock.Text = $"Min Temperature: {((currentTimeData.Location?.MinTemperature != null) ? currentTimeData.Location?.MinTemperature?.Value : "N/A")}°C";
                            MaxTemperatureTextBlock.Text = $"Max Temperature: {((currentTimeData.Location?.MaxTemperature != null) ? currentTimeData.Location?.MaxTemperature?.Value : "N/A")}°C";
                            TimeTextBlock.Text = $"Time: {currentTimeData.From}";
                            LocationTextBlock.Text = $"Location: {currentTimeData.Location?.Latitude.ToString(CultureInfo.InvariantCulture)}, {currentTimeData.Location?.Longitude.ToString(CultureInfo.InvariantCulture)}";
                            ErrorTextBlock.Text = string.Empty; // Clear any previous errors
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => ErrorTextBlock.Text = "Failed to parse weather data.");
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Dispatcher.Invoke(() => ErrorTextBlock.Text = $"HTTP Request Error: {ex.Message}");
        }
        catch (XmlException ex)
        {
            Dispatcher.Invoke(() => ErrorTextBlock.Text = $"XML Parsing Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ErrorTextBlock.Text = $"An unexpected error occured: {ex.Message}");
        }
    }
}