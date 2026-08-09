# NaturalDialogue

A Dalamud plugin that voices FFXIV NPC dialogue using installed Windows TTS voices
(including the British English Natural Voices).

## Layout

| Path | Description |
| --- | --- |
| `Plugin.cs` | Dalamud entry point, configuration and ImGui window. Requires Dalamud and Windows-only `System.Speech`. |
| `src/NaturalDialogue.Core` | Platform independent logic: speaker/gender lookup, voice catalog, settings clamping and dialogue planning. |
| `tests/NaturalDialogue.Core.Tests` | xunit tests for the core library. |

`Plugin.cs` keeps only the glue that talks to Dalamud and `System.Speech`; every decision
(should this line be spoken, which voice, at what volume and rate) lives in
`NaturalDialogue.Core` so it can be unit tested on any platform.

## Running the tests

```bash
dotnet test NaturalDialogue.sln
```

With coverage:

```bash
dotnet test NaturalDialogue.sln --collect:"XPlat Code Coverage"
```

The Cobertura report is written to `tests/NaturalDialogue.Core.Tests/TestResults/<guid>/coverage.cobertura.xml`.
