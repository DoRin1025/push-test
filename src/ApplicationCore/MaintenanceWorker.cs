using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApplicationCore
{
    public class MaintenanceWorker : IMaintenanceWorker
    {
        private const string StatusStarted = "STARTED";
        private const string StatusWaiting = "WAITING";
        private const string StatusRunning = "RUNNING";
        private const string StatusStopped = "STOPPED";

        private readonly int _maintenancePeriod;
        public string Id { get; }
        public Task Task { get; private set; }

        private volatile string _status;

        public string Status
        {
            get => _status;
            private set => _status = value;
        }

        private CancellationTokenSource CancellationTokenSource { get; set; }

        private ICloudMessageService MessageService { get; set; }

        private DateTime LastIterationDate { get; set; }

        private readonly ILogger<IMaintenanceWorker> _logger;

        public MaintenanceWorker(string id, ILogger<IMaintenanceWorker> logger, int maintenancePeriod)
        {
            Id = id;
            _logger = logger;
            _maintenancePeriod = maintenancePeriod;
        }

        public void Start(ICloudMessageService service)
        {
            MessageService = service;
            LastIterationDate = DateTime.MinValue;

            CancellationTokenSource = new CancellationTokenSource();

            Task = Task.Factory.StartNew(async () => await DoWork(), TaskCreationOptions.LongRunning);
            Task.ContinueWith(
                (t) => { _logger.LogCritical("Failed task for maintenance worker " + Id + ":\n" + t.Exception); },
                TaskContinuationOptions.OnlyOnFaulted);

            Status = StatusStarted;
        }

        public void Stop()
        {
            CancellationTokenSource.Cancel();
        }

        private async Task DoWork()
        {
            Status = StatusWaiting;

            while (!CancellationTokenSource.IsCancellationRequested)
            {
                if ((DateTime.Now - LastIterationDate).TotalSeconds < _maintenancePeriod)
                {
                    await Task.Delay(100);
                    continue;
                }

                LastIterationDate = DateTime.Now;

                Status = StatusRunning;
                await DoWorkIteration();
                Status = StatusWaiting;
            }

            _logger.LogCritical("Maintenance Worker " + Id + " cancellation requested");

            Status = StatusRunning;
            await DoWorkIteration();
            Status = StatusStopped;

            _logger.LogCritical("Maintenance Worker " + Id + " exited");
        }

        private async Task DoWorkIteration()
        {
            _logger.LogInformation("Maintenance Worker " + Id + " work iteration started");
            await MessageService.DoMaintenance();
        }
    }
}