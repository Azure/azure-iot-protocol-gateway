namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth;

    public delegate Task<IDeviceClient> DeviceClientFactoryFunc(AuthenticationResult deviceCredentials);
}