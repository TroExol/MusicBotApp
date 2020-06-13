using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VkNet.Model.Attachments;

namespace MusicBotApp.Controllers
{
    [ApiController]
    [Route("/")]
    public class MusicBotController : ControllerBase
    {
        private readonly ILogger<MusicBotController> _logger;

        public MusicBotController(ILogger<MusicBotController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public async Task<string> Post()
        {
            var parsedRequest = new JObject();
            var result = "ok";

            try
            {
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    var request = await reader.ReadToEndAsync();

                    try
                    {
                        parsedRequest = JObject.Parse(request);
                    }
                    catch
                    {
                        throw new JsonReaderException("Не удалось преобразовать JSON");
                    }
                }

                // await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("ok"));

                if (parsedRequest["type"] == null) throw new Exception("Запрос не имеет свойство type");

                //Проверка Secret Key
                var config = Configuration.GetInstance();
                if (!string.IsNullOrEmpty(config.GetConfig("vkSecretKey")))
                {
                    if (parsedRequest["secret"].ToString() != config.GetConfig("vkSecretKey"))
                    {
                        //Ключи неверные
                        _logger.Log(LogLevel.Warning,
                            "Request from undefined server. Secret keys does not match. Server IP: " +
                            this.Request.Host.Host +
                            Environment.NewLine +
                            "Request: " + parsedRequest.ToString());
                        Response.StatusCode = 403;

                        return "Forbidden";
                    }
                }


                switch (parsedRequest["type"].ToString())
                {
                    case "confirmation":
                        result = VkHelper.GetConfirmationToken();

                        break;
                    case "message_new":
                        handleNewMessage(parsedRequest["object"]);

                        break;
                    default:
                        throw new Exception("Неопознанный тип запроса: " + parsedRequest["type"]);
                }
            }
            catch (Exception e)
            {
                //Что-то сломалось
                result = e.Message;

                _logger.Log(LogLevel.Error, e.Message);
            }

            return result;
        }

        [HttpGet]
        public string Get()
        {
            //GET запросы не принимаются. ВК сервер отправляет только POST
            return "This service does not provide HTTP GET requests.";
        }

        //Обработчик новых сообщений
        private void handleNewMessage(JToken obj)
        {
            var message = "";
            var userId = Convert.ToInt32(obj["message"]?["peer_id"]);
            var date = Convert.ToInt32(obj["message"]?["date"]);

            try
            {
                var ownerIdGroupRAP = -28905875;

                if (userId == 0)
                {
                    message = "Не удалось получить информацию об отправителе";
                    throw new Exception("Не удалось получить информацию об отправителе");
                }

                if (date == 0)
                {
                    date = (int) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }

                // Тело сообщения
                var body = obj["message"]?["text"]?.ToString().Trim().ToLower();

                if (body == null)
                {
                    message = "Не удалось получить текст сообщения";
                    throw new Exception("Не удалось получить тело запроса");
                }

                // Новые треки
                if (body.Contains("новые треки"))
                {
                    var query = "#премьераТрека@rhymes";

                    var isSended = false;
                    var musicCountStr = Regex.Match(body, @"[0-9]+").ToString();

                    if (!int.TryParse(musicCountStr, out var musicCount))
                        musicCount = 4;

                    if (musicCount > 15)
                    {
                        VkHelper.SendMessage(userId, "Количество треков не должно превышать 15", date);
                        return;
                    }

                    var posts = VkHelper.SearchWall(ownerIdGroupRAP, query, musicCount);

                    foreach (var post in posts.WallPosts)
                    {
                        if (post.Attachments == null)
                            continue;

                        message = post.Text;
                        var attachments = new List<Audio>();

                        foreach (var attachment in post.Attachments)
                            if (attachment.Instance is Audio)
                                attachments.Add(attachment.Instance as Audio);

                        // Отправка каждой новой песни пользователю
                        VkHelper.SendMessage(userId, message, date, attachments);
                        isSended = true;
                    } // Конец foreach для posts

                    if (!isSended)
                        VkHelper.SendMessage(userId, "Не удалось найти новые треки", date);
                }
                // Случайные треки
                else if (body.Contains("случайные треки"))
                {
                    int countAudios;
                    int randOffset;
                    var audios = new List<Audio>();
                    var audiosByAuthor = new List<Audio>();

                    var match = Regex.Match(body, @"(случайные треки)\s*([0-9]*)\s*(\w*)", RegexOptions.IgnoreCase);
                    var musicCountStr = match.Groups[2].Value;
                    var author = match.Groups[3].Value;

                    if (!int.TryParse(musicCountStr, out var musicCount))
                        musicCount = 1;

                    if (musicCount > 10)
                        musicCount = 10;
                    
                    if (author == "")
                    {
                        countAudios = VkHelper.GetCountAudio(ownerIdGroupRAP);
                        
                        if (countAudios > 0)
                        {
                            if (countAudios < musicCount)
                                musicCount = countAudios > 10 ? 10 : countAudios;

                            randOffset = new Random().Next(0, countAudios - musicCount);

                            audios = VkHelper.GetAudios(ownerIdGroupRAP, musicCount, randOffset).ToList();
                        }
                        else
                            message = "Не удалось найти песни по указанному запросу";
                    }
                    else
                    {
                        audios = VkHelper.SearchAudios(author, true).ToList();

                        countAudios = audios.Count;
                        if (countAudios > 0)
                        {
                            if (countAudios < musicCount)
                                musicCount = countAudios;

                            randOffset = new Random().Next(0, countAudios - musicCount);

                            audiosByAuthor = audios.ToList().GetRange(randOffset, musicCount);
                        }
                        else
                            message = "Не удалось найти песни по указанному запросу";
                    }

                    VkHelper.SendMessage(userId, message, date, author == "" ? audios : audiosByAuthor);
                }
                //Помощь
                else if (body.Contains("функции"))
                {
                    message = "--------" + Environment.NewLine + "Новые треки N" +
                              Environment.NewLine +
                              "&#8226; Данная функция позволяет получить последние новые песни из группы \"Рифмы и Панчи\"" +
                              Environment.NewLine +
                              "&#8226; N - количество песен для получения. Если не указывать, то по умолчанию вернется 4 песни" +
                              Environment.NewLine +
                              "&#8226; Примеры: Новые треки 10 или Новые треки" + Environment.NewLine +
                              "--------" + Environment.NewLine + "Случайные треки N Автор" +
                              Environment.NewLine +
                              "&#8226; Данная функция позволяет получать случайные песни" +
                              Environment.NewLine +
                              "&#8226; N - количество песен для получения. Если не указывать, то по умолчанию вернется 1 песня" +
                              Environment.NewLine +
                              "&#8226; Автор - автор песен, у которого необходимо получить случайные треки. Если не указывать, будет происходить поиск случайных песен из группы \"Рифмы и Панчи\"" +
                              Environment.NewLine +
                              "&#8226; Примеры: Случайные треки 10 Нервы, или Случайные треки 5, или Случайные треки Нервы, или Случайные треки" +
                              Environment.NewLine +
                              "--------" + Environment.NewLine + "Функции" +
                              Environment.NewLine +
                              "&#8226; Сообщает о доступных функциях";

                    VkHelper.SendMessage(userId, message, date);
                }
                else
                {
                    message = "Неизвестная функция. " + Environment.NewLine +
                              "Попробуйте воспользоваться запросом \"Функции\"";

                    VkHelper.SendMessage(userId, message, date);
                }
            }
            catch (Exception e)
            {
                if (message == "")
                {
                    message = "Что-то пошло не так, не удалось обработать запрос";
                }

                if (userId != 0 && date != 0)
                {
                    VkHelper.SendMessage(userId, message, date);
                }

                _logger.Log(LogLevel.Error, e.Message);
            }
        }
    }
}