using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Rest;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Text;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using Opc.Ua;
using System.Runtime.Intrinsics.Arm;
using ServiceSDK.Lib;
enum Errors
{
    EmergencyStop = 1,
    PowerFailure = 2,
    SensorFailue = 4,
    Unknown = 8
}

namespace ServiceSdkDemo.Console
{
    internal static class FeatureSelector
    {
        public static bool twinInitialized = false;
        public static void PrintMenu()
        {
            System.Console.WriteLine(@"
    1 - C2D
    2 - Direct Method
    3 - Device Twin
    4 - Display Telemetry Data
    5 - Disconnect from device
    0 - Exit");
        }

        public static async Task Execute(int feature, ServiceSDK.Lib.IoTHubManager manager, string deviceId, int id, DeviceClient deviceClient)
        {
            var client = new OpcClient("opc.tcp://localhost:4840/");
            client.Connect();

            
           
           
            switch (feature)
            {
                case 1:
                    {
                        System.Console.WriteLine("\nType your message (confirm with enter):");
                        System.Console.WriteLine($"{deviceId}");
                        string messageText = System.Console.ReadLine() ?? string.Empty;
                        await manager.SendMessage(messageText, deviceId);
                        System.Console.WriteLine("Message sent!");
                    }
                    break;
                case 2:
                    {
                        try
                        {
                            var result = await manager.ExecuteDeviceMethod("SendMessages", deviceId);
                            System.Console.WriteLine($"Method executed with status {result}");
                        }
                        catch (DeviceNotFoundException)
                        {
                            System.Console.WriteLine("Device not connected!");
                        }
                    }
                    break;
                case 3:
                    {
                        await updateTwinData(deviceClient, client, id);
                        await manager.SetDesiredProductionRate(50, deviceId);
                    }
                    break;
                case 4:
                    {
                        displayTelemetry(id, client);
                        var data = new System.Dynamic.ExpandoObject() as dynamic;
                        await SendTelemetryData(deviceClient, data);

                    }
                    break;
                case 5:
                    {

                        System.Console.WriteLine("\nDisconnecting...");
                        await Task.Delay(2000);
                        System.Console.Clear();
                        await manager.Disconnect();
                        RestartProgram();
                    }
                    break;
                default:
                    break;
            }



        }

        public static async Task SendTelemetryData(DeviceClient client, dynamic data)
        {
            System.Console.WriteLine("Device sending telemetry data to IoT Hub...");
            var dataString = JsonConvert.SerializeObject(data);
            Microsoft.Azure.Devices.Client.Message eventMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(dataString))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };
            await client.SendEventAsync(eventMessage);
        }


        private static void RestartProgram()
        {
            string fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (fileName != null)
            {
                System.Diagnostics.Process.Start(fileName);
                Environment.Exit(0);
            }
        }

        private static void DisplayNodeData(OpcClient client, OpcNodeInfo node, string indent = "")
        {
            if (node is OpcObjectNodeInfo || node is OpcObjectTypeNodeInfo)
            {
                System.Console.WriteLine($"{indent}{node.DisplayName}:");
                foreach (var child in node.Children())
                {
                    DisplayNodeData(client, child, indent + "\t");
                }
            }
            else if (node is OpcVariableNodeInfo variableNode)
            {
                try
                {
                    var value = client.ReadNode(variableNode.NodeId);
                    System.Console.WriteLine($"{indent}{variableNode.DisplayName}: {value}");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"{indent}{variableNode.DisplayName}: Failed to read ({ex.Message})");
                }
            }
        }

        internal static int ReadInput()
        {
            var keyPressed = System.Console.ReadKey();
            var isParsed = int.TryParse(keyPressed.KeyChar.ToString(), out int result);
            return isParsed ? result : -1;
        }

        public static async Task updateTwinData(DeviceClient deviceClient, OpcClient client, int id)
        {
            var twin = await deviceClient.GetTwinAsync();
            if (!twinInitialized)
            {
                await updateTwinError(errorDetection(client, id), deviceClient);
                await updateTwinProductionValues(getProductionRate(client, id), deviceClient);
                twinInitialized = true;
            }
        }
        public static async Task updateTwinError(string deviceErrors, DeviceClient deviceClient)
        {
            var reportedDeviceTwin = new TwinCollection();
            reportedDeviceTwin["deviceErrors"] = deviceErrors;
            reportedDeviceTwin["lastErrorDate"] = DateTime.Today;
            await deviceClient.UpdateReportedPropertiesAsync(reportedDeviceTwin);
        }
        public static async Task updateTwinProductionValues(int reportedProduction, DeviceClient deviceClient)
        {
            var reportedProperties = new TwinCollection();
            reportedProperties["reportedProduction"] = reportedProduction;

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private static string errorDetection(OpcClient client, int id)
        {
            string errors = string.Empty;

            int deviceErrors = (int)client.ReadNode($"ns=2;s=Device {id}/DeviceError").Value;

            var errorMessages = new List<string>();

            if ((deviceErrors & (int)Errors.Unknown) != 0)
                errorMessages.Add("Unknown");

            if ((deviceErrors & (int)Errors.SensorFailue) != 0)
                errorMessages.Add("SensorFailure");

            if ((deviceErrors & (int)Errors.PowerFailure) != 0)
                errorMessages.Add("PowerFailure");

            if ((deviceErrors & (int)Errors.EmergencyStop) != 0)
                errorMessages.Add("Emergency stop");

            errors = string.Join(", ", errorMessages);
            return errors;

        }
        private static int getProductionRate(OpcClient client, int id)
        {
            int productionRate = (int)client.ReadNode($"ns=2;s=Device {id}/ProductionRate").Value;
            return productionRate;
        }
        private static void displayTelemetry(int id, OpcClient client)
            {
                            string deviceNumber = $"Device {id}";
                            System.Console.WriteLine();
                            OpcReadNode[] nodeAttributes = new OpcReadNode[] {
                            new OpcReadNode($"ns=2;s={deviceNumber}/ProductionStatus", OpcAttribute.DisplayName),
                            new OpcReadNode($"ns=2;s={deviceNumber}/ProductionRate", OpcAttribute.DisplayName),
                            new OpcReadNode($"ns=2;s={deviceNumber}/WorkorderId", OpcAttribute.DisplayName),
                            new OpcReadNode($"ns=2;s={deviceNumber}/Temperature", OpcAttribute.DisplayName),
                            new OpcReadNode($"ns=2;s={deviceNumber}/GoodCount", OpcAttribute.DisplayName),
                            new OpcReadNode($"ns=2;s={deviceNumber}/BadCount", OpcAttribute.DisplayName),
                            new OpcReadNode($"ns=2;s={deviceNumber}/DeviceError", OpcAttribute.DisplayName),
};
                            OpcReadNode[] nodeValues = new OpcReadNode[] {
                            new OpcReadNode($"ns=2;s={deviceNumber}/ProductionStatus"),
                            new OpcReadNode($"ns=2;s={deviceNumber}/ProductionRate"),
                            new OpcReadNode($"ns=2;s={deviceNumber}/WorkorderId"),
                            new OpcReadNode($"ns=2;s={deviceNumber}/Temperature"),
                            new OpcReadNode($"ns=2;s={deviceNumber}/GoodCount"),
                            new OpcReadNode($"ns=2;s={deviceNumber}/BadCount"),
                            new OpcReadNode($"ns=2;s={deviceNumber}/DeviceError"),
};
                            var data = new System.Dynamic.ExpandoObject() as dynamic;
                            IEnumerable<OpcValue> attributeResults = client.ReadNodes(nodeAttributes);
                            IEnumerable<OpcValue> valueResults = client.ReadNodes(nodeValues);
                            var combinedResults = attributeResults.Zip(valueResults, (attribute, value) => new { Attribute = attribute.Value, Value = value.Value });
                            foreach (var item in combinedResults)
                            {
                                ((IDictionary<string, object>) data)[item.Attribute.ToString()] = item.Value;
                            }

                            foreach (var prop in (IDictionary<string, object>) data)
                            {
                                System.Console.WriteLine($"{prop.Key}: {prop.Value}");
                            }
            }
    }
    public class device
    {
        public static DeviceClient deviceClient;
        public device(DeviceClient deviceClient)
        {
            device.deviceClient = deviceClient;
        }
        async Task EmergencyStop()
        {
            IoTHubManager.EmergencyStop();
            await (Task.Delay(1000));
        }
        private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
        {
            System.Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");
            await EmergencyStop();
            return new MethodResponse(0);
        }
        private static async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            System.Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");
            await Task.Delay(1000);

            return new MethodResponse(0);
        }
        public async Task InitializeHandlers()
        {
            await deviceClient.SetMethodDefaultHandlerAsync(DefaultServiceHandler, null);
            await deviceClient.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, null);
        }
    }
}