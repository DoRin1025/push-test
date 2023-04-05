using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System;
using System.Data;
using System.Text;
using ApplicationCore.Interfaces;
using Model;
using Model.PWA;


namespace ApplicationCore.PWA
{
    public class PwaMessageWorker
    {
        public const string STATUS_STARTED = "STARTED";
        public const string STATUS_WAITING = "WAITING";
        public const string STATUS_RUNNING = "RUNNING";
        public const string STATUS_STOPPED = "STOPPED";

        private const int PWA_MULTICAST_SIZE = 1000; // MUST be <= 1000 (PWA restriction)
        private const int MAX_ERROR_STRING_LENGTH = 5000;

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

        private PwaService PwaService { get; set; }

        private readonly IRepository repository = null;//RepositoryFactory.GetRepository();


        private class MessageSendException : Exception
        {
            public MessageSendException(string message)
                : base(message)
            {
            }
        };

        public PwaMessageWorker(string id, PwaService service)
        {
            this.Id = id;
            this.PwaService = service;

            this.CancellationTokenSource = new CancellationTokenSource();

            this.Task = Task.Factory.StartNew(() => DoWork(this), TaskCreationOptions.LongRunning);
            this.Task.ContinueWith(
                (t) =>
                {
                    //Logger.LogPWAError("Failed task for woker " + id + ":\n" + t.Exception.ToString());
                },
                TaskContinuationOptions.OnlyOnFaulted);

            Status = STATUS_STARTED;
        }

        public void Stop()
        {
            this.CancellationTokenSource.Cancel();
        }

        private void DoWork(PwaMessageWorker worker)
        {
            this.Status = STATUS_WAITING;

            // TODO: process messages that are still in queue when cancellation is requested
            while (!worker.CancellationTokenSource.IsCancellationRequested)
            {
                Message message = null;
                if (!this.PwaService.Queue.TryDequeue(out message))
                {
                    Thread.Sleep(100);
                    continue;
                }

                this.CurrentMessage = message;
                this.Status = STATUS_RUNNING;

                message.Status = Message.STATUS_PROCESSING;
               // Logger.LogPWAEvent("Worker " + worker.Id + " processing message: " + message.ToString());

                try
                {
                    if (message.PwaRegistrations == null && !message.IsTestMessage)
                    {
                        message.PwaRegistrations = repository.GetPwaRegistrations(message);
                  //      Logger.LogPWAEvent("Worker " + worker.Id + " got registrations for message: " +
                  //                       message.ToString());
                    }
                    else if (message.IsTestMessage && message.RegistrationsTotal == -1)
                    {
                        Thread.Sleep(10000); // simulate slow DB read
                        message.RegistrationsTotal = 60000;
                    }

                    if (message.IsBig() && GetBigMessageWorkersCount() > PwaService.MAX_BIG_MESSAGE_WORKERS)
                    {
                        message.Status = Message.STATUS_FAILED;
                        message.ErrorReason = Message.ERROR_OVER_CAPACITY;
                        message.ErrorMessage = "Server is overloaded. Try again later.";
                            //      Logger.LogPWAError("Worker " + worker.Id +
                              ///                        " too many big messages processing. Failing message: " + message.ToString());
                    }
                    else
                    {
                        //add keys to message 
                        message = AddKeysToMessage(message);
                        SendMessages(message, this.PwaService.InvalidRegistrationsQueue);

                        message.Status = Message.STATUS_DELIVERED;
                     //   Logger.LogPWAEvent("Worker " + worker.Id + " delivered message: " + message.ToString());
                    }
                }
                catch (MessageSendException e)
                {
                    message.Status = Message.STATUS_FAILED;
                    message.ErrorReason = Message.ERROR_INTERNAL_SERVER_ERROR;
                    if (e.Message == PwaMessageSender.ERROR_MESSAGE_TOO_BIG)
                    {
                        message.ErrorReason = Message.ERROR_PAYLOAD_TOO_BIG;
                    }
                    // the message.ErrorMessage already has error details, so no need adding it here

//                    Logger.LogPWAError("Worker " + worker.Id + " error sending message: " + message.ToString() +
  //                                     ", error:\n" + e.ToString());
                }
                catch (Exception e)
                {
                    message.Status = Message.STATUS_FAILED;
                    message.ErrorReason = Message.ERROR_INTERNAL_SERVER_ERROR;
                    message.ErrorMessage += e.Message;
                        //  Logger.LogPWAError("Worker " + worker.Id + " error sending message: " + message.ToString() +
                          //                    ", error:\n" + e.ToString());
                }

                this.Status = STATUS_WAITING;
            }
            // while end

            this.Status = STATUS_STOPPED;

         //   Logger.LogPWAEvent("Worker " + worker.Id + " cancellation requested");
        }

        /// <summary>
        /// This is not 100% accurate considering multiple threads 
        /// but it is good enough for our purpose
        /// </summary>
        private int GetBigMessageWorkersCount()
        {
            int count = 0;

            foreach (PwaMessageWorker worker in this.PwaService.Workers)
            {
                if (worker.Status == STATUS_RUNNING && worker.CurrentMessage.IsBig())
                    count++;
            }

            return count;
        }

        private void SendTestMessages(Message message)
        {
            int registrationsCount = message.RegistrationsTotal;

            for (int i = 0; i < registrationsCount; i += PWA_MULTICAST_SIZE)
            {
                // take next PWA_MULTICAST_SIZE registrations or what's left in the list
                int rangeCount = Math.Min(PWA_MULTICAST_SIZE, registrationsCount - i);
                int partialRegistrationsCount = rangeCount;

                // 1 second per 1k registrations
                Thread.Sleep(partialRegistrationsCount);
                PwaMessageSender.Result result = new PwaMessageSender.Result();
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

        private void SendMessages(Message message, ConcurrentQueue<string> invalidRegistrationsQueue)
        {
            if (message.IsTestMessage)
            {
                SendTestMessages(message);
                return;
            }

            List<PwaDeviceRegistration> registrations = message.PwaRegistrations;
            StringBuilder errorStringBuilder = new StringBuilder(MAX_ERROR_STRING_LENGTH + 1000);
            bool truncated = false;
            for (int i = 0; i < registrations.Count; i += PWA_MULTICAST_SIZE)
            {
                // take next PWA_MULTICAST_SIZE registrations or what's left in the list
                int rangeCount = Math.Min(PWA_MULTICAST_SIZE, registrations.Count - i);
                List<PwaDeviceRegistration> partialRegistrations = registrations.GetRange(i, rangeCount);

                PwaMessageSender.Result result = PwaMessageSender.Send(message, partialRegistrations);

                message.RegistrationsProcessed += partialRegistrations.Count;
                message.RegistrationsDelivered += result.success;
                //message.RegistrationsUpdated += result.canonicalIds;
                message.RegistrationsFailed += result.failure;
                message.RegistrationsUnregistered += result.unregister;
                //message.Registrations
                foreach (string registration in result.invalidRegistrations)
                {
                    invalidRegistrationsQueue.Enqueue(registration);
                }
            }

            message.ErrorMessage = errorStringBuilder.ToString();
        }

        private Message AddKeysToMessage(Message message)
        {

           // SqlConnection conn = new SqlConnection(SCEnvironment.SQL_CONN_STRING);
            try
            {
                // using (var cmd = new SqlCommand(@"SELECT * FROM pwa_app_keys 
                //                               WHERE publisher_id = @publisher_id AND username = @username AND app_id = @appid",
                //     conn))
                // {
                //     conn.Open();
                //     cmd.Parameters.AddWithValue("@publisher_id", message.PublisherId);
                //     cmd.Parameters.AddWithValue("@username", message.AppOwnerUsername);
                //     cmd.Parameters.AddWithValue("@appid", message.AppId);
                //     using (var rdr = cmd.ExecuteReader())
                //     {
                //         while (rdr.Read())
                //         {
                //             for (int i = 0; i < rdr.FieldCount; i++)
                //             {
                //                 if (rdr.GetName(i).Equals("public_key"))
                //                     message.PwaPublicKey = (rdr.IsDBNull(i) ? "" : rdr.GetString(i));
                //
                //                 if (rdr.GetName(i).Equals("private_key"))
                //                     message.PwaPrivateKey = (rdr.IsDBNull(i) ? "" : rdr.GetString(i));
                //             }
                //         }
                //     }
                // }


                return message;
            }
            catch (Exception ex)
            {
               // Logger.LogPWAError("AddKeysToMessage method, throw new error: " + ex.ToString());
            }
            finally
            {
             //   if (conn != null)
                {
               //     conn.Dispose();
                }
            }

            return message;
        }
    }
}