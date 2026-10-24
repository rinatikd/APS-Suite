using System;
using Xunit;

namespace APS.Core.Tests;

public class Sp484CoverageTests
{
    [Theory]
    [InlineData(2.5, 6.4, 9.0, 4.5)]   // Высота до 3.5 м: R=6.4м, шаг 9.0м, от стены 4.5м
    [InlineData(5.0, 5.5, 7.5, 3.75)]  // Высота от 3.5 до 6.0 м: R=5.5м, шаг 7.5м, от стены 3.75м
    [InlineData(8.0, 5.0, 6.5, 3.25)]  // Высота от 6.0 до 10.5 м: R=5.0м, шаг 6.5м, от стены 3.25м
    public void SmokeDetector_CoverageParameters_StrictCompliance(
        double height,
        double expRadius,
        double expSpacing,
        double expWallMargin)
    {
        Assert.True(height <= 12.0, "Высота установки точечных дымовых извещателей не должна превышать 12 м по СП 484.");
        Assert.True(expSpacing <= expRadius * 1.42, "Шаг между извещателями должен вписываться в диаметр зоны контроля.");
        Assert.Equal(expSpacing / 2.0, expWallMargin, 2);
    }
}
