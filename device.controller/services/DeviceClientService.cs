

using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using device.controller.models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using device.controller.helpers;
using Microsoft.Azure.Devices.Client.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

namespace device.controller.services
{


    public static class ExtensionOperation
    {
        public static bool TryParse<T>(string s, out T value) {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
            try {
                value = (T) converter.ConvertFromString(s);
                return true;
            } catch {
                value = default(T);
                return false;
            }
        }
    }
    public interface IDeviceClientService
    {
        Task UpdateDeviceTwinAsync(string propertyName, object value, object userContext);

        Task InitializeAndSetupClientAsync(Func <ICollection<KeyValuePair<string, object>>, Task <IEnumerable<KeyValuePair<string, object>>>>  callbackDevice, CancellationToken cancellationToken);
        Task SendMessagesAsync(Message message, CancellationToken cancellationToken);
        Task receiveMessagesAsync(CancellationToken cancellationToken);

        //Task<string> DeviceTwinGetInitialState(String name);
        Task<Nullable<T>> DeviceTwinGetInitialState<T>(String name) where T: struct;

        void ReleaseResources();
        
    }
   
    public class DeviceClientService : IDeviceClientService
    {
        private static readonly TransportType[] _amqpTransports = new[] { TransportType.Amqp, TransportType.Amqp_Tcp_Only, TransportType.Amqp_WebSocket_Only };
         private Func <ICollection<KeyValuePair<string, object>>,  Task <IEnumerable<KeyValuePair<string, object>>>> _callbackDevice;
        private readonly ILogger _logger;        
        private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);

        private readonly List<string> _deviceConnectionStrings;

        private static readonly TimeSpan s_sleepDuration = TimeSpan.FromSeconds(5);
        private readonly TransportType _transportType;
         // Mark these fields as volatile so that their latest values are referenced.
        private static volatile DeviceClient s_deviceClient;
        private static volatile ConnectionStatus s_connectionStatus = ConnectionStatus.Disconnected;

         // An UnauthorizedException is handled in the connection status change handler through its corresponding status change event.
        // We will ignore this exception when thrown by the client API operation.
        private readonly Dictionary<Type, string> _exceptionsToBeIgnored = new Dictionary<Type, string> { { typeof(UnauthorizedException), "Unauthorized exceptions are handled by the ConnectionStatusChangeHandler." } };
        private readonly ClientOptions _clientOptions = new ClientOptions { SdkAssignsMessageId = SdkAssignsMessageId.WhenUnset };
        private bool IsDeviceConnected => s_connectionStatus == ConnectionStatus.Connected;
   
        public DeviceClientService(List<string> connectionStrings, TransportType transportType, ILogger logger )
        {
            //,DeviceClient iotClient
            _logger = logger;
            
             if (connectionStrings == null
                || !connectionStrings.Any())
            {
                throw new ArgumentException("At least one connection string must be provided.", nameof(connectionStrings));
            }
            _deviceConnectionStrings = connectionStrings;

            _logger.LogInformation($"Supplied with {_deviceConnectionStrings.Count} connection string(s).");

            _transportType = transportType;
            _logger.LogInformation($"Using {_transportType} transport.");            
        }
            
          
        public async Task InitializeAndSetupClientAsync(Func <ICollection<KeyValuePair<string, object>>, Task <IEnumerable<KeyValuePair<string, object>>>>  callbackDevice, CancellationToken cancellationToken)
        {
            _callbackDevice = callbackDevice;
            if (ShouldClientBeInitialized(s_connectionStatus))
            {
                // Allow a single thread to dispose and initialize the client instance.
                await _initSemaphore.WaitAsync();
                try
                {
                    if (ShouldClientBeInitialized(s_connectionStatus))
                    {
                        _logger.LogDebug($"Attempting to initialize the client instance, current status={s_connectionStatus}");

                        // If the device client instance has been previously initialized, then dispose it.
                        if (s_deviceClient != null)
                        {
                            await s_deviceClient.CloseAsync();
                            s_deviceClient.Dispose();
                            s_deviceClient = null;
                        }

                        s_deviceClient = DeviceClient.CreateFromConnectionString(_deviceConnectionStrings.First(), _transportType, _clientOptions);
                        s_deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChanges);
                        _logger.LogDebug("Initialized the client instance.");
                    }
                }
                finally
                {
                    _initSemaphore.Release();
                }

                // Force connection now.
                // We have set the "shouldExecuteOperation" function to always try to open the connection.
                // OpenAsync() is an idempotent call, it has the same effect if called once or multiple times on the same client.
                await RetryOperationHelper.RetryTransientExceptionsAsync(
                    operationName: "OpenConnection",
                    asyncOperation: async () => await s_deviceClient.OpenAsync(cancellationToken),
                    shouldExecuteOperation: () => true,
                    logger: _logger,
                    exceptionsToBeIgnored: _exceptionsToBeIgnored,
                    cancellationToken: cancellationToken);
                _logger.LogDebug($"The client instance has been opened.");

                // You will need to subscribe to the client callbacks any time the client is initialized.
                await RetryOperationHelper.RetryTransientExceptionsAsync(
                    operationName: "SubscribeTwinUpdates",
                    asyncOperation: async () => await s_deviceClient.SetDesiredPropertyUpdateCallbackAsync(HandleTwinUpdateNotificationsAsync, cancellationToken),
                    shouldExecuteOperation: () => IsDeviceConnected,
                    logger: _logger,
                    exceptionsToBeIgnored: _exceptionsToBeIgnored,
                    cancellationToken: cancellationToken);
                _logger.LogDebug("The client has subscribed to desired property update notifications.");
            }
        }

       private async Task InitializeAndOpenClientAsync()
        {
            if (ShouldClientBeInitialized(s_connectionStatus))
            {
                // Allow a single thread to dispose and initialize the client instance.
                await _initSemaphore.WaitAsync();
                try
                {
                    if (ShouldClientBeInitialized(s_connectionStatus))
                    {
                        _logger.LogDebug($"Attempting to initialize the client instance, current status={s_connectionStatus}");

                        // If the device client instance has been previously initialized, then dispose it.
                        if (s_deviceClient != null)
                        {
                            await s_deviceClient.CloseAsync();
                            s_deviceClient.Dispose();
                            s_deviceClient = null;
                             _logger.LogDebug($"Client instance disbosed, current status={s_connectionStatus}");

                        
                        }
                    }

                    s_deviceClient = DeviceClient.CreateFromConnectionString(_deviceConnectionStrings.First(), _transportType, _clientOptions);
                    s_deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChanges);
                    _logger.LogDebug($"Initialized the client instance.");
                }
                finally
                {
                    _initSemaphore.Release();
                }

                try
                {
                    // Force connection now.
                    // OpenAsync() is an idempotent call, it has the same effect if called once or multiple times on the same client.
                    await s_deviceClient.OpenAsync();
                    _logger.LogDebug($"Opened the client instance.");
                    
                  // _thermostat = DeviceTwinGetInitialState(s_deviceClient, "Thermostat");
                }
                catch (UnauthorizedException)
                {
                    // Handled by the ConnectionStatusChangeHandler
                }
            }
        }

        /// <summary>
        /// It is not good practice to have async void methods, however, DeviceClient.SetConnectionStatusChangesHandler() event handler signature has a void return type.
        /// As a result, any operation within this block will be executed unmonitored on another thread.
        /// To prevent multi-threaded synchronization issues, the async method InitializeClientAsync being called in here first grabs a lock
        /// before attempting to initialize or dispose the device client instance.
        /// <param name="ConnectionStatus">Connection status, etc. connected, disconnected ...</param>
        /// <param name="ConnectionStatusChangeReason">Connection status reason of connection error</param>
        /// </summary>               
        private async void ConnectionStatusChanges(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            _logger.LogInformation($"Connection status changed: status={status}, reason={reason}");
            s_connectionStatus = status;

            switch (status)
            {
                case ConnectionStatus.Connected:
                    _logger.LogDebug("### The DeviceClient is CONNECTED; all operations will be carried out as normal.");
                    break;

                case ConnectionStatus.Disconnected_Retrying:
                    _logger.LogDebug("### The DeviceClient is retrying based on the retry policy. Do NOT close or open the DeviceClient instance");
                    break;

                case ConnectionStatus.Disabled:
                    _logger.LogDebug("### The DeviceClient has been closed gracefully." +
                        "\nIf you want to perform more operations on the device client, you should dispose (DisposeAsync()) and then open (OpenAsync()) the client.");
                    break;

                case ConnectionStatus.Disconnected:
                    switch (reason)
                    {
                        case ConnectionStatusChangeReason.Bad_Credential:
                            // When getting this reason, the current connection string being used is not valid.
                            // If we had a backup, we can try using that.
                            _deviceConnectionStrings.RemoveAt(0);
                            if (_deviceConnectionStrings.Any())
                            {
                                _logger.LogWarning($"The current connection string is invalid. Trying another.");
                                await InitializeAndOpenClientAsync();
                                break;
                            }

                            _logger.LogWarning("### The supplied credentials are invalid. Update the parameters and run again.");
                            break;

                        case ConnectionStatusChangeReason.Device_Disabled:
                            _logger.LogWarning("### The device has been deleted or marked as disabled (on your hub instance)." +
                                "\nFix the device status in Azure and then create a new device client instance.");
                            break;

                        case ConnectionStatusChangeReason.Retry_Expired:
                            _logger.LogWarning("### The DeviceClient has been disconnected because the retry policy expired." +
                                "\nIf you want to perform more operations on the device client, you should dispose (DisposeAsync()) and then open (OpenAsync()) the client.");

                            await InitializeAndOpenClientAsync();
                            break;

                        case ConnectionStatusChangeReason.Communication_Error:
                            _logger.LogWarning("### The DeviceClient has been disconnected due to a non-retry-able exception. Inspect the exception for details." +
                                "\nIf you want to perform more operations on the device client, you should dispose (DisposeAsync()) and then open (OpenAsync()) the client.");

                            await InitializeAndOpenClientAsync();
                            break;

                        default:
                            _logger.LogError("### This combination of ConnectionStatus and ConnectionStatusChangeReason is not expected, contact the client library team with logs.");
                            break;

                    }

                    break;

                default:
                    _logger.LogError("### This combination of ConnectionStatus and ConnectionStatusChangeReason is not expected, contact the client library team with logs.");
                    break;
            }
        }
        public virtual void ReleaseResources()
        {
            _initSemaphore.Dispose();
        }

        private async Task HandleTwinUpdateNotificationsAsync(TwinCollection twinUpdateRequest, object userContext)
        {
            CancellationToken cancellationToken = (CancellationToken)userContext;

            if (!cancellationToken.IsCancellationRequested)
            {
                var reportedProperties = new TwinCollection();

                _logger.LogInformation($"Twin property update requested: \n{twinUpdateRequest.ToJson()}");

                
                var objList = twinUpdateRequest.Cast<KeyValuePair<string, object>>().ToList();
           
                var reportedProps =  await _callbackDevice(objList);
                               
                if (reportedProps.Count() > 0)
                {
                    foreach (KeyValuePair<string, object> desiredProperty in reportedProps)
                    {
                         reportedProperties[desiredProperty.Key] = desiredProperty.Value;

                    }
                    await RetryOperationHelper.RetryTransientExceptionsAsync(
                        operationName: "UpdateReportedProperties",
                        asyncOperation: async () => await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken),
                        shouldExecuteOperation: () => IsDeviceConnected,
                        logger: _logger,
                        exceptionsToBeIgnored: _exceptionsToBeIgnored,
                        cancellationToken: cancellationToken);

                }               
            }
        }

        public async Task UpdateDeviceTwinAsync(string propertyName, object value, object userContext)
        {
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties[propertyName] = value; 
        
            CancellationToken cancellationToken = (CancellationToken)userContext;

            if (!cancellationToken.IsCancellationRequested)
            { 
                await RetryOperationHelper.RetryTransientExceptionsAsync(
                    operationName: "UpdateReportedProperties",
                    asyncOperation: async () => await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken),
                    shouldExecuteOperation: () => IsDeviceConnected,
                    logger: _logger,
                    exceptionsToBeIgnored: _exceptionsToBeIgnored,
                    cancellationToken: cancellationToken);
            }        
        }

        public async Task SendMessagesAsync(Message message, CancellationToken cancellationToken)
        {
            if (IsDeviceConnected)
            {                
                await RetryOperationHelper.RetryTransientExceptionsAsync(
                    operationName: $"SendD2CMessage",
                    asyncOperation: async () => await s_deviceClient.SendEventAsync(message),
                    shouldExecuteOperation: () => IsDeviceConnected,
                    logger: _logger,
                    exceptionsToBeIgnored: _exceptionsToBeIgnored,
                    cancellationToken: cancellationToken);

                _logger.LogInformation($"Device sent message one to IoT hub.");
            }           
        }

        public async Task receiveMessagesAsync(CancellationToken cancellationToken)
        {
            var c2dReceiveExceptionsToBeIgnored = new Dictionary<Type, string>(_exceptionsToBeIgnored)
            {
                { typeof(DeviceMessageLockLostException), "Attempted to complete a received message whose lock token has expired" }
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!IsDeviceConnected)
                {
                    await Task.Delay(s_sleepDuration);
                    continue;
                }
                else if (_transportType == TransportType.Http1)
                {
                    // The call to ReceiveAsync over HTTP completes immediately, rather than waiting up to the specified
                    // time or when a cancellation token is signaled, so if we want it to poll at the same rate, we need
                    // to add an explicit delay here.
                    await Task.Delay(s_sleepDuration);
                }

                _logger.LogInformation($"Device waiting for C2D messages from the hub for {s_sleepDuration}." +
                    $"\nUse the IoT Hub Azure Portal or Azure IoT Explorer to send a message to this device.");

                await RetryOperationHelper.RetryTransientExceptionsAsync(
                    operationName: "ReceiveAndCompleteC2DMessage",
                    asyncOperation: async () => await ReceiveMessageAndCompleteAsync(),
                    shouldExecuteOperation: () => IsDeviceConnected,
                    logger: _logger,
                    exceptionsToBeIgnored: c2dReceiveExceptionsToBeIgnored,
                    cancellationToken: cancellationToken);
            }
        }

        public async Task ReceiveMessageAndCompleteAsync()
        {
            using var cts = new CancellationTokenSource(s_sleepDuration);
            Message receivedMessage = null;
            try
            {
                // AMQP library does not take a cancellation token but does take a time span
                // so we'll call this API differently.
                if (_amqpTransports.Contains(_transportType))
                {
                    receivedMessage = await s_deviceClient.ReceiveAsync(s_sleepDuration);
                }
                else
                {
                    receivedMessage = await s_deviceClient.ReceiveAsync(cts.Token);
                }
            }
            catch (IotHubCommunicationException ex) when (ex.InnerException is OperationCanceledException)
            {
                _logger.LogInformation("Timed out waiting to receive a message.");
            }

            if (receivedMessage == null)
            {
                _logger.LogInformation("No message received.");
                return;
            }

            try
            {
                string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                var formattedMessage = new StringBuilder($"Received message: [{messageData}]");

                foreach (var prop in receivedMessage.Properties)
                {
                    formattedMessage.AppendLine($"\n\tProperty: key={prop.Key}, value={prop.Value}");
                }
                _logger.LogInformation(formattedMessage.ToString());

                await s_deviceClient.CompleteAsync(receivedMessage);
                _logger.LogInformation($"Completed message [{messageData}].");
            }
            finally
            {
                receivedMessage.Dispose();
            }
        }
 
        public async Task<Nullable<T>> DeviceTwinGetInitialState<T>(String name) where T: struct 
        {
            Twin twin = await s_deviceClient.GetTwinAsync().ConfigureAwait(false);   
            if (!twin.Properties.Desired.Contains(name)) return null;    
            ExtensionOperation.TryParse<T>(Convert.ToString(twin.Properties.Desired[name]),out T value);
            return value;        
            
            
          
            
        }


        public delegate bool TryParseHandler<T>(string value, out T result);


        /// <summary>
        /// If the client reports Connected status, it is already in operational state.
        /// If the client reports Disconnected_retrying status, it is trying to recover its connection.
        /// If the client reports Disconnected status, you will need to dispose and recreate the client.
        /// If the client reports Disabled status, you will need to dispose and recreate the client.
        /// <param name="ConnectionStatus">Connection status, etc. connected, disconnected ...</param>
        /// </summary>        
        private bool ShouldClientBeInitialized(ConnectionStatus connectionStatus)
        {
            return (connectionStatus == ConnectionStatus.Disconnected || connectionStatus == ConnectionStatus.Disabled)
                && _deviceConnectionStrings.Any();
        }
    }
}