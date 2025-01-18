using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Opc.UaFx;
using Opc.UaFx.Client;
using ServiceSdkDemo.Console;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;

namespace ServiceSDK.Lib
{

    public class IoTHubManager
    {
        private readonly ServiceClient client;
        private readonly RegistryManager registry;
        public static OpcClient OPCclient = new OpcClient(File.ReadAllLines($"../../../../settings.txt")[1]);
        public static string iotHubConnectionString = (File.ReadAllLines($"../../../../settings.txt")[4]);
        public IoTHubManager(ServiceClient client, RegistryManager registry)
        {
            this.client = client;
            this.registry = registry;
        }
        public async Task Disconnect()
        {
            if (client != null)
            {
                await client.CloseAsync();
                client.Dispose();
            }

            if (registry != null)
            {
                await registry.CloseAsync();
                registry.Dispose();

            }
        }
        public async Task CreateDesiredProductionRate(int DPR)
        {
            var twin = await registry.GetTwinAsync(Program.selectedDeviceId);
            var patch = new
            {
                properties = new
                {
                    desired = new
                    {
                        desiredProduction = DPR
                    }
                }
            };
            await registry.UpdateTwinAsync(Program.selectedDeviceId, JsonConvert.SerializeObject(patch), twin.ETag);

        }
        public static async Task EmergencyStop()
        {
            OPCclient.Connect();
            Console.WriteLine($"Device {Program.selectedDeviceId} shutting down ...");
            OPCclient.CallMethod($"ns=2;s=Device {Program.id}", $"ns=2;s=Device {Program.id}/EmergencyStop");
            OPCclient.WriteNode($"ns=2;s=Device {Program.id}/ProductionRate", OpcAttribute.Value, 0);
            await Task.Delay(1000);
            OPCclient.Disconnect();
        }
        public static async Task ResetErrorStatus()
        {
            OPCclient.Connect();
            OPCclient.CallMethod($"ns=2;s=Device {Program.id}", $"ns=2;s=Device {Program.id}/ResetErrorStatus");
            await Task.Delay(1000);
            OPCclient.Disconnect();
        }
        public static async Task SetDesiredProductionRate(int DPR)
        {
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

            try
            {
                var twin = await registryManager.GetTwinAsync(Program.selectedDeviceId);
                var desiredPropertiesPatch = new
                {
                    properties = new
                    {
                        desired = new
                        {
                            desiredProduction = DPR
                        }
                    }
                };
                string patchJson = JsonConvert.SerializeObject(desiredPropertiesPatch);
                await registryManager.UpdateTwinAsync(Program.selectedDeviceId, patchJson, twin.ETag);

                Console.WriteLine($"Successfully updated desiredProduction to {DPR} for device: {Program.selectedDeviceId}");
            }
            finally
            {
                // Dispose of the RegistryManager
                await registryManager.CloseAsync();
            }
            await Task.Delay(1000);
        }
        public static async Task DecreaseDesiredProductionRate(int DPR)
        {
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

            try
            {
                var twin = await registryManager.GetTwinAsync(Program.selectedDeviceId);
                int desiredProductionRate = 0;
                if (twin.Properties.Desired.Contains("desiredProduction"))
                {
                    desiredProductionRate = twin.Properties.Desired["desiredProduction"].ToObject<int>();
                }
                var desiredPropertiesPatch = new
                {
                    properties = new
                    {
                        desired = new
                        {
                            desiredProduction = desiredProductionRate - DPR
                        }
                    }
                };
                string patchJson = JsonConvert.SerializeObject(desiredPropertiesPatch);
                await registryManager.UpdateTwinAsync(Program.selectedDeviceId, patchJson, twin.ETag);

                Console.WriteLine($"Successfully updated desiredProduction to {desiredProductionRate - DPR} for device: {Program.selectedDeviceId}");
            }
            finally
            {
                // Dispose of the RegistryManager
                await registryManager.CloseAsync();
            }
            await Task.Delay(1000);
        }
        public async Task SetReportedProductionRate()
        {
            OPCclient.Connect();
            var twin = await registry.GetTwinAsync(Program.selectedDeviceId);
            var desiredProductionRate = 0; 
            if (twin.Properties.Desired.Contains("desiredProduction"))
            {
                desiredProductionRate = twin.Properties.Desired["desiredProduction"].ToObject<int>();
            }

            OPCclient.WriteNode($"ns=2;s=Device {Program.id}/ProductionRate", OpcAttribute.Value, desiredProductionRate);
            await Task.Delay(1000);
        }
    }
}