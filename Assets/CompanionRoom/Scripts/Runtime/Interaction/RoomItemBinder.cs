using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CompanionRoom
{
    /// <summary>
    /// Gives the furniture placed in the room its interaction. Every supplied prop (marked with
    /// <see cref="ImportedRoomAsset"/>) whose id is listed below gets that item component when the
    /// room starts — unless one was already added in the inspector, which then wins with its own
    /// settings. Moving or swapping the furniture needs no rebinding; add a line to bind another prop.
    /// </summary>
    public static class RoomItemBinder
    {
        public readonly struct Binding
        {
            public readonly string assetId;
            public readonly Type type;
            public readonly string name;
            public readonly float cameraFocus;

            public Binding(string assetId, Type type, string name, float cameraFocus)
            {
                this.assetId = assetId;
                this.type = type;
                this.name = name;
                this.cameraFocus = cameraFocus;
            }
        }

        public static readonly IReadOnlyList<Binding> Bindings = new[]
        {
            new Binding("Window", typeof(WindowItem), "窗戶（切換光線）", 0f),
            new Binding("Pendulum", typeof(PendulumItem), "靈擺（開始使用）", 0.25f),
            new Binding("TarotCards", typeof(TarotDeckItem), "塔羅牌（開始占卜）", 0.25f),
            new Binding("Computer", typeof(ComputerItem), "電腦（設定）", 0.45f),
            // The camera barely moves: the record book flies to the player instead.
            new Binding("Bookcase", typeof(RecordCabinetItem), "書櫃（占卜手帳）", 0.12f),
        };

        /// <summary>Adds the missing item components; returns how many were added.</summary>
        public static int BindPlacedProps()
        {
            int added = 0;
            foreach (var prop in Object.FindObjectsByType<ImportedRoomAsset>(FindObjectsSortMode.None))
            {
                if (prop.assetId == "TarotCards" && prop.GetComponent<TarotDeckVisual>() == null)
                    prop.gameObject.AddComponent<TarotDeckVisual>();
                var binding = Find(prop.assetId);
                if (binding == null || prop.GetComponent<RoomItem>() != null)
                    continue;
                var item = (RoomItem)prop.gameObject.AddComponent(binding.Value.type);
                item.Bind(binding.Value.name, binding.Value.cameraFocus);
                added++;
            }
            return added;
        }

        public static Binding? Find(string assetId)
        {
            foreach (var binding in Bindings)
                if (binding.assetId == assetId)
                    return binding;
            return null;
        }
    }
}
