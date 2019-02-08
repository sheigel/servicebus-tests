using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.InteropExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace consumer
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var host = new HostBuilder().ConfigureAppConfiguration((ctx, cfgBuilder) =>
                    cfgBuilder.AddEnvironmentVariables())
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();

                    services.AddSingleton<IHostedService, PrintTextToConsoleService>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });


            await host.RunConsoleAsync();
        }
    }

    public class PrintTextToConsoleService : IHostedService, IDisposable
    {
        private readonly IConfiguration cfg;
        private readonly ILogger logger;
        private QueueClient queueClient;
        private Timer timer;

        public PrintTextToConsoleService(ILogger<PrintTextToConsoleService> logger, IConfiguration cfg)
        {
            this.logger = logger;
            this.cfg = cfg;
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting");
            var serviceBusConnectionString = cfg.GetValue<string>("CON_STR");
            logger.LogInformation("Connection string: {0}", serviceBusConnectionString);


            queueClient = new QueueClient(serviceBusConnectionString, "salesmessages");
            queueClient.PrefetchCount = 5;
            var messageHandlerOptions = new MessageHandlerOptions(ex =>
            {
                logger.LogError(ex.Exception, "Failed");
                return Task.CompletedTask;
            })
            {
                MaxConcurrentCalls = 2,
                AutoComplete = false
            };

            queueClient.RegisterMessageHandler(MessageHandler, messageHandlerOptions);

            async Task MessageHandler(Message msg, CancellationToken ct)
            {
                Console.WriteLine($"Received message: {Encoding.UTF8.GetString(msg.Body)}");
                await queueClient.CompleteAsync(msg.SystemProperties.LockToken)
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping.");


            return Task.CompletedTask;
        }


        private void DoWork(object state)
        {
            logger.LogInformation("Doing work");
            var message = "Sure would like a large pepperoni!";
            var encodedMessage = new Message(Encoding.UTF8.GetBytes(message));
            queueClient.SendAsync(encodedMessage);
        }
    }
}
