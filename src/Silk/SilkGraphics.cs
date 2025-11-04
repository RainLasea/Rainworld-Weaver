using RWCustom;
using UnityEngine;

namespace Weaver.Silk
{
    public class SilkGraphics
    {
        public Player player;
        public SilkPhysics silk;

        private TriangleMesh lineMesh;
        private FSprite tipSprite;
        private FSprite anchorSprite;
        private FSprite glowSprite;
        private FSprite tensionIndicator;
        private FSprite pullIndicator;
        private bool spritesInitiated;
        private RoomCamera currentCamera;

        private bool wasVisible;
        private SilkMode lastDrawnMode;
        private bool wasPulling;

        private float currentTension;
        private float displayedTension;

        private const int ROPE_SEGMENTS = 12;
        private Vector2[] ropePoints;

        public SilkGraphics(Player player)
        {
            this.player = player;
            this.silk = WeaverSilkData.Get(player);
            this.wasVisible = false;
            this.lastDrawnMode = SilkMode.Retracted;
            this.wasPulling = false;
            this.ropePoints = new Vector2[ROPE_SEGMENTS];
            this.spritesInitiated = false;
            this.currentTension = 0f;
            this.displayedTension = 0f;
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            if (spritesInitiated) return;

            currentCamera = rCam;

            TriangleMesh.Triangle[] tris = new TriangleMesh.Triangle[(ROPE_SEGMENTS - 1) * 2];
            for (int i = 0; i < ROPE_SEGMENTS - 1; i++)
            {
                int vertIndex = i * 4;
                tris[i * 2] = new TriangleMesh.Triangle(vertIndex, vertIndex + 1, vertIndex + 2);
                tris[i * 2 + 1] = new TriangleMesh.Triangle(vertIndex + 1, vertIndex + 2, vertIndex + 3);
            }

            lineMesh = new TriangleMesh("Futile_White", tris, false, false);
            lineMesh.color = Color.white;
            lineMesh.isVisible = false;

            tipSprite = new FSprite("Circle20", true);
            tipSprite.color = Color.white;
            tipSprite.scale = 0.5f;
            tipSprite.isVisible = false;

            anchorSprite = new FSprite("Circle20", true);
            anchorSprite.color = Color.white;
            anchorSprite.scale = 0.4f;
            anchorSprite.isVisible = false;

            glowSprite = new FSprite("Futile_White", true);
            glowSprite.shader = rCam.game.rainWorld.Shaders["FlatLight"];
            glowSprite.scale = 0.6f;
            glowSprite.alpha = 0.5f;
            glowSprite.isVisible = false;

            tensionIndicator = new FSprite("pixel", true);
            tensionIndicator.scaleX = 30f;
            tensionIndicator.scaleY = 3f;
            tensionIndicator.color = Color.yellow;
            tensionIndicator.alpha = 0f;
            tensionIndicator.isVisible = false;

            pullIndicator = new FSprite("Futile_White", true);
            pullIndicator.shader = rCam.game.rainWorld.Shaders["FlatLight"];
            pullIndicator.scale = 1.2f;
            pullIndicator.color = new Color(0.3f, 1f, 0.3f);
            pullIndicator.alpha = 0f;
            pullIndicator.isVisible = false;

            FContainer midground = rCam.ReturnFContainer("Midground");
            FContainer hud = rCam.ReturnFContainer("HUD");

            midground.AddChild(lineMesh);
            midground.AddChild(tipSprite);
            midground.AddChild(anchorSprite);
            midground.AddChild(glowSprite);
            midground.AddChild(pullIndicator);
            hud.AddChild(tensionIndicator);

            spritesInitiated = true;
        }

        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (!spritesInitiated) return;

            if (player == null || player.slatedForDeletetion || silk == null)
            {
                HideAllSprites();
                return;
            }

            Vector2 headPos = Vector2.Lerp(player.bodyChunks[0].lastPos, player.bodyChunks[0].pos, timeStacker);
            Vector2 silkTipPos = Vector2.Lerp(silk.lastPos, silk.pos, timeStacker);

            float distance = Vector2.Distance(headPos, silkTipPos);
            UpdateTension(distance);

            bool shouldBeVisible = silk.mode != SilkMode.Retracted &&
                                  silk.mode != SilkMode.Retracting &&
                                  distance >= 3f;

            wasVisible = shouldBeVisible;
            lastDrawnMode = silk.mode;
            wasPulling = silk.pullingObject;

            UpdateAnchor(headPos, camPos);

            if (shouldBeVisible)
            {
                UpdateSilkLine(headPos, silkTipPos, camPos, distance);
            }
            else
            {
                lineMesh.isVisible = false;
                tipSprite.isVisible = false;
            }

            UpdateTensionIndicator(headPos, camPos);
            UpdatePullIndicator(silkTipPos, camPos);
        }

        private void UpdateTension(float distance)
        {
            if (!silk.Attached || silk.pullingObject)
            {
                currentTension = 0f;
                displayedTension = Mathf.Lerp(displayedTension, 0f, 0.2f);
                return;
            }

            float overExtension = Mathf.Max(0f, distance - silk.requestedRopeLength);
            currentTension = Mathf.Clamp01(overExtension / 100f);
            displayedTension = Mathf.Lerp(displayedTension, currentTension, 0.15f);
        }

        private void UpdateTensionIndicator(Vector2 headPos, Vector2 camPos)
        {
            if (!silk.Attached || displayedTension < 0.05f || silk.pullingObject)
            {
                tensionIndicator.isVisible = false;
                return;
            }

            tensionIndicator.isVisible = true;
            tensionIndicator.x = headPos.x - camPos.x;
            tensionIndicator.y = headPos.y - camPos.y + 25f;
            tensionIndicator.scaleX = 30f * displayedTension;
            tensionIndicator.alpha = Mathf.Clamp01(displayedTension * 0.8f);
            tensionIndicator.color = Color.Lerp(Color.yellow, Color.red, displayedTension);
        }

        private void UpdatePullIndicator(Vector2 tipPos, Vector2 camPos)
        {
            if (!silk.pullingObject || !silk.AttachedToItem)
            {
                pullIndicator.isVisible = false;
                return;
            }

            pullIndicator.isVisible = true;
            pullIndicator.x = tipPos.x - camPos.x;
            pullIndicator.y = tipPos.y - camPos.y;
            float pulse = 0.6f + Mathf.Sin(Time.time * 8f) * 0.4f;
            pullIndicator.scale = pulse * 1.5f;
            pullIndicator.alpha = pulse * 0.7f;
            pullIndicator.color = new Color(0.3f, 1f, 0.3f, 0.8f);
        }

        private void UpdateAnchor(Vector2 headPos, Vector2 camPos)
        {
            bool showAnchor = silk.mode != SilkMode.Retracted && silk.mode != SilkMode.Retracting;
            anchorSprite.isVisible = showAnchor;
            glowSprite.isVisible = showAnchor;

            if (!showAnchor) return;

            anchorSprite.x = headPos.x - camPos.x;
            anchorSprite.y = headPos.y - camPos.y;
            glowSprite.x = anchorSprite.x;
            glowSprite.y = anchorSprite.y;

            Color anchorColor = GetPlayerColor();

            switch (silk.mode)
            {
                case SilkMode.ShootingOut:
                    anchorColor.a = 0.9f;
                    anchorSprite.scale = 0.45f;
                    glowSprite.scale = 0.7f;
                    break;

                case SilkMode.AttachedToTerrain:
                case SilkMode.AttachedToObject:
                    anchorColor.a = 1f;
                    float pulse = 0.4f + Mathf.Sin(Time.time * 4f) * 0.1f;
                    anchorSprite.scale = pulse;

                    if (silk.pullingObject)
                    {
                        pulse = 0.5f + Mathf.Sin(Time.time * 10f) * 0.15f;
                        anchorSprite.scale = pulse;
                        glowSprite.scale = pulse * 2.2f;
                        anchorColor = Color.Lerp(anchorColor, Color.green, 0.3f);
                    }
                    else
                    {
                        glowSprite.scale = pulse * (1.8f + displayedTension * 0.5f);
                    }
                    break;
            }

            anchorSprite.color = anchorColor;
            Color glowColor = silk.pullingObject ? Color.Lerp(anchorColor, Color.green, 0.4f) : Color.Lerp(anchorColor, Color.red, displayedTension * 0.5f);
            glowSprite.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.4f);
        }

        private void UpdateSilkLine(Vector2 startPos, Vector2 endPos, Vector2 camPos, float distance)
        {
            lineMesh.isVisible = true;
            tipSprite.isVisible = true;
            CalculateRopePoints(startPos, endPos, distance);

            float baseWidth = 3f;
            float width = silk.pullingObject ? baseWidth * 1.2f : baseWidth * (1f + displayedTension * 0.4f);

            if (silk.mode == SilkMode.ShootingOut) width = baseWidth * 0.8f;

            for (int i = 0; i < ROPE_SEGMENTS - 1; i++)
            {
                Vector2 segStart = ropePoints[i];
                Vector2 segEnd = ropePoints[i + 1];
                Vector2 segDir = (segEnd - segStart).normalized;
                Vector2 perpendicular = Custom.PerpendicularVector(segDir);
                float segmentT = (float)i / (ROPE_SEGMENTS - 1);
                float widthMultiplier = 1f - Mathf.Abs(segmentT * 2f - 1f) * 0.3f;
                float segmentWidth = width * widthMultiplier;

                int vertIndex = i * 4;
                lineMesh.MoveVertice(vertIndex, segStart - perpendicular * segmentWidth * 0.5f - camPos);
                lineMesh.MoveVertice(vertIndex + 1, segStart + perpendicular * segmentWidth * 0.5f - camPos);
                lineMesh.MoveVertice(vertIndex + 2, segEnd - perpendicular * segmentWidth * 0.5f - camPos);
                lineMesh.MoveVertice(vertIndex + 3, segEnd + perpendicular * segmentWidth * 0.5f - camPos);
            }

            Color silkColor = GetPlayerColor();
            float alpha = 1f;

            if (silk.pullingObject)
            {
                silkColor = Color.Lerp(silkColor, new Color(0.3f, 1f, 0.3f), 0.4f);
                alpha = 0.95f + Mathf.Sin(Time.time * 12f) * 0.05f;
            }
            else
            {
                silkColor = Color.Lerp(silkColor, new Color(1f, 0.3f, 0.3f), displayedTension * 0.6f);

                switch (silk.mode)
                {
                    case SilkMode.ShootingOut:
                        alpha = 0.95f * (0.9f + Mathf.Sin(Time.time * 15f) * 0.1f);
                        break;

                    case SilkMode.AttachedToTerrain:
                    case SilkMode.AttachedToObject:
                        alpha = 1f;
                        alpha *= displayedTension > 0.5f ? 0.95f + Mathf.Sin(Time.time * 8f) * 0.05f : 0.95f + Mathf.Sin(Time.time * 3f) * 0.05f;
                        break;
                }
            }

            silkColor.a = alpha;
            lineMesh.color = silkColor;

            tipSprite.x = endPos.x - camPos.x;
            tipSprite.y = endPos.y - camPos.y;
            tipSprite.scale = (width / 10f) * 1.5f;
            tipSprite.color = silkColor;

            if (silk.Attached) tipSprite.rotation += silk.pullingObject ? 15f : 5f;
            else tipSprite.rotation = Custom.VecToDeg(endPos - startPos);
        }

        private void CalculateRopePoints(Vector2 start, Vector2 end, float distance)
        {
            for (int i = 0; i < ROPE_SEGMENTS; i++)
            {
                float t = (float)i / (ROPE_SEGMENTS - 1);
                Vector2 point = Vector2.Lerp(start, end, t);

                if (silk.mode == SilkMode.ShootingOut)
                {
                    float sag = Mathf.Sin(t * Mathf.PI) * (distance * 0.02f);
                    point.y -= sag;
                }
                else if (silk.Attached)
                {
                    if (silk.pullingObject)
                    {
                        float sag = Mathf.Sin(t * Mathf.PI) * (distance * 0.01f);
                        point.y -= sag;
                        float shake = Mathf.Sin(Time.time * 20f + t * 10f) * 0.5f;
                        point.x += shake;
                    }
                    else
                    {
                        float slack = Mathf.Max(0, distance - silk.requestedRopeLength);
                        float baseSag = Mathf.Sin(t * Mathf.PI) * (distance * 0.05f);
                        float slackSag = Mathf.Sin(t * Mathf.PI) * Mathf.Min(slack * 0.3f, distance * 0.15f);
                        point.y -= baseSag + slackSag;

                        if (silk.attachedTime > 10) point.x += Mathf.Sin(Time.time * 2f + t * Mathf.PI) * (slack * 0.1f);
                    }
                }

                ropePoints[i] = point;
            }
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContainer) { }

        private Color GetPlayerColor()
        {
            if (player?.graphicsModule is PlayerGraphics playerGraphics)
            {
                try
                {
                    Color baseColor = PlayerGraphics.SlugcatColor(playerGraphics.CharacterForColor);
                    return new Color(
                        Mathf.Min(baseColor.r * 1.3f + 0.1f, 1f),
                        Mathf.Min(baseColor.g * 1.3f + 0.1f, 1f),
                        Mathf.Min(baseColor.b * 1.3f + 0.1f, 1f),
                        1f
                    );
                }
                catch { }
            }
            return new Color(0.9f, 0.9f, 1f, 1f);
        }

        private void HideAllSprites()
        {
            if (lineMesh != null) lineMesh.isVisible = false;
            if (tipSprite != null) tipSprite.isVisible = false;
            if (anchorSprite != null) anchorSprite.isVisible = false;
            if (glowSprite != null) glowSprite.isVisible = false;
            if (tensionIndicator != null) tensionIndicator.isVisible = false;
            if (pullIndicator != null) pullIndicator.isVisible = false;
        }

        public void RemoveSprites()
        {
            if (!spritesInitiated) return;

            try
            {
                if (lineMesh != null) { lineMesh.RemoveFromContainer(); lineMesh = null; }
                if (tipSprite != null) { tipSprite.RemoveFromContainer(); tipSprite = null; }
                if (anchorSprite != null) { anchorSprite.RemoveFromContainer(); anchorSprite = null; }
                if (glowSprite != null) { glowSprite.RemoveFromContainer(); glowSprite = null; }
                if (tensionIndicator != null) { tensionIndicator.RemoveFromContainer(); tensionIndicator = null; }
                if (pullIndicator != null) { pullIndicator.RemoveFromContainer(); pullIndicator = null; }
            }
            catch { }

            spritesInitiated = false;
            wasVisible = false;
            wasPulling = false;
        }
    }
}