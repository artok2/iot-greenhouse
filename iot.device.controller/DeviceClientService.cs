using System;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;

namespace iot.device.controller
{
    public interface IDeviceClientService
    {
     //   Task SendMsgIotHub(Telemetry telemetry);
     //   Task UpdateRoomTemperature(double temperature);
      //  Task UpdateRoomAction(Telemetry telemetry);
        RoomAction RoomState { get; } 

        int ThermoStat { get; set; } 
    }
    public enum RoomAction { Unknown, Heating, Cooling, Green }
    


    public class DeviceClientService : IDeviceClientService
    {
        private const string ModelId = "dtmi:pnptestapp:raspberrypi3;1";

        private  string idScope = Environment.GetEnvironmentVariable("DPS_IDSCOPE");
        private  string registrationId = Environment.GetEnvironmentVariable("DPS_REGISTRATION_ID");
        private  string primaryKey = Environment.GetEnvironmentVariable("DPS_PRIMARY_KEY");
        private  string secondaryKey = Environment.GetEnvironmentVariable("DPS_SECONDARY_KEY");
        private const string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";        



        private string DeviceConnectionString = Environment.GetEnvironmentVariable("DEVICE_CONNECTION");
        static RoomAction previousRoomState = RoomAction.Unknown;
        private DeviceClient _iotClient = null;
        private readonly ILogger<DeviceClientService> _logger;        
        const double TemperatureThreshold = 69.0;
        private int previousRoomTemperature;
        private int _thermostat = 0; // default 0

        public RoomAction RoomState { get => previousRoomState; } 
        public int ThermoStat { get => _thermostat;  set => _thermostat = value; } 

         // Mark these fields as volatile so that their latest values are referenced.
//        private static volatile DeviceClient s_deviceClient;
 //       private static volatile ConnectionStatus s_connectionStatus = ConnectionStatus.Disconnected;
        
        public DeviceClientService(ILogger<DeviceClientService> logger, DeviceClient iotClient)
        {
            _logger = logger;
            _iotClient = iotClient;
            
          
        }
    }
}