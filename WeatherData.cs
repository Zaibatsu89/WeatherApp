using System.Xml.Serialization;

namespace WeatherApp
{
    [XmlRoot(ElementName = "weatherdata")] // Root element of the XML
    public class WeatherData
    {
        [XmlElement("product")]
        public Product? Product { get; set; }
    }

    public class Product
    {
        [XmlElement("time")]
        public Time[]? Time { get; set; }
    }

    public class Time
    {
        [XmlElement("location")]
        public Location? Location { get; set; }

        [XmlAttribute("from")]
        public string? From { get; set; }
    }

    public class Location
    {
        [XmlElement("temperature")]
        public Temperature? Temperature { get; set; }

        [XmlElement("windSpeed")]
        public WindSpeed? WindSpeed { get; set; }

        [XmlElement("windDirection")]
        public WindDirection? WindDirection { get; set; }

        [XmlElement("humidity")]
        public Humidity? Humidity { get; set; }

        [XmlElement("pressure")]
        public Pressure? Pressure { get; set; }

        [XmlElement("cloudiness")]
        public Cloudiness? Cloudiness { get; set; }

        [XmlElement("dewpointTemperature")]
        public DewpointTemperature? DewpointTemperature { get; set; }

        [XmlElement("symbol")]
        public Symbol? Symbol { get; set; }

        [XmlElement("precipitation")]
        public Precipitation? Precipitation { get; set; }

        [XmlElement("minTemperature")]
        public MinTemperature? MinTemperature { get; set; }

        [XmlElement("maxTemperature")]
        public MaxTemperature? MaxTemperature { get; set; }

        [XmlElement("sunrise")]
        public Sunrise? Sunrise { get; set; }

        [XmlElement("sunset")]
        public Sunset? Sunset { get; set; }

        [XmlAttribute("latitude")]
        public double Latitude { get; set; }

        [XmlAttribute("longitude")]
        public double Longitude { get; set; }
    }

    public class Temperature
    {
        [XmlAttribute("value")]
        public double Value { get; set; }
    }

    public class WindSpeed
    {
        [XmlAttribute("mps")]
        public double Mps { get; set; }

        [XmlAttribute("beaufort")]
        public int Beaufort { get; set; }

        [XmlAttribute("name")]
        public string? Name { get; set; }
    }

    public class WindDirection
    {
        [XmlAttribute("name")]
        public string? Name { get; set; }
    }

    public class Humidity
    {
        [XmlAttribute("value")]
        public double Value { get; set; }
    }

    public class Pressure
    {
        [XmlAttribute("value")]
        public double Value { get; set; }
    }

    public class Cloudiness
    {
        [XmlAttribute("percent")]
        public double Percent { get; set; }
    }

    public class DewpointTemperature
    {
        [XmlAttribute("value")]
        public double Value { get; set; }
    }

    public class Symbol
    {
        [XmlAttribute("code")]
        public string? Code { get; set; }
    }

    public class Precipitation
    {
        [XmlAttribute("value")]
        public double Value { get; set; }
    }

    public class MinTemperature
    {
        [XmlAttribute("value")]
        public double Value { get; set; }
    }

    public class MaxTemperature
    {
        [XmlAttribute("value")]
        public double Value { get; set; }
    }

    public class Sunrise
    {
        [XmlAttribute("time")]
        public string? Time { get; set; }
    }

    public class Sunset
    {
        [XmlAttribute("time")]
        public string? Time { get; set; }
    }
}