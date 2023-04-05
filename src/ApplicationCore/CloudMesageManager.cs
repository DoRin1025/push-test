using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Model;
using Model.PWA;

namespace ApplicationCore
{
    public class CloudMessageManager : ICloudMessageManager
    {
        private string Id { get; set; }

        //    private readonly ICloudMessageService _gcmService;
        private readonly ICloudMessageService _apnService;
        // private readonly ICloudMessageService _pwaService;  

        public ConcurrentDictionary<string, Message> Messages { get; set; }

        private DateTime CreatedDate { get; set; }

        public CloudMessageManager(ICloudMessageService apnService)
        {
            //   _gcmService = gcmService;
            _apnService = apnService;
            Messages = new ConcurrentDictionary<string, Message>();
            // _pwaService = pwaService;
        }

        public async Task StartServices()
        {
            //  _gcmService.Start();
            await _apnService.Start();
            //   _pwaService.Start();
        }

        public void StopServices()
        {
            //   _gcmService.Stop();
            _apnService.Stop();
            //    _pwaService.Stop();

            Thread.Sleep(2000); // give some time to flush logs from background threads
        }

        public bool Enqueue(Message message)
        {
            if (Messages.ContainsKey(message.Id))
                return false;

            bool success = message.Platform switch
            {
                //  Message.PLATFORM_GCM => _gcmService.Enqueue(message),
                Message.PLATFORM_APN => _apnService.Enqueue(message),
                // Message.PLATFORM_WEB_PUSH => _pwaService.Enqueue(message),
                _ => throw new ArgumentException("Unsupported cloud messaging platform")
            };

            if (!success) return false;
            Messages.TryAdd(message.Id, message);
            message.Status = Message.STATUS_QUEUED;

            return true;
        }

        public void RegisterDevice(DeviceRegistration registration)
        {
            switch (registration.Platform)
            {
                case Message.PLATFORM_GCM:
                    //     _gcmService.RegisterDevice(registration);
                    break;
                case Message.PLATFORM_APN:
                    _apnService.RegisterDevice(registration);
                    break;
                default:
                    throw new ArgumentException("Unsupported cloud messaging platform");
            }
        }

        public void RegisterDevice(PwaDeviceRegistration registration)
        {
            if (registration.Platform == Message.PLATFORM_WEB_PUSH)
            {
                //   _pwaService.RegisterDevice(registration);
            }
            else
            {
                throw new ArgumentException("Unsupported cloud messaging platform");
            }
        }

        public string GetStats()
        {
            var sb = new StringBuilder();

            sb.Append("ID: ").Append(Id).Append("\n")
                .Append("Now: ").Append(DateTime.Now).Append("\n")
                .Append("Started: ").Append(CreatedDate).Append("\n")
                .Append("Uptime: ").Append(DateTime.Now - CreatedDate).Append("\n\n");

            //    sb.Append(_gcmService.GetStats());

            sb.Append("\n\n-----\n\n");

            sb.Append(_apnService.GetStats());

            sb.Append("\n\n-----\n\n");

            int gcmMessagesCount = 0;
            int apnMessagesCount = 0;
            int pwaMessagesCount = 0;
            foreach (KeyValuePair<string, Message> entry in Messages)
            {
                if (entry.Value.Platform == Message.PLATFORM_GCM)
                {
                    gcmMessagesCount++;
                }
                else if (entry.Value.Platform == Message.PLATFORM_APN)
                {
                    apnMessagesCount++;
                }
                else if (entry.Value.Platform == Message.PLATFORM_WEB_PUSH)
                {
                    pwaMessagesCount++;
                }
            }

            sb.Append("Messages processed: ").Append(Messages.Count)
                .Append(" (GCM: ").Append(gcmMessagesCount)
                .Append(", APN: ").Append(apnMessagesCount).Append("\n")
                .Append(", PWA: ").Append(pwaMessagesCount).Append(")\n");

            return sb.ToString();
        }
    }
}