using System;
using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;

namespace ZLogger.Formatters
{
    public static class ZLoggerOptionsSystemTextJsonExtensions
    {
        public static ZLoggerOptions UseJsonFormatter(this ZLoggerOptions options, Action<SystemTextJsonZLoggerFormatter> jsonConfigure = null)
        {
            return options.UseFormatter(() =>
            {
                var formatter = new SystemTextJsonZLoggerFormatter();
                jsonConfigure?.Invoke(formatter);
                return formatter;
            });
        }
    }

    public class SystemTextJsonZLoggerFormatter : IZLoggerFormatter
    {
        static readonly JsonEncodedText CategoryNameText = JsonEncodedText.Encode(nameof(LogInfo.CategoryName));
        static readonly JsonEncodedText TimestampText = JsonEncodedText.Encode(nameof(LogInfo.Timestamp));
        static readonly JsonEncodedText LogLevelText = JsonEncodedText.Encode(nameof(LogInfo.LogLevel));
        static readonly JsonEncodedText EventIdText = JsonEncodedText.Encode(nameof(LogInfo.EventId));
        static readonly JsonEncodedText EventIdNameText = JsonEncodedText.Encode("EventIdName");
        static readonly JsonEncodedText ExceptionText = JsonEncodedText.Encode(nameof(LogInfo.Exception));

        static readonly JsonEncodedText NameText = JsonEncodedText.Encode("Name");
        static readonly JsonEncodedText MessageText = JsonEncodedText.Encode("Message");
        static readonly JsonEncodedText StackTraceText = JsonEncodedText.Encode("StackTrace");
        static readonly JsonEncodedText InnerExceptionText = JsonEncodedText.Encode("InnerException");

        static readonly JsonEncodedText Trace = JsonEncodedText.Encode(nameof(LogLevel.Trace));
        static readonly JsonEncodedText Debug = JsonEncodedText.Encode(nameof(LogLevel.Debug));
        static readonly JsonEncodedText Information = JsonEncodedText.Encode(nameof(LogLevel.Information));
        static readonly JsonEncodedText Warning = JsonEncodedText.Encode(nameof(LogLevel.Warning));
        static readonly JsonEncodedText Error = JsonEncodedText.Encode(nameof(LogLevel.Error));
        static readonly JsonEncodedText Critical = JsonEncodedText.Encode(nameof(LogLevel.Critical));
        static readonly JsonEncodedText None = JsonEncodedText.Encode(nameof(LogLevel.None));

        public JsonEncodedText MessagePropertyName { get; set; } = JsonEncodedText.Encode("Message");
        public JsonEncodedText PayloadPropertyName { get; set; } = JsonEncodedText.Encode("Payload");
        public Action<Utf8JsonWriter, LogInfo> MetadataFormatter { get; set; } = DefaultMetadataFormatter;

        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        Utf8JsonWriter jsonWriter;

        public void FormatLogEntry<TEntry, TPayload>(
            IBufferWriter<byte> writer,
            TEntry entry,
            TPayload payload,
            ReadOnlySpan<byte> utf8Message)
            where TEntry : IZLoggerEntry
        {
            if (jsonWriter == null)
            {
                jsonWriter = new Utf8JsonWriter(writer);
            }
            else
            {
                jsonWriter.Reset(writer);
            }

            jsonWriter.WriteStartObject();
            MetadataFormatter.Invoke(jsonWriter, entry.LogInfo);
            jsonWriter.WriteString(MessagePropertyName, utf8Message);

            if (payload != null)
            {
                jsonWriter.WritePropertyName(PayloadPropertyName);
                JsonSerializer.Serialize(jsonWriter, payload, JsonSerializerOptions);
            }

            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
        }

        static JsonEncodedText LogLevelToEncodedText(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return Trace;
                case LogLevel.Debug:
                    return Debug;
                case LogLevel.Information:
                    return Information;
                case LogLevel.Warning:
                    return Warning;
                case LogLevel.Error:
                    return Error;
                case LogLevel.Critical:
                    return Critical;
                case LogLevel.None:
                    return None;
                default:
                    return JsonEncodedText.Encode(((int)logLevel).ToString());
            }
        }

        public static void DefaultMetadataFormatter(Utf8JsonWriter jsonWriter, LogInfo info)
        {
            jsonWriter.WriteString(CategoryNameText, info.CategoryName);
            jsonWriter.WriteString(LogLevelText, LogLevelToEncodedText(info.LogLevel));
            jsonWriter.WriteNumber(EventIdText, info.EventId.Id);
            jsonWriter.WriteString(EventIdNameText, info.EventId.Name);
            jsonWriter.WriteString(TimestampText, info.Timestamp);

            // Write Exception
            if (info.Exception is { } ex)
            {
                WriteException(jsonWriter, ex);
            }
        }

        public static void WriteException(Utf8JsonWriter jsonWriter, Exception ex)
        {
            if (ex == null)
            {
                jsonWriter.WriteNullValue();
            }
            else
            {
                jsonWriter.WriteStartObject();
                {
                    jsonWriter.WriteString(NameText, ex.GetType().FullName);
                    jsonWriter.WriteString(MessageText, ex.Message);
                    jsonWriter.WriteString(StackTraceText, ex.StackTrace);
                    jsonWriter.WritePropertyName(InnerExceptionText);
                    {
                        WriteException(jsonWriter, ex.InnerException);
                    }
                }
                jsonWriter.WriteEndObject();
            }
        }
    }
}
