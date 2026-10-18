using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Logging;
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

        // Text-to-Speech Engine. Null when the platform speech engine is unavailable.
        private readonly SpeechSynthesizer? _speechSynthesizer;
        private readonly List<string> _availableMaleVoices = new();
        private readonly List<string> _availableFemaleVoices = new();

        // Voices that failed to load, so the failure is reported once instead of every line.
        private readonly HashSet<string> _brokenVoices = new();

        private readonly Action _openConfigUi;
        
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
            _configuration = LoadConfiguration(pluginInterface);
            _configuration.Initialize(_pluginInterface);

            // Initialize TTS Engine. A missing or broken speech engine must not take the
            // whole plugin down, so the failure is reported and the plugin loads muted.
            try
            {
                _speechSynthesizer = new SpeechSynthesizer();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to create the speech synthesizer. Dialogue will not be voiced.");
                _chatGui.PrintError("[NaturalDialogue] Could not start Windows text-to-speech. Dialogue will not be voiced.");
            }

            InitializeVoices();

            // Set up UI
            _configWindow = new ConfigWindow(this, _configuration, _availableMaleVoices, _availableFemaleVoices);
            _windowSystem.AddWindow(_configWindow);
            _openConfigUi = () => _configWindow.IsOpen = true;
            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi += _openConfigUi;

            // Subscribe to the chat event
            _chatGui.OnChatMessage += OnChatMessage;
        }

        private static Configuration LoadConfiguration(DalamudPluginInterface pluginInterface)
        {
            // A corrupt or incompatible config file throws while deserializing; falling back
            // to defaults is preferable to failing to load, but the cause must be visible.
            try
            {
                return pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to read the saved configuration. Falling back to defaults.");
                return new Configuration();
            }
        }

        private void InitializeVoices()
        {
            if (_speechSynthesizer == null) return;

            List<InstalledVoice> installedVoices;
            try
            {
                installedVoices = _speechSynthesizer.GetInstalledVoices().ToList();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to enumerate installed voices.");
                _chatGui.PrintError("[NaturalDialogue] Could not read the installed Windows voices. Dialogue will not be voiced.");
                return;
            }

            var britishCulture = new CultureInfo("en-GB");

            foreach (var voice in installedVoices)
            {
                VoiceInfo info;
                try
                {
                    info = voice.VoiceInfo;
                }
                catch (Exception ex)
                {
                    // A single misregistered voice must not hide the remaining ones.
                    PluginLog.Warning(ex, "Skipping a voice whose metadata could not be read.");
                    continue;
                }

                if (!info.Culture.Equals(britishCulture)) continue;

                if (info.Gender == VoiceGender.Male)
                {
                    _availableMaleVoices.Add(info.Name);
                }
                else if (info.Gender == VoiceGender.Female)
                {
                    _availableFemaleVoices.Add(info.Name);
                }
            }

            if (!_availableMaleVoices.Any() && !_availableFemaleVoices.Any())
            {
                PluginLog.Warning("No en-GB voices are installed; dialogue will not be voiced.");
                _chatGui.PrintError("[NaturalDialogue] No British English (en-GB) voices are installed. Install them via Windows Language Settings.");
                return;
            }

            // Set default voice if none configured
            if (string.IsNullOrEmpty(_configuration.SelectedMaleVoice) && _availableMaleVoices.Any())
                _configuration.SelectedMaleVoice = _availableMaleVoices[0];
            if (string.IsNullOrEmpty(_configuration.SelectedFemaleVoice) && _availableFemaleVoices.Any())
                _configuration.SelectedFemaleVoice = _availableFemaleVoices[0];
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            // Dalamud invokes this for every chat line; an escaping exception would break the
            // chat pipeline for every other handler, so failures are contained and logged here.
            try
            {
                HandleChatMessage(type, sender, message);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Unhandled error while voicing a dialogue line.");
            }
        }

        private void HandleChatMessage(XivChatType type, SeString sender, SeString message)
        {
            if (!_configuration.IsEnabled || _speechSynthesizer == null) return;

            // We only care about NPC dialogue types.
            // XivChatType.Dialogue, XivChatType.Tell, etc. may be relevant.
            // For this example, we focus on the primary dialogue type.
            if (type != XivChatType.Dialogue) return;

            string speakerName = sender.TextValue.Trim();
            string dialogueText = message.TextValue.Trim();

            if (string.IsNullOrWhiteSpace(speakerName) || string.IsNullOrWhiteSpace(dialogueText)) return;

            // Stop any currently speaking line to prevent overlap
            _speechSynthesizer.SpeakAsyncCancelAll();

            // Determine the voice to use
            VoiceGender gender = VoiceGender.Neutral;
            if (_characterGenderMap.TryGetValue(speakerName, out var knownGender))
            {
                gender = knownGender;
            }

            string? voiceToUse = null;
            if (gender == VoiceGender.Male && !string.IsNullOrEmpty(_configuration.SelectedMaleVoice))
            {
                voiceToUse = _configuration.SelectedMaleVoice;
            }
            else if (gender == VoiceGender.Female && !string.IsNullOrEmpty(_configuration.SelectedFemaleVoice))
            {
                voiceToUse = _configuration.SelectedFemaleVoice;
            }

            if (voiceToUse == null)
            {
                PluginLog.Verbose($"No voice configured for speaker '{speakerName}' (gender {gender}); skipping line.");
                return;
            }

            if (_brokenVoices.Contains(voiceToUse)) return;

            try
            {
                _speechSynthesizer.SelectVoice(voiceToUse);
                _speechSynthesizer.Volume = Math.Clamp(_configuration.Volume, 0, 100);
                _speechSynthesizer.Rate = Math.Clamp(_configuration.Rate, -10, 10);
                _speechSynthesizer.SpeakAsync(dialogueText);
            }
            catch (Exception ex)
            {
                // The selected voice was uninstalled or is unusable. Report it once and stop
                // retrying it, rather than failing silently on every dialogue line.
                _brokenVoices.Add(voiceToUse);
                PluginLog.Error(ex, $"Failed to speak dialogue with voice '{voiceToUse}'.");
                _chatGui.PrintError($"[NaturalDialogue] The voice '{voiceToUse}' could not be used. Pick another one in the plugin settings.");
            }
        }

        public void Dispose()
        {
            // Every step runs even if an earlier one throws, so nothing is left subscribed or leaked.
            SafeDispose("unsubscribe from chat messages", () => _chatGui.OnChatMessage -= OnChatMessage);
            SafeDispose("unsubscribe the window system", () => _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw);
            SafeDispose("unsubscribe the config UI handler", () => _pluginInterface.UiBuilder.OpenConfigUi -= _openConfigUi);
            SafeDispose("remove the plugin windows", () => _windowSystem.RemoveAllWindows());
            SafeDispose("dispose the config window", () => _configWindow.Dispose());
            SafeDispose("stop pending speech", () => _speechSynthesizer?.SpeakAsyncCancelAll());
            SafeDispose("dispose the speech synthesizer", () => _speechSynthesizer?.Dispose());
        }

        private static void SafeDispose(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to {what} during disposal.");
            }
        }
    }

    // You would create separate files for Configuration and the UI Window for better organization.
    // For this example, I will keep them here for simplicity.

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
            _pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        }

        public void Save()
        {
            if (_pluginInterface == null)
                throw new InvalidOperationException($"{nameof(Configuration)}.{nameof(Initialize)} must be called before saving.");

            _pluginInterface.SavePluginConfig(this);
        }
    }

    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration _configuration;
        private readonly List<string> _maleVoices;
        private readonly List<string> _femaleVoices;
        private string? _saveError;

        public ConfigWindow(Plugin plugin, Configuration configuration, List<string> maleVoices, List<string> femaleVoices) : base(
            "NaturalDialogue Configuration", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)
        {
            _configuration = configuration;
            _maleVoices = maleVoices;
            _femaleVoices = femaleVoices;
        }

        // Draw runs inside Dalamud's render loop, where an exception is fatal to the UI,
        // so a failed save is surfaced in the window instead of being thrown or ignored.
        private void TrySave()
        {
            try
            {
                _configuration.Save();
                _saveError = null;
            }
            catch (Exception ex)
            {
                _saveError = ex.Message;
                PluginLog.Error(ex, "Failed to save the NaturalDialogue configuration.");
            }
        }

        public override void Draw()
        {
            if (ImGui.Checkbox("Enable NaturalDialogue", ref _configuration.IsEnabled))
            {
                TrySave();
            }

            ImGui.Spacing();
            ImGui.Text("Voice Settings");
            ImGui.Separator();

            // Male Voice Selector
            int selectedMaleIndex = _maleVoices.IndexOf(_configuration.SelectedMaleVoice);
            if (ImGui.Combo("Male Voice (en-GB)", ref selectedMaleIndex, _maleVoices.ToArray(), _maleVoices.Count)
                && selectedMaleIndex >= 0 && selectedMaleIndex < _maleVoices.Count)
            {
                _configuration.SelectedMaleVoice = _maleVoices[selectedMaleIndex];
                TrySave();
            }

            // Female Voice Selector
            int selectedFemaleIndex = _femaleVoices.IndexOf(_configuration.SelectedFemaleVoice);
            if (ImGui.Combo("Female Voice (en-GB)", ref selectedFemaleIndex, _femaleVoices.ToArray(), _femaleVoices.Count)
                && selectedFemaleIndex >= 0 && selectedFemaleIndex < _femaleVoices.Count)
            {
                _configuration.SelectedFemaleVoice = _femaleVoices[selectedFemaleIndex];
                TrySave();
            }
            
            if (!_maleVoices.Any() && !_femaleVoices.Any())
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No British English (en-GB) voices found!");
                ImGui.Text("Please install them via Windows Language Settings.");
            }

            ImGui.Spacing();
            ImGui.Text("Audio Settings");
            ImGui.Separator();

            if (ImGui.SliderInt("Volume", ref _configuration.Volume, 0, 100))
            {
                TrySave();
            }
            if (ImGui.SliderInt("Rate", ref _configuration.Rate, -10, 10))
            {
                TrySave();
            }

            if (_saveError != null)
            {
                ImGui.Spacing();
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), $"Could not save settings: {_saveError}");
            }
        }

        public void Dispose() { }
    }
}
