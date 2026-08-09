using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Speech.Synthesis;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using System.Globalization;
using Dalamud.Interface.Windowing;

namespace NaturalDialogue
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "NaturalDialogue";

        private readonly DalamudPluginInterface _pluginInterface;
        private readonly ChatGui _chatGui;
        
        // Configuration and Window System
        private readonly Configuration _configuration;
        private readonly WindowSystem _windowSystem = new("NaturalDialogue");
        private readonly ConfigWindow _configWindow;

        // Maximum number of characters passed to the synthesizer for a single line.
        private const int MaxSpokenCharacters = 1000;

        // Text-to-Speech Engine
        private readonly SpeechSynthesizer _speechSynthesizer;
        private List<InstalledVoice> _installedVoices;
        private List<string> _availableMaleVoices = new();
        private List<string> _availableFemaleVoices = new();
        
        // Character to Gender Mapping
        private readonly Dictionary<string, VoiceGender> _characterGenderMap = new()
        {
            // A small, expandable list of known characters
            { "Y'shtola", VoiceGender.Female },
            { "Thancred", VoiceGender.Male },
            { "Urianger", VoiceGender.Male },
            { "Alphinaud", VoiceGender.Male },
            { "Alisaie", VoiceGender.Female },
            { "G'raha Tia", VoiceGender.Male },
            { "Krile", VoiceGender.Female },
            { "Tataru", VoiceGender.Female },
            { "Cid", VoiceGender.Male },
            { "Lyse", VoiceGender.Female },
            { "Hien", VoiceGender.Male },
            { "Minfilia", VoiceGender.Female }
        };

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ChatGui chatGui)
        {
            _pluginInterface = pluginInterface;
            _chatGui = chatGui;

            // Load Configuration
            _configuration = _pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            _configuration.Initialize(_pluginInterface);
            _configuration.Sanitize();

            // Initialize TTS Engine
            _speechSynthesizer = new SpeechSynthesizer();
            InitializeVoices();

            // Set up UI
            _configWindow = new ConfigWindow(this, _configuration, _availableMaleVoices, _availableFemaleVoices);
            _windowSystem.AddWindow(_configWindow);
            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            // Subscribe to the chat event
            _chatGui.OnChatMessage += OnChatMessage;
        }

        private void InitializeVoices()
        {
            _installedVoices = _speechSynthesizer.GetInstalledVoices().ToList();
            var britishCulture = new CultureInfo("en-GB");

            foreach (var voice in _installedVoices)
            {
                if (voice.VoiceInfo.Culture.Equals(britishCulture))
                {
                    if (voice.VoiceInfo.Gender == VoiceGender.Male)
                    {
                        _availableMaleVoices.Add(voice.VoiceInfo.Name);
                    }
                    else if (voice.VoiceInfo.Gender == VoiceGender.Female)
                    {
                        _availableFemaleVoices.Add(voice.VoiceInfo.Name);
                    }
                }
            }

            // Discard persisted voice names that are not actually installed before falling back to a default.
            if (!_availableMaleVoices.Contains(_configuration.SelectedMaleVoice))
                _configuration.SelectedMaleVoice = _availableMaleVoices.FirstOrDefault() ?? "";
            if (!_availableFemaleVoices.Contains(_configuration.SelectedFemaleVoice))
                _configuration.SelectedFemaleVoice = _availableFemaleVoices.FirstOrDefault() ?? "";
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (!_configuration.IsEnabled) return;

            // We only care about NPC dialogue types.
            // XivChatType.Dialogue, XivChatType.Tell, etc. may be relevant.
            // For this example, we focus on the primary dialogue type.
            if (type != XivChatType.Dialogue) return;

            string speakerName = sender.TextValue.Trim();
            string dialogueText = message.TextValue.Trim();

            if (string.IsNullOrWhiteSpace(speakerName) || string.IsNullOrWhiteSpace(dialogueText)) return;

            if (dialogueText.Length > MaxSpokenCharacters)
                dialogueText = dialogueText.Substring(0, MaxSpokenCharacters);

            // Stop any currently speaking line to prevent overlap
            _speechSynthesizer.SpeakAsyncCancelAll();

            // Determine the voice to use
            VoiceGender gender = VoiceGender.Neutral;
            if (_characterGenderMap.TryGetValue(speakerName, out var knownGender))
            {
                gender = knownGender;
            }

            string voiceToUse = null;
            if (gender == VoiceGender.Male && !string.IsNullOrEmpty(_configuration.SelectedMaleVoice))
            {
                voiceToUse = _configuration.SelectedMaleVoice;
            }
            else if (gender == VoiceGender.Female && !string.IsNullOrEmpty(_configuration.SelectedFemaleVoice))
            {
                voiceToUse = _configuration.SelectedFemaleVoice;
            }

            // If a valid voice is found, speak the text
            if (voiceToUse != null)
            {
                try
                {
                    _speechSynthesizer.SelectVoice(voiceToUse);
                    _speechSynthesizer.Volume = Math.Clamp(_configuration.Volume, Configuration.MinVolume, Configuration.MaxVolume);
                    _speechSynthesizer.Rate = Math.Clamp(_configuration.Rate, Configuration.MinRate, Configuration.MaxRate);
                    _speechSynthesizer.SpeakAsync(dialogueText);
                }
                catch
                {
                    // Handle case where voice is not found or fails
                }
            }
        }

        private void OpenConfigUi() => _configWindow.IsOpen = true;

        public void Dispose()
        {
            _chatGui.OnChatMessage -= OnChatMessage;
            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            _speechSynthesizer.Dispose();
        }
    }

    // You would create separate files for Configuration and the UI Window for better organization.
    // For this example, I will keep them here for simplicity.

    public class Configuration : IPluginConfiguration
    {
        public const int MinVolume = 0;
        public const int MaxVolume = 100;
        public const int MinRate = -10;
        public const int MaxRate = 10;

        public int Version { get; set; } = 0;
        public bool IsEnabled { get; set; } = true;
        public string SelectedMaleVoice { get; set; } = "";
        public string SelectedFemaleVoice { get; set; } = "";
        public int Volume { get; set; } = 100; // 0-100
        public int Rate { get; set; } = 0;   // -10 to 10
        [NonSerialized] private DalamudPluginInterface? _pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
        }

        /// <summary>
        /// Brings values loaded from the on-disk config file back into their supported ranges.
        /// </summary>
        public void Sanitize()
        {
            Volume = Math.Clamp(Volume, MinVolume, MaxVolume);
            Rate = Math.Clamp(Rate, MinRate, MaxRate);
            SelectedMaleVoice ??= "";
            SelectedFemaleVoice ??= "";
        }

        public void Save()
        {
            _pluginInterface!.SavePluginConfig(this);
        }
    }

    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration _configuration;
        private readonly List<string> _maleVoices;
        private readonly List<string> _femaleVoices;

        public ConfigWindow(Plugin plugin, Configuration configuration, List<string> maleVoices, List<string> femaleVoices) : base(
            "NaturalDialogue Configuration", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)
        {
            _configuration = configuration;
            _maleVoices = maleVoices;
            _femaleVoices = femaleVoices;
        }

        public override void Draw()
        {
            if (ImGui.Checkbox("Enable NaturalDialogue", ref _configuration.IsEnabled))
            {
                _configuration.Save();
            }

            ImGui.Spacing();
            ImGui.Text("Voice Settings");
            ImGui.Separator();

            // Male Voice Selector
            int selectedMaleIndex = _maleVoices.IndexOf(_configuration.SelectedMaleVoice);
            if (ImGui.Combo("Male Voice (en-GB)", ref selectedMaleIndex, _maleVoices.ToArray(), _maleVoices.Count))
            {
                _configuration.SelectedMaleVoice = _maleVoices[selectedMaleIndex];
                _configuration.Save();
            }

            // Female Voice Selector
            int selectedFemaleIndex = _femaleVoices.IndexOf(_configuration.SelectedFemaleVoice);
            if (ImGui.Combo("Female Voice (en-GB)", ref selectedFemaleIndex, _femaleVoices.ToArray(), _femaleVoices.Count))
            {
                _configuration.SelectedFemaleVoice = _femaleVoices[selectedFemaleIndex];
                _configuration.Save();
            }
            
            if (!_maleVoices.Any() && !_femaleVoices.Any())
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No British English (en-GB) voices found!");
                ImGui.Text("Please install them via Windows Language Settings.");
            }

            ImGui.Spacing();
            ImGui.Text("Audio Settings");
            ImGui.Separator();

            if (ImGui.SliderInt("Volume", ref _configuration.Volume, Configuration.MinVolume, Configuration.MaxVolume))
            {
                _configuration.Save();
            }
            if (ImGui.SliderInt("Rate", ref _configuration.Rate, Configuration.MinRate, Configuration.MaxRate))
            {
                _configuration.Save();
            }
        }

        public void Dispose() { }
    }
}
