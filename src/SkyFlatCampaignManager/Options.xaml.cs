using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.SkyFlatCampaignManager;

[Export(typeof(ResourceDictionary))]
public partial class Options : ResourceDictionary
{
    public Options()
    {
        InitializeComponent();
    }
}
