namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth;

    public delegate Task<IIotHubClient> DeviceClientFactoryFunc(AuthenticationResult deviceCredentials);
}