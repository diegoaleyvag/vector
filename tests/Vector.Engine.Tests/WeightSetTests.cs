using Vector.Domain;

namespace Vector.Engine.Tests;

/// <summary>Invariant 8: WeightSet.FromTiers always sums to exactly 10000 basis points.</summary>
public class WeightSetTests
{
    private static readonly ConstraintDimension[] CanonicalDimensions =
        [.. Enum.GetValues<ConstraintDimension>().OrderBy(d => (int)d)];

    private static IReadOnlyDictionary<ConstraintDimension, int> Tiers(int[] values)
    {
        var dict = new Dictionary<ConstraintDimension, int>();
        for (var i = 0; i < CanonicalDimensions.Length; i++)
        {
            dict[CanonicalDimensions[i]] = values[i];
        }
        return dict;
    }

    [Fact]
    public void AllZeroTiers_DistributesEquallyAt1250Each()
    {
        var weights = WeightSet.FromTiers(Tiers([0, 0, 0, 0, 0, 0, 0, 0]));

        Assert.Equal(8, weights.BasisPoints.Count);
        foreach (var kv in weights.BasisPoints)
        {
            Assert.Equal(1250, kv.Value);
        }
        Assert.Equal(10000, weights.BasisPoints.Values.Sum());
    }

    [Fact]
    public void SingleNonZeroTier_ReceivesAllBasisPoints()
    {
        var weights = WeightSet.FromTiers(Tiers([0, 0, 3, 0, 0, 0, 0, 0]));

        Assert.Equal(10000, weights[ConstraintDimension.CostPressure]);
        Assert.Equal(10000, weights.BasisPoints.Values.Sum());
    }

    [Theory]
    [InlineData(new object[] { new[] { 1, 1, 1, 1, 1, 1, 1, 1 } })]
    [InlineData(new object[] { new[] { 3, 2, 1, 0, 0, 1, 2, 3 } })]
    [InlineData(new object[] { new[] { 3, 3, 3, 3, 3, 3, 3, 3 } })]
    [InlineData(new object[] { new[] { 0, 0, 0, 1, 0, 0, 0, 0 } })]
    [InlineData(new object[] { new[] { 2, 3, 1, 3, 0, 2, 1, 3 } })]
    [InlineData(new object[] { new[] { 1, 0, 0, 0, 0, 0, 0, 0 } })]
    public void SumIsAlwaysExactly10000(int[] tiers)
    {
        var weights = WeightSet.FromTiers(Tiers(tiers));

        Assert.Equal(10000, weights.BasisPoints.Values.Sum());
        Assert.Equal(8, weights.BasisPoints.Count);
    }

    [Fact]
    public void FromTiers_TieBreaksLargestRemaindersByAscendingDimension()
    {
        // 8 dimensions, each raw tier = 1: ideal share is 10000/8 = 1250.0 exactly (no remainder to distribute).
        // Use a sum that does not divide evenly to force remainder-based tie-breaking: three dims tier=1, sum=3.
        var tiers = new Dictionary<ConstraintDimension, int>
        {
            [ConstraintDimension.DataSensitivity] = 1,
            [ConstraintDimension.LatencyTarget] = 1,
            [ConstraintDimension.CostPressure] = 1,
            [ConstraintDimension.DeterminismReproducibility] = 0,
            [ConstraintDimension.KnowledgeFreshness] = 0,
            [ConstraintDimension.ToolActionNeed] = 0,
            [ConstraintDimension.HumanReview] = 0,
            [ConstraintDimension.OperationalMaturity] = 0,
        };

        var weights = WeightSet.FromTiers(tiers);

        // 1*10000/3 = 3333 remainder 1 for each of the three dims; total floor = 9999, 1 bp remains.
        // All three remainders tie, so the ascending-dimension tie-break gives the extra bp to DataSensitivity.
        Assert.Equal(3334, weights[ConstraintDimension.DataSensitivity]);
        Assert.Equal(3333, weights[ConstraintDimension.LatencyTarget]);
        Assert.Equal(3333, weights[ConstraintDimension.CostPressure]);
        Assert.Equal(0, weights[ConstraintDimension.HumanReview]);
        Assert.Equal(10000, weights.BasisPoints.Values.Sum());
    }
}
