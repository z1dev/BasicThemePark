using FlaxEngine;

namespace Game;

public static class AssetGlobals
{
    public static Model FlowingBarf;
    public static Material FlowingBarfMat;
    public static Model Vomit;
}


/// <summary>
/// GlobalAssets Script.
/// </summary>
public class GlobalAssets : Script
{
    public Model flowingBarf;
    public Material flowingBarfMat;
    public Model vomit;

    /// <inheritdoc/>
    public override void OnAwake()
    {
        AssetGlobals.FlowingBarf = flowingBarf;
        AssetGlobals.FlowingBarfMat = flowingBarfMat;
        AssetGlobals.Vomit = vomit;
    }
}
