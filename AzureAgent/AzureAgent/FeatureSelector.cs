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
using Microsoft.Azure.Amqp.Framing;
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
        public static bool Initialized = false;
        public static string ExistingError = string.Empty;
        public static async Task SendTelemetryData(DeviceClient client, dynamic data)
        {
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

        public static async Task updateTwinData(DeviceClient deviceClient, OpcClient client)
        {
                var twin = await deviceClient.GetTwinAsync();
                await updateTwinError(errorDetection(client, Program.id), deviceClient);
                await updateTwinProductionValues(getProductionRate(client, Program.id), deviceClient);
        }
        public static async Task updateTwinError(string deviceErrors, DeviceClient deviceClient)
        {
            var reportedDeviceTwin = new TwinCollection();
            reportedDeviceTwin["deviceErrors"] = deviceErrors;
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
        private static void displayTelemetry(OpcClient client, DeviceClient deviceClient)
            {
                            string deviceNumber = $"Device {Program.id}";
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
                        
                            ((IDictionary<string, object>)data)["DeviceId"] = Program.selectedDeviceId;

                             foreach (var prop in (IDictionary<string, object>) data)
                            {
                                System.Console.WriteLine($"{prop.Key}: {prop.Value}");
                            }
                            SendTelemetryData(deviceClient, data);
        }


        

        public static async Task update(DeviceClient deviceClient, OpcClient client, ServiceSDK.Lib.IoTHubManager manager)
        {
            client.Connect();
          
            if (!Initialized)
            {
                displayTelemetry(client, deviceClient);
                await manager.CreateDesiredProductionRate(20);
                await manager.SetDesiredProductionRate();
                await updateTwinData(deviceClient, client);
                Initialized = true;
            }

            displayTelemetry(client, deviceClient);
            updateTwinData(deviceClient, client);
            string error = errorDetection(client, Program.id);
           
            if (error != string.Empty && error!= ExistingError)
            {
                ExistingError = error;
                var dataString = JsonConvert.SerializeObject(error);
                Microsoft.Azure.Devices.Client.Message eventMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(dataString))
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8"
                };
                await deviceClient.SendEventAsync(eventMessage);
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
        async Task ResetErrorStatus()
        {
            IoTHubManager.ResetErrorStatus();
            await (Task.Delay(1000));
        }
        private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
        {
            System.Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");
            await ResetErrorStatus();
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
            await deviceClient.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatusHandler, null);
        }
    }
}