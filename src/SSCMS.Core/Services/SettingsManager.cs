﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Datory;
using Microsoft.Extensions.Configuration;
using SSCMS.Models;
using SSCMS.Utils;

namespace SSCMS.Core.Services
{
    public class SettingsManager : ISettingsManager
    {
        private readonly IConfiguration _config;

        public SettingsManager(IConfiguration config, string contentRootPath, string webRootPath, Assembly entryAssembly)
        {
            _config = config;
            ContentRootPath = contentRootPath;
            WebRootPath = webRootPath;

            if (entryAssembly != null)
            {
                Version = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    .InformationalVersion;

                if (entryAssembly
                    .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
                    .SingleOrDefault() is TargetFrameworkAttribute targetFrameworkAttribute)
                {
                    TargetFramework = targetFrameworkAttribute.FrameworkName;
                }
            }

            if (string.IsNullOrEmpty(SecurityKey))
            {
                var securityKey = StringUtils.GetShortGuid(false) + StringUtils.GetShortGuid(false) +
                                  StringUtils.GetShortGuid(false);
                var filePath = PathUtils.Combine(contentRootPath, Constants.ConfigFileName);
                var json = FileUtils.ReadText(filePath);
                json = json.Replace(@"""SecurityKey"": """",", $@"""SecurityKey"": ""{securityKey}"",");
                FileUtils.WriteText(filePath, json);
            }


            //var menusPath = PathUtils.GetLangPath(contentRootPath, "en", "menus.yml");
            //if (FileUtils.IsFileExists(menusPath))
            //{
            //    Menus = YamlUtils.FileToObject<IList<Menu>>(menusPath);
            //}

            //var permissionsPath = PathUtils.GetLangPath(contentRootPath, "en", "permissions.yml");
            //if (FileUtils.IsFileExists(permissionsPath))
            //{
            //    Permissions = YamlUtils.FileToObject<PermissionsSettings>(permissionsPath);
            //}
        }

        public string ContentRootPath { get; }
        public string WebRootPath { get; }
        public string Version { get; }
        public string TargetFramework { get; }
        public bool IsNightlyUpdate => _config.GetValue<bool>(nameof(IsNightlyUpdate));
        public bool IsProtectData => _config.GetValue<bool>(nameof(IsProtectData));
        public string SecurityKey => _config.GetValue<string>(nameof(SecurityKey));
        public DatabaseType DatabaseType => TranslateUtils.ToEnum(IsProtectData ? Decrypt(_config.GetValue<string>("Database:Type")) : _config.GetValue<string>("Database:Type"), DatabaseType.MySql);
        public string DatabaseConnectionString => IsProtectData ? Decrypt(_config.GetValue<string>("Database:ConnectionString")) : _config.GetValue<string>("Database:ConnectionString");
        public IDatabase Database => new Database(DatabaseType, DatabaseConnectionString);
        public string RedisConnectionString => IsProtectData ? Decrypt(_config.GetValue<string>("Redis:ConnectionString")) : _config.GetValue<string>("Redis:ConnectionString");
        public IRedis Redis => new Redis(RedisConnectionString);

        //public IList<Menu> Menus { get; }
        //public PermissionsSettings Permissions { get; }

        public string Encrypt(string inputString, string securityKey = null)
        {
            return TranslateUtils.EncryptStringBySecretKey(inputString, !string.IsNullOrEmpty(securityKey) ? securityKey : SecurityKey);
        }

        public string Decrypt(string inputString, string securityKey = null)
        {
            return TranslateUtils.DecryptStringBySecretKey(inputString, !string.IsNullOrEmpty(securityKey) ? securityKey : SecurityKey);
        }

        public async Task SaveSettingsAsync(bool isNightlyUpdate, bool isProtectData, DatabaseType databaseType, string databaseConnectionString, string redisConnectionString)
        {
            var path = PathUtils.Combine(ContentRootPath, Constants.ConfigFileName);

            var type = databaseType.GetValue();
            var databaseConnectionStringValue = databaseConnectionString;
            var redisConnectionStringValue = redisConnectionString;
            if (isProtectData)
            {
                type = Encrypt(type, SecurityKey);
                databaseConnectionStringValue = Encrypt(databaseConnectionStringValue, SecurityKey);
                redisConnectionStringValue = Encrypt(redisConnectionString, SecurityKey);
            }

            var json = $@"
{{
  ""IsNightlyUpdate"": {isNightlyUpdate.ToString().ToLower()},
  ""IsProtectData"": {isProtectData.ToString().ToLower()},
  ""SecurityKey"": ""{SecurityKey}"",
  ""Database"": {{
    ""Type"": ""{type}"",
    ""ConnectionString"": ""{databaseConnectionStringValue}""
  }},
  ""Redis"": {{
    ""ConnectionString"": ""{redisConnectionStringValue}""
  }}
}}";

            await FileUtils.WriteTextAsync(path, json.Trim());
        }
    }
}
