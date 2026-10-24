using System;
using System.Text.Json.Serialization;

namespace APS.Core;

public class EquipmentItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("rated_voltage_v")]
    public double RatedVoltageV { get; set; }

    [JsonPropertyName("max_output_current_a")]
    public double MaxOutputCurrentA { get; set; }

    [JsonPropertyName("battery_capacity_ah")]
    public double BatteryCapacityAh { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
