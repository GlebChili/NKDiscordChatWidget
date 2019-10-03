using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NKDiscordChatWidget.DiscordBot.Classes;
using NKDiscordChatWidget.General;
using System.Linq;

namespace NKDiscordChatWidget.WidgetServer
{
    /// <summary>
    /// На этот end point приходит фактически один запрос: смена настроек отображения чата
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public class WebsocketClientSide : Hub, IDisposable
    {
        private static IHubContext<WebsocketClientSide> _hubContext;

        private static readonly ConcurrentDictionary<string, WebsocketClientSide> _clients =
            new ConcurrentDictionary<string, WebsocketClientSide>();

        protected readonly ChatDrawOption _chatDrawOption = new ChatDrawOption();

        protected string _guildID;
        protected string _channelID;

        /// <summary>
        /// Контекст всех подключенных по WebSocket клиентов к end-point'у /websocketChat
        /// </summary>
        public static IHubContext<WebsocketClientSide> hubContext
        {
            get => _hubContext;
            set
            {
                if (_hubContext != null)
                {
                    throw new Exception();
                }

                _hubContext = value;
            }
        }

        /// <summary>
        /// Отправка сообщения всем клиентам
        ///
        /// Этот метод (event) вызывается, когда у нас создано/отредактировано сообщение. В редактирование
        /// входит также изменение реакций
        /// </summary>
        public static void UpdateMessage(EventMessageCreate message)
        {
            UpdateMessages(new[] {message});
        }

        /// <summary>
        /// Отправка нескольких сообщений всем клиентам
        ///
        /// Этот метод (event) вызывается, когда у нас создано/отредактировано сообщение. В редактирование
        /// входит также изменение реакций
        /// </summary>
        public static void UpdateMessages(IEnumerable<EventMessageCreate> messages)
        {
            if (!messages.Any())
            {
                return;
            }

            foreach (var (connectionId, client) in _clients)
            {
                var localMessages = messages
                    .Where(message =>
                        (client._guildID == message.guild_id) && (client._channelID == message.channel_id))
                    .ToList();

                if (!localMessages.Any())
                {
                    continue;
                }

                // Сообщение для этого сервера (гильдии) и канала

                var answer = new AnswerFull
                {
                    channel_title = string.Format("Discord Widget Chat: {0}-{1} [nkt]",
                        client._guildID, client._channelID),
                };

                foreach (var localAnswerMessage in localMessages.Select(message =>
                    MessageArtist.DrawMessage(message, client._chatDrawOption)))
                {
                    answer.messages.Add(localAnswerMessage);
                }

                answer.time_answer = ((DateTimeOffset) DateTime.Now).ToUnixTimeMilliseconds() * 0.001d;
                hubContext.Clients.Client(connectionId).SendCoreAsync("ReceiveMessage", new[]
                {
                    answer,
                });
            }
        }

        /// <summary>
        /// Этот метод (event) вызывается, когда у нас сообщение было удалено
        /// </summary>
        public static void RemoveMessage(string guildId, string channelId, string messageId)
        {
            foreach (var (connectionId, client) in _clients)
            {
                if ((client._guildID != guildId) || (client._channelID != channelId))
                {
                    continue;
                }

                hubContext.Clients.Client(connectionId).SendCoreAsync("RemoveMessage", new[]
                {
                    messageId
                });
            }
        }

        protected IClientProxy GetThisProxy()
        {
            return this.Clients.Client(this.Context.ConnectionId);
        }

        public new void Dispose()
        {
            _clients.TryRemove(this.Context.ConnectionId, out _);
            // base.Dispose();
        }

        #region ClientSideSignals

        /// <summary>
        /// Смена настроек отображения чата
        /// </summary>
        /// <param name="jsonOptions"></param>
        /// <returns></returns>
        // ReSharper disable once UnusedMember.Global
        public void ChangeDrawOptionAndGetAllMessages(string jsonOptions)
        {
            _clients[this.Context.ConnectionId] = this;

            Dictionary<string, object> newChatDrawOption;
            try
            {
                newChatDrawOption = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonOptions);
            }
            catch (JsonException e)
            {
                Console.Error.WriteLine(e);
                return;
            }

            foreach (var field in _chatDrawOption.GetType().GetFields())
            {
                if (!newChatDrawOption.ContainsKey(field.Name))
                {
                    // Такого поля нет в настройках
                    continue;
                }

                var value = newChatDrawOption[field.Name];

                switch (field.FieldType.FullName)
                {
                    case "System.Boolean":
                    {
                        switch (value)
                        {
                            case int i:
                                field.SetValue(_chatDrawOption, (i > 0));
                                break;
                            case long l:
                                field.SetValue(_chatDrawOption, (l > 0));
                                break;
                            case string s:
                                field.SetValue(_chatDrawOption, (int.Parse(s) > 0));
                                break;
                        }

                        break;
                    }
                    case "NKDiscordChatWidget.General.EmojiPackType": // @todo сделать правильно, через Enum
                    {
                        int valueInt = 0;
                        switch (value)
                        {
                            case int i:
                                valueInt = i;
                                break;
                            case long l:
                                valueInt = (int) l;
                                break;
                            case bool u:
                                valueInt = u ? 1 : 0;
                                break;
                            case string s:
                                valueInt = int.Parse(s);
                                break;
                        }

                        field.SetValue(_chatDrawOption, (EmojiPackType) valueInt);

                        break;
                    }
                    case "System.Int32":
                    {
                        switch (value)
                        {
                            case int i:
                                field.SetValue(_chatDrawOption, i);
                                break;
                            case long l:
                                field.SetValue(_chatDrawOption, (int) l);
                                break;
                            case string s: // hint: Самый частый вариант мутаций
                                field.SetValue(_chatDrawOption, int.Parse(s));
                                break;
                            case bool u:
                                field.SetValue(_chatDrawOption, u ? 1 : 0);
                                break;
                        }

                        break;
                    }
                    case "System.Int64":
                    {
                        switch (value)
                        {
                            case int i:
                                field.SetValue(_chatDrawOption, i);
                                break;
                            case long l:
                                field.SetValue(_chatDrawOption, l);
                                break;
                            case string s:
                                field.SetValue(_chatDrawOption, long.Parse(s));
                                break;
                            case bool u:
                                field.SetValue(_chatDrawOption, u ? 1 : 0);
                                break;
                        }

                        break;
                    }
                    // @todo string (сейчас строк в настройках не используется)
                    default:
                        Console.Error.WriteLine(
                            "ChangeDrawOption object has field {0} with unexpected type {1}",
                            field.Name, field.FieldType.FullName);
                        break;
                }
            }

            _guildID = newChatDrawOption["guild"] as string;
            _channelID = newChatDrawOption["channel"] as string;

            {
                var answer = new AnswerFull
                {
                    channel_title = string.Format("Discord Widget Chat: {0}-{1} [nkt]", _guildID, _channelID),
                };

                List<EventMessageCreate> messages;
                if (NKDiscordChatWidget.DiscordBot.Bot.messages.ContainsKey(_guildID) &&
                    (NKDiscordChatWidget.DiscordBot.Bot.messages[_guildID].ContainsKey(_channelID)))
                {
                    messages = NKDiscordChatWidget.DiscordBot.Bot.messages[_guildID][_channelID].Values.ToList();
                }
                else
                {
                    // Такого сервера/канала не существует или там нет ни одного сообщения и никогда не было
                    messages = new List<EventMessageCreate>();
                }

                messages.Sort((a, b) => a.timestampAsDT.CompareTo(b.timestampAsDT));
                for (var i = Math.Max(messages.Count - 1000, 0); i < messages.Count; i++)
                {
                    answer.existedID.Add(messages[i].id);
                }

                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < messages.Count; i++)
                {
                    // @todo не работает мердж сообщений от одного пользователя
                    var message = messages[i];
                    var localAnswerMessage = MessageArtist.DrawMessage(message, _chatDrawOption);
                    answer.messages.Add(localAnswerMessage);
                }

                answer.time_answer = ((DateTimeOffset) DateTime.Now).ToUnixTimeMilliseconds() * 0.001d;

                hubContext.Clients.Client(this.Context.ConnectionId)
                    .SendCoreAsync("ReceiveFullMessageList", new[]
                    {
                        answer,
                    });
            }
        }

        #endregion
    }
}