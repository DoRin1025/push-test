using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic; 
using System;
using System.Text;
using ApplicationCore.Interfaces;
using Model;


namespace ApplicationCore.GCM
{
    public class GcmMessageWorker
    {
        public const string STATUS_STARTED = "STARTED";
        public const string STATUS_WAITING = "WAITING";
        public const string STATUS_RUNNING = "RUNNING";
        public const string STATUS_STOPPED = "STOPPED";

        private const int GCM_MULTICAST_SIZE = 1000; // MUST be <= 1000 (GCM restriction)
        private const int MAX_ERROR_STRING_LENGTH = 5000;

        // Seattle Clouds Messaging system version (version 1 was using C2DM, version 2 - GCM, version 3 - FCM).
        private static string[] C2DM_AND_GCM_VERSION = new string[] {"1", "2"};
        private static string[] FCM_VERSION = new string[] {"3"};

        public string Id { get; private set; }
        public Task Task { get; private set; }

        private volatile string status;

        public string Status
        {
            get { return status; }
            private set { status = value; }
        }

        private volatile Message currentMessage;

        public Message CurrentMessage
        {
            get { return currentMessage; }
            private set { currentMessage = value; }
        }

        private CancellationTokenSource CancellationTokenSource { get; set; }

        private GcmService GcmService { get; set; }

        private readonly IRepository repository = null; //RepositoryFactory.GetRepository();

        private class MessageSendException : Exception
        {
            public MessageSendException(string message)
                : base(message)
            {
            }
        };

        private class MessageSendServerKeyException : Exception
        {
            public MessageSendServerKeyException(string message)
                : base(message)
            {
            }
        };


        public GcmMessageWorker(string id, GcmService service)
        {
            this.Id = id;
            this.GcmService = service;

            this.CancellationTokenSource = new CancellationTokenSource();

            this.Task = Task.Factory.StartNew(() => DoWork(this), TaskCreationOptions.LongRunning);
            this.Task.ContinueWith(
                (t) =>
                {
                    // todoLogger.LogCmError("Failed task for woker " + id + ":\n" + t.Exception.ToString());
                },
                TaskContinuationOptions.OnlyOnFaulted);

            Status = STATUS_STARTED;
        }

        public void Stop()
        {
            this.CancellationTokenSource.Cancel();
        }

        private void DoWork(GcmMessageWorker worker)
        {
            this.Status = STATUS_WAITING;

            // TODO: process messages that are still in queue when cancellation is requested
            while (!worker.CancellationTokenSource.IsCancellationRequested)
            {
                Message message = null;
                if (!this.GcmService.Queue.TryDequeue(out message))
                {
                    Thread.Sleep(100);
                    continue;
                }

                this.CurrentMessage = message;
                this.Status = STATUS_RUNNING;

                message.Status = Message.STATUS_PROCESSING;
                // todo Logger.LogCmEvent("Worker " + worker.Id + " processing message: " + message.ToString());
                string gcmStatus = string.Empty;
                try
                {
                    if (message.Registrations == null && !message.IsTestMessage)
                    {
                        //remove deleting blacklisted devices, after database will be clear.
                        long count = repository.DeleteBlackListedDevices(message);
                        if (count > 0)
                        {
                            // todo      Logger.LogCmEvent("Worker " + worker.Id + " deleted blacklisted devices - " + count +
                            //           "  for message: " + message.ToString());
                        }

                        if (!string.IsNullOrEmpty(message.FcmLegacyServerKey))
                        {
                            // TODO: workaround because sc server not correctly set send legacy gcm 
                            // delete this code in the future after many users update applications
                            List<string> gcmRegistrations =
                                repository.GetGCMRegistrations(message, C2DM_AND_GCM_VERSION);
                            if (!message.SendLegacyGcm && gcmRegistrations.Count > 0)
                            {
                                message.SendLegacyGcm = true;
                            }

                            if (message.SendLegacyGcm)
                            {
                                message.Registrations = gcmRegistrations;
                                message.FcmRegistrations = repository.GetGCMRegistrations(message, FCM_VERSION);
                                message.ServerEndPoint = "GCM/FCM";
                            }
                            else
                            {
                                message.Registrations = repository.GetGCMRegistrations(message, FCM_VERSION);
                                message.ServerEndPoint = "FCM";
                            }
                        }
                        else
                        {
                            message.Registrations = repository.GetGCMRegistrations(message, C2DM_AND_GCM_VERSION);
                            message.ServerEndPoint = "GCM";
                        }

                        // todo   Logger.LogCmEvent("Worker " + worker.Id + " got registrations for message: " +
                        //           message.ToString());
                    }
                    else if (message.IsTestMessage && message.RegistrationsTotal == -1)
                    {
                        Thread.Sleep(10000); // simulate slow DB read
                        message.RegistrationsTotal = 60000;
                    }

                    if (message.IsBig() && GetBigMessageWorkersCount() > GcmService.MAX_BIG_MESSAGE_WORKERS)
                    {
                        message.Status = Message.STATUS_FAILED;
                        message.ErrorReason = Message.ERROR_OVER_CAPACITY;
                        message.ErrorMessage = "Server is overloaded. Try again later.";
                        // todo    Logger.LogCmError("Worker " + worker.Id +
                        //           " too many big messages processing. Failing message: " + message.ToString());
                    }
                    else
                    {
                        if (message.SendLegacyGcm)
                        {
                            //send gcm
                            SendMessages(message, message.Registrations, this.GcmService.InvalidRegistrationsQueue,
                                null);

                            message.LegacyGcmDelivered = message.RegistrationsDelivered;
                            //send fcm 
                            SendMessages(message, message.FcmRegistrations, this.GcmService.InvalidRegistrationsQueue,
                                message.FcmLegacyServerKey);
                        }
                        else
                        {
                            SendMessages(message, message.Registrations, this.GcmService.InvalidRegistrationsQueue,
                                message.FcmLegacyServerKey);
                        }

                        message.Status = Message.STATUS_DELIVERED;
                        // todo// todo  Logger.LogCmEvent("Worker " + worker.Id + " delivered message: " + message.ToString());
                    }
                }
                catch (MessageSendException e)
                {
                    message.Status = Message.STATUS_FAILED;
                    message.ErrorReason = Message.ERROR_INTERNAL_SERVER_ERROR;
                    if (e.Message == GcmMessageSender.ERROR_MESSAGE_TOO_BIG)
                    {
                        message.ErrorReason = Message.ERROR_PAYLOAD_TOO_BIG;
                    }
                    // the message.ErrorMessage already has error details, so no need adding it here

                    // todo  Logger.LogCmError("Worker " + worker.Id + " error sending message: " + message.ToString() +
                    //    ", error:\n" + e.ToString());
                }
                catch (MessageSendServerKeyException e)
                {
                    message.Status = Message.STATUS_FAILED;
                    message.ErrorReason = Message.ERROR_FCM_CONFIGURATION;

                    // todo  Logger.LogCmError("Worker " + worker.Id + " error sending message: " + message.ToString() +
                    //    ", error:\n" + e.ToString());
                }
                catch (Exception e)
                {
                    message.Status = Message.STATUS_FAILED;
                    message.ErrorReason = Message.ERROR_INTERNAL_SERVER_ERROR;
                    message.ErrorMessage += e.Message;
                    // todo Logger.LogCmError("Worker " + worker.Id + " error sending message: " + message.ToString() +
                    //           ", error:\n" + e.ToString());
                }

                this.Status = STATUS_WAITING;
            }
            // while end

            this.Status = STATUS_STOPPED;

            // todo     Logger.LogCmEvent("Worker " + worker.Id + " cancellation requested");
        }

        /// <summary>
        /// This is not 100% accurate considering multiple threads 
        /// but it is good enough for our purpose
        /// </summary>
        private int GetBigMessageWorkersCount()
        {
            int count = 0;

            foreach (GcmMessageWorker worker in this.GcmService.Workers)
            {
                if (worker.Status == STATUS_RUNNING && worker.CurrentMessage.IsBig())
                    count++;
            }

            return count;
        }

        private void SendTestMessages(Message message)
        {
            int registrationsCount = message.RegistrationsTotal;

            for (int i = 0; i < registrationsCount; i += GCM_MULTICAST_SIZE)
            {
                // take next GCM_MULTICAST_SIZE registrations or what's left in the list
                int rangeCount = Math.Min(GCM_MULTICAST_SIZE, registrationsCount - i);
                int partialRegistrationsCount = rangeCount;

                // 1 second per 1k registrations
                Thread.Sleep(partialRegistrationsCount);
                GcmMessageSender.Result result = new GcmMessageSender.Result();
                result.success = partialRegistrationsCount;

                message.RegistrationsProcessed += partialRegistrationsCount;
                message.RegistrationsDelivered += result.success;
                message.RegistrationsUpdated += result.canonicalIds;
                message.RegistrationsFailed += result.failure;
                message.RegistrationsUnregistered += result.unregister;

                if (!string.IsNullOrEmpty(result.errorMessage))
                    message.ErrorMessage += result.errorMessage + "\n";

                if (result.severeErrorOccured)
                    throw new MessageSendException(result.errorMessage);
            }
        }

        private void SendMessages(Message message, List<string> registrations,
            ConcurrentQueue<string> invalidRegistrationsQueue, string fcmLegacyServerKey)
        {
            if (message.IsTestMessage)
            {
                SendTestMessages(message);
                return;
            }

            var sender = new GcmMessageSender();
            StringBuilder errorStringBuilder = new StringBuilder(MAX_ERROR_STRING_LENGTH + 1000);
            bool truncated = false;
            for (int i = 0; i < registrations.Count; i += GCM_MULTICAST_SIZE)
            {
                // take next GCM_MULTICAST_SIZE registrations or what's left in the list
                int rangeCount = Math.Min(GCM_MULTICAST_SIZE, registrations.Count - i);
                List<string> partialRegistrations = registrations.GetRange(i, rangeCount);

                GcmMessageSender.Result result = sender.Send(message.Data, partialRegistrations, fcmLegacyServerKey);

                message.RegistrationsProcessed += partialRegistrations.Count;
                message.RegistrationsDelivered += result.success;
                message.RegistrationsUpdated += result.canonicalIds;
                message.RegistrationsFailed += result.failure;
                message.RegistrationsUnregistered += result.unregister;

                foreach (string registration in result.invalidRegistrations)
                {
                    invalidRegistrationsQueue.Enqueue(registration);
                }

                if (!string.IsNullOrEmpty(result.errorMessage))
                {
                    // truncate error messages but make sure severe errors are fully included
                    if (errorStringBuilder.Length < MAX_ERROR_STRING_LENGTH || result.severeErrorOccured)
                    {
                        // separate error messages with empty line
                        if (errorStringBuilder.Length != 0)
                        {
                            errorStringBuilder.Append('\n');
                        }

                        errorStringBuilder.Append(result.errorMessage);
                    }
                    else if (!truncated)
                    {
                        // mark truncation for the first time
                        errorStringBuilder.Append("\n=====TRUNCATED=====\n");
                        truncated = true;
                    }
                }

                if (result.severeErrorOccured)
                {
                    message.ErrorMessage = errorStringBuilder.ToString();

                    if (result.invalidLegacyServerKey)
                    {
                        throw new MessageSendServerKeyException(result.errorMessage);
                    }
                    else
                    {
                        throw new MessageSendException(result.errorMessage);
                    }
                }
            }

            message.ErrorMessage = errorStringBuilder.ToString();
        }
    }
}