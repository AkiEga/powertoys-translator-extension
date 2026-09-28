using System.Text.Json.Serialization;

namespace Translator;

[JsonSerializable(typeof(TranslationRequest))]
internal sealed partial class WorkerRequestJsonContext : JsonSerializerContext;
