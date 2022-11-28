using DisGram.Models.Telegram;
using Telegram.Bot;
using System.Threading;
using System.Threading.Tasks;
using DisGram.Models.Discord;
using DisGram.Models;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using System.Reflection;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace DisGram
{
    class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync(args).GetAwaiter().GetResult();

        private DisGramBot? disGramBot;
        private CommandService? commands;
        private IServiceProvider? services;

        public async Task MainAsync(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                    {
                        services.AddDbContext<DisGramContext>(ServiceLifetime.Transient);
                    })
                .Build();

            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();
            DisGramContext context = host.Services.GetRequiredService<DisGramContext>();
            disGramBot = new DisGramBot(context, config);
            await host.StartAsync();

            services = new ServiceCollection()
                  .AddSingleton(disGramBot._disBot._client)
                  .BuildServiceProvider();

            commands = new CommandService();
            await commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: services);

            await disGramBot._disBot._commands.AddModulesAsync(Assembly.GetEntryAssembly(), host.Services);
            disGramBot._services = services;

            await disGramBot.Run();
        }
    }
}