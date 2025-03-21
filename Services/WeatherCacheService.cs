// WeatherCacheService.cs
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WeatherApp.Services
{
    public class WeatherCacheService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromHours(1); // Met.no recommends caching for at least 1 hour
        private XDocument? _cachedWeatherData;
        private DateTime _cacheExpirationTime;
        private string? _lastCachedLocation;

        public WeatherCacheService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0 (https://github.com/Zaibatsu89/WeatherApp)");
            
            // Create cache directory in user's AppData folder
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WeatherApp",
                "Cache");
                
            Directory.CreateDirectory(_cacheDirectory);
        }

        public async Task<XDocument> GetWeatherDataAsync(double latitude, double longitude, bool forceRefresh = false)
        {
            // Validate coordinates
            if (latitude < -90 || latitude > 90)
                throw new WeatherServiceException($"Invalid latitude value: {latitude}. Must be between -90 and 90.", WeatherServiceError.RequestFailed);
            if (longitude < -180 || longitude > 180)
                throw new WeatherServiceException($"Invalid longitude value: {longitude}. Must be between -180 and 180.", WeatherServiceError.RequestFailed);

            // Use invariant culture for cache key to maintain consistency
            var locationKey = $"{latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}," +
                             $"{longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";
            
            // Check in-memory cache
            if (_cachedWeatherData != null && 
                _lastCachedLocation == locationKey && 
                DateTime.UtcNow < _cacheExpirationTime && 
                !forceRefresh)
            {
                return _cachedWeatherData;
            }

            var cacheFilePath = Path.Combine(_cacheDirectory, $"weather_{locationKey.Replace(",", "_")}.cache");
            var metadataFilePath = Path.Combine(_cacheDirectory, $"weather_{locationKey.Replace(",", "_")}.meta");

            // Check file cache
            if (!forceRefresh && File.Exists(cacheFilePath) && File.Exists(metadataFilePath))
            {
                var metadataJson = await File.ReadAllTextAsync(metadataFilePath);
                var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson);
                
                if (metadata?.ExpirationTime != null && DateTime.UtcNow < metadata.ExpirationTime)
                {
                    try
                    {
                        var xmlContent = await File.ReadAllTextAsync(cacheFilePath);
                        _cachedWeatherData = XDocument.Parse(xmlContent);
                        _lastCachedLocation = locationKey;
                        _cacheExpirationTime = metadata.ExpirationTime;
                        
                        return _cachedWeatherData;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading cache: {ex.Message}");
                    }
                }
            }

            // Fetch fresh data
            try 
            {
                // Get sun data first
                var sunriseData = await GetSunriseDataAsync(latitude, longitude);

                // Now get weather data
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/xml");
                
                string weatherApiUrl = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "https://api.met.no/weatherapi/locationforecast/2.0/classic?lat={0:F6}&lon={1:F6}",
                    latitude,
                    longitude
                );
                
                System.Diagnostics.Debug.WriteLine($"Fetching weather data from: {weatherApiUrl}");
                
                HttpResponseMessage weatherResponse = await _httpClient.GetAsync(weatherApiUrl);
                
                if (weatherResponse.IsSuccessStatusCode)
                {
                    string weatherXmlContent = await weatherResponse.Content.ReadAsStringAsync();
                    
                    try 
                    {
                        _cachedWeatherData = XDocument.Parse(weatherXmlContent);
                        
                        // Add sunrise/sunset data if available
                        if (sunriseData?.Properties != null)
                        {
                            var weatherDataRoot = _cachedWeatherData.Root;
                            if (weatherDataRoot != null)
                            {
                                // Create sunrise element with ISO 8601 formatted time
                                var sunriseElement = new XElement("sunrise",
                                    new XAttribute("time", sunriseData.Properties.Sunrise.Time));
                                
                                // Create sunset element with ISO 8601 formatted time
                                var sunsetElement = new XElement("sunset",
                                    new XAttribute("time", sunriseData.Properties.Sunset.Time));
                                
                                weatherDataRoot.Add(sunriseElement);
                                weatherDataRoot.Add(sunsetElement);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new WeatherServiceException($"Failed to parse weather data: {ex.Message}", WeatherServiceError.ParseError);
                    }
                    
                    _lastCachedLocation = locationKey;
                    
                    // Parse cache expiration from HTTP headers or use default
                    _cacheExpirationTime = GetExpirationTimeFromHeaders(weatherResponse) ?? 
                                         DateTime.UtcNow.Add(_defaultCacheDuration);

                    // Save to file cache
                    await File.WriteAllTextAsync(cacheFilePath, _cachedWeatherData.ToString());
                    
                    var metadata = new CacheMetadata
                    {
                        CreationTime = DateTime.UtcNow,
                        ExpirationTime = _cacheExpirationTime,
                        Location = locationKey
                    };
                    
                    string metadataJson = JsonSerializer.Serialize(metadata);
                    await File.WriteAllTextAsync(metadataFilePath, metadataJson);
                    
                    return _cachedWeatherData;
                }
                
                // Handle specific HTTP errors
                if (weatherResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new WeatherServiceException("Rate limit exceeded. Try again later.", WeatherServiceError.RateLimited);
                }
                
                // For BadRequest, try to get more details from the response
                if (weatherResponse.StatusCode == HttpStatusCode.BadRequest)
                {
                    string errorContent = await weatherResponse.Content.ReadAsStringAsync();
                    throw new WeatherServiceException($"API request failed: {errorContent}", WeatherServiceError.RequestFailed);
                }
                
                throw new WeatherServiceException($"API request failed with status code: {weatherResponse.StatusCode}", 
                                               WeatherServiceError.RequestFailed);
            }
            catch (HttpRequestException ex)
            {
                throw new WeatherServiceException($"Network error while fetching weather data: {ex.Message}", 
                                               WeatherServiceError.RequestFailed);
            }
        }

        private async Task<SunriseData?> GetSunriseDataAsync(double latitude, double longitude)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                
                string apiUrl = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "https://api.met.no/weatherapi/sunrise/3.0/sun?lat={0:F6}&lon={1:F6}&date={2:yyyy-MM-dd}",
                    latitude,
                    longitude,
                    DateTime.Now
                );
                
                System.Diagnostics.Debug.WriteLine($"Fetching sun data from: {apiUrl}");
                
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Received sun data: {jsonContent}");
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<SunriseData>(jsonContent, options);
                }
                
                System.Diagnostics.Debug.WriteLine($"Failed to get sunrise data: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting sunrise data: {ex.Message}");
                return null;
            }
        }

        private DateTime? GetExpirationTimeFromHeaders(HttpResponseMessage response)
        {
            // Try to get max-age from Cache-Control header
            if (response.Headers.CacheControl?.MaxAge is TimeSpan maxAge)
            {
                return DateTime.UtcNow.Add(maxAge);
            }
            
            // Try to get Expires header
            if (response.Headers.TryGetValues("Expires", out var expiresValues))
            {
                if (DateTime.TryParse(expiresValues.FirstOrDefault(), out var expiresDate))
                {
                    return expiresDate;
                }
            }
            
            return null;
        }

        public void ClearCache()
        {
            try
            {
                _cachedWeatherData = null;
                _lastCachedLocation = null;
                
                // Clear file cache - delete all cache files in the directory
                foreach (var file in Directory.GetFiles(_cacheDirectory, "weather_*.cache"))
                {
                    File.Delete(file);
                }
                
                foreach (var file in Directory.GetFiles(_cacheDirectory, "weather_*.meta"))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing cache: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
        
        private class CacheMetadata
        {
            public DateTime CreationTime { get; set; }
            public DateTime ExpirationTime { get; set; }
            public required string Location { get; set; }
        }

        private class SunriseData
        {
            public string Type { get; set; } = string.Empty;
            public Geometry Geometry { get; set; } = new();
            public When When { get; set; } = new();
            public SunProperties Properties { get; set; } = new();
        }

        private class Geometry
        {
            public string Type { get; set; } = string.Empty;
            public double[] Coordinates { get; set; } = Array.Empty<double>();
        }

        private class When
        {
            public string[] Interval { get; set; } = Array.Empty<string>();
        }

        private class SunProperties
        {
            public string Body { get; set; } = string.Empty;
            public SunEvent Sunrise { get; set; } = new();
            public SunEvent Sunset { get; set; } = new();
            public SolarEvent Solarnoon { get; set; } = new();
            public SolarEvent Solarmidnight { get; set; } = new();
        }

        private class SunEvent
        {
            public string Time { get; set; } = string.Empty;
            public double Azimuth { get; set; }
        }

        private class SolarEvent
        {
            public string Time { get; set; } = string.Empty;
            public double Disc_centre_elevation { get; set; }
            public bool Visible { get; set; }
        }
    }

    public enum WeatherServiceError
    {
        RequestFailed,
        RateLimited,
        ParseError
    }

    public class WeatherServiceException : Exception
    {
        public WeatherServiceError ErrorType { get; }

        public WeatherServiceException(string message, WeatherServiceError errorType) 
            : base(message)
        {
            ErrorType = errorType;
        }
    }
}