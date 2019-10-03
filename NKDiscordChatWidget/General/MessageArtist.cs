using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using NKDiscordChatWidget.DiscordBot.Classes;

namespace NKDiscordChatWidget.General
{
    public static class MessageArtist
    {
        public static AnswerMessage DrawMessage(EventMessageCreate message, ChatDrawOption chatDrawOption)
        {
            string htmlContent = string.Format("<div class='content-message' data-id='{1}'>{0}</div>",
                DrawMessageContent(message, chatDrawOption), message.id);
            var timeUpdate = (message.edited_timestampAsDT != DateTime.MinValue)
                ? message.edited_timestampAsDT
                : message.timestampAsDT;
            /*
            if (chatOption.merge_same_user_messages)
            {
                // Соединяем сообщения одного и того же человека
                // @todo где-то здесь баг имплементации. Надо поправить
                for (var j = i + 1; j < messages.Count; j++)
                {
                    if (messages[j].author.id == messages[i].author.id)
                    {
                        var localTimeUpdate = (messages[j].edited_timestampAsDT != DateTime.MinValue)
                            ? messages[j].edited_timestampAsDT
                            : messages[j].timestampAsDT;
                        if (localTimeUpdate > timeUpdate)
                        {
                            timeUpdate = localTimeUpdate;
                        }

                        htmlContent += string.Format("<div class='content-message' data-id='{1}'>{0}</div>",
                            DrawMessageContent(messages[j], chatOption), messages[j].id);
                        i = j;
                    }
                }
            }
            
            */

            string nickColor = "inherit";
            if (message.member.roles.Any())
            {
                var roleID = message.member.roles.First();
                var roles = NKDiscordChatWidget.DiscordBot.Bot.guilds[message.guild_id].roles.ToList();
                roles.Sort((a, b) => a.position.CompareTo(b.position));
                Role role = roles.FirstOrDefault(t => t.id == roleID);
                if (role != null)
                {
                    nickColor = role.color.ToString("X");
                    nickColor = "#" + nickColor.PadLeft(6, '0');
                }
            }

            string sha1hash;
            using (var hashA = SHA1.Create())
            {
                byte[] data = hashA.ComputeHash(Encoding.UTF8.GetBytes(htmlContent));
                sha1hash = data.Aggregate("", (current, c) => current + c.ToString("x2"));
            }

            var html = string.Format(
                "<div class='user'><img src='{0}' alt='{1}'></div>" +
                "<div class='content'>" +
                "<div class='content-header'><span class='content-user' style='color: {4};'>{1}</span><span class='content-time'>{3:hh:mm:ss dd.MM.yyyy}</span></div>" +
                "{2}" +
                "</div><div style='clear: both;'></div><hr>",
                HttpUtility.HtmlEncode(message.author.avatarURL),
                HttpUtility.HtmlEncode(
                    !string.IsNullOrEmpty(message.member.nick)
                        ? message.member.nick
                        : message.author.username
                ),
                htmlContent,
                message.timestampAsDT,
                nickColor
            );

            return new AnswerMessage()
            {
                id = message.id,
                time = ((DateTimeOffset) message.timestampAsDT).ToUnixTimeMilliseconds() * 0.001d,
                time_update = ((DateTimeOffset) timeUpdate).ToUnixTimeMilliseconds() * 0.001d,
                html = html,
                hash = sha1hash,
            };
        }

        private static string DrawMessageContent(EventMessageCreate message, ChatDrawOption chatOption)
        {
            var guildID = message.guild_id;
            var guild = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID];
            var thisGuildEmojis = new HashSet<string>(guild.emojis.Select(emoji => emoji.id).ToList());

            // Основной текст
            string directContentHTML = NKDiscordChatWidget.General.MessageMark.RenderAsHTML(
                message.content, chatOption, message.mentions, guildID);
            bool containOnlyUnicodeAndSpace;
            {
                var rEmojiWithinText = new Regex(@"<\:(.+?)\:([0-9]+)>", RegexOptions.Compiled);
                long[] longs = { };
                if (message.content != null)
                {
                    longs = Utf8ToUnicode.ToUnicodeCode(rEmojiWithinText.Replace(message.content, ""));
                }

                containOnlyUnicodeAndSpace = Utf8ToUnicode.ContainOnlyUnicodeAndSpace(longs);
            }
            string html = string.Format("<div class='content-direct {1}'>{0}</div>",
                directContentHTML, containOnlyUnicodeAndSpace ? "only-emoji" : "");

            // attachments
            if ((message.attachments != null) && message.attachments.Any() && (chatOption.attachments != 2))
            {
                string attachmentHTML = "";

                foreach (var attachment in message.attachments)
                {
                    var extension = attachment.filename.Split('.').Last().ToLowerInvariant();
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (extension)
                    {
                        case "mp4":
                        case "webm":
                            attachmentHTML += string.Format(
                                "<div class='attachment {1}'><video><source src='{0}'></video></div>",
                                HttpUtility.HtmlEncode(attachment.proxy_url),
                                (chatOption.attachments == 1) ? "blur" : ""
                            );
                            break;
                        case "jpeg":
                        case "jpg":
                        case "bmp":
                        case "gif":
                        case "png":
                            attachmentHTML += string.Format(
                                "<div class='attachment {3}'><img src='{0}' data-width='{1}' data-height='{2}'></div>",
                                HttpUtility.HtmlEncode(attachment.proxy_url),
                                attachment.width,
                                attachment.height,
                                (chatOption.attachments == 1) ? "blur" : ""
                            );
                            break;
                    }
                }

                html += string.Format("<div class='attachment-block'>{0}</div>", attachmentHTML);
            }

            // Preview
            if ((message.embeds != null) && message.embeds.Any() && (chatOption.link_preview != 2))
            {
                string previewHTML = "";

                foreach (var embed in message.embeds)
                {
                    var subHTML = "";
                    if (embed.provider != null)
                    {
                        subHTML += string.Format("<div class='provider'>{0}</div>",
                            HttpUtility.HtmlEncode(embed.provider.name));
                    }

                    if (embed.author != null)
                    {
                        subHTML += string.Format("<div class='author'>{0}</div>",
                            HttpUtility.HtmlEncode(embed.author.name));
                    }

                    subHTML += string.Format("<div class='title'>{0}</div>",
                        HttpUtility.HtmlEncode(embed.title));

                    if (embed.thumbnail != null)
                    {
                        subHTML += string.Format("<div class='preview'><img src='{0}' alt='{1}'></div>",
                            HttpUtility.HtmlEncode(embed.thumbnail.proxy_url),
                            HttpUtility.HtmlEncode("Превью для «" + embed.title + "»")
                        );
                    }

                    var nickColor = embed.color.ToString("X");
                    nickColor = "#" + nickColor.PadLeft(6, '0');

                    previewHTML += string.Format(
                        "<div class='embed {2}'><div class='embed-pill' style='background-color: {1};'></div>" +
                        "<div class='embed-content'>{0}</div></div>",
                        subHTML,
                        nickColor,
                        (chatOption.link_preview == 1) ? "blur" : ""
                    );
                }

                html += string.Format("<div class='embed-block'>{0}</div>", previewHTML);
            }

            // Реакции
            if (
                (message.reactions != null) && message.reactions.Any() &&
                ((chatOption.message_relative_reaction != 2) || (chatOption.message_stranger_reaction != 2))
            )
            {
                // Реакции
                var reactionHTMLs = new List<string>();
                foreach (var reaction in message.reactions)
                {
                    bool isRelative = ((reaction.emoji.id == null) || thisGuildEmojis.Contains(reaction.emoji.id));
                    int emojiShow = isRelative
                        ? chatOption.message_relative_reaction
                        : chatOption.message_stranger_reaction;
                    if (emojiShow == 2) // @todo убрать магическую константу
                    {
                        continue;
                    }

                    AddMessageReactionHTML(
                        reactionHTMLs,
                        reaction,
                        emojiShow,
                        chatOption
                    );
                }

                var s = reactionHTMLs.Aggregate("", (current, s1) => current + s1);
                html += string.Format("<div class='content-reaction'>{0}</div>", s);
            }

            return html;
        }

        private static void AddMessageReactionHTML(
            ICollection<string> reactionHTMLs,
            NKDiscordChatWidget.DiscordBot.Classes.EventMessageCreate.EventMessageCreate_Reaction reaction,
            int emojiShow,
            ChatDrawOption chatOption
        )
        {
            if (reaction.emoji.id != null)
            {
                // Эмодзи из Дискорда (паки эмодзей с серверов)
                reactionHTMLs.Add(string.Format(
                    "<div class='emoji {2}'><img src='{0}' alt=':{1}:'><span class='count'>{3}</span></div>",
                    HttpUtility.HtmlEncode(reaction.emoji.URL),
                    HttpUtility.HtmlEncode(reaction.emoji.name),
                    (emojiShow == 1) ? "blur" : "",
                    reaction.count
                ));
            }
            else
            {
                // Стандартные Unicode-эмодзи
                string emojiHtml;
                var emojiPack = chatOption.unicode_emoji_displaying;
                if (emojiPack != EmojiPackType.StandardOS)
                {
                    var longs = Utf8ToUnicode.ToUnicodeCode(reaction.emoji.name);
                    var u = longs.Any(code => !UnicodeEmojiEngine.IsInIntervalEmoji(code, emojiPack));

                    if (u)
                    {
                        // Реакция без ID, но при этом не является эмодзи, рисуем как есть
                        emojiHtml = HttpUtility.HtmlEncode(reaction.emoji.name);
                    }
                    else
                    {
                        // Реакция без ID и является эмодзи, поэтому рисуем как картинку
                        var localEmojiList = UnicodeEmojiEngine.RenderEmojiAsStringList(
                            emojiPack, longs);
                        emojiHtml = "";
                        // ReSharper disable once LoopCanBeConvertedToQuery
                        foreach (var item in localEmojiList)
                        {
                            // hint: вообще-то localEmojiList.Count==1 ВСЕГДА, но на всякий случай...
                            var emojiSubFolderName = UnicodeEmojiEngine.GetImageSubFolder(emojiPack);
                            var emojiExtension = UnicodeEmojiEngine.GetImageExtension(emojiPack);
                            var url = string.Format("/images/emoji/{0}/{1}.{2}",
                                emojiSubFolderName,
                                item.emojiCode,
                                emojiExtension
                            );

                            emojiHtml += string.Format("<img src='{0}' alt=':{1}:'>",
                                HttpUtility.HtmlEncode(url),
                                HttpUtility.HtmlEncode(reaction.emoji.name)
                            );
                        }
                    }
                }
                else
                {
                    emojiHtml = HttpUtility.HtmlEncode(reaction.emoji.name);
                }

                reactionHTMLs.Add(string.Format(
                    "<div class='emoji {1}'>{0}<span class='count'>{2}</span></div>",
                    emojiHtml,
                    (emojiShow == 1) ? "blur" : "",
                    reaction.count
                ));
            }
        }
    }

    // ReSharper disable NotAccessedField.Global
    public class AnswerMessage
    {
        public string id;
        public double time;
        public double time_update;
        public string html;

        /// <summary>
        /// Уникальный хеш, взятый от HTML-контента сообщения
        /// </summary>
        public string hash;
    }

    public class AnswerFull
    {
        public readonly List<AnswerMessage> messages = new List<AnswerMessage>();
        public double time_answer;
        public readonly HashSet<string> existedID = new HashSet<string>();

        /// <summary>
        /// Заголовок для окна виджета (нужно только для дебага этого виджета)
        /// </summary>
        public string channel_title;
    }
    // ReSharper restore NotAccessedField.Global
}