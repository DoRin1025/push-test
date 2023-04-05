using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Model.Utils
{
    public class JsonUtil
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Settings);
        }

        public static TObject Deserialize<TObject>(string json)
        {
            return JsonConvert.DeserializeObject<TObject>(json, Settings);
        }
        public static string GetJsonValueString(JObject jsonObject, string key)
        {
            return jsonObject.ContainsKey(key) ? jsonObject[key].ToString() : null;
        }

        public static string GetJsonValueString(JObject jsonObject, string key, string defaultValue)
        {
            return jsonObject.ContainsKey(key) ? jsonObject[key].ToString() : defaultValue;
        }
    }
}