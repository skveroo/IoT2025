using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using Opc.UaFx;
using Opc.UaFx.Client;
using System;

namespace ServiceSdkDemo.Console
{
    
    internal static class FeatureSelector
    {
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

        public static async Task Execute(int feature, ServiceSDK.Lib.IoTHubManager manager, string deviceId,int id)
        {
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
                        System.Console.WriteLine("\nType property name (confirm with enter):");
                        string propertyName = System.Console.ReadLine() ?? string.Empty;
                        var random = new Random();
                        await manager.UpdateDesiredTwin(deviceId, propertyName, random.Next());
                    }
                    break;
                case 4:
                    {
                        using (var client = new OpcClient("opc.tcp://localhost:4840/"))
                        {
                            string deviceNumber= $"Device {id}";
                            
                            client.Connect();
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

                            IEnumerable<OpcValue> attributeResults = client.ReadNodes(nodeAttributes);
                            IEnumerable<OpcValue> valueResults = client.ReadNodes(nodeValues);
                            var combinedResults = attributeResults.Zip(valueResults, (attribute, value) => new { Attribute = attribute.Value, Value = value.Value });
                            foreach (var item in combinedResults)
                            {
                                System.Console.WriteLine($"{item.Attribute}: {item.Value}");
                            }
                        }
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
    }
}