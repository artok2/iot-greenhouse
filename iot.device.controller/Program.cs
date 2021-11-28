using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iot.device.controller
{
    class Program
    {
        
        private const string ModelId = "dtmi:pnptestapp:raspberrypi3;1";

        private static string idScope = Environment.GetEnvironmentVariable("DPS_IDSCOPE");
        private static string registrationId = Environment.GetEnvironmentVariable("DPS_REGISTRATION_ID");
        private static string primaryKey = Environment.GetEnvironmentVariable("DPS_PRIMARY_KEY");
        private static string secondaryKey = Environment.GetEnvironmentVariable("DPS_SECONDARY_KEY");
        private const string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";        
        
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                  
                 
                       services.AddSingleton<ILogger>(serviceProvider =>
                       {
                           var logger = serviceProvider.GetRequiredService<ILogger<DeviceClientService>>();
                           return logger;
                        });
                                              
                });

                
        

        private static async Task<DeviceRegistrationResult> SetupDeviceClientAsync(string registrationId,string deviceSymmetricKey,string dpsIdScope,string modelId)
        {
           
 
            var symmetricKeyProvider = new SecurityProviderSymmetricKey(registrationId, deviceSymmetricKey, null);
            var mqttTransportHandler = new ProvisioningTransportHandlerMqtt();
            var pdc = ProvisioningDeviceClient.Create("global.azure-devices-provisioning.net", dpsIdScope, symmetricKeyProvider, mqttTransportHandler);
 
            var pnpPayload = new ProvisioningRegistrationAdditionalData
            {
                JsonData = $"{{ \"modelId\": \"{modelId}\" }}",
            };
            
 
            return await pdc.RegisterAsync(pnpPayload);
        } 

         public static DeviceClient InitializeDeviceClient(DeviceRegistrationResult dpsRegistrationResult, string deviceSymmetricKey)
        {
            string hostname = dpsRegistrationResult.AssignedHub;
 
            var authenticationMethod = new DeviceAuthenticationWithRegistrySymmetricKey(dpsRegistrationResult.DeviceId, deviceSymmetricKey);

             
 
           var options = new ClientOptions
           {
               ModelId = ModelId
           };
 
            return DeviceClient.Create(hostname, authenticationMethod, TransportType.Mqtt, options);

        }

      
    }

} 