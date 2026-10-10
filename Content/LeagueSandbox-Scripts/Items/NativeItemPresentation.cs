using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSandbox.GameServer.GameObjects;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemHelpers
{
    internal static class NativeItemPresentation
    {
        private const string OwnersKey = "itemPresentationOwners";
        public static bool IsIndicator(Buff buff)
        {
            return buff.Variables.GetBool("itemPresentationOnly");
        }

        public static Buff Show(Buff controller, string name, bool continuous = false)
        {
            var shown = controller.TargetUnit.GetBuffWithName(name);
            if (shown != null && !shown.Elapsed() && IsIndicator(shown))
            {
                var owners = shown.Variables.Get<List<Buff>>(OwnersKey);
                owners.RemoveAll(b => b.Elapsed());
                if (owners.Count == 0 && shown.SourceUnit != controller.SourceUnit)
                {
                    RemoveBuff(shown);
                    shown = null;
                }
            }

            if (shown == null || shown.Elapsed() || !IsIndicator(shown))
            {
                if (shown != null && !shown.Elapsed())
                {
                    return null;
                }

                var variables = new BuffVariables();
                variables.Set("itemPresentationOnly", true);
                variables.Set(OwnersKey, new List<Buff> { controller });
                float duration = name == "Slow" ? controller.Duration / Math.Max(0.0001f, 1 - controller.TargetUnit.Stats.Tenacity.Total) : controller.Duration;
                shown = AddBuff(
                    name,
                    duration,
                    1,
                    null,
                    controller.TargetUnit,
                    controller.SourceUnit,
                    controller.IsBuffInfinite() || continuous,
                    buffVariables: variables
                );
            }
            else
            {
                var owners = shown.Variables.Get<List<Buff>>(OwnersKey);
                owners.RemoveAll(b => b.Elapsed());
                if (!owners.Contains(controller))
                {
                    owners.Add(controller);
                }

                Sync(shown);
            }

            return shown;
        }

        public static void Remove(Buff controller)
        {
            foreach (var shown in controller.TargetUnit.GetBuffs().ToArray())
            {
                if (!IsIndicator(shown))
                {
                    continue;
                }

                var owners = shown.Variables.Get<List<Buff>>(OwnersKey);
                owners.Remove(controller);
                owners.RemoveAll(b => b.Elapsed());
                if (owners.Count == 0)
                {
                    RemoveBuff(shown);
                }
                else
                {
                    Sync(shown);
                }
            }
        }

        public static void Sync(Buff shown, float diff = 0)
        {
            if (!IsIndicator(shown) || shown.Elapsed())
            {
                return;
            }

            var owners = shown.Variables.Get<List<Buff>>(OwnersKey);
            owners.RemoveAll(b => b.Elapsed() || (b.TargetUnit.IsDead
                && !b.Variables.GetBool("presentationPersistsThroughDeath")));
            if (owners.Count == 0)
            {
                RemoveBuff(shown);
                return;
            }

            int stacks = Math.Max(1, owners.Max(b => b.StackCount));
            if (shown.BuffAddType != GameServerCore.Enums.BuffAddType.STACKS_AND_OVERLAPS && shown.StackCount != stacks)
            {
                shown.SetStacks(stacks);
            }

            float amount = shown.Name == "WitsEndBuff" || shown.Name == "MaladyCounter" ? stacks * 5f : 0;
            if (amount > 0 && shown.ToolTipData.Values[0].Value != amount)
            {
                shown.SetToolTipVar(0, amount);
            }

            if (!shown.IsBuffInfinite())
            {
                var buffs = shown.TargetUnit.GetBuffs();
                int shownIndex = buffs.IndexOf(shown);
                float remaining = owners.Max(b => b.IsBuffInfinite() ? shown.Duration : Math.Max(0, b.Duration - b.TimeElapsed - (buffs.IndexOf(b) > shownIndex ? diff / 1000f : 0)));
                float extension = remaining - (shown.Duration - shown.TimeElapsed);
                if (extension > 0.001f)
                {
                    shown.Refresh(remaining);
                }
            }
        }
    }
}
