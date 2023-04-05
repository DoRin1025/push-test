using System.Threading.Tasks;
using ApplicationCore.APNS;

namespace ApplicationCore.Interfaces
{
    public interface IMessageWorker
    {
        public const string StatusStarted = "STARTED";
        public const string StatusWaiting = "WAITING";
        public const string StatusRunning = "RUNNING";
        public const string StatusStopped = "STOPPED";

        string Id { get; set; }
        Task Task { get; set; }
        void Start(ApnService apnService);
        void Stop();
    }
}