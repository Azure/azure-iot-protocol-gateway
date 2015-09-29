namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface IDeviceClient
    {
        Task SendAsync(Message message);

        Task<Message> ReceiveAsync();

        Task AbandonAsync(string lockToken);

        Task CompleteAsync(string lockToken);

        Task RejectAsync(string lockToken);

        Task DisposeAsync();
    }
}