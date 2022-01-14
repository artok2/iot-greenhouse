using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Threading;

namespace device.controller.services
{
    public class GpioService
    {
        
        private readonly GpioController _controller;
        private readonly List<int> list = new List<int>();

        public GpioService()
        {        
            _controller = new GpioController();

        }

        public void SetFanOnOff(int gpioPin, bool state)
        {
            
            if (list.FindIndex(element => element == gpioPin) < 0) {
                list.Add(gpioPin);
                _controller.OpenPin(gpioPin, PinMode.Output);
            }
            var pinState = ((state) ? PinValue.Low : PinValue.High);
            var pinCurrentState =_controller.Read(gpioPin);

            if ( pinState != pinCurrentState )
                _controller.Write(gpioPin, pinState);
        }
        
        public void CloseAllDevices()
        {            
            Console.WriteLine("Closing down devices...");

            foreach (var gPioPinItem in list)
            {
                _controller.Write( gPioPinItem, PinValue.High);
                _controller.ClosePin(gPioPinItem);
            }
         
        }
    }
}