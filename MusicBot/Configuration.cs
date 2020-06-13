using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MusicBotApp
{
    /// <summary>
    /// Singleton
    /// Конфигурация бота
    /// </summary>
    public class Configuration
    {
        private static Configuration instance;

        private Dictionary<string, string> configs;
        
        private const string CONFIG_FILE_NAME = "config.json";
        
        public static Configuration GetInstance()
        {
            if (instance == null)
            {
                instance = new Configuration();
            }

            return instance;
        }
        
        
        private Configuration()
        {
            ReadConfig();
        }

        public string GetConfig(string key)
        {
            if (configs.ContainsKey(key))
            {
                return configs[key];
            }

            return null;
        }

        private void ReadConfig()
        {
            if (!File.Exists(CONFIG_FILE_NAME))
            {
                CreateConfigFile();
                throw new Exception("Config file has been created in the program directory.");
            }

            var config = File.ReadAllText(CONFIG_FILE_NAME);
            configs = JsonSerializer.Deserialize<Dictionary<string, string>>(config);
        }

        private void CreateConfigFile()
        {
            if (File.Exists(CONFIG_FILE_NAME))
            {
                return;
            }
            
            var defaultMap = new Dictionary<string, string>
            {
                ["vkAccessToken"] = "",
                ["vkServiceToken"] = "",
                ["vkConfirmationToken"] = "",
                ["vkSecretKey"] = "",
                ["vkLogin"] = "",
                ["vkPassword"] = "",
            };
            
            File.WriteAllText(CONFIG_FILE_NAME, JsonSerializer.Serialize(defaultMap));
        }
        
        
    }
}