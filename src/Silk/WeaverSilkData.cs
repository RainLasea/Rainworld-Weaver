using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Weaver.Silk
{
    public static class WeaverSilkData
    {
        private static readonly ConditionalWeakTable<Player, SilkPhysics> CwTable = new();
        private static readonly ConditionalWeakTable<Player, SilkGraphics> CwGraphicsTable = new();
        private static readonly Dictionary<Player, RoomCamera.SpriteLeaser> PlayerSpriteLeasers = new();

        public static SilkPhysics Get(Player player) => CwTable.GetValue(player, p => new SilkPhysics(p));
        public static SilkGraphics GetGraphics(Player player) => CwGraphicsTable.GetValue(player, p => new SilkGraphics(p));

        public static void Initialize()
        {
            On.Player.ctor += Player_ctor;
            On.Player.Update += Player_Update;
            On.Player.Destroy += Player_Destroy;
            On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
        }

        public static void Cleanup()
        {
            On.Player.ctor -= Player_ctor;
            On.Player.Update -= Player_Update;
            On.Player.Destroy -= Player_Destroy;
            On.PlayerGraphics.InitiateSprites -= PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.DrawSprites -= PlayerGraphics_DrawSprites;
            On.PlayerGraphics.AddToContainer -= PlayerGraphics_AddToContainer;
            PlayerSpriteLeasers.Clear();
        }

        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            Get(self);
            GetGraphics(self);
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);
            if (CwTable.TryGetValue(self, out SilkPhysics silk))
                silk.Update();
        }

        private static void Player_Destroy(On.Player.orig_Destroy orig, Player self)
        {
            if (CwGraphicsTable.TryGetValue(self, out SilkGraphics graphics))
            {
                graphics.RemoveSprites();
                CwGraphicsTable.Remove(self);
            }

            if (CwTable.TryGetValue(self, out SilkPhysics silk))
                CwTable.Remove(self);

            PlayerSpriteLeasers.Remove(self);
            orig(self);
        }

        private static void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);
            Player player = self.owner as Player;
            if (player == null) return;
            if (Plugin.SilkFeatureEnabled != null && Plugin.SilkFeatureEnabled.TryGet(player, out bool enabled) && !enabled) return;

            if (CwGraphicsTable.TryGetValue(player, out SilkGraphics silkGraphics))
            {
                PlayerSpriteLeasers[player] = sLeaser;
                silkGraphics.InitiateSprites(sLeaser, rCam);
            }
        }

        private static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            Player player = self.owner as Player;
            if (player == null) return;
            if (Plugin.SilkFeatureEnabled != null && Plugin.SilkFeatureEnabled.TryGet(player, out bool enabled) && !enabled) return;

            if (CwGraphicsTable.TryGetValue(player, out SilkGraphics silkGraphics))
                silkGraphics.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        }

        private static void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            orig(self, sLeaser, rCam, newContatiner);
            Player player = self.owner as Player;
            if (player == null) return;
            if (Plugin.SilkFeatureEnabled != null && Plugin.SilkFeatureEnabled.TryGet(player, out bool enabled) && !enabled) return;

            if (CwGraphicsTable.TryGetValue(player, out SilkGraphics silkGraphics))
                silkGraphics.AddToContainer(sLeaser, rCam, newContatiner);
        }
    }
}