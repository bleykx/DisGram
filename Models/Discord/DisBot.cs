using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DisGram.Models.Discord
{
    public class DisBot
    {
        public readonly DiscordSocketClient _client;
        public DisGramBot? _disGramBot;
        private DisGramContext _context;
        public CommandService _commands;
        public ulong _guild;
        public List<SocketCategoryChannel> _guildCategories = new List<SocketCategoryChannel>();
        public InteractionService _interactionService;
        public bool _AnyoneHere;
        public IniFile _ini { get; set; }

        public DisBot(DisGramContext _context, IniFile ini)
        {
            _ini = ini;
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All,
                AlwaysResolveStickers = true
            };
            _client = new DiscordSocketClient(config);
            // Subscribing to client events, so that we may receive them whenever they're invoked.
            this._context = _context;
            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.MessageReceived += MessageReceivedAsync;
            _client.InteractionCreated += InteractionCreatedAsync;
            _client.ModalSubmitted += ModalResponseAsync;
            _client.ButtonExecuted += ButtonHandler;
            _client.UserUpdated += UserUpdated;
            _client.PresenceUpdated += PresenceUpdate;
            _client.RoleUpdated += RoleUpdate;
            _client.GuildMemberUpdated += GuildMemberUpdate;
            _client.ChannelDestroyed += ChannelDestroyed;
            _client.Connected += Connected;

            if (UInt64.TryParse(_ini.Read("DiscordGuild", "App_Settings"), out _guild))
            {
                _guild = Convert.ToUInt64(_ini.Read("DiscordGuild", "App_Settings"));
            }

            _commands = new CommandService();
            _interactionService = new InteractionService(_client);
        }

        private Task Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage);
            return Task.CompletedTask;
        }

        public async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            // If the message was not in the cache, downloading it will result in getting a copy of `after`.
            var message = await before.GetOrDownloadAsync();
            Console.WriteLine($"{message} -> {after}");
        }

        public async Task ShowTelegramPhoto(FileStream file, Customer user)
        {
            if (await _context.Tickets.Where(w => w.Customer == user).AnyAsync())
            {
                Ticket ticket = await _context.Tickets.Where(w => w.Customer == user).FirstAsync();
                var channel = (ISocketMessageChannel)_client.GetChannel(ticket.DiscordChannelId);
                await channel.SendFileAsync(file.Name);
                System.IO.File.Delete(file.Name);
            }
        }

        private Task LogAsync(LogMessage log)
        {
            switch (log.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine(log.ToString());
            Console.ResetColor();
            return Task.CompletedTask;
        }

        // The Ready event indicates that the client has opened a
        // connection and it is now safe to access the cache.
        private async Task ReadyAsync()
        {
            var guild = _client.GetGuild(_guild);

            while (guild == null)
            {
                string? newGuild = _client.Guilds.First().Id.ToString();
                _ini.Write("DiscordGuild", newGuild, "App_Settings");
                //Console.ForegroundColor = ConsoleColor.Magenta;
                //Console.WriteLine("\nDiscord Server Id incorrect");
                //Console.WriteLine("Right click on your Discord serv then click on last option \"Copy Id\"");
                //Console.WriteLine("Insert the new one");

                //string? newGuild = Console.ReadLine();
                //_ini.Write("DiscordGuild", newGuild, "App_Settings");

                while (UInt64.TryParse(_ini.Read("DiscordGuild", "App_Settings"), out _guild) == false)
                {
                    bool ok = UInt64.TryParse(newGuild, out _guild);
                    if (UInt64.TryParse(_ini.Read("DiscordGuild", "App_Settings"), out _guild) == false)
                    {
                        Console.WriteLine("\nBad format, try again");
                        newGuild = Console.ReadLine().ToString();
                        _ini.Write("DiscordGuild", newGuild, "App_Settings");
                    }
                }

                _guild = Convert.ToUInt64(_ini.Read("DiscordGuild", "App_Settings"));
                guild = _client.GetGuild(_guild);
            }

            GuildPermissions guildPermissions = new GuildPermissions(
                administrator: true);
            if (!guild.Roles.Any(a => a.Name == "Admin"))
            {
                var role = await guild.CreateRoleAsync("Admin", guildPermissions);
            }

            GuildPermissions staffPermissions = new GuildPermissions(
                viewChannel: true,
                sendMessages: true,
                attachFiles: true,
                changeNickname: true


            );
            if (!guild.Roles.Any(a => a.Name == "Staff Member"))
            {
                var role = await guild.CreateRoleAsync("Staff Member", staffPermissions);
            }
            await RefreshChannels();

            Console.WriteLine($"{_client.CurrentUser} is connected!");
            await IsAnioneHere();
            // Let's do our global command
            var categoryCommand = new SlashCommandBuilder()
                    .WithName("category")
                    .WithDefaultMemberPermissions(GuildPermission.Administrator)
                    .WithDescription("Manage DisGram categories")
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("action")
                        .WithDescription("Select an action")
                        .WithRequired(true)
                        .AddChoice("Create", 1)
                        .AddChoice("Edit", 2)
                        .AddChoice("Delete", 3)
                        .WithType(ApplicationCommandOptionType.Integer)
                    );

            var customTextCommand = new SlashCommandBuilder()
                    .WithName("custom_text")
                    .WithDefaultMemberPermissions(GuildPermission.Administrator)
                    .WithDescription("Manage Telegram texts")
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("text")
                        .WithDescription("Select which text edit")
                        .WithRequired(true)
                        .AddChoice("Start text", 1)
                        .AddChoice("Start button label", 2)
                        .AddChoice("Start button response", 3)
                        .AddChoice("Ticket closed by customer", 4)
                        .AddChoice("Ticket closed by staff", 5)
                        .AddChoice("Unavailability", 6)
                        .WithType(ApplicationCommandOptionType.Integer)
                        );

            var presenceCommand = new SlashCommandBuilder()
                    .WithName("presence")
                    .WithDefaultMemberPermissions(GuildPermission.Administrator)
                    .WithDescription("Enable presence mode")
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("enable")
                        .WithDescription("Enable / Disable presence mode")
                        .WithRequired(true)
                        .AddChoice("On", 1)
                        .AddChoice("Off", 2)
                        .WithType(ApplicationCommandOptionType.Integer)
                        );


            try
            {
                // With global commands we don't need the guild.
                await _client.CreateGlobalApplicationCommandAsync(categoryCommand.Build());
                await _client.CreateGlobalApplicationCommandAsync(customTextCommand.Build());
                await _client.CreateGlobalApplicationCommandAsync(presenceCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch (HttpException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }
        }

        // This is not the recommended way to write a bot - consider
        // reading over the Commands Framework sample.
        private async Task MessageReceivedAsync(SocketMessage message)
        {
            _client.PurgeUserCache();

            // The bot should never respond to itself.
            if (message.Author.Id == _client.CurrentUser.Id)
                return;


            if (_guildCategories.SelectMany(s => s.Channels.Select(s2 => s2.Id)).ToList().Contains(message.Channel.Id))
            {
                await _disGramBot._gramBot.ShowDiscordMessage(message);
            }
            if (!(message is SocketUserMessage msg)) return;

            int argPos = 0;
            if (!(msg.HasStringPrefix("i~>", ref argPos))) return;

            var context = new SocketCommandContext(_client, msg);
            await _commands.ExecuteAsync(context, argPos, _disGramBot._services);
        }

        // For better functionality & a more developer-friendly approach to handling any kind of interaction, refer to:
        // https://discordnet.dev/guides/int_framework/intro.html
        private async Task InteractionCreatedAsync(SocketInteraction interaction)
        {
            // safety-casting is the best way to prevent something being cast from being null.
            // If this check does not pass, it could not be cast to said type.
            if (interaction is SocketMessageComponent component)
            {
                // Check for the ID created in the button mentioned above.
                switch (component.Data.CustomId)
                {
                    case "claim":
                        Ticket ticket = await _context.Tickets.Where(w => w.DiscordChannelId == interaction.ChannelId).Include(i => i.Customer).Include(i => i.StaffMember).FirstAsync();

                        bool isAdmin = false;
                        var embedBuilder = new EmbedBuilder();

                        foreach (SocketRole role in ((SocketGuildUser)component.User).Roles)
                        {
                            if (role.Name == "Admin") { isAdmin = true; break; }
                        }
                        if (ticket.State != Ticket.TicketState.Closed && ticket.State == Ticket.TicketState.Unclaimed && ticket.StaffMember == null)
                        {
                            ticket.StaffMember = await _context.StaffMembers.Where(w => w.Name == component.User.Username).FirstAsync();
                            ticket.State = Ticket.TicketState.Claimed;
                            //guild.ModifyAsync(prop => prop.Name = "🟢" + ticket.Customer.Name + "_" + ticket.Id.ToString());
                            embedBuilder
                                .WithTitle("Ticket claimed")
                                .WithDescription(component.User.Username)
                                .WithColor(Color.Green)
                                .WithThumbnailUrl(component.User.GetAvatarUrl() ?? component.User.GetDefaultAvatarUrl())
                                .WithCurrentTimestamp();
                            _context.Tickets.Update(ticket);
                            await _context.SaveChangesAsync();
                            await interaction.RespondAsync("", embed: embedBuilder.Build());
                        }
                        else if ((ticket.StaffMember != null) && ticket.StaffMember.Id == interaction.User.Id || isAdmin)
                        {
                            ticket.StaffMember = null;
                            ticket.State = Ticket.TicketState.Unclaimed;
                            //SocketGuildChannel guild = _client.GetGuild(_guild).Channels.Where(w => w.Id == ticket.DiscordChannelId).FirstAsync();
                            //await guild.ModifyAsync(prop => prop.Name = "🟠" + ticket.Customer.Name + "_" + ticket.Id.ToString());
                            embedBuilder
                                .WithTitle("Ticket unclaimed")
                                .WithDescription(component.User.Username)
                                .WithColor(Color.Orange)
                                .WithThumbnailUrl(component.User.GetAvatarUrl() ?? component.User.GetDefaultAvatarUrl())
                                .WithCurrentTimestamp();
                            _context.Tickets.Update(ticket);
                            await _context.SaveChangesAsync();
                            await interaction.RespondAsync("", embed: embedBuilder.Build());
                        }
                        else if (ticket.StaffMember != null)
                        {
                            await interaction.RespondAsync($"Only {ticket.StaffMember.Name} or an administrator can unclaimed this ticket");
                        }
                        else
                        {
                            await interaction.RespondAsync("You can't claim a closed ticket");
                        }

                        break;
                    case "close":
                        ticket = await _context.Tickets.Where(w => w.DiscordChannelId == interaction.ChannelId).Include(i => i.Customer).FirstAsync();
                        List<Category> categories = _context.Categories.ToList();
                        if (categories.Count == 0)
                        {
                            categories.Add(new Category { Name = "Telegram", ButtonLabel = "Start chatting", ButtonReply = "Chat request sent" });
                        }
                        List<string?> categoriesName = categories.Select(category => category.ButtonLabel).ToList();

                        if (categoriesName.Count == 1)
                        {
                            await _disGramBot._gramBot._client.SendTextMessageAsync(chatId: (int)ticket.Customer.Id,
                                                            text: _ini.Read("TicketClosedByStaff", "Telegram_Text"),
                                                            replyMarkup: _disGramBot._gramBot.CreateCategoriesKeyboard(true));
                        }
                        else
                        {
                            await _disGramBot._gramBot._client.SendTextMessageAsync(chatId: (int)ticket.Customer.Id,
                                    text: _ini.Read("TicketClosedByStaff", "Telegram_Text"),
                                    replyMarkup: _disGramBot._gramBot.CreateStartChatKeyboard(true));
                        }

                        embedBuilder = new EmbedBuilder()
                        .WithTitle($"Ticket get closed")
                        .WithDescription("You can reopen it with \"reopen\" button \n or delete it with \"delete\" button")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp();

                        var cb = new ComponentBuilder()
                            .WithButton("Delete Ticket", "deleteTicket", ButtonStyle.Danger)
                            .WithButton("Reopen Ticket", "reopen", ButtonStyle.Primary);

                        await _disGramBot.CloseTicket(ticket);

                        await interaction.RespondAsync(embed: embedBuilder.Build(), components: cb.Build());
                        break;
                    case "deleteTicket":
                        ticket = await _context.Tickets.Where(w => w.DiscordChannelId == interaction.ChannelId).Include(i => i.Customer).FirstAsync();
                        if (ticket.State == Ticket.TicketState.Closed)
                        {
                            await _disGramBot.RemoveTicket(ticket);
                            await interaction.RespondAsync();
                        }
                        else
                        {
                            await interaction.RespondAsync("This ticket isn't closed, you can't delete it");
                        }
                        break;
                    case "reopen":
                        ticket = await _context.Tickets.Where(w => w.DiscordChannelId == interaction.ChannelId).Include(i => i.Customer).FirstAsync();
                        if (ticket.State == Ticket.TicketState.Closed)
                        {
                            embedBuilder = new EmbedBuilder()
                               .WithTitle($"Ticket get reopened")
                               .WithColor(Color.Green)
                               .WithCurrentTimestamp();

                            cb = new ComponentBuilder()
                                .WithButton("Close Ticket", "close", ButtonStyle.Danger)
                                .WithButton("Claim / Unclaim", "claim", ButtonStyle.Primary);


                            await _disGramBot.ReopenTicket(ticket);
                            await interaction.RespondAsync(embed: embedBuilder.Build(), components: cb.Build());
                        }
                        else
                        {
                            await interaction.RespondAsync("This ticket isn't closed, you can't re-open it");
                        }
                        break;
                    default:
                        Console.WriteLine("An ID has been received that has no handler!");
                        break;
                }
            }
        }

        [SlashCommand("category", "Manage categories")]
        public async Task CategoryManage(SocketSlashCommand command)
        {
            List<Category> categories = _context.Categories.ToList();
            if (categories.Any(category => category.Name == "Telegram"))
                categories.Remove(categories.Where(w => w.Name == "Telegram").First());

            switch (command.Data.Options.First().Value.ToString())
            {
                case "1":
                    ModalBuilder modal = new ModalBuilder();
                    modal.Title = "Create a new Category";
                    modal.CustomId = "create_category";
                    modal.AddTextInput("Category Name :", "category_name", TextInputStyle.Short, placeholder: "Name");
                    modal.AddTextInput("Telegram button Text :", "telegram_but_text", TextInputStyle.Paragraph);
                    modal.AddTextInput("Telegram button Reply :", "telegram_but_reply", TextInputStyle.Paragraph);
                    await command.RespondWithModalAsync(modal.Build());
                    break;

                case "2":
                    if (categories.Count == 0)
                    {
                        await command.RespondAsync("You haven't created any categories yet");
                        break;
                    }
                    List<ButtonBuilder> buttons = new List<ButtonBuilder>();
                    categories.ForEach(delegate (Category category)
                    {
                        buttons.Add(new ButtonBuilder { Label = category.Name, CustomId = $"edit_category_{category.Name}", Style = ButtonStyle.Secondary });
                    });
                    var component = new ComponentBuilder();
                    buttons.ForEach(delegate (ButtonBuilder button)
                    {
                        component.WithButton(button);
                    });
                    await command.RespondAsync("Click on the category you want to edit", components: component.Build());
                    break;

                case "3":
                    if (categories.Count == 0)
                    {
                        await command.RespondAsync("You haven't created any categories yet");
                        break;
                    }
                    buttons = new List<ButtonBuilder>();
                    categories.ForEach(delegate (Category category)
                    {
                        buttons.Add(new ButtonBuilder { Label = category.Name, CustomId = $"delete_category_{category.Name}", Style = ButtonStyle.Danger });
                    });
                    component = new ComponentBuilder();
                    buttons.ForEach(delegate (ButtonBuilder button)
                    {
                        component.WithButton(button);
                    });
                    await command.RespondAsync("Click on the category you want to delete", components: component.Build());
                    break;
            }
        }

        [SlashCommand("presence", "Enable/disable presence mode")]
        private async Task PresenceMode(SocketSlashCommand command)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle("Presence mode")
                .WithColor(Color.Green)
                .WithThumbnailUrl(command.User.GetAvatarUrl() ?? command.User.GetDefaultAvatarUrl())
                .WithCurrentTimestamp();

            if (command.Data.Options.First().Value.ToString() == "1")
            {
                _ini.Write("Enabled", "True", "Presence");
                embedBuilder.Description = "Presence Mode turned on";
            }
            else
            {
                _ini.Write("Enabled", "False", "Presence");
                embedBuilder.Description = "Presence Mode turned off";
            }

            await command.RespondAsync(embed: embedBuilder.Build());
        }

        [SlashCommand("custom_text", "Manage custom texts")]
        private async Task CustomTextManage(SocketSlashCommand command)
        {
            switch (command.Data.Options.First().Value.ToString())
            {
                case "1":
                    string startText = _ini.Read("StartText", "Telegram_Text");
                    ModalBuilder modal = new ModalBuilder();
                    modal.Title = "Edit Telegram Start message";
                    modal.CustomId = "start_text";
                    modal.AddTextInput("Text", "text", TextInputStyle.Paragraph, value: startText, placeholder: "Enter new Telegram start text");
                    await command.RespondWithModalAsync(modal.Build());
                    break;

                case "2":
                    string startButtonLabel = _ini.Read("StartButtonLabel", "Telegram_Text");
                    modal = new ModalBuilder();
                    modal.Title = "Edit Telegram Start button label";
                    modal.CustomId = "start_button_label";
                    modal.AddTextInput("Text", "text", TextInputStyle.Paragraph, value: startButtonLabel, placeholder: "Enter new Telegram start button label");
                    await command.RespondWithModalAsync(modal.Build());
                    break;

                case "3":
                    string startButtonResponse = _ini.Read("StartButtonResponse", "Telegram_Text");
                    modal = new ModalBuilder();
                    modal.Title = "Edit Telegram Start button response";
                    modal.CustomId = "start_button_response";
                    modal.AddTextInput("Text", "text", TextInputStyle.Paragraph, value: startButtonResponse, placeholder: "Enter new Telegram start button response");
                    await command.RespondWithModalAsync(modal.Build());
                    break;

                case "4":
                    string customerCloseText = _ini.Read("TicketClosedByUser", "Telegram_Text");
                    modal = new ModalBuilder();
                    modal.Title = "Edit response when customers close ticket";
                    modal.CustomId = "customer_close_text";
                    modal.AddTextInput("Text", "text", TextInputStyle.Paragraph, value: customerCloseText, placeholder: "Enter new text that appears when a customer closes a ticket");
                    await command.RespondWithModalAsync(modal.Build());
                    break;

                case "5":
                    string staffCloseText = _ini.Read("TicketClosedByStaff", "Telegram_Text");
                    modal = new ModalBuilder();
                    modal.Title = "Edit response when staff closes ticket";
                    modal.CustomId = "staff_close_text";
                    modal.AddTextInput("Text", "text", TextInputStyle.Paragraph, value: staffCloseText, placeholder: "Enter new text that appears when a staff member closes a ticket");
                    await command.RespondWithModalAsync(modal.Build());
                    break;

                case "6":
                    string unavailabilityText = _ini.Read("WelcomeText", "Telegram_Text");
                    modal = new ModalBuilder();
                    modal.Title = "Edit response when no one is available";
                    modal.CustomId = "unavailability_text";
                    modal.AddTextInput("Text", "text", TextInputStyle.Paragraph, value: unavailabilityText, placeholder: "Enter new text that appears when no one is available");
                    await command.RespondWithModalAsync(modal.Build());
                    break;
            }
        }
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case "category":
                    await CategoryManage(command);
                    break;
                case "custom_text":
                    await CustomTextManage(command);
                    break;
                case "presence":
                    await PresenceMode(command);
                    break;
            }
        }

        private async Task ModalResponseAsync(SocketModal modal)
        {
            List<SocketMessageComponentData> components = modal.Data.Components.ToList();
            var embedBuilder = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithThumbnailUrl(modal.User.GetAvatarUrl() ?? modal.User.GetDefaultAvatarUrl())
                .WithCurrentTimestamp();

            AllowedMentions mentions = new AllowedMentions();
            mentions.AllowedTypes = AllowedMentionTypes.Users;

            if (modal.Data.CustomId.Split("_")[0] == "edit")
            {

                string category_name = components.First(x => x.CustomId == "category_name").Value;
                string telegram_but_text = components.First(x => x.CustomId == "telegram_but_text").Value;
                string telegram_but_reply = components.First(x => x.CustomId == "telegram_but_reply").Value;
                string oldCategoryName = modal.Data.CustomId.Split("_")[2];

                Category category = await _context.Categories.Where(w => w.Name == oldCategoryName).FirstAsync();
                category.Name = category_name;
                category.ButtonLabel = telegram_but_text;
                category.ButtonReply = telegram_but_reply;
                new Category { Name = category_name, ButtonLabel = telegram_but_text, ButtonReply = telegram_but_reply };

                _context.Update(category);
                await _context.SaveChangesAsync();
                await EditCategoryChannel(oldCategoryName, category);
                embedBuilder.Title = "Category editing";
                embedBuilder.Description = $"Category {category.Name} edited.";
                embedBuilder.AddField("Name", category_name, true);
                embedBuilder.AddField("Button text", telegram_but_text, true);
                embedBuilder.AddField("Button reply", telegram_but_reply, true);

                await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                return;
            }

            switch (modal.Data.CustomId)
            {
                case "create_category":
                    string category_name = components.First(x => x.CustomId == "category_name").Value;
                    string telegram_but_text = components.First(x => x.CustomId == "telegram_but_text").Value;
                    string telegram_but_reply = components.First(x => x.CustomId == "telegram_but_reply").Value;
                    if (await _context.Categories.AnyAsync(category => category.Name == category_name))
                    {
                        embedBuilder.Title = "Category creating";
                        embedBuilder.Color = Color.Red;
                        embedBuilder.Description = $"Category with name {category_name} already exist";
                        await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                        return;
                    }

                    Category category = new Category { Name = category_name, ButtonLabel = telegram_but_text, ButtonReply = telegram_but_reply };
                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();
                    await RefreshChannels();
                    embedBuilder.Title = "Category creating";
                    embedBuilder.Description = $"Category {category.Name} created";
                    await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                    break;

                case "start_text":
                    string startText = components.First(x => x.CustomId == "text").Value;
                    _ini.Write("StartText", startText, "Telegram_Text");
                    embedBuilder.Title = "New Telegram start text";
                    embedBuilder.Description = startText;
                    await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                    break;

                case "start_button_label":
                    string startButtonLabel = components.First(x => x.CustomId == "text").Value;
                    _ini.Write("StartButtonLabel", startButtonLabel, "Telegram_Text");
                    embedBuilder.Title = "New Telegram start button label";
                    embedBuilder.Description = startButtonLabel;
                    await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                    break;

                case "start_button_response":
                    string startButtonResponse = components.First(x => x.CustomId == "text").Value;
                    _ini.Write("StartButtonResponse", startButtonResponse, "Telegram_Text");
                    embedBuilder.Title = "New Telegram start button response";
                    embedBuilder.Description = startButtonResponse;
                    await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                    break;

                case "customer_close_text":
                    string customerCloseText = components.First(x => x.CustomId == "text").Value;
                    _ini.Write("TicketClosedByUser", customerCloseText, "Telegram_Text");
                    embedBuilder.Title = "New Telegram start text";
                    embedBuilder.Description = customerCloseText;
                    await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                    break;

                case "staff_close_text":
                    string staffCloseText = components.First(x => x.CustomId == "text").Value;
                    _ini.Write("TicketClosedByStaff", staffCloseText, "Telegram_Text");
                    embedBuilder.Title = "New Telegram start text";
                    embedBuilder.Description = staffCloseText;
                    await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                    break;

                case "unavailability_text":
                    string unavailabilityText = components.First(x => x.CustomId == "text").Value + "\n \n Don't forget to enable presence mode with /presence command ";
                    _ini.Write("UnavailableText", unavailabilityText, "Presence");
                    embedBuilder.Title = "New unavailability text";
                    embedBuilder.Description = unavailabilityText;
                    await modal.RespondAsync(embed: embedBuilder.Build(), allowedMentions: mentions);
                    break;
            }
        }

        private async Task ButtonHandler(SocketMessageComponent component)
        {
            string categoryName = "";
            Category category = new();
            if (component.Data.CustomId.Split("_").Last().Any())
            {
                categoryName = component.Data.CustomId.Split("_").Last();
            }
            if (await _context.Categories.Where(w => w.Name == categoryName).AnyAsync())
            {
                category = await _context.Categories.Where(w => w.Name == categoryName).FirstAsync();
            }

            switch (component.Data.CustomId.Split("_").First())
            {
                case "claim":
                    break;
                case "close":
                    return;
                case "reopen":
                    return;
                case "delete":
                    await RemoveTicketsFromCategory(category);
                    _context.Categories.Remove(category);
                    await _context.SaveChangesAsync();
                    await DeleteCategoryChannel(category);

                    var embedBuilder = new EmbedBuilder()
                        .WithAuthor(component.User)
                        .WithTitle("Category delete")
                        .WithDescription($"Category {categoryName} has been deleted.")
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp();

                    await component.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
                    break;
                case "edit":
                    ModalBuilder modal = new ModalBuilder();
                    modal.Title = "Editing " + component.Data.CustomId.Split("_")[1] + " category";
                    modal.CustomId = "edit_category_" + component.Data.CustomId.Split("_")[2];
                    modal.AddTextInput("Category Name :", "category_name", TextInputStyle.Short, value: category.Name);
                    modal.AddTextInput("Telegram button Text :", "telegram_but_text", TextInputStyle.Short, value: category.ButtonLabel);
                    modal.AddTextInput("Telegram button Reply :", "telegram_but_reply", TextInputStyle.Paragraph, value: category.ButtonReply);
                    await component.RespondWithModalAsync(modal.Build());
                    break;
                default:
                    break;
            }
        }

        public async Task CreateCategoryChannel(Category category)
        {
            var guild = _client.GetGuild(_guild);
            var categoryChannel = await guild.CreateCategoryChannelAsync(category.Name);

            var staffMemberRole = _client.GetGuild(_guild).Roles.Where(role => role.Name == "Staff Member").First();

            foreach (var channel in _client.GetGuild(_guild).Channels)
            {
                if (channel.Name == categoryChannel.Name)
                {
                    await channel.AddPermissionOverwriteAsync(staffMemberRole,
                    OverwritePermissions.DenyAll(channel).Modify(
                    viewChannel: PermValue.Allow, readMessageHistory: PermValue.Allow, attachFiles: PermValue.Allow, addReactions: PermValue.Allow)
                    );
                }
            }
            await categoryChannel.ModifyAsync(x =>
            {
                if (category.Name == "Closed 🔒")
                {
                    x.Position = 999;
                }
                else
                {
                    x.Position = guild.CategoryChannels.Count - 1;
                }
            });
            if (_guildCategories.Any(a => a.Name == "Telegram"))
            {
                await DeleteCategoryChannel(new Category { Name = "Telegram" });
            }
        }

        private async Task DeleteCategoryChannel(Category category)
        {
            var guild = _client.GetGuild(_guild);
            if (guild.CategoryChannels.Any(w => w.Name == category.Name))
            {
                var channel = guild.CategoryChannels.Where(w => w.Name == category.Name).First();
                await channel.DeleteAsync();
            }
        }

        private async Task EditCategoryChannel(string oldCategory, Category newCategory)
        {
            var guild = _client.GetGuild(_guild);
            if (guild.CategoryChannels.Any(c => c.Name == oldCategory))
            {
                var channel = guild.CategoryChannels.Where(w => w.Name == oldCategory).First();
                await channel.ModifyAsync(m =>
                {
                    m.Name = newCategory.Name;
                }).ConfigureAwait(false); ;
            }
        }

        public async Task UserUpdated(SocketUser before, SocketUser after)
        {
            return;
        }
        public async Task PresenceUpdate(SocketUser user, SocketPresence before, SocketPresence after)
        {
            if (await _context.StaffMembers.AnyAsync(a => a.Id == (ulong)user.Id))
            {
                StaffMember staffMember = await _context.StaffMembers.Where(w => w.Id == (ulong)user.Id).FirstAsync();
                staffMember.Available = after.Status.Equals(UserStatus.Online);
                _context.Update(staffMember);
                await _context.SaveChangesAsync();
            }
            await IsAnioneHere();
            return;
        }

        public async Task RoleUpdate(SocketRole before, SocketRole after)
        {
            return;
        }

        public async Task GuildMemberUpdate(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
        {

            if ((!after.Roles.Any(a => a.Name == "Staff Member")) || (after.Roles.Any() == false && await _context.StaffMembers.AnyAsync(member => member.Id == before.Id)))
            {
                if (await _context.StaffMembers.AnyAsync(a => a.Id == (ulong)after.Id))
                {
                    StaffMember user = await _context.StaffMembers.Where(w => w.Id == (ulong)after.Id).FirstAsync();
                    await RemoveTicketStaffMember(user);
                    _context.StaffMembers.Remove(user);
                    await _context.SaveChangesAsync();
                }
            }
            else if ((!before.Value.Roles.Any(a => a.Name == "Staff Member") && after.Roles.Any(a => a.Name == "Staff Member")))
            {
                bool status = after.Status.Equals(UserStatus.Online);
                await _context.StaffMembers.AddAsync(new StaffMember { Id = (ulong)after.Id, Name = after.Username, Available = status });
                await _context.SaveChangesAsync();
            }
            await IsAnioneHere();
            return;
        }

        public async Task RefreshChannels()
        {
            SocketGuild guild = _client.GetGuild(_guild);
            _guildCategories = guild.CategoryChannels.Where(c => c.Position != 0).ToList();
            List<Category> categories = _context.Categories.ToList();

            if (!guild.Channels.Any(channel => channel.Name == "bot-commands"))
            {
                var botCommandChannel = await _client.GetGuild(_guild).CreateTextChannelAsync("bot-commands");
                var AdminRole = _client.GetGuild(_guild).Roles.Where(role => role.Name == "Admin").First();

                await botCommandChannel.AddPermissionOverwriteAsync(AdminRole,
                    OverwritePermissions.DenyAll(botCommandChannel ).Modify(
                    viewChannel: PermValue.Allow, readMessageHistory: PermValue.Allow, attachFiles: PermValue.Allow, addReactions: PermValue.Allow)
                    );
            }

            if (categories.Count == 0)
            {
                Category telegramCategory = new Category { Name = "Telegram", ButtonLabel = _ini.Read("StartButtonLabel", "Telegram_Text"), ButtonReply = _ini.Read("StartButtonResponse", "Telegram_Text") };
                categories.Add(telegramCategory);
                _context.Categories.Add(telegramCategory);
                await _context.SaveChangesAsync();
            }
            if (categories.Count > 1)
            {
                Category telegramCategory = await _context.Categories.Where(category => category.Name == "Telegram").FirstAsync();
                categories.Remove(telegramCategory);
            }
            categories.Insert(0, new Category { Name = "Closed 🔒" });
            Thread.Sleep(1000);
            categories.ForEach(async delegate (Category category)
            {
                if (!_guildCategories.Any(a => a.Name == category.Name))
                {
                    await CreateCategoryChannel(category);
                }
            });

            _guildCategories = guild.CategoryChannels.Where(c => c.Position != 0).ToList();
        }

        public async Task RemoveTicketsFromCategory(Category category)
        {
            if (await _context.Tickets.AnyAsync(a => a.Category == category))
            {
                List<Ticket> ticketsToRemove = _context.Tickets.Where(w => w.Category == category).ToList();
                ticketsToRemove.ForEach(async delegate (Ticket ticket)
                {
                    await _disGramBot.RemoveTicket(ticket);
                    //if (_client.GetGuild(_guild).Channels.Any(w => w.Id == ticket.DiscordChannelId))
                    //{
                    //    SocketGuildChannel guild = _client.GetGuild(_guild).Channels.Where(w => w.Id == ticket.DiscordChannelId).First();
                    //    await guild.DeleteAsync();
                    //}
                });
            }
        }

        async public Task RemoveTicketStaffMember(StaffMember staffMember)
        {
            List<Ticket> ticketClaimedByStaffMember = _context.Tickets.Where(ticket => ticket.StaffMember == staffMember).ToList();
            ticketClaimedByStaffMember.ForEach(
                async delegate (Ticket ticket)
                {
                    ticket.StaffMember = null;
                    ticket.State = Ticket.TicketState.Unclaimed;

                    var user = await _client.GetUserAsync((ulong)staffMember.Id);
                    var ticketGuild = _client.GetGuild(_guild).GetTextChannel((ulong)ticket.DiscordChannelId);

                    EmbedBuilder embedBuilder = new EmbedBuilder()
                        .WithTitle("Ticket unclaimed")
                        .WithDescription(user.Username)
                        .WithColor(Color.Orange)
                        .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                        .WithCurrentTimestamp();

                    await ticketGuild.SendMessageAsync("", embed: embedBuilder.Build());
                });

            _context.UpdateRange(ticketClaimedByStaffMember);
            await _context.SaveChangesAsync();
        }

        async private Task Connected()
        {

        }

        async private Task ChannelDestroyed(SocketChannel socketChannel)
        {
            await RefreshChannels();
            var categoryChannel = socketChannel as SocketCategoryChannel;

            if (categoryChannel != null)
            {
                if (await _context.Categories.AnyAsync(category => category.Name == categoryChannel.Name))
                {
                    Category category = await _context.Categories.Where(category => category.Name == categoryChannel.Name).FirstAsync();
                    await RemoveTicketsFromCategory(category);
                }
                else if (categoryChannel.Name == "Closed 🔒")
                {
                    List<Ticket> tickets = await _context.Tickets.Where(ticket => ticket.State == Ticket.TicketState.Closed).ToListAsync();
                    tickets.ForEach(async ticket =>
                    {
                        await _disGramBot.RemoveTicket(ticket);
                    });
                }
                return;
            }

            var ticketChannel = socketChannel as SocketTextChannel;
            if (ticketChannel != null)
            {
                string[] splitedName = ticketChannel.Name.Split("_");
                string customerName = "";
                for (int i = 0; i < splitedName.Length - 1; i++)
                {
                    customerName += splitedName[i];
                    if (i < splitedName.Length - 2)
                    {
                        customerName += "_";
                    }
                }

                if (customerName == "") { return; }
                if (customerName.Length == 1)
                    customerName = char.ToUpper(customerName[0]).ToString();
                else
                    customerName = char.ToUpper(customerName[0]) + customerName.Substring(1);

                if (await _context.Tickets.AnyAsync(ticket => ticket.Customer.Name == customerName))
                {
                    Ticket ticket = await _context.Tickets.Where(ticket => ticket.Customer.Name == customerName).FirstAsync();
                    _context.Tickets.Remove(ticket);
                    await _context.SaveChangesAsync();

                    await _disGramBot.RemoveTicket(ticket);

                    await _disGramBot._gramBot._client.SendTextMessageAsync(chatId: (int)ticket.Customer.Id,
                                        text: "Your previous ticket has been deleted",
                                        parseMode: ParseMode.MarkdownV2,
                                        replyMarkup: _disGramBot._gramBot.CreateStartChatKeyboard(false));
                }
            }
        }

        async public Task IsAnioneHere()
        {
            _AnyoneHere = await _context.StaffMembers.AnyAsync(a => a.Available == true);
        }
    }
}