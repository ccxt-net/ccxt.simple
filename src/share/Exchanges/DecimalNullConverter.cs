using Newtonsoft.Json;

namespace CCXT.Simple.Exchanges
{
    public class XDecimalNullConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return 0m;
            }
            else if (reader.TokenType == JsonToken.String)
            {
                if (String.IsNullOrEmpty(reader.Value.ToString()))
                    return 0m;
            }

            return Convert.ToDecimal(reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue((decimal)value);
        }
    }
}