using DisGram.Models.Discord;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Discord.Rest;
using Discord;
using MessageType = Telegram.Bot.Types.Enums.MessageType;
using System.Runtime.CompilerServices;
using System.Linq;

namespace DisGram.Models.Telegram
{
    public class GramBot
    {
        public DisGramBot? _disGramBot;
        public TelegramBotClient? _client;
        private readonly DisGramContext? _context;

        public GramBot(DisGramContext context)
        {
            _context = context;
        }

        async public Task ReadyAsync(string? apiKey)
        {
            _client = new TelegramBotClient(apiKey);
            var result = new bool();
            try
            {
                result = await _client.TestApiAsync();
            }
            catch (Exception ex)
            {
                result = false;
            }
            while (result == false)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nTelegram API key is not valid, try again");
                Console.WriteLine("You can find it in t.me/BotFather Telegram chat");
                Console.ResetColor();
                apiKey = Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                _disGramBot._ini.Write("Telegram", apiKey, "API_Key");
                Console.ResetColor();

                _client = new TelegramBotClient(apiKey);
                try
                {
                    result = await _client.TestApiAsync();
                }
                catch (Exception ex)
                {
                }
            }
            var me = await _client.GetMeAsync();

            Console.Title = me.Username ?? _disGramBot._ini.Read("BotName", "App_Settings");
            using var cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            _client.StartReceiving(updateHandler: HandleUpdateAsync,
                               errorHandler: HandleErrorAsync,
                               receiverOptions: new ReceiverOptions()
                               {
                                   AllowedUpdates = Array.Empty<UpdateType>()
                               },
                               cancellationToken: cts.Token);

            Console.WriteLine($"Start listening for @{me.Username}");
        }

        async public Task ShowDiscordMessage(SocketMessage message)
        {
            var UNick = (message.Author as SocketGuildUser).Nickname;
            if (UNick == null)
            {
                UNick = message.Author.Username;
            }

            int ticketID = Int32.Parse(message.Channel.Name.Substring(message.Channel.Name.LastIndexOf('_') + 1));
            Ticket? ticket = await _context.Tickets.Where(w => w.Id == ticketID).Include(i => i.Customer).FirstAsync();
            if (message.Attachments.Count() > 0)
                foreach (var attachment in message.Attachments)
                {
                    await _client.SendTextMessageAsync((int)ticket.Customer.Id, UNick + " : " + attachment.ProxyUrl);
                }
            else
                await _client.SendTextMessageAsync((int)ticket.Customer.Id, UNick + " : " + message.Content);
        }

        async private Task<Task> SendDiscordMessage(Ticket ticket, string message)
        {
            await _disGramBot._disBot._client.GetGuild(_disGramBot._disBot._guild).GetTextChannel(ticket.DiscordChannelId).SendMessageAsync(ticket.Customer.Name + " : " + message);

            return Task.CompletedTask;
        }

        async public Task<Message> Close(ITelegramBotClient botClient, Message message)
        {
            Customer? user = await _context.Customers.FindAsync((ulong)message.From.Id);

            List<Category> categories = _context.Categories.ToList();

            List<string?> categoriesName = categories.Select(category => category.ButtonLabel).ToList();

            if (await _context.Tickets.Where(w => w.Customer == user).AnyAsync())
            {
                Ticket ticket = await _context.Tickets.Where(w => w.Customer == user).FirstAsync();
                await _disGramBot.CloseTicket(ticket);

                EmbedBuilder embedBuilder = new EmbedBuilder()
                                .WithTitle($"Ticket get closed")
                                .WithDescription("You can reopen it with \"reopen\" button \n or delete it with \"delete\" button")
                                .WithColor(Color.Red)
                                .WithCurrentTimestamp();

                var cb = new ComponentBuilder()
                    .WithButton("Delete Ticket", "deleteTicket", ButtonStyle.Danger)
                    .WithButton("Reopen Ticket", "reopen", ButtonStyle.Primary);

                await _disGramBot._disBot._client.GetGuild(_disGramBot._disBot._guild).GetTextChannel(ticket.DiscordChannelId).SendMessageAsync(embed: embedBuilder.Build(), components: cb.Build());

                return await _client.SendTextMessageAsync(
                                chatId: (int)ticket.Customer.Id,
                                text: _disGramBot._ini.Read("TicketClosedByUser", "Telegram_Text"),
                                replyMarkup: CreateStartChatKeyboard(true)
                              );
            }
            else
            {
                return await _client.SendTextMessageAsync(
                                chatId: message.From.Id,
                                text: "No ticket opened.",
                                replyMarkup: CreateStartChatKeyboard(true)
                              );
            }
        }

        async public Task<Message> NewTicket(ITelegramBotClient botClient, Message message)
        {
            var userPPs = await botClient.GetUserProfilePhotosAsync(message.From.Id);
            var filePP = await botClient.GetFileAsync(userPPs.Photos.First().First().FileId);//userPPs.Photos[0][0].FileId);

            string destinationFilePath = $"../" + message.From.Id + "PP.jpg";
            await using FileStream fileStream = System.IO.File.OpenWrite(destinationFilePath);
            await botClient.DownloadFileAsync(
                filePath: filePP.FilePath,
                destination: fileStream);
            fileStream.Close();

            if (_disGramBot._ini.Read("Enabled", "Presence") == "True")
            {
                if (_disGramBot._disBot._AnyoneHere == false)
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                            text: _disGramBot._ini.Read("UnavailableText", "Presence"),
                                            replyMarkup: CreateStartChatKeyboard(false)
                                            );
                }
            }
            Customer? user = await _context.Customers.FindAsync((ulong)message.From.Id);

            List<Category> categories = _context.Categories.ToList();
            Category category = new();
            if (_disGramBot._ini.Read("StartButtonLabel", "Telegram_Text") == message.Text)
            {
                category = await _context.Categories.Where(w => w.Name == "Telegram").FirstAsync();

                //if (!_disGramBot._disBot._guildCategories.Any(category => category.Name == "Telegram"))
                //{
                //    await _disGramBot._disBot.CreateCategoryChannel(category);
                //}
            }
            else
            {
                category = categories.Where(w => w.ButtonLabel == message.Text).First();
            }

            Ticket ticket = new Ticket { Customer = user, Category = category };

            ReplyKeyboardMarkup replyKeyboardMarkup =
            new(
                new KeyboardButton("Close 🔒")
            )
            {
                ResizeKeyboard = true
            };

            if (await _context.Tickets.Where(w => w.Customer.Id == user.Id).FirstOrDefaultAsync() == null || await _context.Tickets.AnyAsync(ticket => ticket.Customer.Id == user.Id && ticket.State == Ticket.TicketState.Closed))
            {
                if (await _context.Tickets.AnyAsync(ticket => ticket.Customer.Id == user.Id && ticket.State == Ticket.TicketState.Closed))
                {
                    Ticket oldTicket = await _context.Tickets.Where(ticket => ticket.Customer.Id == user.Id).FirstAsync();
                    //_context.Tickets.Remove(oldTicket);
                    //await _context.SaveChangesAsync();
                    await _disGramBot.RemoveTicket(oldTicket);
                }
                await _disGramBot.CreateTicket(ticket, fileStream);
            }
            else
            {
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: "You already have a ticket opened.\nClick on \"Close 🔒\" to close it."
                                                        );
            }

            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: ticket.Category.ButtonReply,
                                                        parseMode: ParseMode.MarkdownV2,
                                                        replyMarkup: replyKeyboardMarkup
                                                        );
        }

        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(botClient, update.Message!),
                UpdateType.EditedMessage => BotOnMessageReceived(botClient, update.EditedMessage!),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(botClient, update.CallbackQuery!),
                UpdateType.InlineQuery => BotOnInlineQueryReceived(botClient, update.InlineQuery!),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(botClient, update.ChosenInlineResult!),
                _ => UnknownUpdateHandlerAsync(botClient, update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        private async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
        {
            await _disGramBot._disBot.IsAnioneHere();
            Customer? user = new Customer { Id = (ulong)message.From.Id };

            if (_context.Customers.Find(user.Id) == null)
            {
                user.Name = message.From.FirstName.ToString();
                _context.Customers.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                user = _context.Customers.Find((ulong)message.From.Id);
            }

            if (message.Type == MessageType.Photo)
            {
                var fileId = message.Photo.Last().FileId;
                var fileInfo = await botClient.GetFileAsync(fileId);
                var filePath = fileInfo.FilePath;

                string destinationFilePath = $"../" + user.Name + ".jpg";
                await using FileStream fileStream = System.IO.File.OpenWrite(destinationFilePath);
                await botClient.DownloadFileAsync(
                    filePath: filePath,
                    destination: fileStream);
                fileStream.Close();
                await _disGramBot._disBot.ShowTelegramPhoto(fileStream, user);
            }
            else if (message.Type == MessageType.Document)
            {
                string? fileId = message.Document.Thumb.FileId;
                var fileInfo = await botClient.GetFileAsync(fileId);
                var filePath = fileInfo.FilePath;

                string destinationFilePath = $"../" + user.Name + ".jpg";
                await using FileStream fileStream = System.IO.File.OpenWrite(destinationFilePath);
                await botClient.DownloadFileAsync(
                    filePath: filePath,
                    destination: fileStream);
                fileStream.Close();
                await _disGramBot._disBot.ShowTelegramPhoto(fileStream, user);
            }

            if (message.Type != MessageType.Text)
                return;

            if (message.Text[0] != '/' && message.Text != "Close 🔒")
            {
                if (await _context.Tickets.AnyAsync(ticket => ticket.Customer == user && ticket.State != Ticket.TicketState.Closed))
                {
                    Ticket ticket = await _context.Tickets.Where(w => w.Customer == user).FirstAsync();
                    await SendDiscordMessage(ticket, message.Text);

                    return;
                }
            }

            List<Category> categories = _context.Categories.ToList();
            //if (categories.Count == 0)
            //{
            //    categories.Add(new Category { Name = "Telegram", ButtonLabel = "Start chatting", ButtonReply = "Chat request sent" });
            //}

            bool isCategoryButton = categories.Any(a => a.ButtonLabel == message.Text);

            if (isCategoryButton || _disGramBot._ini.Read("StartButtonLabel", "Telegram_Text") == message.Text || message.Text == "Reopen previous ticket")
            {
                if (message.Text == _disGramBot._ini.Read("StartButtonLabel", "Telegram_Text") && categories.Count > 1)
                {
                    if (_disGramBot._ini.Read("Enabled", "Presence") == "True")
                    {
                        if (_disGramBot._disBot._AnyoneHere)
                        {
                            await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: _disGramBot._ini.Read("StartButtonResponse", "Telegram_Text"),
                                                parseMode: ParseMode.MarkdownV2,
                                                replyMarkup: CreateCategoriesKeyboard(false)
                                            );
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: _disGramBot._ini.Read("UnavailableText", "Presence"),
                                                parseMode: ParseMode.MarkdownV2,
                                                replyMarkup: CreateStartChatKeyboard(false)
                                            );
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                            text: _disGramBot._ini.Read("StartButtonResponse", "Telegram_Text"),
                                            parseMode: ParseMode.MarkdownV2,
                                            replyMarkup: CreateCategoriesKeyboard(false)
                                        );
                    }
                }
                else if (message.Text == "Reopen previous ticket")
                {
                    if (_disGramBot._ini.Read("Enabled", "Presence") == "True")
                    {
                        if (!_disGramBot._disBot._AnyoneHere)
                        {
                            await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: _disGramBot._ini.Read("UnavailableText", "Presence"),
                                                                parseMode: ParseMode.MarkdownV2,
                                                                replyMarkup: CreateStartChatKeyboard(true));
                            return;
                        }
                    }
                    if (await _context.Tickets.AnyAsync(ticket => ticket.Customer.Id == (ulong)message.From.Id))
                    {
                        Ticket previousTicket = await _context.Tickets.Where(ticket => ticket.Customer.Id == (ulong)message.From.Id).FirstAsync();
                        SocketTextChannel? socketTextChannel = _disGramBot._disBot._client.GetChannel(previousTicket.DiscordChannelId) as SocketTextChannel;

                        var embedBuilder = new EmbedBuilder()
                            .WithTitle($"Ticket get reopened")
                            .WithColor(Color.Green)
                            .WithCurrentTimestamp();

                        var cb = new ComponentBuilder()
                            .WithButton("Close Ticket", "close", ButtonStyle.Danger)
                            .WithButton("Claim / Unclaim", "claim", ButtonStyle.Primary);

                        await socketTextChannel.SendMessageAsync(embed: embedBuilder.Build(), components:cb.Build());
                        await _disGramBot.ReopenTicket(previousTicket);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                            text: "Error, can't reopen previous ticket, a new ticket will be created",
                                            parseMode: ParseMode.MarkdownV2);
                        await NewTicket(botClient, message);
                    }
                }
                else
                {
                    await NewTicket(botClient, message);
                }
                return;
            }
            else
            {
                var action = message.Text switch
                {
                    "/start" => Start(botClient, message),
                    "Close 🔒" => Close(botClient, message),
                    //"Start chatting" => CreateStartChatKeyboard(),
                    _ => Start(botClient, message),
                };
                Message sentMessage = await action;
                Console.WriteLine($"Receive message type: {message.Type}");
                Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
            }
        }

        async Task<Message> SendFile(ITelegramBotClient botClient, Message message)
        {
            await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

            const string filePath = @"Files/ok.png";
            using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = filePath.Split(Path.DirectorySeparatorChar).Last();

            return await botClient.SendPhotoAsync(chatId: message.Chat.Id,
                                                  photo: new InputOnlineFile(fileStream, fileName),
                                                  caption: "Nice Picture");
        }

        async Task<Message> Start(ITelegramBotClient botClient, Message message)
        {
            return await _client.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: _disGramBot._ini.Read("StartText", "Telegram_Text"),
                            replyMarkup: CreateStartChatKeyboard(false));
        }

        public async Task<Message> ShowCategories(ITelegramBotClient botClient, Message message)
        {
            List<Category> categories = _context.Categories.ToList();

            List<string?> categoriesName = categories.Select(category => category.ButtonLabel).ToList();

            ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
                {
                    new KeyboardButton[] { categoriesName.Select(s => s).First()}
                })
            {
                ResizeKeyboard = true
            };

            return await botClient.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: "Choose a category",
                            replyMarkup: replyKeyboardMarkup
                            );
        }

        // Process Inline Keyboard callback data
        private async Task BotOnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: $"Received {callbackQuery.Data}");

            await botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: $"Received {callbackQuery.Data}");
        }

        private async Task BotOnInlineQueryReceived(ITelegramBotClient botClient, InlineQuery inlineQuery)
        {
            Console.WriteLine($"Received inline query from: {inlineQuery.From.Id}");

            InlineQueryResult[] results = {
            // displayed result
            new InlineQueryResultArticle(
                id: "3",
                title: "McEvys",
                inputMessageContent: new InputTextMessageContent(
                    "hello"
                )
            )
        };

            await botClient.AnswerInlineQueryAsync(inlineQueryId: inlineQuery.Id,
                                                   results: results,
                                                   isPersonal: true,
                                                   cacheTime: 0);
        }

        private Task BotOnChosenInlineResultReceived(ITelegramBotClient botClient, ChosenInlineResult chosenInlineResult)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResult.ResultId}");
            return Task.CompletedTask;
        }

        private Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }

        public ReplyKeyboardMarkup CreateCategoriesKeyboard(bool reopen)
        {
            List<Category> categories = _context.Categories.ToList();
            if (categories.Count() > 1 && categories.Any(a => a.Name == "Telegram")) { categories.Remove(categories.Where(w => w.Name == "Telegram").First()); }
            List<String?> categoriesName = categories.Select(category => category.ButtonLabel).ToList();
            KeyboardButton[] keyboard = new KeyboardButton[categoriesName.Count];
            if (reopen)
            {
                keyboard = new KeyboardButton[categoriesName.Count + 1];
            }
            else
            {
                keyboard = new KeyboardButton[categoriesName.Count];
            }

            int i = 0;
            categoriesName.ForEach(delegate (string? category)
            {
                keyboard[i] = category;
                i++;
            });
            if (reopen) { keyboard[keyboard.Length - 1] = ("Reopen previous ticket"); }

            ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
                {
                    keyboard
                })
            { ResizeKeyboard = true };

            return replyKeyboardMarkup;
        }

        public ReplyKeyboardMarkup CreateStartChatKeyboard(bool reopen)
        {
            string startText = _disGramBot._ini.Read("StartButtonLabel", "Telegram_Text");
            KeyboardButton[] keyboard;
            if (reopen)
            {
                keyboard = new KeyboardButton[2];
                keyboard[1] = "Reopen previous ticket";
            }
            else
            {
                keyboard = new KeyboardButton[1];
            }

            keyboard[0] = startText;

            ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
                {
                    keyboard
                })
            { ResizeKeyboard = true };

            return replyKeyboardMarkup;
        }

        public async Task<Message> StartMessage(int chatId)
        {
            if (_disGramBot._ini.Read("Enabled", "Presence") == "True")
            {
                if (_disGramBot._disBot._AnyoneHere)
                {
                    if (_context.Categories.Count() > 1)
                    {
                        return await _client.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: _disGramBot._ini.Read("StartText", "Telegram_Text"),
                                        replyMarkup: CreateStartChatKeyboard(false)
                                        );
                    }
                    else
                    {
                        return await _client.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: _disGramBot._ini.Read("StartText", "Telegram_Text"),
                                        replyMarkup: CreateCategoriesKeyboard(false)
                                        );
                    }
                }
                else
                {
                    return await _client.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: _disGramBot._ini.Read("UnavailableText", "Presence"),
                                    replyMarkup: CreateStartChatKeyboard(false)
                                    );
                }
            }
            else
            {
                if (_context.Categories.Count() > 1)
                {
                    return await _client.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: _disGramBot._ini.Read("StartText", "Telegram_Text"),
                                    replyMarkup: CreateStartChatKeyboard(false)
                                    );
                }
                else
                {
                    return await _client.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: _disGramBot._ini.Read("StartText", "Telegram_Text"),
                                    replyMarkup: CreateCategoriesKeyboard(false)
                                    );
                }
            }
        }
    }
}