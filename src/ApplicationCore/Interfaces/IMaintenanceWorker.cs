using System.Threading.Tasks;

namespace ApplicationCore.Interfaces
{
    public interface IMaintenanceWorker
    {
        string Id { get; }
        public Task Task { get; }
        public string Status { get; }
        void Start(ICloudMessageService service);

        void Stop();
    }
}