using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.Ua;
using Opc.UaFx.Server;
using System.Net.Mime;
using System.Text;
using Opc.UaFx;

namespace DeviceSdkDemo.Device
{
    public class VirtualDevice
    {
        private readonly DeviceClient client;
        private int productionRate = 0;
        private int deviceErrors = 0;
        private readonly Random rnd = new Random();
        private OpcServer opcServer;

        public VirtualDevice(DeviceClient deviceClient)
        {
            this.client = deviceClient;
        }
        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"Unknown method: {methodRequest.Name}");
            return new MethodResponse(0);
        }

        public async Task InitializeHandlers()
        {
            await client.SetReceiveMessageHandlerAsync(OnC2dMessageReceivedAsync, client);
            await client.SetMethodHandlerAsync("SendMessages", SendMessagesHandler, client);
            await client.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, client);
            await client.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatusHandler, client);
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);
            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, client);
            InitializeOpcServer();
        }

        private readonly object _lock = new object();
        private OpcServer server;
        private readonly Dictionary<string, OpcDataVariableNode> nodeIds = new();
        private List<OpcFolderNode> folderNodes = new();
        private OpcFolderNode deviceFolder;

        private void InitializeOpcServer()
        {
            lock (_lock)
            {
                server?.Stop();
                server?.Dispose();
                nodeIds.Clear();
                folderNodes.Clear();

                deviceFolder = new OpcFolderNode("device");
                folderNodes.Add(deviceFolder);

                nodeIds["goodCount"] = new OpcDataVariableNode<int>(deviceFolder, "goodCount", 0);
                nodeIds["badCount"] = new OpcDataVariableNode<int>(deviceFolder, "badCount", 0);
                nodeIds["temperature"] = new OpcDataVariableNode<int>(deviceFolder, "temperature", 0);
                nodeIds["productionStatus"] = new OpcDataVariableNode<int>(deviceFolder, "productionStatus", 0);
                nodeIds["workOrderId"] = new OpcDataVariableNode<string>(deviceFolder, "workOrderId", string.Empty);

                server = new OpcServer("opc.tcp://localhost:4840/", folderNodes);
                server.Start();
                Console.WriteLine("OPC UA Server started at opc.tcp://localhost:4840/");
            }
        }

        public async Task SendTelemetryData()
        {
            var data = new
            {
                productionStatus = rnd.Next(0, 2),
                workOrderId = Guid.NewGuid().ToString(),
                goodCount = rnd.Next(1, 100),
                badCount = rnd.Next(1, 20),
                temperature = rnd.Next(20, 40)
            };

            SendToOpcServer(data);

            var dataString = JsonConvert.SerializeObject(data);
            var eventMessage = new Message(Encoding.UTF8.GetBytes(dataString))
            {
                ContentType = MediaTypeNames.Application.Json,
                ContentEncoding = "utf-8"
            };

            Console.WriteLine($"Sending telemetry: {dataString}");
            await client.SendEventAsync(eventMessage);
        }

        private void SendToOpcServer(dynamic data)
        {
            Console.WriteLine("Sending data to OPC Server:");
            Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));

            lock (_lock)
            {
                if (nodeIds.TryGetValue("goodCount", out var goodNode))
                    ((OpcDataVariableNode<int>)goodNode).Value = data.goodCount;

                if (nodeIds.TryGetValue("badCount", out var badNode))
                    ((OpcDataVariableNode<int>)badNode).Value = data.badCount;

                if (nodeIds.TryGetValue("temperature", out var tempNode))
                    ((OpcDataVariableNode<int>)tempNode).Value = data.temperature;

                if (nodeIds.TryGetValue("productionStatus", out var statusNode))
                    ((OpcDataVariableNode<int>)statusNode).Value = data.productionStatus;

                if (nodeIds.TryGetValue("workOrderId", out var workOrderNode))
                    ((OpcDataVariableNode<string>)workOrderNode).Value = data.workOrderId;
            }
        }
    
    public async Task UpdateTwinAsync()
        {
            var twin = await client.GetTwinAsync();
            Console.WriteLine($"Initial twin value: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");

            var reportedProperties = new TwinCollection
            {
                ["ProductionRate"] = productionRate,
                ["DeviceErrors"] = deviceErrors,
                ["DateTimeLastAppLaunch"] = DateTime.Now
            };

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"Desired properties changed: {JsonConvert.SerializeObject(desiredProperties)}");

            if (desiredProperties.Contains("ProductionRate"))
            {
                productionRate = desiredProperties["ProductionRate"];
                Console.WriteLine($"Updated Production Rate to: {productionRate}");
            }

            var reportedProperties = new TwinCollection
            {
                ["ProductionRate"] = productionRate,
                ["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now
            };

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        public async Task SendDeviceError(int errorFlag)
        {
            deviceErrors |= errorFlag;
            var errorData = new { deviceErrors };
            var messageString = JsonConvert.SerializeObject(errorData);
            var message = new Message(Encoding.UTF8.GetBytes(messageString))
            {
                ContentType = MediaTypeNames.Application.Json,
                ContentEncoding = "utf-8"
            };

            Console.WriteLine($"Sending device error: {messageString}");
            await client.SendEventAsync(message);
            await UpdateTwinAsync();
        }

        private async Task OnC2dMessageReceivedAsync(Message receivedMessage, object _)
        {
            Console.WriteLine($"C2D message received: {Encoding.ASCII.GetString(receivedMessage.GetBytes())}");
            await client.CompleteAsync(receivedMessage);
            receivedMessage.Dispose();
        }

        private async Task<MethodResponse> SendMessagesHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"Direct method executed: {methodRequest.Name}");

            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { nrOfMessages = 1, delay = 1000 });

            for (int i = 0; i < payload.nrOfMessages; i++)
            {
                await SendTelemetryData();
                await Task.Delay(payload.delay);
            }

            return new MethodResponse(0);
        }

        private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Emergency Stop triggered.");
            await SendDeviceError(1);
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Resetting device error status.");
            deviceErrors = 0;
            await UpdateTwinAsync();
            return new MethodResponse(0);
        }
    }
}
