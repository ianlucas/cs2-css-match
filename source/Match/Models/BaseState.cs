/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace Match;

public class BaseState
{
    public virtual string Name { get; set; } = "default_state";
    protected bool _matchCancelled = false;
    private readonly List<Action> _commands = [];
    private readonly List<Action> _commandListeners = [];
    private readonly List<Action> _gameEvents = [];
    private readonly List<Action> _listeners = [];
    private readonly List<Action> _nativeHooks = [];

    public virtual void Load() { }

    public virtual void Unload()
    {
        Timers.ClearAll();
        foreach (var cleanup in _listeners)
            cleanup();
        foreach (var cleanup in _nativeHooks)
            cleanup();
        foreach (var cleanup in _commandListeners)
            cleanup();
        foreach (var cleanup in _commands)
            cleanup();
        foreach (var cleanup in _gameEvents)
            cleanup();
    }

    protected void RegisterCommand(
        List<string> commandNames,
        string description,
        CommandInfo.CommandCallback handler
    )
    {
        foreach (var name in commandNames)
        {
            Runtime.Plugin.AddCommand(name, description, handler);
            _commands.Add(() => Runtime.Plugin.RemoveCommand(name, handler));
        }
    }

    protected void AddCommandListener(
        string name,
        CommandInfo.CommandListenerCallback handler,
        HookMode mode = HookMode.Pre
    )
    {
        Runtime.Plugin.AddCommandListener(name, handler, mode);
        _commandListeners.Add(() => Runtime.Plugin.RemoveCommandListener(name, handler, mode));
    }

    protected void HookGameEvent<T>(
        BasePlugin.GameEventHandler<T> handler,
        HookMode mode = HookMode.Post
    )
        where T : GameEvent
    {
        Runtime.Plugin.RegisterEventHandler(handler, mode);
        _gameEvents.Add(() => Runtime.Plugin.DeregisterEventHandler(handler, mode));
    }

    protected void RegisterListener<T>(T handler)
        where T : Delegate
    {
        Runtime.Plugin.RegisterListener(handler);
        _listeners.Add(() => Runtime.Plugin.RemoveListener(handler));
    }

    protected void AddHook(
        BaseMemoryFunction fn,
        Func<DynamicHook, HookResult> handler,
        HookMode mode = HookMode.Pre
    )
    {
        fn.Hook(handler, mode);
        _nativeHooks.Add(() => fn.Unhook(handler, mode));
    }
}
