using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VkNet;
using VkNet.AudioBypassService.Extensions;
using VkNet.Enums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using VkNet.Utils;

namespace MusicBotApp
{
    /// <summary>
    /// Класс-помощник в работе с Vk Api
    /// Через данный класс идет взаимодействие с VK
    /// </summary>
    public class VkHelper
    {
        private static VkApi apiAccess;
        private static VkApi apiService;
        private static VkApi apiAccount;

        private static VkHelper instance = new VkHelper();

        public VkHelper GetInstance()
        {
            return instance;
        }

        private VkHelper()
        {
        }

        public static string GetConfirmationToken()
        {
            return Configuration.GetInstance().GetConfig("vkConfirmationToken");
        }

        // Access token или service token
        private string GetToken(bool isAccess)
        {
            var config = Configuration.GetInstance();
            return isAccess ? config.GetConfig("vkAccessToken") : config.GetConfig("vkServiceToken");
        }

        private void ConnectToVkWithAccess(bool isAccess = true)
        {
            if (isAccess)
            {
                if (apiAccess == null || !apiAccess.IsAuthorized)
                {
                    apiAccess = new VkApi();
                    try
                    {
                        apiAccess.Authorize(new ApiAuthParams
                        {
                            AccessToken = GetToken(isAccess)
                        });
                    }
                    catch (VkApiException e)
                    {
                        Console.Error.WriteLine("Неизвестная ошибка при подключении к Vk Api через access токен!\n" + e); //FIXME: DEBUG
                    }
                }
            }
            else
            {
                if (apiService == null || !apiService.IsAuthorized)
                {
                    apiService = new VkApi();
                    try
                    {
                        apiService.Authorize(new ApiAuthParams
                        {
                            AccessToken = GetToken(isAccess)
                        });
                    }
                    catch (VkApiException e)
                    {
                        Console.Error.WriteLine("Неизвестная ошибка при подключении к Vk Api через service токен!\n" + e); //FIXME: DEBUG
                    }
                }
            }
        }

        private void ConnectToVkWithAccount()
        {
            if (apiAccount == null || !apiAccount.IsAuthorized)
            {
                var services = new ServiceCollection();
                services.AddAudioBypass();

                apiAccount = new VkApi(services);
                var config = Configuration.GetInstance();

                apiAccount.Authorize(new ApiAuthParams
                {
                    Login = config.GetConfig("vkLogin"),
                    Password = config.GetConfig("vkPassword"),
                });
            }
        }


        public static void SendMessage(int userId, string message, int date, List<Audio> attachments = null)
        {
            // Подключение к API
            instance.ConnectToVkWithAccess(true);

            // Тело функции
            apiAccess.Messages.Send(new MessagesSendParams
            {
                RandomId = date + new Random().Next(0, 999), // уникальный
                UserId = userId,
                Message = message,
                Attachments = attachments
            });
        }

        public static WallGetObject SearchWall(int ownerId, string query, int count = 4)
        {
            // Подключение к API
            instance.ConnectToVkWithAccess(false);

            // Тело функции
            var posts = apiService.Wall.Search(new WallSearchParams
            {
                OwnerId = ownerId,
                Query = query,
                Count = count
            });

            return posts;
        }

        public static int GetCountAudio(long ownerId)
        {
            // Подключение к API
            instance.ConnectToVkWithAccount();

            // Тело функции
            var count = apiAccount.Audio.GetCount(ownerId);

            return Convert.ToInt32(count);
        }

        public static VkCollection<Audio> GetAudios(int ownerId, int count, int offset)
        {
            // Подключение к API
            instance.ConnectToVkWithAccount();

            var audios = apiAccount.Audio.Get(new AudioGetParams()
            {
                OwnerId = ownerId,
                Offset = offset,
                Count = count
            });

            return audios;
        }

        public static VkCollection<Audio> SearchAudios(string query, bool isPerformerOnly)
        {
            // Подключение к API
            instance.ConnectToVkWithAccount();

            var audios = apiAccount.Audio.Search(new AudioSearchParams()
            {
                Query = query,
                PerformerOnly = isPerformerOnly,
                Autocomplete = true,
                Sort = AudioSort.Popularity
            });

            return audios;
        }
    }
}