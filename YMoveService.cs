using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Threading;

namespace YMoveStandalone;

internal sealed unsafe class YMoveService : IDisposable
{
    private const string NormalMoveSignature =
        "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B F9 41 8B D8";

    private const string CombatMoveSignature =
        "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B F9 41 8B F0";

    private delegate nint SendNormalMoveDelegate(nuint a1, nint data, uint a3);
    private delegate nint SendCombatMoveDelegate(nuint a1, nint data, uint a3);

    private readonly Configuration config;
    private readonly IFramework framework;
    private readonly ICondition condition;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private Hook<SendNormalMoveDelegate>? normalMoveHook;
    private Hook<SendCombatMoveDelegate>? combatMoveHook;
    private volatile bool canApplyNow;
    private int detourFailureReported;
    private DateTime nextSafetyRefresh = DateTime.MinValue;

    internal bool IsReady => normalMoveHook is not null && combatMoveHook is not null && LastError is null;
    internal bool IsEnabled => normalMoveHook?.IsEnabled == true && combatMoveHook?.IsEnabled == true;
    internal byte CurrentFloor { get; private set; }
    internal string SafetyState { get; private set; } = "初期化中";
    internal string? LastError { get; private set; }

    internal YMoveService(
        Configuration config,
        IFramework framework,
        IGameInteropProvider interop,
        ISigScanner sigScanner,
        ICondition condition,
        IClientState clientState,
        IPluginLog log)
    {
        this.config = config;
        this.framework = framework;
        this.condition = condition;
        this.clientState = clientState;
        this.log = log;

        try
        {
            var normalAddress = sigScanner.ScanText(NormalMoveSignature);
            var combatAddress = sigScanner.ScanText(CombatMoveSignature);

            normalMoveHook = interop.HookFromAddress<SendNormalMoveDelegate>(normalAddress, NormalMoveDetour);
            combatMoveHook = interop.HookFromAddress<SendCombatMoveDelegate>(combatAddress, CombatMoveDetour);
            SafetyState = "フック準備完了";
            log.Information("Movement hooks resolved: normal={Normal:X}, combat={Combat:X}", normalAddress, combatAddress);
        }
        catch (Exception exception)
        {
            LastError = $"移動関数を検出できませんでした: {exception.Message}";
            SafetyState = "利用不可";
            config.Enabled = false;
            log.Error(exception, "Failed to initialize movement hooks.");
        }

        framework.Update += OnFrameworkUpdate;
        RefreshSafetyState();
    }

    internal bool SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            normalMoveHook?.Disable();
            combatMoveHook?.Disable();
            config.Enabled = false;
            return true;
        }

        if (!IsReady)
            return false;

        try
        {
            normalMoveHook!.Enable();
            combatMoveHook!.Enable();
            config.Enabled = true;
            Interlocked.Exchange(ref detourFailureReported, 0);
            RefreshSafetyState();
            return true;
        }
        catch (Exception exception)
        {
            normalMoveHook?.Disable();
            combatMoveHook?.Disable();
            config.Enabled = false;
            LastError = $"フックを有効化できませんでした: {exception.Message}";
            log.Error(exception, "Failed to enable movement hooks.");
            return false;
        }
    }

    internal void RefreshSafetyState()
    {
        if (!config.AutoDeepDungeonSafety)
        {
            CurrentFloor = 0;
            canApplyNow = clientState.IsLoggedIn;
            SafetyState = canApplyNow ? "安全制御なし（全エリアで適用）" : "未ログイン";
            return;
        }

        if (!clientState.IsLoggedIn)
        {
            CurrentFloor = 0;
            canApplyNow = false;
            SafetyState = "未ログインのため停止";
            return;
        }

        if (!condition[ConditionFlag.InDeepDungeon])
        {
            CurrentFloor = 0;
            canApplyNow = false;
            SafetyState = "ディープダンジョン外のため停止";
            return;
        }

        var territoryInfo = TerritoryInfo.Instance();
        if (territoryInfo != null && territoryInfo->InSanctuary)
        {
            CurrentFloor = 0;
            canApplyNow = false;
            SafetyState = "安全地帯のため停止";
            return;
        }

        var eventFramework = EventFramework.Instance();
        var deepDungeon = eventFramework == null ? null : eventFramework->GetInstanceContentDeepDungeon();
        CurrentFloor = deepDungeon == null ? (byte)0 : deepDungeon->Floor;

        if (CurrentFloor == 0)
        {
            canApplyNow = false;
            SafetyState = "階層を取得できないため停止";
            return;
        }

        if (CurrentFloor % 10 == 0)
        {
            canApplyNow = false;
            SafetyState = $"B{CurrentFloor}: 10階区切りのため停止";
            return;
        }

        if ((clientState.TerritoryType == 1108 || clientState.TerritoryType == 1290) && CurrentFloor == 99)
        {
            canApplyNow = false;
            SafetyState = "B99終端のため停止";
            return;
        }

        canApplyNow = true;
        SafetyState = $"B{CurrentFloor}: 適用可能";
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        normalMoveHook?.Disable();
        combatMoveHook?.Disable();
        normalMoveHook?.Dispose();
        combatMoveHook?.Dispose();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var now = DateTime.UtcNow;
        if (now < nextSafetyRefresh)
            return;

        nextSafetyRefresh = now.AddMilliseconds(250);
        RefreshSafetyState();
    }

    private nint NormalMoveDetour(nuint a1, nint data, uint a3)
    {
        try
        {
            if (ShouldApply(data))
                ((float*)data)[3] += config.VerticalOffset;
        }
        catch (Exception exception)
        {
            HandleDetourFailure(exception);
        }

        return normalMoveHook!.Original(a1, data, a3);
    }

    private nint CombatMoveDetour(nuint a1, nint data, uint a3)
    {
        try
        {
            if (ShouldApply(data))
            {
                var values = (float*)data;
                values[4] += config.VerticalOffset;
                values[7] += config.VerticalOffset;
            }
        }
        catch (Exception exception)
        {
            HandleDetourFailure(exception);
        }

        return combatMoveHook!.Original(a1, data, a3);
    }

    private bool ShouldApply(nint data)
    {
        var offset = config.VerticalOffset;
        return data != 0
            && config.Enabled
            && canApplyNow
            && float.IsFinite(offset)
            && offset != 0f;
    }

    private void HandleDetourFailure(Exception exception)
    {
        canApplyNow = false;
        SafetyState = "実行時エラーのため適用停止";
        LastError = $"補正処理を停止しました: {exception.Message}";

        if (Interlocked.Exchange(ref detourFailureReported, 1) == 0)
            log.Error(exception, "Movement detour failed; coordinate modification was stopped.");
    }
}
