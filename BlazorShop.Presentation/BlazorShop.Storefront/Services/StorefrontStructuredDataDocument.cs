namespace BlazorShop.Storefront.Services
{
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    public sealed class StorefrontStructuredDataDocument
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.Default,
            WriteIndented = false,
        };

        private StorefrontStructuredDataDocument(JsonObject payload)
        {
            Payload = payload;
        }

        public JsonObject Payload { get; }

        public bool IsEmpty => Payload["@graph"] is not JsonArray graph || graph.Count == 0;

        public static StorefrontStructuredDataDocument Empty { get; } = CreateGraph([]);

        public static StorefrontStructuredDataDocument CreateGraph(IEnumerable<JsonObject> nodes)
        {
            ArgumentNullException.ThrowIfNull(nodes);

            var graph = new JsonArray();
            foreach (var node in nodes)
            {
                if (node.Count > 0)
                {
                    graph.Add(node);
                }
            }

            return new StorefrontStructuredDataDocument(new JsonObject
            {
                ["@context"] = "https://schema.org",
                ["@graph"] = graph,
            });
        }

        public string ToJson()
        {
            return Payload.ToJsonString(SerializerOptions);
        }
    }
}