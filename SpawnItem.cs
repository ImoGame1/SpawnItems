using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using MEC;
using UnityEngine;
using PlayerRole = PlayerRoles.RoleTypeId;

namespace SpawnItem
{
    public sealed class Plugin : Plugin<Config>
    {
        public override string Name => "SpawnItem";
        public override string Description => "Daje itemy po spawnie na podstawie klasy i skonfigurowanych loadoutów";
        public override string Author => "Imo_Game";
        public override Version Version => new Version(1, 3, 0);
        public override Version RequiredApiVersion => new Version(1, 0, 0);

        private static readonly HashSet<PlayerRole> BlockedRoles = new()
        {
            PlayerRole.Overwatch,
            PlayerRole.Tutorial,
            PlayerRole.Filmmaker,
            PlayerRole.None,
            PlayerRole.Scp049,
            PlayerRole.Scp0492,
            PlayerRole.Scp079,
            PlayerRole.Scp096,
            PlayerRole.Scp106,
            PlayerRole.Scp173,
            PlayerRole.Scp939,
        };


        public override void Enable()
        {
            PlayerEvents.Spawned += OnPlayerSpawned;
        }

        public override void Disable()
        {
            PlayerEvents.Spawned -= OnPlayerSpawned;
        }

        private void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {
            if (ev.Player == null || !ev.Player.IsAlive)
                return;

            var role = ev.Player.Role;
            if (BlockedRoles.Contains(role))
                return;

            if (!Config.Loadouts.TryGetValue(role, out var loadouts) || loadouts.Count == 0)
                return;

            Timing.CallDelayed(0.5f, () => TryGiveLoadout(ev.Player, loadouts));
        }

        private void TryGiveLoadout(Player player, IReadOnlyList<LoadoutEntry> loadouts)
        {
            if (player == null || !player.IsAlive)
                return;

            var roll = UnityEngine.Random.Range(0, 100);
            var cumulative = 0;

            foreach (var entry in loadouts)
            {
                cumulative += entry.Chance;
                if (roll < cumulative)
                {
                    GiveLoadout(player, entry);
                    return;
                }
            }
        }

        private void GiveLoadout(Player player, LoadoutEntry entry)
        {
            var received = new List<string>();

            foreach (var itemKey in entry.Items)
            {
                if (string.IsNullOrWhiteSpace(itemKey))
                    continue;

                var displayName = Config.ItemNames.TryGetValue(itemKey, out var customName)
                    ? customName
                    : (Enum.TryParse<ItemType>(itemKey, true, out _) ? itemKey : $"Custom item {itemKey}");

                if (Enum.TryParse<ItemType>(itemKey, true, out var itemType))
                {
                    if (player.AddItem(itemType) != null)
                        received.Add(displayName);
                }
                else if (int.TryParse(itemKey, out var customId))
                {
                    if (UciBridge.TryGive(player, customId))
                        received.Add(displayName);
                }
            }

            if (received.Count > 0 && Config.Hint.Enabled)
                ShowCustomHint(player, received, Config.Hint.Duration);
        }


        private void ShowCustomHint(Player player, List<string> items, float duration)
        {
            var h = Config.Hint;
            var sb = new System.Text.StringBuilder();

            // uklad hintu
            sb.Append($"<align={h.Alignment.ToLowerInvariant()}>");
            sb.Append($"<margin-left={F(h.Position.OffsetX)}>");
            sb.Append($"<margin-right={F(h.Position.MarginRight)}>");
            sb.Append($"<voffset={F(h.Position.OffsetY)}>");
            sb.Append($"<line-height={F(h.LineHeight)}%>");
            if (Math.Abs(h.CharSpacing) > 0.001f)
                sb.Append($"<cspace={F(h.CharSpacing)}>");
            if (h.Background.Enabled)
                sb.Append($"<mark={h.Background.Color}>");

            // naglowek
            if (!string.IsNullOrWhiteSpace(h.Header))
                sb.Append(Style(h.Header, h.HeaderStyle)).Append('\n');

            // lista itemow
            for (var i = 0; i < items.Count; i++)
            {
                var line = h.ItemFormat
                    .Replace("{item}", items[i])
                    .Replace("{index}", (i + 1).ToString());
                sb.Append(Style(line, h.ItemStyle));
                if (i < items.Count - 1)
                    sb.Append('\n');
            }

            player.SendHint(sb.ToString(), duration);
        }

        private static string F(float v) =>
            v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string Style(string text, TextStyle st)
        {
            var open = new System.Text.StringBuilder();
            var close = new System.Text.StringBuilder();

            open.Append($"<color={st.Color}><size={st.Size}>");
            close.Insert(0, "</size></color>");

            if (st.Bold)      { open.Append("<b>"); close.Insert(0, "</b>"); }
            if (st.Italic)    { open.Append("<i>"); close.Insert(0, "</i>"); }
            if (st.Underline) { open.Append("<u>"); close.Insert(0, "</u>"); }
            if (st.Strikethrough) { open.Append("<s>"); close.Insert(0, "</s>"); }
            if (st.Uppercase) { open.Append("<uppercase>"); close.Insert(0, "</uppercase>"); }

            return open + text + close;
        }
    }

    // cfg klas

    public class Config
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;

        [Newtonsoft.Json.JsonProperty("loadouts")]
        public Dictionary<PlayerRole, List<LoadoutEntry>> Loadouts { get; set; } = new()
        {
            { PlayerRole.ClassD, new List<LoadoutEntry>() },
            { PlayerRole.Scientist, new List<LoadoutEntry>
                {
                    new LoadoutEntry { Chance = 20, Items = new List<string> { "101", "Flashlight" } },
                    new LoadoutEntry { Chance = 30, Items = new List<string> { "202", "Coin" } },
                }
            },
            { PlayerRole.FacilityGuard, new List<LoadoutEntry>() },
            { PlayerRole.NtfPrivate, new List<LoadoutEntry>() },
            { PlayerRole.NtfSergeant, new List<LoadoutEntry>() },
            { PlayerRole.NtfSpecialist, new List<LoadoutEntry>() },
            { PlayerRole.NtfCaptain, new List<LoadoutEntry>() },
            { PlayerRole.ChaosConscript, new List<LoadoutEntry>() },
            { PlayerRole.ChaosRifleman, new List<LoadoutEntry>() },
            { PlayerRole.ChaosMarauder, new List<LoadoutEntry>() },
            { PlayerRole.ChaosRepressor, new List<LoadoutEntry>() },
        };

        [Newtonsoft.Json.JsonProperty("item_names")]
        public Dictionary<string, string> ItemNames { get; set; } = new()
        {
            { "101", "Bouncy Nade" },
        };

        [Newtonsoft.Json.JsonProperty("hint")]
        public HintConfig Hint { get; set; } = new HintConfig();
    }

    public class HintConfig
    {
        [Newtonsoft.Json.JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        // widocznosc hinta w sekundach
        [Newtonsoft.Json.JsonProperty("duration")]
        public float Duration { get; set; } = 6f;

        // naglowek (puste zeby ukryc)
        [Newtonsoft.Json.JsonProperty("header")]
        public string Header { get; set; } = "Otrzymane przedmioty:";

        [Newtonsoft.Json.JsonProperty("header_style")]
        public TextStyle HeaderStyle { get; set; } = new TextStyle { Color = "#FFD700", Size = 30, Bold = true };

        // linie z itemami
        [Newtonsoft.Json.JsonProperty("item_format")]
        public string ItemFormat { get; set; } = "* {item}";

        [Newtonsoft.Json.JsonProperty("item_style")]
        public TextStyle ItemStyle { get; set; } = new TextStyle { Color = "#FFFFFF", Size = 26 };

        // left / center / right
        [Newtonsoft.Json.JsonProperty("alignment")]
        public string Alignment { get; set; } = "left";

        // odstep miedzy liniami domyslnie 100%
        [Newtonsoft.Json.JsonProperty("line_height")]
        public float LineHeight { get; set; } = 100f;

        // odstep miedzy literami (im mniejsze/negatywne tym ciasniejsze)
        [Newtonsoft.Json.JsonProperty("char_spacing")]
        public float CharSpacing { get; set; } = 0f;

        [Newtonsoft.Json.JsonProperty("position")]
        public PositionConfig Position { get; set; } = new PositionConfig();

        [Newtonsoft.Json.JsonProperty("background")]
        public HintBackgroundConfig Background { get; set; } = new HintBackgroundConfig();
    }

    public class TextStyle
    {
        // kolor tekstu jako HEX
        [Newtonsoft.Json.JsonProperty("color")]
        public string Color { get; set; } = "#FFFFFF";

        [Newtonsoft.Json.JsonProperty("size")]
        public int Size { get; set; } = 26;

        [Newtonsoft.Json.JsonProperty("bold")]
        public bool Bold { get; set; } = false;

        [Newtonsoft.Json.JsonProperty("italic")]
        public bool Italic { get; set; } = false;

        [Newtonsoft.Json.JsonProperty("underline")]
        public bool Underline { get; set; } = false;

        [Newtonsoft.Json.JsonProperty("strikethrough")]
        public bool Strikethrough { get; set; } = false;

        [Newtonsoft.Json.JsonProperty("uppercase")]
        public bool Uppercase { get; set; } = false;
    }

    public class PositionConfig
    {
        // odstep od krawedzi ekranu lewo
        [Newtonsoft.Json.JsonProperty("offset_x")]
        public float OffsetX { get; set; } = 40f;

        // odstep od krawedzi prawo
        [Newtonsoft.Json.JsonProperty("margin_right")]
        public float MarginRight { get; set; } = 0f;

        // przesuwanie pionowe/poziome
        // Wartość UJEMNA = niżej, DODATNIA = wyżej.
        [Newtonsoft.Json.JsonProperty("offset_y")]
        public float OffsetY { get; set; } = -300f;
    }

    public class HintBackgroundConfig
    {
        // czy literki maja miec tło
        [Newtonsoft.Json.JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        // "#RRGGBBAA" - AA to przezroczystosc (00 = niewidoczne, FF = pełne)
        [Newtonsoft.Json.JsonProperty("color")]
        public string Color { get; set; } = "#00000080";
    }

    public class LoadoutEntry
    {
        [Newtonsoft.Json.JsonProperty("chance")]
        public int Chance { get; set; } = 0;

        [Newtonsoft.Json.JsonProperty("items")]
        public List<string> Items { get; set; } = new();
    }

//tego nie tykac ponizej jak co bo zjebiesz
// to dalem dla beki ale serio nie tykaj lepiej nic, w sensie nie edytuj
    // ==================== HELPER EXTENSIONS ====================

    public static class ColorExtensions
    {
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }

    // ==================== UCI BRIDGE ====================

    public static class UciBridge
    {
        public static bool TryGive(Player player, int customItemId)
        {
            return false;
        }
    }
}