// WeatherViewModel.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using WeatherApp.Services;
using Windows.Devices.Geolocation;

namespace WeatherApp.ViewModels
{
    public class WeatherViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly WeatherCacheService _cacheService;

        private string _location = "Getting location...";
        private double _latitude;
        private double _longitude;
        private double _temperature;
        private string _windDirection = string.Empty;
        private double _windSpeed;
        private double _humidity;
        private double _pressure;
        private double _cloudiness;
        private WeatherSymbol _currentWeather;
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private DateTime _lastUpdated;
        private DateTime _currentDate = DateTime.Now;
        private DateTime? _sunrise;
        private DateTime? _sunset;

        public string Location { 
            get => _location; 
            set => SetProperty(ref _location, value); 
        }
        
        public double Temperature { 
            get => _temperature; 
            set => SetProperty(ref _temperature, value); 
        }
        
        public string WindDirection { 
            get => _windDirection; 
            set => SetProperty(ref _windDirection, value); 
        }
        
        public double WindSpeed { 
            get => _windSpeed; 
            set => SetProperty(ref _windSpeed, value); 
        }
        
        public double Humidity { 
            get => _humidity; 
            set => SetProperty(ref _humidity, value); 
        }
        
        public double Pressure { 
            get => _pressure; 
            set => SetProperty(ref _pressure, value); 
        }
        
        public double Cloudiness { 
            get => _cloudiness; 
            set => SetProperty(ref _cloudiness, value); 
        }
        
        public WeatherSymbol CurrentWeather { 
            get => _currentWeather; 
            set {
                if (SetProperty(ref _currentWeather, value))
                {
                    OnPropertyChanged(nameof(WeatherIcon));
                    OnPropertyChanged(nameof(WeatherDescription));
                }
            }
        }
        
        public bool IsLoading 
        { 
            get => _isLoading; 
            set 
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(ShowWeatherContent));
                    System.Diagnostics.Debug.WriteLine($"IsLoading changed to {value}, ShowWeatherContent is {ShowWeatherContent}");
                }
            }
        }
        
        public string StatusMessage {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        public DateTime LastUpdated {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }
        
        public DateTime CurrentDate
        {
            get => _currentDate;
            private set => SetProperty(ref _currentDate, value);
        }

        public DateTime? Sunrise
        {
            get => _sunrise;
            set => SetProperty(ref _sunrise, value);
        }
    
        public DateTime? Sunset
        {
            get => _sunset;
            set => SetProperty(ref _sunset, value);
        }
    
        public string FormattedSunrise => Sunrise?.ToString("HH:mm") ?? "--:--";
        public string FormattedSunset => Sunset?.ToString("HH:mm") ?? "--:--";

        public bool ShowWeatherContent => !IsLoading;

        public string FormattedTemperature => $"{Temperature:F1}°C";
        public string FormattedWindSpeed => $"{WindSpeed:F1} m/s";
        public string FormattedHumidity => $"{Humidity:F1}%";
        public string FormattedPressure => $"{Pressure:F0} hPa";
        public string FormattedCloudiness => $"{Cloudiness:F0}%";
        public string FormattedLastUpdated => LastUpdated != default ? $"Last updated: {LastUpdated:g}" : string.Empty;
        
        public string WeatherDescription => DetermineWeatherDescription();
        
        public BitmapImage? WeatherIcon
        {
            get
            {
                try
                {
                    var uri = $"pack://application:,,,/WeatherApp;component/Assets/{CurrentWeather}.png";
                    System.Diagnostics.Debug.WriteLine($"Loading weather icon from: {uri}");
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(uri);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    
                    System.Diagnostics.Debug.WriteLine("Weather icon loaded successfully");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading weather icon: {ex}");
                    return null;
                }
            }
        }

        public ICommand RefreshCommand { get; }

        public WeatherViewModel()
        {
            _cacheService = new WeatherCacheService();
            RefreshCommand = new RelayCommand(async _ => await RefreshWeatherDataAsync());
            PropertyChanged += (s, e) => { }; // Initialize the event
            
            // Set up timer to update current date/time
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, e) => CurrentDate = DateTime.Now;
            timer.Start();
            
            // Initial load with location
            LoadWeatherDataAsync(false).ConfigureAwait(false);
        }

        private async Task GetLocationAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Getting device location...";

                var geolocator = new Geolocator();
                var geoposition = await geolocator.GetGeopositionAsync();
                
                if (geoposition != null)
                {
                    var coordinate = geoposition.Coordinate;
                    _latitude = coordinate.Point.Position.Latitude;
                    _longitude = coordinate.Point.Position.Longitude;
                    
                    Location = $"Location: {_latitude:F6}, {_longitude:F6}";
                    StatusMessage = "Location obtained successfully";
                    return;
                }
                
                StatusMessage = "Failed to get location. Using default location.";
                // Fall back to default location (Friesland)
                _latitude = 53.029893;
                _longitude = 5.6570753;
                Location = "Friesland, Netherlands";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Location error: {ex.Message}. Using default location.";
                // Fall back to default location (Friesland)
                _latitude = 53.029893;
                _longitude = 5.6570753;
                Location = "Friesland, Netherlands";
            }
        }

        public async Task LoadWeatherDataAsync(bool forceRefresh = false)
        {
            if (IsLoading) return;
            
            try
            {
                IsLoading = true;
                
                // Get location first if we don't have it
                if (_latitude == 0 && _longitude == 0)
                {
                    await GetLocationAsync();
                }

                StatusMessage = forceRefresh ? "Fetching fresh weather data..." : "Loading weather data...";

                var weatherData = await _cacheService.GetWeatherDataAsync(_latitude, _longitude, forceRefresh);
                LastUpdated = DateTime.Now;

                ParseWeatherData(weatherData);

                StatusMessage = $"Weather data {(forceRefresh ? "refreshed" : "loaded")} successfully";
            }
            catch (WeatherServiceException ex)
            {
                HandleWeatherServiceError(ex);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading weather data: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        public async Task RefreshWeatherDataAsync()
        {
            await LoadWeatherDataAsync(true);
        }
        
        private void HandleWeatherServiceError(WeatherServiceException ex)
        {
            switch (ex.ErrorType)
            {
                case WeatherServiceError.RateLimited:
                    StatusMessage = "Too many requests. Please try again later.";
                    break;
                case WeatherServiceError.ParseError:
                    StatusMessage = "Error parsing weather data. Please try again.";
                    break;
                default:
                    StatusMessage = $"Error: {ex.Message}";
                    break;
            }
        }
        
        private void ParseWeatherData(XDocument weatherData)
        {
            try
            {
                // Find sunrise/sunset data from the root level
                var sunriseElement = weatherData.Descendants("sunrise").FirstOrDefault();
                var sunsetElement = weatherData.Descendants("sunset").FirstOrDefault();
                
                if (sunriseElement?.Attribute("time")?.Value is string sunriseVal)
                    Sunrise = DateTime.Parse(sunriseVal);
                
                if (sunsetElement?.Attribute("time")?.Value is string sunsetVal)
                    Sunset = DateTime.Parse(sunsetVal);

                // Find the first location element that has forecast data
                var timeElement = weatherData.Descendants("time")
                    .FirstOrDefault(e => e.Attribute("datatype")?.Value == "forecast");
                        
                if (timeElement != null)
                {
                    var location = timeElement.Element("location");
                    if (location != null)
                    {
                        var temperature = location.Element("temperature");
                        var windDir = location.Element("windDirection");
                        var windSpeed = location.Element("windSpeed");
                        var humidity = location.Element("humidity");
                        var pressure = location.Element("pressure");
                        var cloudiness = location.Element("cloudiness");

                        // Update properties with null checks
                        if (temperature?.Attribute("value")?.Value is string tempVal)
                            Temperature = double.Parse(tempVal, System.Globalization.CultureInfo.InvariantCulture);
                        
                        if (windDir?.Attribute("name")?.Value is string windDirVal)
                            WindDirection = windDirVal;
                        
                        if (windSpeed?.Attribute("mps")?.Value is string windSpeedVal)
                            WindSpeed = double.Parse(windSpeedVal, System.Globalization.CultureInfo.InvariantCulture);
                        
                        if (humidity?.Attribute("value")?.Value is string humidityVal)
                            Humidity = double.Parse(humidityVal, System.Globalization.CultureInfo.InvariantCulture);
                        
                        if (pressure?.Attribute("value")?.Value is string pressureVal)
                            Pressure = double.Parse(pressureVal, System.Globalization.CultureInfo.InvariantCulture);
                        
                        if (cloudiness?.Attribute("percent")?.Value is string cloudinessVal)
                            Cloudiness = double.Parse(cloudinessVal, System.Globalization.CultureInfo.InvariantCulture);

                        // Update the weather symbol after all properties are set
                        CurrentWeather = DetermineWeatherSymbol();
                        System.Diagnostics.Debug.WriteLine($"Weather data parsed: Temp={Temperature:F1}°C, Wind={WindDirection} {WindSpeed:F1}m/s, Clouds={Cloudiness:F1}%, Symbol={CurrentWeather}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No location element found in the weather data");
                        throw new WeatherServiceException("No location data found in weather response", WeatherServiceError.ParseError);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No time element found in the weather data");
                    throw new WeatherServiceException("No forecast data found in weather response", WeatherServiceError.ParseError);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing weather data: {ex}");
                throw new WeatherServiceException($"Failed to parse weather data: {ex.Message}", WeatherServiceError.ParseError);
            }
        }
        
        private WeatherSymbol DetermineWeatherSymbol()
        {
            // Simple logic to determine weather symbol based on cloudiness
            if (Cloudiness < 10) return WeatherSymbol.ClearSky;
            if (Cloudiness < 50) return WeatherSymbol.PartlyCloudy;
            return WeatherSymbol.Cloudy;
        }
        
        private string DetermineWeatherDescription()
        {
            // Generate a human-readable weather description
            if (Cloudiness < 10) return "Clear sky";
            if (Cloudiness < 50) return "Partly cloudy";
            return "Cloudy";
        }

        public void Dispose()
        {
            _cacheService?.Dispose();
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            
            // Also raise property changed for formatted properties
            if (propertyName == nameof(Temperature)) OnPropertyChanged(nameof(FormattedTemperature));
            if (propertyName == nameof(WindSpeed)) OnPropertyChanged(nameof(FormattedWindSpeed));
            if (propertyName == nameof(Humidity)) OnPropertyChanged(nameof(FormattedHumidity));
            if (propertyName == nameof(Pressure)) OnPropertyChanged(nameof(FormattedPressure));
            if (propertyName == nameof(Cloudiness)) 
            {
                OnPropertyChanged(nameof(FormattedCloudiness));
                OnPropertyChanged(nameof(WeatherDescription));
                OnPropertyChanged(nameof(WeatherIcon));
            }
            if (propertyName == nameof(LastUpdated))
            {
                OnPropertyChanged(nameof(FormattedLastUpdated));
            }
            if (propertyName == nameof(Sunrise)) OnPropertyChanged(nameof(FormattedSunrise));
            if (propertyName == nameof(Sunset)) OnPropertyChanged(nameof(FormattedSunset));
            
            return true;
        }
        #endregion
    }
    
    public enum WeatherSymbol
    {
        ClearSky,
        PartlyCloudy, 
        Cloudy
    }
    
    // Continued implementation of RelayCommand class
    public class RelayCommand : ICommand
    {
        private readonly Func<object, Task> _asyncExecute;
        private readonly Func<object, bool>? _canExecute;
        private bool _isExecuting;

        public RelayCommand(Func<object, Task> execute, Func<object, bool>? canExecute = null)
        {
            _asyncExecute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            CanExecuteChanged += (s, e) => { }; // Initialize the event
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute(parameter!));
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _asyncExecute(parameter!);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => 
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}