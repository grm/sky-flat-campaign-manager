using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Containers;

public enum SkyFlatEventType
{
    CampaignRequired,
    CampaignNotRequired,
    BeforeWait,
    AfterWait,
    BeforeFilter,
    AfterFilter,
    CampaignCompleted,
    SessionIncomplete,
    Error
}

[ExportMetadata("Name", "SkyFlatEventContainer")]
[ExportMetadata("Description", "Blocking custom-event container owned by a Sky Flat Campaign Container.")]
[ExportMetadata("Icon", "Pen_NoFill_SVG")]
[Export(typeof(ISequenceContainer))]
[JsonObject(MemberSerialization.OptIn)]
public sealed class SkyFlatEventContainer : SequenceContainer, ISequenceContainer
{
    [JsonProperty]
    public SkyFlatEventType EventType { get; set; }

    /// <summary>
    /// Optional user-defined text resolved by the owning campaign container immediately before the
    /// event runs. The resolved text becomes this container's Name, so Ground Station can send it
    /// using its $INSTRUCTION_SET$ token without SFCM depending on Ground Station.
    /// </summary>
    [JsonProperty]
    public string MessageTemplate { get; set; } = string.Empty;

    [ImportingConstructor]
    public SkyFlatEventContainer() : base(new SkyFlatEventExecutionStrategy()) { }

    public SkyFlatEventContainer(SkyFlatEventType eventType, ISequenceContainer parent)
        : base(new SkyFlatEventExecutionStrategy())
    {
        EventType = eventType;
        Name = FriendlyName(eventType);
        AttachNewParent(parent);
    }

    public void ResetParent(ISequenceContainer parent) => AttachNewParent(parent);

    public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
    {
        if (Items.Count == 0) return;
        await base.Execute(progress, token).ConfigureAwait(false);
    }

    public override object Clone()
    {
        var clone = new SkyFlatEventContainer(EventType, Parent!)
        {
            MessageTemplate = MessageTemplate,
            Items = new ObservableCollection<ISequenceItem>(Items.Select(i => (ISequenceItem)i.Clone()))
        };
        foreach (var item in clone.Items) item.AttachNewParent(clone);
        return clone;
    }

    public static string FriendlyName(SkyFlatEventType type) => type switch
    {
        SkyFlatEventType.CampaignRequired => "Campaign Required",
        SkyFlatEventType.CampaignNotRequired => "Campaign Not Required / Skip",
        SkyFlatEventType.BeforeWait => "Before Wait",
        SkyFlatEventType.AfterWait => "After Wait",
        SkyFlatEventType.BeforeFilter => "Before Filter",
        SkyFlatEventType.AfterFilter => "After Filter Complete",
        SkyFlatEventType.CampaignCompleted => "Campaign Completed",
        SkyFlatEventType.SessionIncomplete => "Session Incomplete",
        SkyFlatEventType.Error => "Error",
        _ => type.ToString()
    };
}

internal sealed class SkyFlatEventExecutionStrategy : IExecutionStrategy
{
    private readonly SequentialStrategy sequential = new();
    public object Clone() => sequential.Clone();
    public Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        => sequential.Execute(context, progress, token);
}
