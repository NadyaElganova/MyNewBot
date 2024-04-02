using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyNewBot
{
    class Program
    {
        private static string token { get; set; } = "5525093941:AAF0jU3mjyl2vBAtiC2Rny04TXu8JNdnHfU";

        private static TelegramBotClient botClient;
        public enum ChatState
        {
            Normal,
            WaitingForCity
        }
        public static string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5/weather";
        public static string ApiKey { get; set; } = "128d810842a5516fc4f94b5a11cd0067";
        private static HttpClient _httpClient { get; set; } = new HttpClient ();

        private static Dictionary<long, ChatState> chatStates = new Dictionary<long, ChatState>();

        static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            botClient = new TelegramBotClient(token);


            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await botClient.GetMeAsync();

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
        }

        private async static Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only process Message updates
            if (update.Type != UpdateType.Message)
                return;

            // Only process text messages
            if (update.Message.Type != MessageType.Text)
                return;

            var chatId = update.Message.Chat.Id;

            string messageText = update.Message.Text;

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

            ReplyKeyboardMarkup replyKeyboardMarkup = new ReplyKeyboardMarkup(
            new[]
            {
                new KeyboardButton[] { "Стикер", "Повтори за мной"},
                new KeyboardButton[] { "Погода", "Adress"},
            });

            switch (messageText)
            {
                case "Стикер":
                    await botClient.SendStickerAsync(
                        chatId: chatId,
                        sticker: "https://chpic.su/_data/stickers/m/melvin_vk/melvin_vk_001.webp?v=1711667679",
                        cancellationToken: cancellationToken);
                    break;

                case "Повтори за мной":
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "You said:\n" + messageText,
                        cancellationToken: cancellationToken);
                    break;

                case "Adress":
                    await botClient.SendVenueAsync(
                        chatId: chatId,
                        latitude: 55.74749095868835,
                        longitude: 37.607985806832865,
                        title: "Yahmur SPA Premium",
                        address: "ул. Ленивка, 6, Москва, 119019",
                        cancellationToken: cancellationToken);
                    break;

                case "Погода":
                    SetChatState(chatId, ChatState.WaitingForCity); // Установка состояния ожидания города
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Введите название города:",
                        cancellationToken: cancellationToken);
                    break;

                default:
                    var chatState = GetChatState(chatId);
                    switch (chatState)
                    {
                        case ChatState.WaitingForCity:
                            // В этом случае, пользователь вводит город
                            await HandleCityInput(botClient, update, cancellationToken);
                            break;

                        default:
                            // В обычном состоянии просто отправляем ответ
                            await botClient.SendTextMessageAsync(
                                chatId: chatId,
                                text: "Choose a response",
                                replyMarkup: replyKeyboardMarkup,
                                cancellationToken: cancellationToken);
                            break;
                    }
                    break;
            }
        }
        private static void SetChatState(long chatId, ChatState state)
        {
            if (chatStates.ContainsKey(chatId))
            {
                chatStates[chatId] = state;
            }
            else
            {
                chatStates.Add(chatId, state);
            }
        }
        private static ChatState GetChatState(long chatId)
        {
            if (chatStates.ContainsKey(chatId))
            {
                return chatStates[chatId];
            }
            else
            {
                return ChatState.Normal;
            }
        }

        private static async Task HandleCityInput(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var chatId = update.Message.Chat.Id;
            var city = update.Message.Text;

            // Отправляем запрос на API погоды для получения прогноза погоды
            var weatherForecast = await GetWeatherApi(city);

            // Отправляем прогноз погоды пользователю
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: weatherForecast,
                cancellationToken: cancellationToken);

            // Возвращаемся к обычному состоянию чата
            SetChatState(chatId, ChatState.Normal);
        }

        private static async Task<string> GetWeatherApi(string city)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}?q={city}&appid={ApiKey}&units=metric");
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<WeatherApiResponse>(json);
            if (result.cod != 200)
            {
                throw new Exception(result.message);
            }
            var res = $" {result.weather[0].main}, минимальная температура: {result.main.temp_min}c, максимальная температура: {result.main.temp_max}c, скорость ветра: {result.wind.speed}";

            return res;
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            string errorMessage;

            if (exception is ApiRequestException apiRequestException)
            {
                errorMessage = $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}";
            }
            else
            {
                errorMessage = exception.ToString();
            }

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }



        
    }
}
