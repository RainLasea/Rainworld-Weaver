using System.Collections.Generic;
using UnityEngine;

namespace Weaver.Silk
{
    public static class SilkAimInput
    {
        private const KeyCode TONGUE_KEY = KeyCode.F;
        private const KeyCode QUICK_RELEASE_KEY = KeyCode.Space;
        private static readonly HashSet<int> silkRequestPlayers = new();
        private static readonly Dictionary<int, bool> keyDownLastFrame = new();
        private static readonly Dictionary<int, bool> spaceDownLastFrame = new();
        private static readonly Dictionary<int, bool> verticalInputLastFrame = new();

        public static bool IsShooting(Player player)
        {
            int playerNum = player.playerState?.playerNumber ?? -1;
            return playerNum >= 0 && silkRequestPlayers.Contains(playerNum);
        }

        public static bool IsReleasing(Player player) => false;

        public static void Initialize() => On.Player.Update += Player_Update_Input;

        public static void Cleanup()
        {
            On.Player.Update -= Player_Update_Input;
            silkRequestPlayers.Clear();
            keyDownLastFrame.Clear();
            spaceDownLastFrame.Clear();
            verticalInputLastFrame.Clear();
        }

        private static Vector2 GetMouseAimDirection(Player player)
        {
            var cam = Weaver.Mouse.MouseAimSystem.GetCurrentCamera();
            Vector2 aimVector;

            if (cam != null)
            {
                Vector2 mouseWorldPos = new Vector2(Futile.mousePosition.x + cam.pos.x, Futile.mousePosition.y + cam.pos.y);
                Vector2 headPos = player.bodyChunks[0].pos;
                aimVector = mouseWorldPos - headPos;
            }
            else
            {
                if (player.bodyChunks[0].vel.magnitude > 0.5f)
                    aimVector = player.bodyChunks[0].vel;
                else if (player.input[0].x != 0 || player.input[0].y != 0)
                    aimVector = new Vector2(player.input[0].x, player.input[0].y);
                else
                    aimVector = Vector2.right * player.flipDirection;
            }

            if (aimVector.magnitude < 0.1f)
                aimVector = Vector2.right * player.flipDirection;

            return aimVector.normalized;
        }

        private static Vector2 PerpendicularVector(Vector2 v) => new Vector2(v.y, -v.x);

        private static void MovePlayerVertically(Player player, SilkPhysics silk, float direction)
        {
            Vector2 toAnchor = (silk.pos - player.bodyChunks[0].pos).normalized;
            float climbForce = direction * 0.8f;

            for (int i = 0; i < player.bodyChunks.Length; i++)
                player.bodyChunks[i].vel += toAnchor * climbForce;
        }

        private static void Player_Update_Input(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);
            if (self.room == null) return;
            if (Plugin.SilkFeatureEnabled != null && Plugin.SilkFeatureEnabled.TryGet(self, out bool enabled) && !enabled) return;

            int playerNum = self.playerState?.playerNumber ?? -1;
            if (playerNum < 0) return;

            SilkPhysics silk = WeaverSilkData.Get(self);

            bool keyDown = Input.GetKey(TONGUE_KEY);
            bool wasKeyDown = keyDownLastFrame.ContainsKey(playerNum) && keyDownLastFrame[playerNum];
            bool spaceDown = Input.GetKey(QUICK_RELEASE_KEY);
            bool wasSpaceDown = spaceDownLastFrame.ContainsKey(playerNum) && spaceDownLastFrame[playerNum];
            bool wasVerticalInput = verticalInputLastFrame.ContainsKey(playerNum) && verticalInputLastFrame[playerNum];
            bool currentVerticalInput = self.input[0].y != 0;

            keyDownLastFrame[playerNum] = keyDown;
            spaceDownLastFrame[playerNum] = spaceDown;
            verticalInputLastFrame[playerNum] = currentVerticalInput;

            bool keyPressed = keyDown && !wasKeyDown;
            bool spacePressed = spaceDown && !wasSpaceDown;

            if (wasVerticalInput && !currentVerticalInput && silk.Attached)
                silk.idealRopeLength = silk.requestedRopeLength;

            if (spacePressed && silk.Attached)
            {
                silk.Release();
                return;
            }

            if (keyPressed)
            {
                if (silk.mode == SilkMode.Retracted)
                    silk.Shoot(GetMouseAimDirection(self));
                else if (silk.Attached)
                    silk.Release();
            }

            if (silk.Attached)
            {
                bool attachedToTerrain = silk.mode == SilkMode.AttachedToTerrain;

                if (self.input[0].y != 0 && attachedToTerrain)
                {
                    if (self.input[0].y > 0)
                    {
                        MovePlayerVertically(self, silk, 1f);
                        silk.idealRopeLength = Mathf.Max(silk.idealRopeLength - 4f, 50f);
                    }
                    else if (self.input[0].y < 0)
                    {
                        MovePlayerVertically(self, silk, -1f);
                        silk.idealRopeLength = Mathf.Min(silk.idealRopeLength + 4f, 800f);
                    }
                }

                if (self.input[0].x != 0)
                {
                    Vector2 toAnchor = (silk.pos - self.bodyChunks[0].pos).normalized;
                    Vector2 perpendicular = PerpendicularVector(toAnchor);
                    float swingForce = self.input[0].x * 1.2f;

                    for (int i = 0; i < self.bodyChunks.Length; i++)
                    {
                        self.bodyChunks[i].vel += perpendicular * swingForce;
                        if (Mathf.Abs(toAnchor.x) > 0.3f)
                            self.bodyChunks[i].vel.y -= 0.3f;
                    }
                }
            }
        }
    }
}