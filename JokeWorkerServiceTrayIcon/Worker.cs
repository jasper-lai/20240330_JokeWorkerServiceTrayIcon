namespace JokeWorkerServiceTrayIcon
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly JokeService _jokeService;

        public Worker(ILogger<Worker> logger, JokeService jokeService)
        {
            _logger = logger;
            _jokeService = jokeService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    //if (_logger.IsEnabled(LogLevel.Information))
                    //{
                    //    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    //}
                    // await Task.Delay(1000, stoppingToken);

                    string joke = _jokeService.GetJoke();
                    _logger.LogWarning("{Joke}", joke);

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 如果是由 services.msc 停掉服務, 則會跑到這個 OperationCanceledException
                // 這算是正常的操作, 不應該擲回非 0 的 exit code.
                // When the stopping token is canceled, for example, a call made from services.msc,
                // we shouldn't exit with a non-zero exit code. In other words, this is expected...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);

                // Terminates this process and returns an exit code to the operating system.
                // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
                // performs one of two scenarios:
                // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
                // 2. When set to "StopHost": will cleanly stop the host, and log errors.
                //
                // In order for the Windows Service Management system to leverage configured
                // recovery options, we need to terminate the process with a non-zero exit code.
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 服務啟動時
        /// </summary>
        /// <param name="stoppingToken">The stopping token.</param>
        public override async Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service started");
            // 基底類別 BackgroundService: 在 StartAsync() 呼叫 ExecuteAsync、
            await base.StartAsync(stoppingToken);
        }

        /// <summary>
        /// 服務停止時
        /// </summary>
        /// <param name="stoppingToken">The stopping token.</param>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service stopped");
            // 基底類別 BackgroundService: 在 StopAsync() 時呼叫 stoppingToken.Cancel() 優雅結束
            await base.StopAsync(stoppingToken);
        }
    }
}
