using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Xml.Serialization;
using Prism.Commands;
using Prism.Mvvm;

namespace DiscordOverlay
{
    [Serializable]
    public class Config : BindableBase
    {
        public static Config Current { get; private set; } = !WPFHelper.IsDesignMode ? null : new Config();

        private Config()
        {
            this.PropertyChanged += (_, e) =>
            {
                this.AutoSave();

                switch (e.PropertyName)
                {
                    case nameof(this.IsLimitSpeaking):
                    case nameof(this.IsSmallAvatars):
                    case nameof(this.FontSize):
                    case nameof(this.CurrentVoiceChannelPresetName):
                        this.UpdateVoiceWidgetUri();
                        break;
                }
            };

            this.VoiceChannelPresets.CollectionChanged += (_, __) => this.AutoSave();
            this.CreateViewSource();
        }

        public static readonly string FileName = Path.Combine(
            Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
            @"DiscordOverlay.config");

        public static void Load()
        {
            if (!File.Exists(FileName))
            {
                Current = new Config();
                Current.isLoaded = true;
                Current.UpdateVoiceWidgetUri();
                Current.Save();
                return;
            }

            try
            {
                using (var sr = new StreamReader(FileName, new UTF8Encoding(false)))
                {
                    if (sr.BaseStream.Length > 0)
                    {
                        var xs = new XmlSerializer(typeof(Config));
                        var data = xs.Deserialize(sr) as Config;
                        if (data != null)
                        {
                            Current = data;
                            Current.isLoaded = true;
                            Current.UpdateVoiceWidgetUri();
                        }
                    }
                }
            }
            catch (Exception)
            {
                Current = new Config();
                Current.isLoaded = true;
                Current.UpdateVoiceWidgetUri();
                Current.Save();
            }
        }

        public void Save()
        {
            lock (this)
            {
                var directoryName = Path.GetDirectoryName(FileName);

                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                var ns = new XmlSerializerNamespaces();
                ns.Add(string.Empty, string.Empty);

                var buffer = new StringBuilder();
                using (var sw = new StringWriter(buffer))
                {
                    var xs = new XmlSerializer(typeof(Config));
                    xs.Serialize(sw, this, ns);
                }

                buffer.Replace("utf-16", "utf-8");

                File.WriteAllText(
                    FileName,
                    buffer.ToString() + Environment.NewLine,
                    new UTF8Encoding(false));
            }
        }

        public async void AutoSave()
        {
            if (this.isLoaded)
            {
                try
                {
                    if (this.isAutoSaving)
                    {
                        return;
                    }

                    lock (this)
                    {
                        if (this.isAutoSaving)
                        {
                            return;
                        }

                        this.isAutoSaving = true;
                    }

                    await Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        this.Save();
                    });
                }
                finally
                {
                    this.isAutoSaving = false;
                }
            }
        }

        private bool isLoaded;
        private volatile bool isAutoSaving;

        private double left = 10;

        public double Left
        {
            get => this.left;
            set => this.SetProperty(ref this.left, Math.Round(value, 1));
        }

        private double top = 50;

        public double Top
        {
            get => this.top;
            set => this.SetProperty(ref this.top, Math.Round(value, 1));
        }

        private double width = 312;

        public double Width
        {
            get => this.width;
            set => this.SetProperty(ref this.width, Math.Round(value, 1));
        }

        private double height = 600;

        public double Height
        {
            get => this.height;
            set => this.SetProperty(ref this.height, Math.Round(value, 1));
        }

        private bool isHide;

        [XmlIgnore]
        public bool IsHide
        {
            get => this.isHide;
            set => this.SetProperty(ref this.isHide, value);
        }

        private bool isLayoutLocked;

        public bool IsLayoutLocked
        {
            get => this.isLayoutLocked;
            set => this.SetProperty(ref this.isLayoutLocked, value);
        }

        private bool isLimitSpeaking;

        public bool IsLimitSpeaking
        {
            get => this.isLimitSpeaking;
            set => this.SetProperty(ref this.isLimitSpeaking, value);
        }

        private bool isSmallAvatars;

        public bool IsSmallAvatars
        {
            get => this.isSmallAvatars;
            set => this.SetProperty(ref this.isSmallAvatars, value);
        }

        private int fontSize = 14;

        public int FontSize
        {
            get => this.fontSize;
            set => this.SetProperty(ref this.fontSize, value);
        }

        private CollectionViewSource viewSource;

        private void CreateViewSource()
        {
            var src = new CollectionViewSource()
            {
            };

            src.Source = this.voiceChannelPresets;
            src.IsLiveSortingRequested = true;

            src.SortDescriptions.AddRange(new[]
            {
                new SortDescription(nameof(VoiceChannelPreset.Order), ListSortDirection.Ascending)
            });

            this.viewSource = src;
        }

        [XmlIgnore]
        public ICollectionView VoiceChannelPresetsSorted => this.viewSource.View;

        private readonly ObservableCollection<VoiceChannelPreset> voiceChannelPresets = !WPFHelper.IsDesignMode ?
            new ObservableCollection<VoiceChannelPreset>() :
            new ObservableCollection<VoiceChannelPreset>()
            {
                new VoiceChannelPreset() { Order = 1, Name = "Hojoring - VC1", ServerID = "347997949109731328", ChannelID = "628219088623370253" }
            };

        [XmlArrayItem(ElementName = "Preset")]
        public ObservableCollection<VoiceChannelPreset> VoiceChannelPresets
        {
            get => this.voiceChannelPresets;
            set
            {
                this.voiceChannelPresets.Clear();
                this.voiceChannelPresets.AddRange(value);
            }
        }

        private string currentVoiceChannelPresetName;

        [XmlIgnore]
        public string CurrentVoiceChannelPresetName
        {
            get => this.currentVoiceChannelPresetName;
            set => this.SetProperty(ref this.currentVoiceChannelPresetName, value);
        }

        private string voiceWidgetBaseUri =
            "https://streamkit.discordapp.com/overlay/voice/[server_id]/[channel_id]?text_size=[font_size]&limit_speaking=[is_limit_speaking]&small_avatars=[is_small_avatars]";

        public string VoiceWidgetBaseUri
        {
            get => this.voiceWidgetBaseUri;
            set => this.SetProperty(ref this.voiceWidgetBaseUri, value);
        }

        private string backgroundText = "DISCORD Overlay";

        public string BackgroundText
        {
            get => this.backgroundText;
            set => this.SetProperty(ref this.backgroundText, value);
        }

        [XmlIgnore]
        public string VoiceWidgetUri { get; private set; } = string.Empty;

        private void UpdateVoiceWidgetUri()
        {
            var preset = this.voiceChannelPresets.FirstOrDefault(x =>
                x.Name == this.currentVoiceChannelPresetName);

            if (preset == null)
            {
                this.VoiceWidgetUri = string.Empty;
                this.RaisePropertyChanged(nameof(this.VoiceWidgetUri));
                return;
            }

            this.VoiceWidgetUri = this.voiceWidgetBaseUri
                .Replace("[server_id]", preset.ServerID)
                .Replace("[channel_id]", preset.ChannelID)
                .Replace("[font_size]", this.fontSize.ToString())
                .Replace("[is_limit_speaking]", this.isLimitSpeaking.ToString().ToLower())
                .Replace("[is_small_avatars]", this.isSmallAvatars.ToString().ToLower());

            this.RaisePropertyChanged(nameof(this.VoiceWidgetUri));
        }
    }

    [Serializable]
    public class VoiceChannelPreset : BindableBase
    {
        public VoiceChannelPreset()
        {
            this.PropertyChanged += (_, __) => Config.Current?.AutoSave();
        }

        private bool isCurrent;

        [XmlIgnore]
        public bool IsCurrent
        {
            get => this.isCurrent;
            set
            {
                if (this.SetProperty(ref this.isCurrent, value))
                {
                    if (value)
                    {
                        Config.Current.CurrentVoiceChannelPresetName = this.Name;

                        foreach (var preset in Config.Current.VoiceChannelPresets)
                        {
                            if (preset != this)
                            {
                                preset.IsCurrent = false;
                            }
                        }
                    }

                    if (!Config.Current.VoiceChannelPresets.Any(x => x.IsCurrent))
                    {
                        Config.Current.CurrentVoiceChannelPresetName = string.Empty;
                    }
                }
            }
        }

        private int order;

        [XmlAttribute(AttributeName = "order")]
        public int Order
        {
            get => this.order;
            set => this.SetProperty(ref this.order, value);
        }

        private string name;

        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get => this.name;
            set => this.SetProperty(ref this.name, value);
        }

        private string serverID;

        [XmlAttribute(AttributeName = "serverID")]
        public string ServerID
        {
            get => this.serverID;
            set => this.SetProperty(ref this.serverID, value);
        }

        private string channelID;

        [XmlAttribute(AttributeName = "channelID")]
        public string ChannelID
        {
            get => this.channelID;
            set => this.SetProperty(ref this.channelID, value);
        }

        private DelegateCommand _removeCommand;

        [XmlIgnore]
        public DelegateCommand RemoveCommand =>
            this._removeCommand ?? (this._removeCommand = new DelegateCommand(this.ExecuteRemoveCommand));

        private void ExecuteRemoveCommand()
        {
            if (this.isCurrent)
            {
                Config.Current.CurrentVoiceChannelPresetName = string.Empty;
            }

            Config.Current.VoiceChannelPresets.Remove(this);
        }
    }
}
