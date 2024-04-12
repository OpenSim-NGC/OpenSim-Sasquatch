using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenSim.Server.MoneyServer
{
    public sealed class MoneyService : IHostedService
    {
        private readonly Task _completedTask = Task.CompletedTask;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MoneyService> _logger;

        private readonly MoneyServer _moneyServiceBase;
        public MoneyService(
            IConfiguration configuration, 
            ILogger<MoneyService> logger,
            MoneyServer moneyServiceBase
            )
        {
            _configuration = configuration;
            _logger = logger;
            _moneyServiceBase = moneyServiceBase;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{Service} is running.", nameof(Server.MoneyServer));

            _moneyServiceBase.Startup();
            _moneyServiceBase.Work();
            
            return _completedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{Service} is stopping.", nameof(Server.MoneyServer));

            return _completedTask;
        }
    }
}
