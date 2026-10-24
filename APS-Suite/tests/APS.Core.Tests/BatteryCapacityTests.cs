using System;
using System.Collections.Generic;
using Xunit;
using APS.Core;

namespace APS.Core.Tests;

public class BatteryCapacityTests
{
    [Theory]
    [InlineData(0.25, 1.20, 24.0, 1.0, 1.25, 9.0)]
    [InlineData(0.50, 2.00, 24.0, 1.0, 1.25, 17.5)]
    public void CalculateRequiredCapacityAh_MeetsSp484Standard(
        double standby,
        double alarm,
        double standbyHours,
        double alarmHours,
        double agingFactor,
        double expected)
    {
        double result = PowerSupplyCalculator.CalculateRequiredCapacityAh(
            standby, alarm, standbyHours, alarmHours, agingFactor);
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void CalculateRequiredCapacityAh_UsingDefaults_Succeeds()
    {
        double result = PowerSupplyCalculator.CalculateRequiredCapacityAh(0.25, 1.20);
        Assert.Equal(9.0, result, 2);
    }

    [Fact]
    public void CalculateRequiredCapacityAh_NegativeCurrent_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            PowerSupplyCalculator.CalculateRequiredCapacityAh(-0.1, 1.0);
        });
    }

    [Fact]
    public void SelectOptimalPowerSupply_SelectsCorrectModel()
    {
        var testCatalog = new List<EquipmentItem>
        {
            new() { Id = "rip_1", Type = "power_supply", Model = "РИП-12 исп.01", MaxOutputCurrentA = 1.0, BatteryCapacityAh = 7.0 },
            new() { Id = "rip_2", Type = "power_supply", Model = "РИП-12 исп.05", MaxOutputCurrentA = 3.0, BatteryCapacityAh = 17.0 },
            new() { Id = "rip_3", Type = "power_supply", Model = "РИП-12 исп.06", MaxOutputCurrentA = 4.0, BatteryCapacityAh = 40.0 }
        };

        // Требуется 9.0 А*ч и пиковый ток 1.5 А
        var selected = PowerSupplyCalculator.SelectOptimalPowerSupply(testCatalog, 9.0, 1.5);

        Assert.NotNull(selected);
        Assert.Equal("РИП-12 исп.05", selected.Model);
    }
}
