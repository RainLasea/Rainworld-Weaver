using BepInEx;
using SlugBase.Features;
using UnityEngine;
using Weaver.Mouse;
using Weaver.Silk;
using static SlugBase.Features.FeatureTypes;

namespace Weaver
{
    [BepInPlugin("abysslasea.weaver", "Weaver", "0.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "abysslasea.weaver";
        public const string SlugName = "weaver";
        public static Plugin Instance;
        public static PlayerFeature<bool> MouseAiming;
        public static PlayerFeature<bool> SilkFeatureEnabled;

        public void OnEnable()
        {
            Instance = this;

            MouseAiming = PlayerBool("weaver/mouse_aiming");
            SilkFeatureEnabled = PlayerBool("weaver/silk_enabled");

            WeaverSilkData.Initialize();
            SilkAimInput.Initialize();
            MouseAimSystem.Initialize();
            MouseRender.Initialize();

            On.Player.Update += Player_Update;
        }

        public void OnDisable()
        {
            SilkAimInput.Cleanup();
            WeaverSilkData.Cleanup();
            MouseAimSystem.Cleanup();
            MouseRender.Cleanup();
            On.Player.Update -= Player_Update;
            Instance = null;
        }

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            if (self.graphicsModule == null)
            {
                self.InitiateGraphicsModule();
            }

            if (MouseAiming.TryGet(self, out bool mouseEnabled) && mouseEnabled)
            {
                MouseAimSystem.SetMouseAimEnabled(true, self);
            }
        }
    }
}