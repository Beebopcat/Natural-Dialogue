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
using Dalamud.Interface.Windowing;
using NaturalDialogue.Core;

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

        // Text-to-Speech Engine
        private readonly SpeechSynthesizer _speechSynthesizer;
        private readonly VoiceCatalog _voiceCatalog;
        private readonly DialoguePlanner _dialoguePlanner = new();

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ChatGui chatGui)
        {
            _pluginInterface = pluginInterface;
            _chatGui = chatGui;

            // Load Configuration
            _configuration = _pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            _configuration.Initialize(_pluginInterface);

            // Initialize TTS Engine
            _speechSynthesizer = new SpeechSynthesizer();
            _voiceCatalog = VoiceCatalog.Build(DescribeInstalledVoices());
            ApplyDefaultVoices();

            // Set up UI
            _configWindow = new ConfigWindow(_configuration, _voiceCatalog);
            _windowSystem.AddWindow(_configWindow);
            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            // Subscribe to the chat event
            _chatGui.OnChatMessage += OnChatMessage;
        }

        private IEnumerable<VoiceDescriptor> DescribeInstalledVoices() =>
            _speechSynthesizer.GetInstalledVoices()
                .Select(voice => voice.VoiceInfo)
                .Select(info => new VoiceDescriptor(info.Name, info.Culture.Name, ToSpeakerGender(info.Gender)));

        private static SpeakerGender ToSpeakerGender(VoiceGender gender) => gender switch
        {
            VoiceGender.Male => SpeakerGender.Male,
            VoiceGender.Female => SpeakerGender.Female,
            _ => SpeakerGender.Unknown,
        };

        private void ApplyDefaultVoices()
        {
            if (string.IsNullOrEmpty(_configuration.SelectedMaleVoice))
                _configuration.SelectedMaleVoice = _voiceCatalog.DefaultVoiceFor(SpeakerGender.Male) ?? "";
            if (string.IsNullOrEmpty(_configuration.SelectedFemaleVoice))
                _configuration.SelectedFemaleVoice = _voiceCatalog.DefaultVoiceFor(SpeakerGender.Female) ?? "";
        }

        private void OpenConfigUi() => _configWindow.IsOpen = true;

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            var request = _dialoguePlanner.Plan(
                _configuration,
                type == XivChatType.Dialogue,
                sender.TextValue,
                message.TextValue);

            if (request == null) return;

            // Stop any currently speaking line to prevent overlap
            _speechSynthesizer.SpeakAsyncCancelAll();

            try
            {
                _speechSynthesizer.SelectVoice(request.VoiceName);
                _speechSynthesizer.Volume = request.Volume;
                _speechSynthesizer.Rate = request.Rate;
                _speechSynthesizer.SpeakAsync(request.Text);
            }
            catch
            {
                // Handle case where voice is not found or fails
            }
        }

        public void Dispose()
        {
            _chatGui.OnChatMessage -= OnChatMessage;
            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            _windowSystem.RemoveAllWindows();
            _speechSynthesizer.Dispose();
        }
    }

    // You would create separate files for Configuration and the UI Window for better organization.
    // For this example, I will keep them here for simplicity.

    public class Configuration : IPluginConfiguration, ISpeechSettings
    {
        public int Version { get; set; } = 0;

        // Public fields so that ImGui widgets can bind to them by reference.
        public bool IsEnabled = true;
        public string SelectedMaleVoice = "";
        public string SelectedFemaleVoice = "";
        public int Volume = SpeechSettings.MaxVolume;
        public int Rate = 0;

        [NonSerialized] private DalamudPluginInterface? _pluginInterface;

        bool ISpeechSettings.IsEnabled => IsEnabled;

        string ISpeechSettings.SelectedMaleVoice => SelectedMaleVoice;

        string ISpeechSettings.SelectedFemaleVoice => SelectedFemaleVoice;

        int ISpeechSettings.Volume => SpeechSettings.ClampVolume(Volume);

        int ISpeechSettings.Rate => SpeechSettings.ClampRate(Rate);

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
        }

        public void Save()
        {
            _pluginInterface!.SavePluginConfig(this);
        }
    }

    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration _configuration;
        private readonly string[] _maleVoices;
        private readonly string[] _femaleVoices;
        private readonly bool _hasVoices;

        public ConfigWindow(Configuration configuration, VoiceCatalog voiceCatalog) : base(
            "NaturalDialogue Configuration", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)
        {
            _configuration = configuration;
            _maleVoices = voiceCatalog.MaleVoices.ToArray();
            _femaleVoices = voiceCatalog.FemaleVoices.ToArray();
            _hasVoices = !voiceCatalog.IsEmpty;
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
            int selectedMaleIndex = Array.IndexOf(_maleVoices, _configuration.SelectedMaleVoice);
            if (ImGui.Combo("Male Voice (en-GB)", ref selectedMaleIndex, _maleVoices, _maleVoices.Length))
            {
                _configuration.SelectedMaleVoice = _maleVoices[selectedMaleIndex];
                _configuration.Save();
            }

            // Female Voice Selector
            int selectedFemaleIndex = Array.IndexOf(_femaleVoices, _configuration.SelectedFemaleVoice);
            if (ImGui.Combo("Female Voice (en-GB)", ref selectedFemaleIndex, _femaleVoices, _femaleVoices.Length))
            {
                _configuration.SelectedFemaleVoice = _femaleVoices[selectedFemaleIndex];
                _configuration.Save();
            }

            if (!_hasVoices)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No British English (en-GB) voices found!");
                ImGui.Text("Please install them via Windows Language Settings.");
            }

            ImGui.Spacing();
            ImGui.Text("Audio Settings");
            ImGui.Separator();

            if (ImGui.SliderInt("Volume", ref _configuration.Volume, SpeechSettings.MinVolume, SpeechSettings.MaxVolume))
            {
                _configuration.Save();
            }
            if (ImGui.SliderInt("Rate", ref _configuration.Rate, SpeechSettings.MinRate, SpeechSettings.MaxRate))
            {
                _configuration.Save();
            }
        }

        public void Dispose() { }
    }
}
