using Microsoft.Azure.Devices;
using Opc.UaFx;
using Opc.UaFx.Client;
using ServiceSDK.Lib;
using ServiceSdkDemo.Console;


string filePath = "../../../../settings.txt";
string opcServerAddress = string.Empty;
string connectionString = string.Empty;
Console.WriteLine("Found devices:");
try
{
    string[] lines = File.ReadAllLines(filePath);
    List<string> devices = new List<string>();
    devices.Add("d0");

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
        
        if (lines[i].Trim() == "devices:" )
        {
            for (int k=i+1; k < lines.Length; k++)
            {
                j++;
                string deviceId = lines[k].Trim();
                devices.Add(deviceId);
                Console.WriteLine($"{j}: {deviceId}");
            }
           
        }
    }
    
        int id = 1;
        Console.WriteLine("Choose device number:");
        id = Convert.ToInt32(Console.ReadLine());
    Console.WriteLine($"Connection string: {connectionString}");
    Console.WriteLine($"Connection string: {devices[id]}");
    string selectedDeviceId = (devices[id]);
        using var serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
        using var registryManager = RegistryManager.CreateFromConnectionString(connectionString);

        var manager = new IoTHubManager(serviceClient, registryManager);
        using var opcClient = new OpcClient($"{opcServerAddress}");
        opcClient.Connect();
        Console.WriteLine("Connected to OPC server.");

        int input;
        do
        {
            FeatureSelector.PrintMenu();
            input = FeatureSelector.ReadInput();
            await FeatureSelector.Execute(input, manager, selectedDeviceId, id);
        } while (input != 0);

    }


catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
}




