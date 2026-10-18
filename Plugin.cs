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

        // Text-to-Speech Engine
        private readonly SpeechSynthesizer _speechSynthesizer;
        private readonly VoiceCatalog _voiceCatalog;

        // Character to Gender Mapping
        private static readonly Dictionary<string, VoiceGender> CharacterGenderMap = new()
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

            // Initialize TTS Engine
            _speechSynthesizer = new SpeechSynthesizer();
            _voiceCatalog = VoiceCatalog.FromInstalledVoices(_speechSynthesizer, new CultureInfo("en-GB"));
            ApplyDefaultVoices();

            // Set up UI
            _configWindow = new ConfigWindow(_configuration, _voiceCatalog);
            _windowSystem.AddWindow(_configWindow);
            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi += OpenConfigWindow;

            // Subscribe to the chat event
            _chatGui.OnChatMessage += OnChatMessage;
        }

        private void OpenConfigWindow() => _configWindow.IsOpen = true;

        private void ApplyDefaultVoices()
        {
            foreach (var gender in VoiceCatalog.SupportedGenders)
            {
                if (!string.IsNullOrEmpty(_configuration.GetSelectedVoice(gender))) continue;

                var voices = _voiceCatalog.GetVoices(gender);
                if (voices.Count > 0) _configuration.SetSelectedVoice(gender, voices[0]);
            }
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

            // Stop any currently speaking line to prevent overlap
            _speechSynthesizer.SpeakAsyncCancelAll();

            var gender = CharacterGenderMap.TryGetValue(speakerName, out var knownGender)
                ? knownGender
                : VoiceGender.Neutral;

            var voiceToUse = _configuration.GetSelectedVoice(gender);
            if (string.IsNullOrEmpty(voiceToUse)) return;

            try
            {
                _speechSynthesizer.SelectVoice(voiceToUse);
                _speechSynthesizer.Volume = _configuration.Volume;
                _speechSynthesizer.Rate = _configuration.Rate;
                _speechSynthesizer.SpeakAsync(dialogueText);
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
            _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigWindow;
            _windowSystem.RemoveAllWindows();
            _configWindow.Dispose();
            _speechSynthesizer.Dispose();
        }
    }

    // You would create separate files for Configuration and the UI Window for better organization.
    // For this example, I will keep them here for simplicity.

    /// <summary>Installed synthesizer voices grouped by gender for a single culture.</summary>
    public class VoiceCatalog
    {
        public static readonly IReadOnlyList<VoiceGender> SupportedGenders =
            new[] { VoiceGender.Male, VoiceGender.Female };

        private static readonly IReadOnlyList<string> NoVoices = Array.Empty<string>();

        private readonly Dictionary<VoiceGender, List<string>> _voicesByGender;

        private VoiceCatalog(Dictionary<VoiceGender, List<string>> voicesByGender)
        {
            _voicesByGender = voicesByGender;
        }

        public static VoiceCatalog FromInstalledVoices(SpeechSynthesizer synthesizer, CultureInfo culture)
        {
            var voicesByGender = SupportedGenders.ToDictionary(gender => gender, _ => new List<string>());

            foreach (var voice in synthesizer.GetInstalledVoices())
            {
                var info = voice.VoiceInfo;
                if (!info.Culture.Equals(culture)) continue;
                if (voicesByGender.TryGetValue(info.Gender, out var voices)) voices.Add(info.Name);
            }

            return new VoiceCatalog(voicesByGender);
        }

        public IReadOnlyList<string> GetVoices(VoiceGender gender) =>
            _voicesByGender.TryGetValue(gender, out var voices) ? voices : NoVoices;

        public bool IsEmpty => SupportedGenders.All(gender => GetVoices(gender).Count == 0);
    }

    public class Configuration : IPluginConfiguration
    {
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

        public string GetSelectedVoice(VoiceGender gender) => gender switch
        {
            VoiceGender.Male => SelectedMaleVoice,
            VoiceGender.Female => SelectedFemaleVoice,
            _ => ""
        };

        public void SetSelectedVoice(VoiceGender gender, string voiceName)
        {
            switch (gender)
            {
                case VoiceGender.Male:
                    SelectedMaleVoice = voiceName;
                    break;
                case VoiceGender.Female:
                    SelectedFemaleVoice = voiceName;
                    break;
            }
        }

        public void Save()
        {
            _pluginInterface!.SavePluginConfig(this);
        }
    }

    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration _configuration;
        private readonly VoiceCatalog _voiceCatalog;

        public ConfigWindow(Configuration configuration, VoiceCatalog voiceCatalog) : base(
            "NaturalDialogue Configuration", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)
        {
            _configuration = configuration;
            _voiceCatalog = voiceCatalog;
        }

        public override void Draw()
        {
            if (ImGui.Checkbox("Enable NaturalDialogue", ref _configuration.IsEnabled))
            {
                _configuration.Save();
            }

            DrawSection("Voice Settings");

            foreach (var gender in VoiceCatalog.SupportedGenders)
            {
                DrawVoiceSelector(gender);
            }

            if (_voiceCatalog.IsEmpty)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No British English (en-GB) voices found!");
                ImGui.Text("Please install them via Windows Language Settings.");
            }

            DrawSection("Audio Settings");

            DrawSlider("Volume", ref _configuration.Volume, 0, 100);
            DrawSlider("Rate", ref _configuration.Rate, -10, 10);
        }

        private static void DrawSection(string title)
        {
            ImGui.Spacing();
            ImGui.Text(title);
            ImGui.Separator();
        }

        private void DrawVoiceSelector(VoiceGender gender)
        {
            var voices = _voiceCatalog.GetVoices(gender).ToArray();
            var selectedIndex = Array.IndexOf(voices, _configuration.GetSelectedVoice(gender));

            if (ImGui.Combo($"{gender} Voice (en-GB)", ref selectedIndex, voices, voices.Length))
            {
                _configuration.SetSelectedVoice(gender, voices[selectedIndex]);
                _configuration.Save();
            }
        }

        private void DrawSlider(string label, ref int value, int min, int max)
        {
            if (ImGui.SliderInt(label, ref value, min, max))
            {
                _configuration.Save();
            }
        }

        public void Dispose() { }
    }
}
