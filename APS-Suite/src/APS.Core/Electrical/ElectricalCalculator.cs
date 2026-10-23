using System;
using System.Collections.Generic;

namespace APS.Core.Electrical;

public sealed class RadialLineResult
{
    public double CurrentStandbyMa { get; init; }
    public double CurrentMaxMa { get; init; }
    public double LoopResistanceOhm { get; init; }
    public double VoltageDropStandbyV { get; init; }
    public double VoltageDropMaxV { get; init; }
    public double VoltageEndMaxV { get; init; }
    public bool Ok { get; init; }
    public List<string> Checks { get; } = [];
}

public sealed class RingLineResult
{
    public double CurrentStandbyMa { get; init; }
    public double CurrentMaxMa { get; init; }
    public double LoopResistanceOhm { get; init; }
    public double ResistanceFarDeviceOhm { get; init; }
    public double VoltageDropNormalV { get; init; }
    public double VoltageDropStandbyV { get; init; }
    public double VoltageDropMaxBreakV { get; init; }
    public double VoltageEndMaxV { get; init; }
    public int WorstBreakIndex { get; init; }
    public bool Ok { get; init; }
    public List<string> Checks { get; } = [];
}

public static class ElectricalCalculator
{
    public static RadialLineResult CalculateRadial(
        IReadOnlyList<double> segmentLengthsM,
        IReadOnlyList<double> curStandbyMa,
        IReadOnlyList<double> curMaxMa,
        double rOhmKm,
        double u0,
        double uMin,
        double? iMaxMa = null,
        double? rMaxOhm = null)
    {
        int n = segmentLengthsM.Count;
        var tailS = new double[n + 1];
        var tailM = new double[n + 1];

        for (int i = n - 1; i >= 0; i--)
        {
            tailS[i] = tailS[i + 1] + (i < curStandbyMa.Count ? curStandbyMa[i] : 0.0);
            tailM[i] = tailM[i + 1] + (i < curMaxMa.Count ? curMaxMa[i] : 0.0);
        }

        double duS = 0.0, duM = 0.0, rLoop = 0.0;
        for (int i = 0; i < n; i++)
        {
            double r = 2.0 * rOhmKm * segmentLengthsM[i] / 1000.0;
            rLoop += r;
            duS += r * tailS[i] / 1000.0;
            duM += r * tailM[i] / 1000.0;
        }

        var checks = new List<string>();
        bool ok = true;
        double uEnd = u0 - duM;

        if (uEnd < uMin)
        {
            ok = false;
            checks.Add($"U в конце {uEnd:F2} В < {uMin:F2} В");
        }
        if (iMaxMa.HasValue && tailM[0] > iMaxMa.Value)
        {
            ok = false;
            checks.Add($"Ток {tailM[0]:F1} мА > {iMaxMa.Value:F1} мА");
        }
        if (rMaxOhm.HasValue && rLoop > rMaxOhm.Value)
        {
            ok = false;
            checks.Add($"R линии {rLoop:F1} Ом > {rMaxOhm.Value:F1} Ом");
        }

        var res = new RadialLineResult
        {
            CurrentStandbyMa = Math.Round(tailS[0], 2),
            CurrentMaxMa = Math.Round(tailM[0], 2),
            LoopResistanceOhm = Math.Round(rLoop, 2),
            VoltageDropStandbyV = Math.Round(duS, 3),
            VoltageDropMaxV = Math.Round(duM, 3),
            VoltageEndMaxV = Math.Round(uEnd, 2),
            Ok = ok
        };
        res.Checks.AddRange(checks);
        return res;
    }

    public static RingLineResult CalculateRing(
        IReadOnlyList<double> segmentLengthsM,
        IReadOnlyList<double> curStandbyMa,
        IReadOnlyList<double> curMaxMa,
        double rOhmKm,
        double u0,
        double uMin,
        double? iMaxMa = null,
        double? rMaxOhm = null)
    {
        int n = curMaxMa.Count;
        var r = new double[segmentLengthsM.Count];
        double rtot = 0.0;

        for (int i = 0; i < segmentLengthsM.Count; i++)
        {
            r[i] = 2.0 * rOhmKm * segmentLengthsM[i] / 1000.0;
            rtot += r[i];
        }

        var cum = new List<double>();
        double acc = 0.0;
        for (int k = 0; k < n; k++)
        {
            acc += r[k];
            cum.Add(acc);
        }

        double worstM = 0.0, worstS = 0.0, rFar = 0.0;
        int worstBreak = n;

        for (int b = 0; b <= n; b++)
        {
            double leftM = 0.0, tm = 0.0, rl = 0.0;
            double leftS = 0.0, ts = 0.0;
            for (int i = b - 1; i >= 0; i--)
            {
                tm += curMaxMa[i];
                ts += curStandbyMa[i];
                leftM += r[i] * tm / 1000.0;
                leftS += r[i] * ts / 1000.0;
                rl += r[i];
            }

            double rightM = 0.0, trm = 0.0, rr = 0.0;
            double rightS = 0.0, trs = 0.0;
            for (int i = b; i < n; i++)
            {
                trm += curMaxMa[i];
                trs += curStandbyMa[i];
                rightM += r[i + 1] * trm / 1000.0;
                rightS += r[i + 1] * trs / 1000.0;
                rr += r[i + 1];
            }

            double dM = Math.Max(leftM, rightM);
            double dS = Math.Max(leftS, rightS);
            double rf = Math.Max(rl, rr);

            if (dM > worstM + 1e-12)
            {
                worstM = dM;
                worstBreak = b;
            }
            if (dS > worstS) worstS = dS;
            if (rf > rFar) rFar = rf;
        }

        double iS = 0.0, iM = 0.0;
        foreach (var c in curStandbyMa) iS += c;
        foreach (var c in curMaxMa) iM += c;

        var checks = new List<string>();
        bool ok = true;
        double uEnd = u0 - worstM;

        if (uEnd < uMin)
        {
            ok = false;
            checks.Add($"Обрыв на участке {worstBreak + 1}: U {uEnd:F2} В < {uMin:F2} В");
        }
        if (iMaxMa.HasValue && iM > iMaxMa.Value)
        {
            ok = false;
            checks.Add($"Ток {iM:F1} мА > {iMaxMa.Value:F1} мА");
        }
        if (rMaxOhm.HasValue && rFar > rMaxOhm.Value)
        {
            ok = false;
            checks.Add($"R до дальнего устройства при обрыве {rFar:F1} Ом > {rMaxOhm.Value:F1} Ом");
        }

        var res = new RingLineResult
        {
            CurrentStandbyMa = Math.Round(iS, 2),
            CurrentMaxMa = Math.Round(iM, 2),
            LoopResistanceOhm = Math.Round(rtot, 2),
            ResistanceFarDeviceOhm = Math.Round(rFar, 2),
            VoltageDropStandbyV = Math.Round(worstS, 3),
            VoltageDropMaxBreakV = Math.Round(worstM, 3),
            VoltageEndMaxV = Math.Round(uEnd, 2),
            WorstBreakIndex = worstBreak,
            Ok = ok
        };
        res.Checks.AddRange(checks);
        return res;
    }
}
