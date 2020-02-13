using System.Windows;
using Prism.Commands;

namespace DiscordOverlay
{
    /// <summary>
    /// OptionsWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            this.InitializeComponent();
        }

        public Config Config => Config.Current;

        private DelegateCommand _addVoiceChannelCommand;

        public DelegateCommand AddVoiceChannelCommand =>
            this._addVoiceChannelCommand ?? (this._addVoiceChannelCommand = new DelegateCommand(this.ExecuteAddVoiceChannelCommand));

        private void ExecuteAddVoiceChannelCommand()
        {
            this.Config.VoiceChannelPresets.Add(new VoiceChannelPreset()
            {
                Order = this.Config.VoiceChannelPresets.Count + 1,
                Name = "New Channel",
                ServerID = string.Empty,
                ChannelID = string.Empty
            });
        }
    }
}
