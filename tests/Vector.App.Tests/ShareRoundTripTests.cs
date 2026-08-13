using Vector.App.Services;
using Vector.Data.Sharing;
using Vector.Domain;
using Vector.Engine;

namespace Vector.App.Tests;

public class ShareRoundTripTests : VectorBunitContext
{
    /// <summary>
    /// 8. Share round-trip: StudioState.ToSharePayload + ShareCodec round-trip + HydrateFromShare on a
    /// fresh StudioState reproduces the same profile and outcome digest.
    /// </summary>
    [Fact]
    public void ShareRoundTrip_ReproducesProfileAndOutcomeDigestOnAFreshStudioState()
    {
        var engine = new DecisionEngine();
        var stateA = new StudioState(engine, Rules, Scenarios);
        stateA.LoadScenario("scn.supervised-research");
        stateA.SetWeightTier(ConstraintDimension.ToolActionNeed, 3);
        stateA.SetHard(ConstraintDimension.HumanReview, true);

        var payload = stateA.ToSharePayload();
        var encoded = ShareCodec.Encode(payload);
        var decoded = ShareCodec.Decode(encoded);
        Assert.True(decoded.Ok);

        var stateB = new StudioState(engine, Rules, Scenarios);
        stateB.HydrateFromShare(decoded.Payload!);

        Assert.Equal(stateA.Profile, stateB.Profile);
        Assert.Equal(stateA.Outcome.ConfigDigest, stateB.Outcome.ConfigDigest);
        Assert.Equal(stateA.SelectedScenarioId, stateB.SelectedScenarioId);
    }
}
