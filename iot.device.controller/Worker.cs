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

namespace iot.device.controller
{
    public class Worker : BackgroundService
    { 
        //private readonly GpioService _gpioService;
        const string DeviceId = "raspberrypi";
        private readonly ILogger<Worker> _logger;
        //private readonly SensorService _sensorService;
        private readonly IDeviceClientService _deviceClientService;

        private readonly RoomAction _currentRoomAction = RoomAction.Cooling; //RoomAction.Heating;
        private readonly int _currentThermoStat = 21; //16; //21
        
 

        public Worker(ILogger<Worker> logger, IHostLifetime lifetime /*, IDeviceClientService deviceClientService */)
        {
            _logger = logger;
            _logger.LogInformation("IsSystemd: {isSystemd}", lifetime.GetType() == typeof(SystemdLifetime));
            _logger.LogInformation("IHostLifetime: {hostLifetime}", lifetime.GetType());
            
            //_gpioService = new GpioService();
            //_sensorService = new SensorService (DeviceId);
           
          // _deviceClientService = deviceClientService;
           //_deviceClientService.ThermoStat = _currentThermoStat;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Log on startup.");
            while (!stoppingToken.IsCancellationRequested)
            {

                _logger.LogInformation("Process running.");
                await Task.Delay(1200, stoppingToken);

            }

        }

        public async override Task StopAsync(CancellationToken cancellationToken)
        {
          //  if ( _gpioService != null )
           // {
            //    _gpioService.CloseAllDevices();
           // }

            await base.StopAsync(cancellationToken);
        }
    }
}