using System;
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
}
