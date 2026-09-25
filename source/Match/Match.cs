/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;

namespace Match;

public partial class Match : BasePlugin
{
    public override string ModuleAuthor => "Ian Lucas";
    public override string ModuleDescription => "A CounterStrikeSharp plugin for managing matches";
    public override string ModuleName => "Match";
    public override string ModuleVersion => "1.0.0";

    public bool PendingInternalPush = true;
    public bool DidKickBots = false;

    public override void Load(bool hotReload)
    {
        Runtime.Initialize(this);
        ConVars.Initialize(this);
        Cstv.Initialize();
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
        Natives.CCSPlayerController_ChangeTeam.Hook(OnChangeTeamPre, HookMode.Pre);
        Natives.CCSBotManager_MaintainBotQuota.Hook(OnMaintainBotQuotaPre, HookMode.Pre);
        ConVars.IsBots.ValueChanged += OnIsBotsChanged;
        ConVars.IsMatchmaking.ValueChanged += OnIsMatchmakingChanged;
        Directory.CreateDirectory(PathHelper.GetConfigPath());
    }

    public void OnIsBotsChanged(object? _, bool value)
    {
        if (value)
            DidKickBots = false;
    }

    public void OnIsMatchmakingChanged(object? _, bool value)
    {
        Rules.EnforceMatchmakingRestrictions();
    }

    public override void Unload(bool hotReload)
    {
        Natives.CCSPlayerController_ChangeTeam.Unhook(OnChangeTeamPre, HookMode.Pre);
        Natives.CCSBotManager_MaintainBotQuota.Unhook(OnMaintainBotQuotaPre, HookMode.Pre);
        Rules.State.Unload();
        Cstv.Shutdown();
    }
}
