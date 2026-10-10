using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace YMoveStandalone;

internal sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    internal ConfigWindow(Plugin plugin)
        : base("天遁地改##YMoveStandalone")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(470, 330),
            MaximumSize = new Vector2(760, 650)
        };
        RespectCloseHotkey = true;
    }

    public override void Draw()
    {
        var service = plugin.Service;
        var readyColor = service.IsReady
            ? new Vector4(0.35f, 0.9f, 0.45f, 1f)
            : new Vector4(1f, 0.35f, 0.3f, 1f);

        ImGui.TextColored(readyColor, service.IsReady ? "移動関数: 検出済み" : "移動関数: 検出失敗");
        ImGui.TextWrapped($"現在の制御状態: {service.SafetyState}");

        if (service.LastError is not null)
            ImGui.TextColored(new Vector4(1f, 0.35f, 0.3f, 1f), service.LastError);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(1f, 0.72f, 0.25f, 1f), "注意");
        ImGui.TextWrapped("移動送信データを書き換える機能です。サーバー補正、切断、ゲーム更新後の非互換が起こり得ます。");

        if (IsIChingLoaded())
        {
            ImGui.TextColored(new Vector4(1f, 0.5f, 0.25f, 1f),
                "I-Ching-GLを検出しました。I-Ching側の同じY移動機能とは同時に有効化しないでください。");
        }

        ImGui.Spacing();
        var enabled = service.IsEnabled;
        if (ImGui.Checkbox("機能を有効にする", ref enabled))
            plugin.SetEnabled(enabled);

        var offset = plugin.Config.VerticalOffset;
        ImGui.SetNextItemWidth(300);
        if (ImGui.SliderFloat("Y補正値", ref offset, -10f, 10f, "%.1f"))
        {
            plugin.Config.VerticalOffset = float.IsFinite(offset) ? offset : 0f;
            plugin.Save();
        }

        if (ImGui.Button("-1")) SetOffset(-1f);
        ImGui.SameLine();
        if (ImGui.Button("+1")) SetOffset(1f);
        ImGui.SameLine();
        if (ImGui.Button("0に戻す")) SetOffset(0f);
        ImGui.SameLine();
        if (ImGui.Button("-7")) SetOffset(-7f);

        var safety = plugin.Config.AutoDeepDungeonSafety;
        if (ImGui.Checkbox("ディープダンジョン安全制御", ref safety))
        {
            plugin.Config.AutoDeepDungeonSafety = safety;
            plugin.Service.RefreshSafetyState();
            plugin.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("DD外、安全地帯、10階ごとの区切り、B99終端、階層未取得時には補正しません。");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("コマンド: /ymove");
        ImGui.TextDisabled("初回導入時は無効、補正値は0です。");
    }

    private void SetOffset(float offset)
    {
        plugin.Config.VerticalOffset = offset;
        plugin.Save();
    }

    private static bool IsIChingLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
    {
        var name = assembly.GetName().Name;
        return name is "I-Ching-GL" or "SirenAuth";
    });
}
