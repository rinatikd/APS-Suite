using System;
using System.Collections.Generic;
using System.Linq;

namespace APS.Core.Power;

public sealed class BatteryCalculationResult
{
    public double TotalStandbyCurrentA { get; init; }
    public double TotalAlarmCurrentA { get; init; }
    public double StandbyHours { get; init; }
    public double AlarmHours { get; init; }
    public double AgingFactor { get; init; }
    public double RequiredCapacityAh { get; init; }
    public double? SelectedBatteryCapacityAh { get; init; }
    public int BatteryBlocksCount { get; init; }
    public double BlockVoltageV { get; init; }
    public double SystemVoltageV { get; init; }
    public string Formula { get; init; } = "";
}

public static class BatteryCalculator
{
    public static BatteryCalculationResult Calculate(
        double totalStandbyCurrentA,
        double totalAlarmCurrentA,
        double standbyHours = 24.0,
        double alarmHours = 3.0,
        double agingFactor = 1.25,
        double systemVoltageV = 24.0,
        double blockVoltageV = 12.0,
        IReadOnlyList<double>? standardBatteriesAh = null)
    {
        standardBatteriesAh ??= [7, 12, 17, 26, 40, 65, 100, 150, 200];

        double cReq = agingFactor * ((totalStandbyCurrentA * standbyHours) + (totalAlarmCurrentA * alarmHours));
        double? selected = standardBatteriesAh.Where(b => b >= cReq).Cast<double?>().FirstOrDefault();
        int blocks = Math.Max(1, (int)Math.Round(systemVoltageV / blockVoltageV));

        string formula = $"C = {agingFactor:F2} × ({totalStandbyCurrentA:F3} А × {standbyHours:F0} ч + {totalAlarmCurrentA:F3} А × {alarmHours:F0} ч) = {cReq:F2} А·ч";

        return new BatteryCalculationResult
        {
            TotalStandbyCurrentA = Math.Round(totalStandbyCurrentA, 3),
            TotalAlarmCurrentA = Math.Round(totalAlarmCurrentA, 3),
            StandbyHours = standbyHours,
            AlarmHours = alarmHours,
            AgingFactor = agingFactor,
            RequiredCapacityAh = Math.Round(cReq, 2),
            SelectedBatteryCapacityAh = selected,
            BatteryBlocksCount = blocks,
            BlockVoltageV = blockVoltageV,
            SystemVoltageV = systemVoltageV,
            Formula = formula
        };
    }
}
