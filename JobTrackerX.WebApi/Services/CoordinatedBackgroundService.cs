using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace JobTrackerX.WebApi.Services
{
    //from https://mcguirev10.com/2020/01/05/lifecycle-of-generic-host-background-services.html
    public abstract class CoordinatedBackgroundService : IHostedService, IDisposable
    {
        private readonly CancellationTokenSource _appStoppingTokenSource = new CancellationTokenSource();

        private readonly IHostApplicationLifetime _appLifetime;

        protected CoordinatedBackgroundService(IHostApplicationLifetime appLifetime)
        {
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(
                async () =>
                    await ExecuteAsync(_appStoppingTokenSource.Token).ConfigureAwait(false)
            );
            return InitializingAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _appStoppingTokenSource.Cancel();
            await StoppingAsync(cancellationToken).ConfigureAwait(false);
            Dispose();
        }

        protected virtual Task InitializingAsync(CancellationToken cancelInitToken)
            => Task.CompletedTask;

        protected abstract Task ExecuteAsync(CancellationToken appStoppingToken);

        protected virtual Task StoppingAsync(CancellationToken cancelStopToken)
            => Task.CompletedTask;

        public virtual void Dispose()
        {
        }
    }
}