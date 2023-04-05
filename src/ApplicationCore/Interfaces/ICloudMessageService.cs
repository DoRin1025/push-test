using System.Threading.Tasks;
using Model;

namespace ApplicationCore.Interfaces
{
    public interface ICloudMessageService
    {
        Task DoMaintenance();
        Task Start();
        void Stop();
        bool Enqueue(Message message);
        void RegisterDevice(DeviceRegistration registration);
        string GetStats();
    }
}