using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
using Model;

namespace ApplicationCore.APNS
{
    public class ApnService : ICloudMessageService
    {
        public const int MaxBigMessageWorkers = 7;
        private const int MaxMessagesInQueue = 1000;

        public IList<IMessageWorker> Workers { get; }
        public ConcurrentQueue<Message> Queue { get; private set; }
        private ConcurrentQueue<DeviceRegistration> RegistrationsQueue { get; set; }
        private IMaintenanceWorker MaintenanceWorker { get; }

        private volatile int _deviceRegistrationsProcessed;

        private int DeviceRegistrationsProcessed
        {
            get => _deviceRegistrationsProcessed;
            set => _deviceRegistrationsProcessed = value;
        }

        private readonly IRepository _repository;
        private readonly ILogger<ApnService> _logger;

        public ApnService(IRepository repository, ILogger<ApnService> logger, IList<IMessageWorker> workers,
            IMaintenanceWorker maintenanceWorker)
        {
            _repository = repository;
            _logger = logger;
            Workers = workers;
            MaintenanceWorker = maintenanceWorker;
        }

        public async Task Start()
        {
            Queue = new ConcurrentQueue<Message>();
            RegistrationsQueue = new ConcurrentQueue<DeviceRegistration>();

            for (var i = 1; i < Workers.Count; i++)
            {
                var worker = Workers[i];
                worker.Start(this);
                await Task.Delay(41 * i);
                _logger.LogWarning("APN Worker " + i + "-" + worker.Id + " started");
            }
            
            _logger.LogWarning("APN Maintenance Worker created: " + MaintenanceWorker.Id);
            MaintenanceWorker.Start(this);
        }

        public void Stop()
        {
            var tasksToWait = new Task[Workers.Count];
            for (var i = 0; i < Workers.Count; i++)
            {
                Workers[i].Stop();
                tasksToWait[i] = Workers[i].Task;
            }

            MaintenanceWorker.Stop();

            try
            {
                Task.WaitAll(tasksToWait);
            }
            catch (AggregateException e)
            {
                foreach (var v in e.InnerExceptions)
                {
                    _logger.LogError("Task.WaitAll exception msg: " + v.Message);
                }
            }

            _logger.LogWarning("All APN workers stopped");

            try
            {
                Task.WaitAll(new Task[] {MaintenanceWorker.Task});
            }
            catch (AggregateException e)
            {
                foreach (var v in e.InnerExceptions)
                {
                    _logger.LogError("Task.WaitAll exception msg: " + v.Message);
                }
            }

            _logger.LogInformation("APN Maintenance worker " + MaintenanceWorker.Id + " stopped");
        }

        public bool Enqueue(Message message)
        {
            if (Queue.Count < MaxMessagesInQueue)
            {
                Queue.Enqueue(message);
                return true;
            }
            return false;
        }

        public void RegisterDevice(DeviceRegistration registration)
        {
            RegistrationsQueue.Enqueue(registration);
        }

        private async Task PersistCachedRegsitrations()
        {
            var success = 0;
            var fail = 0;

            if (RegistrationsQueue.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Started persisting APN registrations: " + RegistrationsQueue.Count);

            try
            {
                var result = await _repository.APNSRegister(RegistrationsQueue);

                success = result["success"];
                fail = result["fail"];
                DeviceRegistrationsProcessed += result["deviceRegistrationsProcessed"];
            }
            catch (Exception ex)
            {
                _logger.LogError("Error persisting APN registrations: " + ex);
            }
            
            _logger.LogInformation("Persisted APN registrations: success=" + success + ", fail=" + fail + ", total=" +
                                   DeviceRegistrationsProcessed);
        }

        public async Task DoMaintenance()
        {
            await PersistCachedRegsitrations();
        }

        public string GetStats()
        {
            var sb = new StringBuilder();

            sb.Append("APN Maintenance Worker: ").Append("ID=").Append(MaintenanceWorker.Id).Append(", ")
                .Append("status=").Append(MaintenanceWorker.Status).Append("\n\n");

            sb.Append("APN Workers (").Append(Workers.Count).Append("):\n");

            foreach (var messageWorker in Workers)
            {
                var worker = (ApnMessageWorker) messageWorker;
                sb.Append("ID=").Append(worker.Id).Append(", ")
                    .Append("status=").Append(worker.Status).Append(", ")
                    .Append("currentMessage=")
                    .Append(worker.CurrentMessage != null ? worker.CurrentMessage.ToString() : "(null)")
                    .Append("\n\n");
            }

            sb.Append("\n");

            Message[] queuedMessages = Queue.ToArray();
            sb.Append("APN Queue (").Append(queuedMessages.Length).Append("):\n");
            foreach (Message message in queuedMessages)
            {
                sb.Append("ID=").Append(message.Id).Append(", ")
                    .Append("status=").Append(message.Status).Append(", ")
                    .Append("appId=").Append(message.PublisherId).Append(".").Append(message.AppOwnerUsername)
                    .Append(".").Append(message.AppId)
                    .Append("data=").Append(message.GetDataString())
                    .Append("\n\n");
            }

            sb.Append("\n");

            DeviceRegistration[] registrations = RegistrationsQueue.ToArray();
            sb.Append("APN Registrations Queue (").Append(registrations.Length).Append("):\n");
            int count = 0;
            foreach (DeviceRegistration registration in registrations)
            {
                sb.Append(registration.ToString()).Append("\n\n");

                if (++count >= 10)
                {
                    sb.Append("And " + (registrations.Length - count) + " more registrations\n\n");
                    break;
                }
            }

            sb.Append("\n");

            sb.Append("APN Registrations processed: ").Append(DeviceRegistrationsProcessed);

            return sb.ToString();
        }
    }
}