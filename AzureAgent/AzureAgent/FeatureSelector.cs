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
    0 - Exit");
        }

        public static async Task Execute(int feature, ServiceSDK.Lib.IoTHubManager manager)
        {
            switch (feature)
            {
                case 1:
                    {
                        System.Console.WriteLine("\nType your message (confirm with enter):");
                        string messageText = System.Console.ReadLine() ?? string.Empty;

                        System.Console.WriteLine("Type your device ID (confirm with enter):");
                        string deviceId = System.Console.ReadLine() ?? string.Empty;

                        await manager.SendMessage(messageText, deviceId);

                        System.Console.WriteLine("Message sent!");
                    }
                    break;
                case 2:
                    {
                        System.Console.WriteLine("\nType your device ID (confirm with enter):");
                        string deviceId = System.Console.ReadLine() ?? string.Empty;
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

                        System.Console.WriteLine("\nType your device ID (confirm with enter):");
                        string deviceId = System.Console.ReadLine() ?? string.Empty;

                        var random = new Random();
                        await manager.UpdateDesiredTwin(deviceId, propertyName, random.Next());
                    }
                    break;
                case 4:
                    {
                        using var opcClient = new OpcClient("opc.tcp://localhost:4840");

                        try
                        {
                            opcClient.Connect();
                            System.Console.WriteLine("");
                            System.Console.WriteLine("Connected to OPC server.");
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"Failed to connect to OPC server: {ex.Message}");
                            return;
                        }

                        var rootNode = opcClient.BrowseNode(OpcObjectTypes.ObjectsFolder);
                        foreach (var node in rootNode.Children())
                        {
                            System.Console.WriteLine($"Node found: {node.DisplayName} - {node.NodeId}");
                        }

                        var deviceNode = rootNode.Children().FirstOrDefault(n => n.DisplayName == "device");
                        if (deviceNode != null)
                        {
                            DisplayNodeData(opcClient, deviceNode);
                        }
                        else
                        {
                            System.Console.WriteLine("Device folder not found.");
                        }
                    }
                    break;
                default:
                    break;
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
