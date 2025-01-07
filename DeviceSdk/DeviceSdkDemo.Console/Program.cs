using DeviceSdkDemo.Device;
using Microsoft.Azure.Devices.Client;

string deviceConnectionString = "";
using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
await deviceClient.OpenAsync();
var device = new VirtualDevice(deviceClient);
await device.InitializeHandlers();
await device.UpdateTwinAsync();

Console.WriteLine($"Connection success!");
Console.WriteLine("Finished! Press Enter to close...");
Console.ReadLine();
