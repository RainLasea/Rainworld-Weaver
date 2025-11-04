using UnityEngine;
using RWCustom;

namespace Weaver.Silk
{
    public enum SilkMode
    {
        Retracted,
        ShootingOut,
        AttachedToTerrain,
        AttachedToObject,
        Retracting
    }

    public class SilkPhysics
    {
        public Vector2 pos, lastPos, vel;
        public SilkMode mode;
        public Player player;
        public BodyChunk baseChunk;
        public Vector2 terrainStuckPos;
        public BodyChunk attachedChunk;
        public PhysicalObject attachedObject;
        public float idealRopeLength;
        public float requestedRopeLength;
        public float elastic;
        public int attachedTime;
        public bool returning;
        private IntVector2[] _cachedRtList = new IntVector2[20];
        public bool pullingObject;

        private const float MIN_ROPE_LENGTH = 50f;
        private const float MAX_ROPE_LENGTH = 800f;
        private const float SHOOT_SPEED = 50f;
        private const float GRAVITY = 0.9f;
        private const float OBJECT_PULL_FORCE = 1.8f;

        public SilkPhysics(Player player)
        {
            this.player = player;
            this.baseChunk = player.bodyChunks[0];
            pos = lastPos = baseChunk.pos;
            mode = SilkMode.Retracted;
            elastic = 0f;
            attachedTime = 0;
            pullingObject = false;
        }

        public bool Attached => mode == SilkMode.AttachedToTerrain || mode == SilkMode.AttachedToObject;
        public bool AttachedToItem => mode == SilkMode.AttachedToObject && attachedObject != null;

        public void Release()
        {
            mode = SilkMode.Retracting;
            attachedChunk = null;
            attachedObject = null;
            vel = Vector2.zero;
            requestedRopeLength = 0f;
            elastic = 0f;
            pullingObject = false;
        }

        public void Shoot(Vector2 direction)
        {
            if (mode != SilkMode.Retracted) return;

            pos = lastPos = baseChunk.pos;
            vel = direction.normalized * SHOOT_SPEED;
            mode = SilkMode.ShootingOut;
            idealRopeLength = MAX_ROPE_LENGTH;
            requestedRopeLength = 0f;
            elastic = 0f;
            returning = false;
            pullingObject = false;
        }

        public void Update()
        {
            lastPos = pos;

            if (Attached) attachedTime++;
            else attachedTime = 0;

            switch (mode)
            {
                case SilkMode.Retracted:
                    UpdateRetracted();
                    break;
                case SilkMode.ShootingOut:
                    UpdateShootingOut();
                    break;
                case SilkMode.AttachedToTerrain:
                    UpdateAttachedToTerrain();
                    break;
                case SilkMode.AttachedToObject:
                    UpdateAttachedToObject();
                    break;
                case SilkMode.Retracting:
                    mode = SilkMode.Retracted;
                    break;
            }

            if (mode != SilkMode.Retracted) Elasticity();

            if (Attached) UpdateRopeLength();
        }

        private void UpdateRetracted()
        {
            requestedRopeLength = 0f;
            pos = baseChunk.pos;
            vel = baseChunk.vel;
        }

        private void UpdateShootingOut()
        {
            vel.y -= GRAVITY * Mathf.InverseLerp(0.8f, 0f, elastic);
            pos += vel;

            bool collisionOccurred = false;

            IntVector2? hitTile = SharedPhysics.RayTraceTilesForTerrainReturnFirstSolid(player.room, lastPos, pos);
            if (hitTile != null)
            {
                FloatRect collisionRect = Custom.RectCollision(pos, lastPos, player.room.TileRect(hitTile.Value).Grow(1f));
                Vector2 collisionPoint = new Vector2(collisionRect.left, collisionRect.bottom);
                AttachToTerrain(collisionPoint);
                collisionOccurred = true;
            }

            if (!collisionOccurred && !Custom.DistLess(baseChunk.pos, pos, 60f))
            {
                PhysicalObject hitObject = CheckObjectCollision();
                if (hitObject != null)
                {
                    AttachToObject(hitObject);
                    collisionOccurred = true;
                }
            }

            if (!collisionOccurred)
            {
                if (returning)
                {
                    pos += Custom.RNV() / 1000f;
                    int rayCount = SharedPhysics.RayTracedTilesArray(lastPos, pos, _cachedRtList);

                    for (int i = 0; i < rayCount; i++)
                    {
                        if (player.room.GetTile(_cachedRtList[i]).horizontalBeam)
                        {
                            float midY = player.room.MiddleOfTile(_cachedRtList[i]).y;
                            float crossX = Custom.HorizontalCrossPoint(lastPos, pos, midY).x;
                            float clampedX = Mathf.Clamp(crossX, player.room.MiddleOfTile(_cachedRtList[i]).x - 10f,
                                                          player.room.MiddleOfTile(_cachedRtList[i]).x + 10f);
                            AttachToTerrain(new Vector2(clampedX, midY));
                            break;
                        }
                        if (player.room.GetTile(_cachedRtList[i]).verticalBeam)
                        {
                            float midX = player.room.MiddleOfTile(_cachedRtList[i]).x;
                            float crossY = Custom.VerticalCrossPoint(lastPos, pos, midX).y;
                            float clampedY = Mathf.Clamp(crossY, player.room.MiddleOfTile(_cachedRtList[i]).y - 10f,
                                                          player.room.MiddleOfTile(_cachedRtList[i]).y + 10f);
                            AttachToTerrain(new Vector2(midX, clampedY));
                            break;
                        }
                    }

                    if (Custom.DistLess(baseChunk.pos, pos, 40f))
                    {
                        mode = SilkMode.Retracted;
                    }
                }
                else if (Vector2.Dot(Custom.DirVec(baseChunk.pos, pos), vel.normalized) < 0f)
                {
                    returning = true;
                }
            }
        }

        private PhysicalObject CheckObjectCollision()
        {
            if (player.room == null) return null;

            foreach (var obj in player.room.physicalObjects)
            {
                foreach (var item in obj)
                {
                    if (item == player) continue;

                    bool isPullableItem = item is Weapon ||
                                         item is DangleFruit ||
                                         item is SporePlant ||
                                         item is DataPearl ||
                                         item is Rock ||
                                         item is ScavengerBomb ||
                                         item is Spear ||
                                         item is FirecrackerPlant;

                    if (!isPullableItem) continue;

                    for (int i = 0; i < item.bodyChunks.Length; i++)
                    {
                        BodyChunk chunk = item.bodyChunks[i];
                        float distance = Vector2.Distance(pos, chunk.pos);

                        if (distance < chunk.rad + 5f)
                        {
                            return item;
                        }
                    }
                }
            }

            return null;
        }

        private void UpdateAttachedToTerrain()
        {
            pos = terrainStuckPos;
            vel = Vector2.zero;
        }

        private void UpdateAttachedToObject()
        {
            if (attachedObject != null)
            {
                pos = attachedObject.bodyChunks[0].pos;
                vel = attachedObject.bodyChunks[0].vel;

                if (attachedObject.room != player.room)
                {
                    mode = SilkMode.Retracting;
                    attachedObject = null;
                    attachedChunk = null;
                }

                if (pullingObject) PullAttachedObject();
            }
            else if (attachedChunk != null)
            {
                pos = attachedChunk.pos;
                vel = attachedChunk.vel;

                if (attachedChunk.owner.room != player.room)
                {
                    mode = SilkMode.Retracting;
                    attachedChunk = null;
                }
            }
            else
            {
                mode = SilkMode.Retracting;
            }
        }

        private void PullAttachedObject()
        {
            if (attachedObject == null || attachedObject.bodyChunks == null) return;

            Vector2 toPlayer = baseChunk.pos - pos;
            float distance = toPlayer.magnitude;

            if (distance < 20f)
            {
                pullingObject = false;
                return;
            }

            Vector2 pullDirection = toPlayer.normalized;

            foreach (BodyChunk chunk in attachedObject.bodyChunks)
            {
                float mass = chunk.mass;
                float adjustedForce = OBJECT_PULL_FORCE / Mathf.Max(mass, 0.5f);
                chunk.vel += pullDirection * adjustedForce;
                if (chunk.vel.magnitude > 25f)
                {
                    chunk.vel = chunk.vel.normalized * 25f;
                }
            }
        }

        private void UpdateRopeLength()
        {
            if (pullingObject) return;

            elastic = Mathf.Max(0f, elastic - 0.05f);

            if (requestedRopeLength < idealRopeLength)
            {
                requestedRopeLength = Mathf.Min(requestedRopeLength + (1f - elastic) * 10f, idealRopeLength);
            }
            else if (requestedRopeLength > idealRopeLength)
            {
                requestedRopeLength = Mathf.Max(requestedRopeLength - (1f - elastic) * 10f, idealRopeLength);
            }

            requestedRopeLength = Mathf.Clamp(requestedRopeLength, MIN_ROPE_LENGTH, MAX_ROPE_LENGTH);
        }

        public void Elasticity()
        {
            if (mode == SilkMode.Retracted) return;
            if (requestedRopeLength <= 0f) return;

            Vector2 delta = pos - baseChunk.pos;
            float dist = delta.magnitude;

            if (dist > requestedRopeLength)
            {
                float excessLength = dist - requestedRopeLength;
                Vector2 pullDir = delta.normalized;
                float pullAmount = Mathf.Min(excessLength * 0.6f, 15f);
                Vector2 correction = pullDir * pullAmount;

                for (int i = 0; i < player.bodyChunks.Length; i++)
                {
                    player.bodyChunks[i].pos += correction;
                    player.bodyChunks[i].vel -= Vector2.Dot(player.bodyChunks[i].vel, pullDir) * pullDir * 0.4f;
                }

                elastic = Mathf.Min(elastic + 0.15f, 0.8f);
            }
        }

        private void AttachToTerrain(Vector2 pos)
        {
            terrainStuckPos = pos;
            this.pos = pos;
            vel = Vector2.zero;
            mode = SilkMode.AttachedToTerrain;

            float currentDist = Vector2.Distance(baseChunk.pos, terrainStuckPos);
            idealRopeLength = Mathf.Clamp(currentDist, MIN_ROPE_LENGTH, MAX_ROPE_LENGTH);
            requestedRopeLength = idealRopeLength;
            elastic = 0f;
            pullingObject = false;
        }

        private void AttachToChunk(BodyChunk chunk)
        {
            attachedChunk = chunk;
            attachedObject = chunk.owner as PhysicalObject;
            pos = chunk.pos;
            vel = chunk.vel;
            mode = SilkMode.AttachedToObject;

            float currentDist = Vector2.Distance(baseChunk.pos, chunk.pos);
            idealRopeLength = Mathf.Clamp(currentDist, MIN_ROPE_LENGTH, MAX_ROPE_LENGTH);
            requestedRopeLength = idealRopeLength;
            elastic = 0f;
            pullingObject = false;
        }

        private void AttachToObject(PhysicalObject obj)
        {
            attachedObject = obj;
            attachedChunk = obj.bodyChunks[0];
            pos = attachedChunk.pos;
            vel = attachedChunk.vel;
            mode = SilkMode.AttachedToObject;

            float currentDist = Vector2.Distance(baseChunk.pos, pos);
            idealRopeLength = Mathf.Clamp(currentDist, MIN_ROPE_LENGTH, MAX_ROPE_LENGTH);
            requestedRopeLength = idealRopeLength;
            elastic = 0f;
            pullingObject = false;
        }
    }
}