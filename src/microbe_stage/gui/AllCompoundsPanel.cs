using Godot;

/// <summary>
///   The compounds panel and the agents panel part of the microbe HUD
/// </summary>
public partial class AllCompoundsPanel : PanelContainer
{
#pragma warning disable CA2213
    [Export]
    private GridContainer compoundContainer = null!;
#pragma warning restore CA2213

    /// <inheritdoc cref="BarPanelBase.AddPrimaryBar"/>
    public void AddBar(CompoundProgressBar bar)
    {
        compoundContainer.AddChild(bar);
    }
}
