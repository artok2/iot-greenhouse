using System;
using System.Threading;
using System.Device.I2c;
using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.FilteringMode;
using Iot.Device.Bmxx80.PowerMode;
using Iot.Device.CpuTemperature;
using device.controller.models;
using Microsoft.Extensions.Logging;
using UnitsNet;
using Iot.Device.Common;
using System.IO;


namespace device.controller.services
{
    public class SensorService
    {
        private Bme280 _bme280;
        private I2cDevice i2cDevice;
        private  int measurementTime;
        static CpuTemperature _temperature = new CpuTemperature();
        private int _msgId = 0;
        private string _sensorName;
        private readonly ILogger _logger;     

        // bus id on the raspberry pi 3
        const int busId = 1;
        // set this to the current sea level pressure in the area for correct altitude readings
        Pressure defaultSeaLevelPressure = WeatherHelper.MeanSeaLevel;



        public SensorService(string sensorName, ILogger logger) 
        {
            _sensorName = sensorName;
            var i2cSettings = new I2cConnectionSettings(busId, Bme280.SecondaryI2cAddress);
            i2cDevice = I2cDevice.Create(i2cSettings);
            

            _bme280 = new Bme280(i2cDevice)
            {
                // set higher sampling
                TemperatureSampling = Sampling.LowPower,
                PressureSampling = Sampling.UltraHighResolution,
                HumiditySampling = Sampling.Standard,

            };

            measurementTime = _bme280.GetMeasurementDuration();

          
            _logger = logger;
        }

        public Telemetry GetTelemetryReadings()
        {
            Temperature tempValue;
            Pressure preValue;
            RelativeHumidity humValue;
            Length altValue;

            try
            {
                _bme280.SetPowerMode(Bmx280PowerMode.Forced);
                Thread.Sleep(measurementTime+60);
                _bme280.TryReadTemperature(out tempValue);
                _bme280.TryReadPressure(out  preValue);
                 _bme280.TryReadHumidity(out humValue);
                _logger.LogDebug($"GetTelemetryReadings, temperature {tempValue}");


                // Note that if you already have the pressure value and the temperature, you could also calculate altitude by using
                 // var altValue = WeatherHelper.CalculateAltitude(preValue, defaultSeaLevelPressure, tempValue) which would be more performant.

                 altValue = WeatherHelper.CalculateAltitude(preValue, defaultSeaLevelPressure, tempValue);
            
                //_bme280.TryReadAltitude(out var altValue);
            }
            catch (IOException ioException)
            {
                _logger.LogDebug($"Reading Bme280 temperatures retry: {ioException.Message}");
            
                // see:https://github.com/dotnet/iot/issues/832
                 Thread.Sleep(100);
                

                _bme280.TryReadTemperature(out tempValue);
                _bme280.TryReadPressure(out  preValue);
                 _bme280.TryReadHumidity(out  humValue);
                _logger.LogDebug($"GetTelemetryReadings, temperature {tempValue}");


                // Note that if you already have the pressure value and the temperature, you could also calculate altitude by using
                 // var altValue = WeatherHelper.CalculateAltitude(preValue, defaultSeaLevelPressure, tempValue) which would be more performant.

                altValue = WeatherHelper.CalculateAltitude(preValue, defaultSeaLevelPressure, tempValue);
            
            }



            var telemetry = new Telemetry()
            {
                CPUTemperature = _temperature.IsAvailable ? Math.Round(_temperature.Temperature.DegreesCelsius, 2) : 0,
                Temperature = Math.Round(tempValue.DegreesCelsius, 2),
                Pressure = Math.Round(preValue.Hectopascals, 2),
                Humidity = Math.Round(humValue.Percent, 2),
                Altitude = Math.Round(altValue.Meters, 2),
                DeviceId = _sensorName,
                MessageId = _msgId++
            };
            _logger.LogDebug($"Reading Bme280 temperatures done");
            return telemetry;
        }
    }
}
