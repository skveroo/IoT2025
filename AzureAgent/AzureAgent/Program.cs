using Microsoft.Azure.Devices;
using Microsoft.Rest;
using Opc.UaFx;
using Opc.UaFx.Client;
using ServiceSDK.Lib;
using ServiceSdkDemo.Console;
using Microsoft.Azure.Devices.Client;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class Program
{
    public static string selectedDeviceId;
    public static async Task Main(string[] args)
    {
        string filePath = "../../../../settings.txt";
        string opcServerAddress = (File.ReadAllLines($"../../../../settings.txt")[1]);
        string connectionString = string.Empty;
        Console.WriteLine("Found devices:");

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            List<string> devices = new List<string>();
            List<string> connectionStrings = new List<string>();
            devices.Add("0");
            connectionStrings.Add("0");

            int j = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "device connection string:")
                {
                    if (i + 1 < lines.Length)
                    {
                        connectionString = lines[i + 1].Trim();
                    }
                }

                if (lines[i].Trim() == "devices:")
                {
                    for (int k = i + 1; k < lines.Length; k++)
                    {
                        if (lines[k].Trim().StartsWith("HostName="))
                        {
                            string line = lines[k].Trim();
                            j++;
                            connectionStrings.Add(lines[k]);
                            string deviceId = ExtractDeviceId(line);
                            devices.Add(deviceId);
                            Console.WriteLine($"{j}: {deviceId}");
                        }
                    }
                }
            }

            int id = 1;
            Console.WriteLine($"Selected Device ID: {opcServerAddress}");
            Console.WriteLine("Choose device number:");
            id = Convert.ToInt32(Console.ReadLine());
            selectedDeviceId = devices[id];
            Console.WriteLine($"Selected Device ID: {selectedDeviceId}");

            using var serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            using var registryManager = RegistryManager.CreateFromConnectionString(connectionString);

            var manager = new IoTHubManager(serviceClient, registryManager);
            using var opcClient = new OpcClient($"{opcServerAddress}");
            opcClient.Connect();
            Console.WriteLine("Connected to OPC server.");

            string deviceClientConnectionString = connectionStrings[id];
            using var deviceClient = DeviceClient.CreateFromConnectionString(deviceClientConnectionString, TransportType.Mqtt);
            await deviceClient.OpenAsync();

            int input;   
            var device = new device(deviceClient);
            await device.InitializeHandlers();
            do
            {
                FeatureSelector.PrintMenu();
                input = FeatureSelector.ReadInput();
                await FeatureSelector.Execute(input, manager, selectedDeviceId, id, deviceClient);
            } while (input != 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static string ExtractDeviceId(string line)
    {
        string deviceId = string.Empty;

        string[] parts = line.Split(';');
        foreach (string part in parts)
        {
            if (part.StartsWith("DeviceId="))
            {
                deviceId = part.Substring("DeviceId=".Length);
                break;
            }
        }

        return deviceId;
    }
}
