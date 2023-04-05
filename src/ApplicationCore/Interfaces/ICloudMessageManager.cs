using System.Collections.Concurrent;
using System.Threading.Tasks;
using Model;

namespace ApplicationCore.Interfaces
{
    public interface ICloudMessageManager
    {
        Task StartServices();
        void StopServices();
        bool Enqueue(Message message);
        void RegisterDevice(DeviceRegistration registration);
        string GetStats();
        public ConcurrentDictionary<string, Message> Messages { get; set; }
    }
}