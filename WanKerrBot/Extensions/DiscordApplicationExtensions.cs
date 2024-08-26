using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Net;
using DSharpPlus.Net.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WamBot.Extensions
{
    internal static class DiscordApplicationExtensions
    {
        private class ApplicationEmojiCreatePayload
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("image")]
            public string Image { get; set; }
        }

        private delegate Uri GetApiUriForDelegate(string path);
        private delegate object GetBucketDelegate(RestRequestMethod method, string route, object route_params, out string url); // RateLimitBucket

        private static readonly Type RestClient_Type
            = typeof(DiscordClient).Assembly.GetType("DSharpPlus.Net.RestClient", true);
        private static readonly Type RateLimitBucket_Type
            = typeof(DiscordClient).Assembly.GetType("DSharpPlus.Net.RateLimitBucket", true);

        private static readonly PropertyInfo SnowflakeObject_Discord_Prop
            = typeof(SnowflakeObject).GetProperty("Discord", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo BaseDiscordClient_ApiClient_Prop
            = typeof(BaseDiscordClient).GetProperty("ApiClient", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo DiscordApiClient_RestClient_Prop
            = typeof(DiscordApiClient).GetProperty("_rest", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DiscordApiClient_DoRequestAsync_Method
            = typeof(DiscordApiClient).GetMethod("DoRequestAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RestClient_GetBucket_Method
            = RestClient_Type.GetMethod("GetBucket", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo Utilities_GetApiUriFor_Method
            = typeof(Utilities).GetMethod("GetApiUriFor", BindingFlags.Static | BindingFlags.NonPublic, [typeof(string)]);

        static DiscordApplicationExtensions()
        {
            Debug.Assert(RestClient_Type != null);
            Debug.Assert(RateLimitBucket_Type != null);
            Debug.Assert(SnowflakeObject_Discord_Prop != null);
            Debug.Assert(BaseDiscordClient_ApiClient_Prop != null);
            Debug.Assert(DiscordApiClient_RestClient_Prop != null);
            Debug.Assert(DiscordApiClient_DoRequestAsync_Method != null);
            Debug.Assert(RestClient_GetBucket_Method != null);
            Debug.Assert(Utilities_GetApiUriFor_Method != null);
        }

        public static async Task<DiscordEmoji[]> GetApplicationEmojisAsync(this DiscordApplication application)
        {
            var discord = (BaseDiscordClient)SnowflakeObject_Discord_Prop.GetValue(application);
            var apiClient = (DiscordApiClient)BaseDiscordClient_ApiClient_Prop.GetValue(discord);
            var restClient = DiscordApiClient_RestClient_Prop.GetValue(apiClient);

            var GetBucket = RestClient_GetBucket_Method.CreateDelegate<GetBucketDelegate>(restClient);
            var GetApiUriFor = Utilities_GetApiUriFor_Method.CreateDelegate<GetApiUriForDelegate>();

            var route = "/applications/:application_id/emojis";
            var bucket = GetBucket(RestRequestMethod.GET, route, new { application_id = application.Id }, out var url); // RateLimitBucket
            var uri = GetApiUriFor(url);

            var res = await (Task<RestResponse>)DiscordApiClient_DoRequestAsync_Method.Invoke(apiClient,
                [discord, bucket, uri, RestRequestMethod.GET, route, null, null, (double?)null]);

            var json = JObject.Parse(res.Response);

            return ((JArray)json["items"]).ToDiscordObject<DiscordEmoji[]>();
        }

        public static async Task<DiscordEmoji> CreateApplicationEmojiAsync(this DiscordApplication application, string name, Stream file)
        {
            var discord = (BaseDiscordClient)SnowflakeObject_Discord_Prop.GetValue(application);
            var apiClient = (DiscordApiClient)BaseDiscordClient_ApiClient_Prop.GetValue(discord);
            var restClient = DiscordApiClient_RestClient_Prop.GetValue(apiClient);

            var GetBucket = RestClient_GetBucket_Method.CreateDelegate<GetBucketDelegate>(restClient);
            var GetApiUriFor = Utilities_GetApiUriFor_Method.CreateDelegate<GetApiUriForDelegate>();

            var route = "/applications/:application_id/emojis";
            var bucket = GetBucket(RestRequestMethod.POST, route, new { application_id = application.Id }, out var path); // RateLimitBucket
            var url = GetApiUriFor(path);

            using var imageTool = new ImageTool(file);
            var payload = new ApplicationEmojiCreatePayload()
            {
                Name = name,
                Image = imageTool.GetBase64()
            };

            var res = await (Task<RestResponse>)DiscordApiClient_DoRequestAsync_Method.Invoke(apiClient,
                [discord, bucket, url, RestRequestMethod.POST, route, null, DiscordJson.SerializeObject(payload), (double?)null]);

            return JsonConvert.DeserializeObject<DiscordEmoji>(res.Response);
        }
    }
}
