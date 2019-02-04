using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Primitives;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace producer
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

                    services.AddHostedService<PrintTextToConsoleService>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });
            
            host.UseConsoleLifetime();
            await host.RunConsoleAsync();
        }
    }

    public class PrintTextToConsoleService : IHostedService, IDisposable
    {
        private readonly IConfiguration cfg;
        private readonly ILogger logger;

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private QueueClient queueClient;

        public PrintTextToConsoleService(ILogger<PrintTextToConsoleService> logger, IConfiguration cfg)
        {
            this.logger = logger;
            this.cfg = cfg;
        }

        public void Dispose()
        {
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting");
            var serviceBusConnectionString = cfg.GetValue<string>("CON_STR");
            logger.LogInformation("Connection string: {0}", serviceBusConnectionString);


            queueClient = new QueueClient(serviceBusConnectionString, "salesmessages", TokenProvider.CreateSharedAccessSignatureTokenProvider("RootManageSharedAccessKey", "Y15stKTTgtuGUc4fJ2XtQ2gVWfxMFgwMkX/g1vgPx3I="),TransportType.AmqpWebSockets);
            //queueClient.ServiceBusConnection.TransportType = TransportType.AmqpWebSockets;
            var count = 0;
            while (!cts.IsCancellationRequested)
            {
                await DoWork(count++);

                await Task.Delay(1000);
            }

            //timer = new Timer(DoWork, null, TimeSpan.Zero,
            //  TimeSpan.FromSeconds(5));
            //return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping.");
            cts.Cancel();
            return Task.CompletedTask;
        }

        private async Task DoWork(int count)
        {
            logger.LogInformation($"Doing work for message #{count}");
            var message = $"Sure would like a large pepperoni #{count}!";
            queueClient.ServiceBusConnection.TransportType = TransportType.Amqp;
            var encodedMessage = new Message(Encoding.UTF8.GetBytes(message));
            await queueClient.SendAsync(encodedMessage);
        }
    }
}