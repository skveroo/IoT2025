using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Opc.UaFx;
using Opc.UaFx.Client;

namespace ServiceSDK.Lib
{

    public class IoTHubManager
    {
        private readonly ServiceClient client;

        private readonly RegistryManager registry;
        public static OpcClient OPCclient = new OpcClient(File.ReadAllLines($"../../../../settings.txt")[1]);
        
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
        public async Task SetDesiredProductionRate()
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