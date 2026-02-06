using KeSpider.OutlinkHandlers;
using System.Text.Json.Serialization;

namespace KeSpider;

[JsonSerializable(typeof(OneDriveOutlinkHandler.DriveItem))]
sealed partial class AppJsonSerializerContext : JsonSerializerContext;