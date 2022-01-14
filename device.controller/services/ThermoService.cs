namespace device.controller.services
{
    using System.Threading;
    using System.Threading.Tasks;
    using device.controller.models;
    using device.controller.services;
    using Microsoft.Azure.Devices.Shared;
    using System.Collections.Generic;
    using System;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Azure.Devices.Client;
    using device.controller.helpers;
    using Microsoft.Azure.Devices.Client.Exceptions;
    
    public interface IThermoService
    {        
        Task UpdateRoomTemperature(double temperature, CancellationToken cancellationToken);
        Task UpdateRoomAction(Telemetry telemetry, CancellationToken cancellationToken);
        Task InitializeAsync(CancellationToken cancellationToken);
        Task SendMessageIotHubAsync(Telemetry telemetry, CancellationToken cancellationToken);             

        void ReleaseResources();

        RoomAction RoomState { get; } 

        int ThermoStat { get; set; } 
    }

     public enum RoomAction { Unknown, Heating, Cooling, Green }


    public class ThermoService : DeviceClientService, IThermoService
    {
        const double TemperatureThreshold = 69.0;
        private int previousRoomTemperature;
        private volatile int  _thermostat = 0; // default 0
        public RoomAction RoomState { get => previousRoomState; } 
        public int ThermoStat { get => _thermostat;  set => _thermostat = value; } 
        private readonly ILogger<ThermoService> _logger;

        static RoomAction previousRoomState = RoomAction.Unknown;
        string[] stringArray = { "Thermostat" };

        private RoomAction _currentRoomAction;


        public ThermoService(List<string> connectionStrings, TransportType transportType, ILogger<ThermoService> logger, RoomAction roomAction = RoomAction.Cooling ) : base(connectionStrings, transportType, logger)
        {
            _logger = logger;
            _currentRoomAction = roomAction;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {            
            await InitializeAndSetupClientAsync(HandleTwinUpdateNotificationsAsync,cancellationToken);

            await GetThermostatInitialState();
        }

        private async Task< IEnumerable<KeyValuePair<string, object>>> HandleTwinUpdateNotificationsAsync(ICollection<KeyValuePair<string, object>> updateRequest)
        {
            if (updateRequest is null) throw new ArgumentNullException(nameof(updateRequest));

            var reportedProperties = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> desiredProperty in updateRequest)
            {
                if ( desiredProperty.Key == "Thermostat" )
                {
                    int.TryParse(Convert.ToString(desiredProperty.Value), out int thermoStatus);
                    _thermostat = thermoStatus;
                    _logger.LogInformation($"Setting property {desiredProperty.Key} to {desiredProperty.Value}.");
                    reportedProperties.Add(new KeyValuePair<string, object>(desiredProperty.Key, desiredProperty.Value));

                }                                         
            }
            return reportedProperties;
        }
    
         public async Task UpdateRoomTemperature(double temperature, CancellationToken cancellationToken)
        {
            if (previousRoomTemperature != (int)temperature)
            {                
                await UpdateDeviceTwinAsync("RoomTemperature", temperature, cancellationToken);
                previousRoomTemperature = (int)temperature;                
            }
        }

        public async Task UpdateRoomAction(Telemetry telemetry, CancellationToken cancellationToken)
        {
            var roomState = (int)telemetry.Temperature > _thermostat ? RoomAction.Cooling : (int)telemetry.Temperature < _thermostat ? RoomAction.Heating : RoomAction.Green;

            if (roomState != previousRoomState)
            {
                await UpdateDeviceTwinAsync("RoomAction", roomState.ToString(), cancellationToken);            
                previousRoomState = roomState;

            }
        }

        private async Task GetThermostatInitialState()
        {
            int defaultValue = 21;     
            var val = await DeviceTwinGetInitialState<int>("Thermostat");
            _thermostat = val != null ? (int)val : defaultValue;
             
        }

        public async Task SendMessageIotHubAsync(Telemetry telemetry, CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(telemetry);

            Message eventMessage = new Message(Encoding.UTF8.GetBytes(json));
            _logger.LogInformation($"Sending {json}");
            eventMessage.Properties.Add("temperatureAlert", (telemetry.CPUTemperature > TemperatureThreshold) ? "true" : "false");            

            await SendMessagesAsync(eventMessage ,cancellationToken);
        }

        public override void ReleaseResources()
        {
             base.ReleaseResources();
        }
    }
}