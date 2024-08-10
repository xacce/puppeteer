using Core.Runtime;
using Introvert.RVO2;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Xacce.BlobActor.Runtime;
using Xacce.Susanin.Runtime;

namespace Xacce.Puppeteer.Runtime.Jobs
{
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PuppeteerActorApplyVelocityJob : IJobEntity
    {
        public float deltaTime;
        //
        // public static float Angle(float3 from, float3 to)
        // {
        //     float num = (float)math.sqrt((double)math.lengthsq(from) * (double)math.lengthsq(to));
        //     return (double)num < 1.0000000036274937E-15 ? 0.0f : (float)math.acos((double)math.clamp(math.dot(from, to) / num, -1f, 1f));
        // }

        [BurstCompile]
        private void Execute(in BlobActor.Runtime.BlobActor agent, in IntrovertAgent rvoAagent, DynamicObjectVelocity velocity, in SusaninActor susaninActor, in BlobActorFlags flags,
            ref LocalTransform transform)
        {
            if ((flags.flags & BlobActorFlags.Flag.SyncWithNavMesh) != 0 && !susaninActor.currentLocation.polygon.IsNull()) transform.Position = susaninActor.currentLocation.position;
            ref var actorBlob = ref agent.blob.Value;
            var previousVelocity = velocity.velocity;
            var previousVelocityNormed = math.normalizesafe(velocity.velocity);
            var speed = math.length(rvoAagent.velocity);
            var rvoVelocity = new float3(rvoAagent.velocity.x, previousVelocityNormed.y * speed, rvoAagent.velocity.y);
            var rvoVelocityNormed = math.normalizesafe(rvoVelocity);
            var angle = math.atan2(rvoVelocityNormed.x, rvoVelocityNormed.z);
            var lerpedVelocity = math.lerp(previousVelocity, rvoVelocity, actorBlob.acceleration * deltaTime);


            if ((flags.flags & BlobActorFlags.Flag.AllowMove) != 0)
            {
                velocity.velocity = lerpedVelocity;
                transform.Position += lerpedVelocity * deltaTime;
            }
            else
            {
                velocity.velocity = float3.zero;
            }

            if ((flags.flags & BlobActorFlags.Flag.AllowTurning) != 0)
                transform.Rotation = math.nlerp(transform.Rotation, quaternion.RotateY(angle), actorBlob.rotationSpeed * deltaTime);
        }
    }
}