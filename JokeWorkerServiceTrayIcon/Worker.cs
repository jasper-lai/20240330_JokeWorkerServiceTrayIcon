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
                // �p�G�O�� services.msc �����A��, �h�|�]��o�� OperationCanceledException
                // �o��O���`���ާ@, �������Y�^�D 0 �� exit code.
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
        /// �A�ȱҰʮ�
        /// </summary>
        /// <param name="stoppingToken">The stopping token.</param>
        public override async Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service started");
            // �����O BackgroundService: �b StartAsync() �I�s ExecuteAsync�B
            await base.StartAsync(stoppingToken);
        }

        /// <summary>
        /// �A�Ȱ����
        /// </summary>
        /// <param name="stoppingToken">The stopping token.</param>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service stopped");
            // �����O BackgroundService: �b StopAsync() �ɩI�s stoppingToken.Cancel() �u������
            await base.StopAsync(stoppingToken);
        }
    }
}
