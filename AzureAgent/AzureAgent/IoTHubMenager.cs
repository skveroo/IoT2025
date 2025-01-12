using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task SendMessage(string messageText, string deviceId)
        {
            var messageBody = new { text = messageText };
            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
            message.MessageId = Guid.NewGuid().ToString();
            await client.SendAsync(deviceId, message);
        }

        public async Task<int> ExecuteDeviceMethod(string methodName, string deviceId)
        {
            var method = new CloudToDeviceMethod(methodName);

            var methodBody = new { nrOfMessages = 5, delay = 500 };
            method.SetPayloadJson(JsonConvert.SerializeObject(methodBody));

            var result = await client.InvokeDeviceMethodAsync(deviceId, method);
            return result.Status;
        }

        public async Task<int> ExecuteDeviceMethod2(int desired, string deviceId)
        {
            var method = new CloudToDeviceMethod("SetDesiredProductionRate");

            var methodBody = new { desiredNumber = desired, delay = 500 };
            method.SetPayloadJson(JsonConvert.SerializeObject(methodBody));

            var result = await client.InvokeDeviceMethodAsync(deviceId, method);
            return result.Status;
        }



        public async Task UpdateDesiredTwin(string deviceId, string propertyName, dynamic propertyValue)
        {
            var twin = await registry.GetTwinAsync(deviceId);
            twin.Properties.Desired[propertyName] = propertyValue;
            await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
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

                Console.WriteLine("Disconnected from IoT Hub.");
            }
        }
        public async Task SetDesiredProductionRate(int DPR, string deviceId)
        {
            var twin = await registry.GetTwinAsync(deviceId);
           
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
            await registry.UpdateTwinAsync(deviceId, JsonConvert.SerializeObject(patch), twin.ETag);
        }
        public static async Task EmergencyStop()
        {
            Console.WriteLine($"Device {Program.selectedDeviceId} shutting down ...");
            OPCclient.CallMethod($"ns=2;s=Device {Program.selectedDeviceId}", $"ns=2;s=Device {Program.selectedDeviceId}/EmergencyStop");
            OPCclient.WriteNode($"ns=2;s=Device {Program.selectedDeviceId}/ProductionRate", OpcAttribute.Value, 0);
            await Task.Delay(1000);
        }

        public static async Task ResetErrorStatus(string deviceId, OpcClient client)
        {
            client.CallMethod($"ns=2;s=Device {deviceId}", $"ns=2;s=Device {deviceId}/ResetErrorStatus");
            await Task.Delay(1000);
        }

    }
}