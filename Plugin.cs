using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace YMoveStandalone;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("YMoveStandalone");
    private readonly ConfigWindow configWindow;

    internal Configuration Config { get; }
    internal YMoveService Service { get; }

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Service = new YMoveService(Config, Framework, Interop, SigScanner, Condition, ClientState, Log);
        configWindow = new ConfigWindow(this);

        windowSystem.AddWindow(configWindow);
        CommandManager.AddHandler("/ymove", new CommandInfo(OnCommand)
        {
            HelpMessage = "天遁地改の設定を開閉します。"
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleWindow;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleWindow;

        if (Config.Enabled && !Service.SetEnabled(true))
        {
            Config.Enabled = false;
            Save();
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleWindow;
        CommandManager.RemoveHandler("/ymove");
        windowSystem.RemoveAllWindows();
        Service.Dispose();
    }

    internal void Save() => PluginInterface.SavePluginConfig(Config);

    internal bool SetEnabled(bool enabled)
    {
        var changed = Service.SetEnabled(enabled);
        Config.Enabled = Service.IsEnabled;
        Save();
        return changed;
    }

    private void OnCommand(string _, string __) => ToggleWindow();
    private void ToggleWindow() => configWindow.Toggle();
}
