using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DisGram.Models.Discord;
using DisGram.Models.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DisGram.Models
{
    public class DisGramBot
    {
        readonly public GramBot _gramBot;
        readonly public DisBot _disBot;
        private string _discordApiKey;
        private string _telegramApiKey;
        public IServiceProvider? _services;
        public DisGramContext _context;
        public IniFile? _ini { get; set; }

        public DisGramBot(DisGramContext context, IConfiguration config)
        {
            _context = context;
            _context.Database.Migrate();
            _ini = new IniFile();
            if (_ini.Read("FirstRun", "App_Settings") == "true")
            {
                FirstRun();
            }

            _discordApiKey = _ini.Read("Discord", "API_Key");
            _telegramApiKey = _ini.Read("Telegram", "API_Key");

            _disBot = new DisBot(_context, _ini);
            _gramBot = new GramBot(_context);
            _gramBot._disGramBot = this;
            _disBot._disGramBot = this;
        }

        async public Task Run()
        {

            while (_disBot._client.ConnectionState == ConnectionState.Disconnected)
            {
                await _disBot._client.LoginAsync(TokenType.Bot, _discordApiKey);
                await _disBot._client.StartAsync();
                await Task.Delay(1000);
                if (_disBot._client.ConnectionState == ConnectionState.Disconnected)
                {
                    await _disBot._client.StopAsync();
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("\nDiscord API Key is not valid, try again.");
                    Console.ResetColor();
                    string? newApiKey = Console.ReadLine();
                    _ini.Write("Discord", newApiKey, "API_Key");
                    _discordApiKey = newApiKey;
                }
            }

            await _gramBot.ReadyAsync(_telegramApiKey);

            await Task.Delay(Timeout.Infinite);
        }

        async public Task<Ticket> CreateTicket(Ticket ticket, FileStream file)
        {
            Random random = new();
            string? title;

            if (ticket.Category.Name == "Telegram")
            {
                title = "chat";
            }
            else
            {
                title = ticket.Category.Name;
            }
            string path = file.Name.Split('\\').Last();
            var embedBuilder = new EmbedBuilder()
                    .WithTitle($"New {title} ticket")
                    .WithAuthor(new EmbedAuthorBuilder { Name = ticket.Customer.Name })
                    .WithColor(random.Next(0, 255), random.Next(0, 255), random.Next(0, 255))
                    .WithImageUrl($"attachment://{path}")
                    .WithCurrentTimestamp();

            SocketGuild guild = _disBot._client.GetGuild(_disBot._guild);
            ulong? categoryId = guild.CategoryChannels.FirstOrDefault(category => category.Name.Equals(ticket.Category.Name))?.Id;

            RestTextChannel newChannel = await guild.CreateTextChannelAsync(ticket.Customer.Name + "_" + ticket.Id.ToString(), prop => prop.CategoryId = categoryId);
            var staffMemberRole = _disBot._client.GetGuild(_disBot._guild).Roles.Where(role => role.Name == "Staff Member").First();

            await newChannel.AddPermissionOverwriteAsync(guild.EveryoneRole, OverwritePermissions.DenyAll(newChannel).Modify(
                    viewChannel: PermValue.Allow, readMessageHistory: PermValue.Allow, attachFiles: PermValue.Allow, addReactions: PermValue.Allow)
                    );

            ticket.DiscordChannelId = newChannel.Id;
            ticket.CreatedAt = DateTime.Now;
            ticket.State = Ticket.TicketState.Unclaimed;

            await _context.Tickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            await newChannel.ModifyAsync(prop => prop.Name = ticket.Customer.Name + "_" + ticket.Id.ToString());
            var cb = new ComponentBuilder()
                .WithButton("Close Ticket", "close", ButtonStyle.Danger)
                .WithButton("Claim / Unclaim", "claim", ButtonStyle.Primary);

            await newChannel.SendFileAsync(file.Name, "", embed: embedBuilder.Build(), components: cb.Build());

            return ticket;
        }

        async public Task CloseTicket(Ticket ticket)
        {
            if (_disBot._client.GetGuild(_disBot._guild).Channels.Any(w => w.Id == ticket.DiscordChannelId))
            {
                SocketGuildChannel guild = _disBot._client.GetGuild(_disBot._guild).Channels.Where(w => w.Id == ticket.DiscordChannelId).First();
                ulong closedGuildId = _disBot._guildCategories.Where(w => w.Name == "Closed 🔒").First().Id;
                await guild.ModifyAsync(prop =>
                    {
                        prop.CategoryId = closedGuildId;
                    });
                ISocketMessageChannel newGuild = (ISocketMessageChannel)_disBot._client.GetChannel(guild.Id);

                ticket.State = Ticket.TicketState.Closed;
                ticket.StaffMember = null;
                _context.Tickets.Update(ticket);
                await _context.SaveChangesAsync();
            }
        }

        async public Task ReopenTicket(Ticket ticket)
        {
            Random random = new();

            SocketGuildChannel guild = _disBot._client.GetGuild(_disBot._guild).Channels.Where(w => w.Id == ticket.DiscordChannelId).First();
            await _disBot.RefreshChannels();
            ulong categoryGuildId = _disBot._guildCategories.Where(w => w.Name == ticket.Category.Name).First().Id;
            await guild.ModifyAsync(prop =>
            {
                prop.CategoryId = categoryGuildId;
            });
            ISocketMessageChannel newGuild = (ISocketMessageChannel)_disBot._client.GetChannel(guild.Id);

            ticket.State = Ticket.TicketState.Unclaimed;
            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            ReplyKeyboardMarkup replyKeyboardMarkup =
                new(
                    new KeyboardButton("Close 🔒")
                )
                {
                    ResizeKeyboard = true
                };
            await _gramBot._client.SendTextMessageAsync(chatId: (int)ticket.Customer.Id,
                                            text: "Your previous ticket has been reopened",
                                            parseMode: ParseMode.MarkdownV2,
                                            replyMarkup: replyKeyboardMarkup
                                            );
        }

        async public Task RemoveTicket(Ticket ticket)
        {
            if (_disBot._client.GetGuild(_disBot._guild).Channels.Any(w => w.Id == ticket.DiscordChannelId))
            {
                SocketGuildChannel guild = _disBot._client.GetGuild(_disBot._guild).Channels.Where(w => w.Id == ticket.DiscordChannelId).First();
                await guild.DeleteAsync();
            }
        }

        public void FirstRun()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ____  _                                 ____        _   ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(" |  _ \\(_)___  __ _ _ __ __ _ _ __ ___   | __ )  ___ | |_ ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(" | | | | / __|/ _` | '__/ _` | '_ ` _ \\  |  _ \\ / _ \\| __|");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(" | |_| | \\__ | (_| | | | (_| | | | | | |_| |_) | (_) | |_ ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(" |____/|_|___/\\__, |_|  \\__,_|_| |_| |_(_|____/ \\___/ \\__|");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("              |___/                                       ");
            Console.WriteLine("\n \n \n");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("Insert your Telegram Api key :");
            _ini.Write("Telegram", Console.ReadLine(), "API_Key");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\nInsert your Discord Api key :");
            _ini.Write("Discord", Console.ReadLine(), "API_Key");
            Console.WriteLine("\nInsert your Discord Server Id :");
            _ini.Write("DiscordGuild", Console.ReadLine(), "App_Settings");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Don't forget to edit yours Telegram texts with the /custom_text command on your Discord server");
            Console.WriteLine("and yours categories with the /category Discord Command");
            Console.ResetColor();
            _ini.Write("FirstRun", "false", "App_Settings");
        }
    }
}