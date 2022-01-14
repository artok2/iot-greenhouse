using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Logging;
using device.controller.models;
using device.controller.services;

namespace device.controller
{
    public class Worker : BackgroundService
    { 
        private readonly int _gPioPinFan = 16;
        private readonly GpioService _gpioService;
        const string DeviceId = "raspberrypi";
        private readonly ILogger<Worker> _logger;
        private readonly SensorService _sensorService;       
             
        private readonly IThermoService _thermoService;
        
 

        public Worker(ILogger<Worker> logger, IHostLifetime lifetime, IThermoService thermoService )
        {
            _logger = logger;
            _logger.LogInformation("IsSystemd: {isSystemd}", lifetime.GetType() == typeof(SystemdLifetime));
            _logger.LogInformation("IHostLifetime: {hostLifetime}", lifetime.GetType());
            
            _gpioService = new GpioService();
            _sensorService = new SensorService (DeviceId,logger);
          
           // _deviceClientService.ThermoStat = _currentThermoStat;
            _thermoService = thermoService;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Log on startup.");
            try
            {              

                 _logger.LogWarning($"ExecuteAsync, intializes, warning");  
                 _logger.LogDebug($"ExecuteAsync, intializes, debug");
                await _thermoService.InitializeAsync(stoppingToken);
                await Task.WhenAll(SendTelemetryAsync(stoppingToken));//, _deviceClientService.receiveMessagesAsync(stoppingToken));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unrecoverable exception caught, user action is required, so exiting: \n{ex}");    
                  Environment.ExitCode = -1;          
            }
            _thermoService.ReleaseResources();          

        }

        private  async Task SendTelemetryAsync(CancellationToken stoppingToken)
        {
             while (!stoppingToken.IsCancellationRequested)
            {
                Telemetry telemetry = _sensorService.GetTelemetryReadings();
                 await _thermoService.SendMessageIotHubAsync(telemetry, stoppingToken);

                 await _thermoService.UpdateRoomAction(telemetry, stoppingToken);

                 await _thermoService.UpdateRoomTemperature(telemetry.Temperature, stoppingToken);   
              
                 _gpioService.SetFanOnOff(_gPioPinFan, _thermoService.RoomState == RoomAction.Heating);

                _logger.LogInformation("Process running.");
                await Task.Delay(24000, stoppingToken);

            }
        }

        public async override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker stopping");
            if ( _gpioService != null )
            {
               _gpioService.CloseAllDevices();
            }

            await base.StopAsync(cancellationToken);
        }
    }
}