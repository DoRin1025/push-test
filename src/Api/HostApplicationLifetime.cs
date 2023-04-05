using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Api
{
    public class HostApplicationLifetime : IHostedService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ICloudMessageManager _cloudMessageManager;

        public HostApplicationLifetime(IHostApplicationLifetime hostApplicationLifetime,
            ICloudMessageManager cloudMessageManager)
        {
            _lifetime = hostApplicationLifetime;
            _cloudMessageManager = cloudMessageManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _lifetime.ApplicationStarted.Register(OnStarted);
            _lifetime.ApplicationStopping.Register(OnStopping);
            _lifetime.ApplicationStopped.Register(OnStopped);

            await _cloudMessageManager.StartServices();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cloudMessageManager.StopServices();
            return Task.CompletedTask;
        }

        private void OnStarted()
        {
            // Perform post-startup activities here
        }

        private void OnStopping()
        {
            // Perform on-stopping activities here
        }

        private void OnStopped()
        {
            // Perform post-stopped activities here
        }
    }
}