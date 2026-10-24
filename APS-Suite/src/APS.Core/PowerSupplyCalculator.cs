using System;
using System.Collections.Generic;
using System.Linq;

namespace APS.Core;

public static class PowerSupplyCalculator
{
    public const double DefaultAgingFactor = 1.25;
    public const double DefaultStandbyHours = 24.0;
    public const double DefaultAlarmHours = 1.0;

    /// <summary>
    /// Расчет требуемой емкости АКБ источника питания по СП 484.1311500.2020 и ГОСТ Р 53325.
    /// C = (I_деж * T_деж + I_пож * T_пож) * K_зап
    /// </summary>
    public static double CalculateRequiredCapacityAh(
        double standbyCurrentA,
        double alarmCurrentA,
        double standbyHours = DefaultStandbyHours,
        double alarmHours = DefaultAlarmHours,
        double agingFactor = DefaultAgingFactor)
    {
        if (standbyCurrentA < 0 || alarmCurrentA < 0)
            throw new ArgumentOutOfRangeException("Ток потребления не может быть отрицательным.");

        if (standbyHours <= 0 || alarmHours <= 0)
            throw new ArgumentOutOfRangeException("Время работы должно быть больше нуля.");

        if (agingFactor < 1.0)
            throw new ArgumentOutOfRangeException("Коэффициент запаса не может быть меньше 1.0.");

        double rawCapacity = (standbyCurrentA * standbyHours) + (alarmCurrentA * alarmHours);
        return Math.Round(rawCapacity * agingFactor, 2);
    }

    /// <summary>
    /// Автоматический подбор оптимального РИП по требуемой емкости и пиковому току нагрузки.
    /// </summary>
    public static EquipmentItem? SelectOptimalPowerSupply(
        IEnumerable<EquipmentItem> catalog,
        double requiredCapacityAh,
        double maxAlarmCurrentA)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        return catalog
            .Where(e => string.Equals(e.Type, "power_supply", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Type, "rip", StringComparison.OrdinalIgnoreCase))
            .Where(e => e.MaxOutputCurrentA >= maxAlarmCurrentA)
            .Where(e => e.BatteryCapacityAh >= requiredCapacityAh)
            .OrderBy(e => e.BatteryCapacityAh)
            .ThenBy(e => e.MaxOutputCurrentA)
            .FirstOrDefault();
    }
}
