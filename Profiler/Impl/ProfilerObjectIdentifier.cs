﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using NLog;
using Profiler.Api;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Utils;

// ReSharper disable ConvertIfStatementToReturnStatement

namespace Profiler.Impl
{
    internal static class ProfilerObjectIdentifier
    {
        /// <summary>
        /// Identifies the given object in a human readable name when profiling
        /// </summary>
        /// <param name="o">object to ID</param>
        /// <returns>ID</returns>
        public static string Identify(object o)
        {
            if (o is MyCubeGrid grid)
            {
                string owners = string.Join(", ", grid.BigOwners.Concat(grid.SmallOwners).Distinct().Select(
                    x => Sync.Players?.TryGetIdentity(x)?.DisplayName ?? $"Identity[{x}]"));
                if (string.IsNullOrWhiteSpace(owners))
                    owners = "unknown";
                if (ProfilerData.AnonymousProfilingDumps)
                    return $"Grid {o.GetHashCode():X}";
                return $"{grid.DisplayName ?? ($"{grid.GridSizeEnum} {grid.EntityId}")} owned by [{owners}]";
            }
            if (o is MyDefinitionBase def)
            {
                string typeIdSimple = def.Id.TypeId.ToString().Substring("MyObjectBuilder_".Length);
                string subtype = def.Id.SubtypeName?.Replace(typeIdSimple, "");
                return WithModName(string.IsNullOrWhiteSpace(subtype) ? typeIdSimple : $"{typeIdSimple}::{subtype}",
                    def);
            }
            if (o is MyCharacter character)
            {
                var id = character.GetIdentity();
                if (ProfilerData.AnonymousProfilingDumps)
                    return $"Character {o.GetHashCode():X}";
                return id != null
                    ? $"Character {character.EntityId}, Player {Identify(id)}"
                    : $"Character {character.EntityId}";
            }
            if (o is string str)
            {
                return !string.IsNullOrWhiteSpace(str) ? str : "unknown string";
            }
            if (o is ProfilerFixedEntry fx)
            {
                string res = fx.ToString();
                return !string.IsNullOrWhiteSpace(res) ? res : "unknown fixed";
            }
            if (o is Type type)
            {
                return WithModName(type.Name, type);
            }
            if (o is MyIdentity identity)
            {
                if (ProfilerData.AnonymousProfilingDumps)
                    return $"Identity {o.GetHashCode():X}";
                return
                    $"{identity.DisplayName ?? "unknown identity"} ID={identity.IdentityId} SteamID={MySession.Static?.Players?.TryGetSteamId(identity.IdentityId) ?? 0}";
            }
            if (o is MyCubeBlock block)
            {
                if (ProfilerData.AnonymousProfilingDumps)
                    return $"Block {o.GetHashCode():X}";
                var ownership = MySession.Static?.Players?.TryGetIdentity(block.OwnerId) ??
                                MySession.Static?.Players?.TryGetIdentity(block.BuiltBy);
                return
                    $"{block.GetType().Name} at {block.Min} owned by {Identify(ownership)} on {block.CubeGrid.DisplayName ?? ($"{block.CubeGrid.GridSizeEnum} {block.CubeGrid.EntityId}")}";
            }
            if (o is MyVoxelBase vox)
            {
                if (ProfilerData.AnonymousProfilingDumps)
                    return $"{o.GetType().Name} {o.GetHashCode():X}";
                return $"{o.GetType().Name} {vox.StorageName}";
            }
            if (o is Assembly asm)
            {
                return WithModName(asm.GetName().Name, asm);
            }
            return WithModName(o?.GetType().Name, o) ?? "unknown";
        }

        private static readonly ConcurrentDictionary<Assembly, MyModContext> _modByAssembly =
            new ConcurrentDictionary<Assembly, MyModContext>();

        private static string WithModName(string baseInfo, object o)
        {
            if (!ProfilerData.DisplayModNames)
                return baseInfo;


            MyModContext ctx = null;
            if (o is MyCubeBlock block)
                ctx = block.BlockDefinition?.Context;
            else if (o is MyDefinitionBase def)
                ctx = def.Context;
            if (ctx == null)
            {
                var asm = (o as Assembly) ?? (o as Type)?.Assembly ?? o?.GetType().Assembly;
                if (asm != null)
                    ctx = _modByAssembly.GetOrAdd(asm, GetModByAssembly);
            }

            if (ctx == MyModContext.BaseGame || ctx == MyModContext.UnknownContext || ctx == null || ctx.IsBaseGame)
                return baseInfo;
            var tag = !string.IsNullOrWhiteSpace(ctx.ModName) ? ctx.ModName : ctx.ModId;
            return $"{baseInfo} (Mod: {tag})";
        }

        private static MyModContext GetModByAssembly(Assembly asmToLookup)
        {
            if (MyScriptManager.Static != null)
            {
                if (asmToLookup != null && string.IsNullOrWhiteSpace(asmToLookup.Location)
                ) // real assemblies have a location.
                {
                    var asmName = asmToLookup.GetName().Name;
                    foreach (var kv in MyScriptManager.Static.ScriptsPerMod)
                    {
                        if (kv.Value.Any(x => x.String.EndsWith(asmName)))
                        {
                            return kv.Key;
                        }
                    }
                }
            }
            return null;
        }
    }
}