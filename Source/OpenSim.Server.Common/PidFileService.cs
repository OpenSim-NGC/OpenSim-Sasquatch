using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenSim.Server.Common
{
    public class PidFileService : IHostedService
    {
        private readonly ILogger<PidFileService> _logger;
        private readonly IConfiguration _configuration;

        private bool isPidFileCreated = false;
        private string pidFile = string.Empty;

        public PidFileService(
            ILogger<PidFileService> logger,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                pidFile = _configuration.GetSection("Startup").GetValue("PIDFile", "");

                if (string.IsNullOrWhiteSpace(pidFile))
                    return;

                await WritePidFile();

                isPidFileCreated = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error when starting {nameof(PidFileService)}");
            }
        }

        private async Task WritePidFile()
        {
            var processId = Environment.ProcessId.ToString();
            await File.WriteAllTextAsync(pidFile, processId);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (isPidFileCreated)
                    File.Delete(pidFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when deleting PID file");
            }

            return Task.CompletedTask;
        }
    }
}
