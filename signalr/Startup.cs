using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace signalr
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });


            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddSignalR().AddAzureSignalR();

            services.AddSingleton<Consumer>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
           
            app.UseFileServer();
            app.UseAzureSignalR(routes =>
            {
                routes.MapHub<Chat>("/chat");
            });
            
            app.UseMvc();

            app.ApplicationServices.GetService<Consumer>().StartAsync();
        }
    }


        public class Chat : Hub
        {
            public void BroadcastMessage(string name, string message)
            {
                Clients.All.SendAsync("broadcastMessage", name, message);
            }

            public void Echo(string name, string message)
            {
                Clients.Client(Context.ConnectionId).SendAsync("echo", name, message + " (echo from server)");
            }
    }

        public class Consumer
        {
            private readonly IConfiguration cfg;
            private readonly IHubContext<Chat> hubContext;
            private QueueClient queueClient;

            public Consumer( IConfiguration cfg, IHubContext<Chat> hubContext)
            {
                this.cfg = cfg;
                this.hubContext = hubContext;
            }
            
            public Task StartAsync()
            {
                var serviceBusConnectionString = cfg.GetValue<string>("CON_STR");


                queueClient = new QueueClient(serviceBusConnectionString, "salesmessages");
                queueClient.PrefetchCount = 1000;
                var messageHandlerOptions = new MessageHandlerOptions(ex =>
                {
                    return Task.CompletedTask;
                })
                {
                    MaxConcurrentCalls = 1,
                    AutoComplete = false
                };
                int count = 0;
                queueClient.RegisterMessageHandler(MessageHandler, messageHandlerOptions);

                async Task MessageHandler(Message msg, CancellationToken ct)
                {
                    var message = Encoding.UTF8.GetString(msg.Body);
                    Console.WriteLine($"Received message #{count++}: {message}");
                    await hubContext.Clients.All.SendAsync("broadcastMessage", "base", message);
                    await queueClient.CompleteAsync(msg.SystemProperties.LockToken);

                }

                return Task.CompletedTask;
            }
        }
}
