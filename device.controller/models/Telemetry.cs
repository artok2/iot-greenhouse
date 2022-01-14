using System;
using System.Text.Json.Serialization;

namespace device.controller.models
{
    public class Telemetry
    {
        [JsonPropertyName("cpuTemperature")] 
        public double CPUTemperature { get; set; } = 0;
       
        [JsonPropertyName("temperature")] 
        public double Temperature { get; set; } = 0;

        [JsonPropertyName("id")]
        public string Id { get; set; } = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        [JsonPropertyName("messageId")]
        public int MessageId { get; set; } = 0;

        [JsonPropertyName("deviceId")]
        public string DeviceId {get; set;}

        [JsonPropertyName("altitude")]
        public double Altitude { get; set; } = 0;

        [JsonPropertyName("pressure")]
        public double Pressure { get; set; } = 0;

        [JsonPropertyName("humidity")]
        public double Humidity { get; set; } = 0;
          
    }
    
}
