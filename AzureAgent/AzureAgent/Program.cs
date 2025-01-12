using Microsoft.Azure.Devices;
using Microsoft.Rest;
using Opc.UaFx;
using Opc.UaFx.Client;
using ServiceSDK.Lib;
using ServiceSdkDemo.Console;
using Microsoft.Azure.Devices.Client;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;

string filePath = "../../../../settings.txt";
string opcServerAddress = string.Empty;
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
        if (lines[i].Trim() == "server addres:")
        {
            if (i + 1 < lines.Length)
            {
                opcServerAddress = lines[i + 1].Trim();
            }
        }

        if (lines[i].Trim() == "device connection string:")
        {
            if (i + 1 < lines.Length)
            {
                connectionString = lines[i + 1].Trim();
            }
        }

        if (lines[i].Trim() == "devices:")
        { 
            for (int k = i+1; k < lines.Length; k++)
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
        Console.WriteLine("Choose device number:");
        id = Convert.ToInt32(Console.ReadLine());
   // Console.WriteLine($"Connection string: {connectionString}");
   // Console.WriteLine($"DeviceId: {devices[id]}");
    string selectedDeviceId = (devices[id]);
        using var serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
        using var registryManager = RegistryManager.CreateFromConnectionString(connectionString);

        var manager = new IoTHubManager(serviceClient, registryManager);
        using var opcClient = new OpcClient($"{opcServerAddress}");
        opcClient.Connect();
        Console.WriteLine("Connected to OPC server.");
        int input;

    string deviceClientConnectionString = connectionStrings[id];
    // Console.WriteLine($"Connection string: {deviceClientConnectionString}");
    using var deviceClient = DeviceClient.CreateFromConnectionString(deviceClientConnectionString, TransportType.Mqtt);
    await deviceClient.OpenAsync();

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
static string ExtractDeviceId(string line)
{
    string deviceId = string.Empty;

    // Split the line into parts by semicolon
    string[] parts = line.Split(';');

    // Look for the part starting with "DeviceId="
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




