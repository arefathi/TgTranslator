﻿using System;
using System.Threading.Tasks;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgTranslator.Exceptions;
using TgTranslator.Interfaces;

namespace TgTranslator.Controllers
{
    [Route("api/[controller]")]
    public class TelegramBotController : Controller
    {
        private readonly ICallbackQueryHandler _callbackQueryHandler;
        private readonly TelegramBotClient _client;
        private readonly IMessageHandler _messageHandler;

        public TelegramBotController(TelegramBotClient client, IMessageHandler messageHandler, ICallbackQueryHandler callbackQueryHandler)
        {
            _client = client;
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _callbackQueryHandler = callbackQueryHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        }

        [HttpGet]
        public IActionResult Get() => Ok();

        [HttpPost]
        public async Task<OkResult> Post([FromBody] Update update)
        {
            if (update == null)
                return Ok();

            switch (update.Type)
            {
                case UpdateType.Message:
                    await OnMessage(update.Message);
                    break;
                case UpdateType.CallbackQuery:
                    await OnCallbackQuery(update.CallbackQuery);
                    break;
            }

            return Ok();
        }

        private async Task OnCallbackQuery(CallbackQuery callbackQuery)
        {
            try
            {
                await _callbackQueryHandler.HandleCallbackQueryAsync(callbackQuery);
            }
            catch (UnsupportedCommand exception)
            {
                Log.Error(exception, "Got a CallbackQuery with unsupported command");
            }
            catch (UnsupportedMenuItem exception)
            {
                Log.Error(exception, "Got a CallbackQuery with unsupported item");
            }
            catch (MessageIsNotModifiedException exception)
            {
                Log.Error(exception, "Got a MessageIsNotModifiedException when tried to process CallbackQuery");
            }
        }

        private async Task OnMessage(Message message)
        {
            if (message.Date < Program.StartedTime)
                return;
            
            try
            {
                await _messageHandler.HandleMessageAsync(message);
            }
            catch (InvalidSettingException)
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "It seems that this setting is not supported", replyToMessageId: message.MessageId);
            }
            catch (InvalidSettingValueException)
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "It seems that this value is not supported", replyToMessageId: message.MessageId);
            }
            catch (UnauthorizedSettingChangingException)
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "Hey! Only admins can change settings of this bot!", ParseMode.Default,
                    replyToMessageId: message.MessageId);
            }
            catch (FlurlHttpException exception)
            {
                Log.Error(exception, "Got an HTTP exception:\n{Message}", exception.Message);
            }
            catch (ApiRequestException exception) when (exception.Message == "Bad Request: have no rights to send a message")
            {
                Log.Error("Bot have no rights to send a message in a group: {GroupId}", message.Chat.Id);
            }
        }
    }
}