using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
using Model;
using Model.APNS;

namespace ApplicationCore.APNS
{
    public class ApnMessageWorker : IMessageWorker
    {
        public string Id { get; set; }
        public Task Task { get; set; }

        private volatile string _status;

        public string Status
        {
            get => _status;
            private set => _status = value;
        }

        private volatile Message _currentMessage;

        public Message CurrentMessage
        {
            get => _currentMessage;
            private set => _currentMessage = value;
        }
        
        private CancellationTokenSource CancellationTokenSource { get; }

        private ApnService Service { get; set; }

        private readonly IRepository _repository;
        private readonly SCEnvironment _scEnvironment;
        private readonly IApnHelper _apnHelper;
        private readonly ILogger<IMessageWorker> _logger;

        public ApnMessageWorker(IRepository repository, SCEnvironment scEnvironment, IApnHelper apnHelper,
            ILogger<IMessageWorker> logger, string id)
        {
            _repository = repository;
            _scEnvironment = scEnvironment;
            _apnHelper = apnHelper;
            _logger = logger;
            Id = id;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public void Start(ApnService service)
        {
            Service = service;
            Task = Task.Factory.StartNew(async () => await DoWork(), TaskCreationOptions.LongRunning);
            Task.ContinueWith((t) => { _logger.LogCritical("Failed task for worker" + Id + ":\n" + t.Exception); },
                TaskContinuationOptions.OnlyOnFaulted);

            Status = IMessageWorker.StatusStarted;
        }

        public void Stop()
        {
            CancellationTokenSource.Cancel();
        }

        private async Task DoWork()
        {
            Status = IMessageWorker.StatusWaiting;

            // TODO: process messages that are still in queue when cancellation is requested
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                if (!Service.Queue.TryDequeue(out var message))
                {
                    await Task.Delay(100);
                    continue;
                }

                CurrentMessage = message;
                Status = IMessageWorker.StatusRunning;
                await ProcessMessage(message);
                Status = IMessageWorker.StatusWaiting;
            }

            Status = IMessageWorker.StatusStopped;
            _logger.LogInformation("Worker " + Id + " cancellation requested");
        }

        private async Task ProcessMessage(Message message)
        {
            message.Status = Message.STATUS_PROCESSING;
            _logger.LogInformation("Worker " + Id + " processing message: " + message);

            try
            {
                // we get the cert first because this is the most common problem
                var cert = _apnHelper.GetClientCertificate(message.PublisherId, message.AppOwnerUsername,
                    message.AppId);

                if (message.Registrations == null && !message.IsTestMessage)
                {
                    message.Registrations = await _repository.GetAPNSRegistrations(message);
                    _logger.LogInformation("Worker " + Id + " got registrations for message: " + message);
                }
                else if (message.IsTestMessage && message.RegistrationsTotal == -1)
                {
                    await Task.Delay(10000);
                    message.RegistrationsTotal = 60000;
                }

                if (message.IsBig() && GetBigMessageWorkersCount() > ApnService.MaxBigMessageWorkers)
                {
                    message.Status = Message.STATUS_FAILED;
                    message.ErrorReason = Message.ERROR_OVER_CAPACITY;
                    message.ErrorMessage = "Server is overloaded. Try again later.";
                    _logger.LogError("Worker " + Id + " too many big messages processing. Failing message: " + message);
                }
                else
                {
                    await SendMessages(message, cert);
                    message.Status = Message.STATUS_DELIVERED;
                    _logger.LogInformation("Worker " + Id + " delivered message: " + message);
                }
            }
            catch (ApnConfigurationException e)
            {
                message.Status = Message.STATUS_FAILED;
                message.ErrorReason = Message.ERROR_APN_CONFIGURATION;
                message.ErrorMessage += e.Message;

                if (e.Message != ApnMessageSender.ErrorApnCertificateMissing &&
                    e.Message != ApnMessageSender.ErrorApnCertificateExpired)
                {
                    _logger.LogError("Worker " + Id + " error sending message: " + message + ", error:\n" + e);
                }
            }
            catch (MessagePayloadTooBigException e)
            {
                message.Status = Message.STATUS_FAILED;
                message.ErrorReason = Message.ERROR_PAYLOAD_TOO_BIG;
                message.ErrorMessage += e.Message;
                _logger.LogError("Worker " + Id + " error sending message: " + message + ", error:\n" + e);
            }
            catch (Exception e)
            {
                message.Status = Message.STATUS_FAILED;
                message.ErrorReason = Message.ERROR_INTERNAL_SERVER_ERROR;
                message.ErrorMessage += e.Message;
                _logger.LogError("Worker " + Id + " error sending message: " + message + ", error:\n" + e);
            }
        }

        /// <summary>
        /// This is not 100% accurate considering multiple threads 
        /// but it is good enough for our purpose
        /// </summary>
        private int GetBigMessageWorkersCount()
        {
            int count = 0;


            foreach (var worker in Service.Workers)
            {
                var apnWorker = (ApnMessageWorker) worker;

                if (apnWorker == null)
                {
                    continue;
                }

                if (apnWorker.Status == IMessageWorker.StatusRunning
                    && apnWorker.CurrentMessage.IsBig())
                    count++;
            }

            return count;
        }


        private async Task SendMessages(Message message, X509Certificate2 clientCertificate)
        {
            if (message.Registrations.Count == 0)
                return;

            // todo move to DI
            using var sender =
                new ApnMessageSender(clientCertificate, _scEnvironment.IsDevelopment(), _apnHelper.GenerateApnPayload(message.Data), message.UniqueAppId);
            
            var tasks = message.Registrations.Select(registration => sender.SendAsync(registration)).ToList();
            var continuation = Task.WhenAll(tasks);
            var apnResponses = continuation.Result;

            var invalidDeviceTokens = new List<string>();

            foreach (var apnResponse in apnResponses)
            {
                if (apnResponse.IsSuccess)
                {
                    message.RegistrationsProcessed++;
                    message.RegistrationsDelivered++;
                    continue;
                }

                var errorLog = "[Worker: " + Id + "]";

                if (apnResponse.Exception != null)
                {
                    errorLog = "\nAn error occurred when sending message: " + message
                                                                            + "\nTo device token: " +
                                                                            apnResponse.DeviceToken
                                                                            + "\nReason: " + apnResponse.Exception;
                    _logger.LogError(errorLog);
                    continue;
                }

                if (apnResponse.Error == null)
                {
                    errorLog = "\nAn error occurred when sending message: " + message
                                                                            + "\nTo device token: " +
                                                                            apnResponse.DeviceToken
                                                                            + "\nReason: Unknown error.";
                    _logger.LogError(errorLog);
                    continue;
                }

                if (apnResponse.Error.Reason == ApnError.ReasonEnum.BadDeviceToken
                    || apnResponse.Error.Reason == ApnError.ReasonEnum.Unregistered)
                {
                    invalidDeviceTokens.Add(apnResponse.DeviceToken);

                    errorLog += "\nMessage not sended: " + message
                                                         + "\nTo device token: " + apnResponse.DeviceToken
                                                         + "\nReason: Device Token is invalid.";
                }
                else
                {
                    errorLog = "\nAn error occurred when sending message: " + message
                                                                            + "\nTo device token: " +
                                                                            apnResponse.DeviceToken
                                                                            + "\nReason: " + apnResponse.Error.Reason;
                }

                _logger.LogError(errorLog);
                if (invalidDeviceTokens.Count > 0)
                {
                    await _repository.RemoveApnDeviceRegistrationsAsync(message.PublisherId,
                        message.AppOwnerUsername, message.AppId, invalidDeviceTokens);
                }
            }
        }
    }
}