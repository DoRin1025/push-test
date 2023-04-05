using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Model;
using Model.PWA;
using Model.Utils;

namespace ApplicationCore.PWA
{
    public class PwaService //: ICloudMessageService
    {
        public const int MAX_WORKERS = 15;
        public const int MAX_BIG_MESSAGE_WORKERS = 12;
        public const int MAX_MESSAGES_IN_QUEUE = 1000;
        public const int INVALID_REGISTRATIONS_PROCESS_BATCH = 10000;
        public const int INVALID_REGISTRATIONS_DELETE_BATCH = 200; // maximum is 2100 (or for some queries 2098)

        public List<PwaMessageWorker> Workers { get; private set; }
        public ConcurrentQueue<Message> Queue { get; private set; }
        public ConcurrentQueue<PwaDeviceRegistration> RegistrationsQueue { get; private set; }
        public ConcurrentQueue<string> InvalidRegistrationsQueue { get; private set; }
        public MaintenanceWorker MaintenanceWorker { get; private set; }

        private volatile int deviceRegistrationsProcessed;

        public int DeviceRegistrationsProcessed
        {
            get { return deviceRegistrationsProcessed; }
            private set { deviceRegistrationsProcessed = value; }
        }

        private volatile int deviceRegistrationsDeleted;

        public int DeviceRegistrationsDeleted
        {
            get { return deviceRegistrationsDeleted; }
            private set { deviceRegistrationsDeleted = value; }
        }

        private readonly IRepository repository = null; // RepositoryFactory.GetRepository();
        private const bool DEBUG = false;

        public void Start()
        {
            Workers = new List<PwaMessageWorker>();
            Queue = new ConcurrentQueue<Message>();
            RegistrationsQueue = new ConcurrentQueue<PwaDeviceRegistration>();
            InvalidRegistrationsQueue = new ConcurrentQueue<string>();

            string id;
            for (int i = 1; i <= MAX_WORKERS; i++)
            {
                id = i + "-" + CryptoUtil.GetRandomAlphanumericString(8);

                Workers.Add(new PwaMessageWorker(id, this));
                Thread.Sleep(41 * i);
                //Logger.LogPWAEvent("PWA Worker " + id + " created");
            }

            id = CryptoUtil.GetRandomAlphanumericString(8);
            MaintenanceWorker = null; //new MaintenanceWorker(id, null); //this);
            // Logger.LogPWAEvent("PWA Maintenance Worker created: " + id);
        }

        public void Stop()
        {
            Task[] tasksToWait = new Task[Workers.Count];
            for (int i = 0; i < Workers.Count; i++)
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
                // foreach (var v in e.InnerExceptions)
                //  Logger.LogPWAError("Task.WaitAll exception msg: " + v.Message);
            }

            //   Logger.LogPWAEvent("All PWA workers stopped");

            try
            {
                Task.WaitAll(new Task[] {MaintenanceWorker.Task});
            }
            catch (AggregateException e)
            {
                //   foreach (var v in e.InnerExceptions)
                //          Logger.LogPWAError("Task.WaitAll exception msg: " + v.Message);
            }

            ///            Logger.LogPWAEvent("PWA Maintenance worker " + MaintenanceWorker.Id + " stopped");
        }

        public bool Enqueue(Message message)
        {
            if (Queue.Count < MAX_MESSAGES_IN_QUEUE)
            {
                Queue.Enqueue(message);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void RegisterDevice(DeviceRegistration registration)
        {
            if (registration is PwaDeviceRegistration deviceRegistration)
            {
                RegistrationsQueue.Enqueue(deviceRegistration);
            }
        }

        private void PersistCachedRegistrations()
        {
            int success = 0;
            int fail = 0;

            if (RegistrationsQueue.Count == 0)
            {
                if (DEBUG)
                    //  Logger.LogPWAEvent("Persisted PWA registrations: 0");

                    return;
            }

//            Logger.LogPWAEvent("Started persisting PWA registrations: " + RegistrationsQueue.Count);

            var result = repository.PwaRegister(RegistrationsQueue);

            success = result["success"];
            fail = result["fail"];
            DeviceRegistrationsProcessed += result["deviceRegistrationsProcessed"];

            //    Logger.LogPWAEvent("Persisted PWA registrations: success=" + success + ", fail=" + fail + ", total=" +
            //                     DeviceRegistrationsProcessed);
        }

        private void DeleteInvalidRegistrations()
        {
            int success = 0;
            int fail = 0;

            if (InvalidRegistrationsQueue.Count == 0)
            {
                if (DEBUG)
                    //         Logger.LogPWAEvent("Deleted invalid PWA registrations: 0");

                    return;
            }
            //Logger.LogPWAEvent("Started deleting invalid PWA registrations: " + InvalidRegistrationsQueue.Count);


            try
            {
                var result = repository.DeleteInvalidRegistrations(InvalidRegistrationsQueue,
                    INVALID_REGISTRATIONS_DELETE_BATCH, Message.PLATFORM_WEB_PUSH);

                success = result["success"];
                fail = result["success"];
                DeviceRegistrationsDeleted += result["deviceRegistrationsDeleted"];
            }
            catch (Exception ex)
            {
                //      Logger.LogPWAError("Error deleting invalid PWA registrations: " + ex.ToString());
            }

            //   Logger.LogPWAEvent("Deleted invalid PWA registrations: success=" + success + ", fail=" + fail + ", total=" +
            //                    DeviceRegistrationsDeleted
            //                 + ", leftInQueue=" + InvalidRegistrationsQueue.Count);
        }

        public void DoMaintenance()
        {
            PersistCachedRegistrations();
            DeleteInvalidRegistrations();
        }

        public string GetStats()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("PWA Maintenance Worker: ").Append("ID=").Append(MaintenanceWorker.Id).Append(", ")
                .Append("status=").Append(MaintenanceWorker.Status).Append("\n\n");

            sb.Append("PWA Workers (").Append(Workers.Count).Append("):").Append("\n");

            foreach (PwaMessageWorker worker in Workers)
            {
                sb.Append("ID=").Append(worker.Id).Append(", ")
                    .Append("status=").Append(worker.Status).Append(", ")
                    .Append("currentMessage=")
                    .Append(worker.CurrentMessage != null ? worker.CurrentMessage.ToString() : "(null)")
                    .Append("\n\n");
            }

            sb.Append("\n");

            Message[] queuedMessages = Queue.ToArray();
            sb.Append("PWA Queue (").Append(queuedMessages.Length).Append("):").Append("\n");
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

            PwaDeviceRegistration[] registrations = RegistrationsQueue.ToArray();
            sb.Append("PWA Registrations Queue (").Append(registrations.Length).Append("):\n");
            int count = 0;
            foreach (PwaDeviceRegistration registration in registrations)
            {
                sb.Append(registration.ToString()).Append("\n\n");

                if (++count >= 10)
                {
                    sb.Append("And " + (registrations.Length - count) + " more registrations\n\n");
                    break;
                }
            }

            sb.Append("\n");

            sb.Append("PWA Registrations processed: ").Append(DeviceRegistrationsProcessed);

            sb.Append("\n\n");

            string[] invalidRegistrations = InvalidRegistrationsQueue.ToArray();
            sb.Append("Invalid PWA Registrations Queue (").Append(invalidRegistrations.Length).Append("):\n");
            count = 0;
            foreach (string registration in invalidRegistrations)
            {
                sb.Append(registration).Append("\n\n");

                if (++count >= 10)
                {
                    sb.Append("And " + (invalidRegistrations.Length - count) + " more invalid registrations\n\n");
                    break;
                }
            }

            sb.Append("\n");

            sb.Append("Invalid PWA Registrations deleted: ").Append(DeviceRegistrationsDeleted);

            return sb.ToString();
        }
    }
}