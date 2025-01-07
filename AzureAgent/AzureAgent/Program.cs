using Microsoft.Azure.Devices;
using Opc.UaFx;
using Opc.UaFx.Client;
using ServiceSDK.Lib;
using ServiceSdkDemo.Console;

string serviceConnectionString = "";
using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

var manager = new IoTHubManager(serviceClient, registryManager);
using var opcClient = new OpcClient("opc.tcp://localhost:4840/");
opcClient.Connect();
Console.WriteLine("Connected to OPC server.");

int input;
do
{
    FeatureSelector.PrintMenu();
    input = FeatureSelector.ReadInput();
    await FeatureSelector.Execute(input, manager);
} while (input != 0);
