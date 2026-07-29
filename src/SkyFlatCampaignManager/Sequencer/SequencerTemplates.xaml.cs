using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer;

[Export(typeof(ResourceDictionary))]
public partial class SequencerTemplates : ResourceDictionary
{
    public SequencerTemplates()
    {
        InitializeComponent();
    }
}
