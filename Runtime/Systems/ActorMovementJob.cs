using BlobActor.Runtime;
using Introvert.RVO2;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Susanin.Systems
{
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    [WithAll(typeof(AgentFollowToDirectionTag))]
    internal partial struct ActorFollowToDirectionJob : IJobEntity
    {
        public float deltaTime;
        public static float Angle(float3 from, float3 to)
        {
            float num = (float)math.sqrt((double)math.lengthsq(from) * (double)math.lengthsq(to));
            return (double)num < 1.0000000036274937E-15 ? 0.0f : (float)math.acos((double)math.clamp(math.dot(from, to) / num, -1f, 1f));
        }
        [BurstCompile]
        private void Execute(in Actor agent, in IntrovertRvoAgent rvoAgent, ref ActorRuntime runtime, ref LocalTransform transform)
        {
            ref var actorBlob = ref agent.blob.Value;
            var previousVelocity = runtime.velocity;
            var previousVelocityNormed = math.normalizesafe(runtime.velocity);
            var speed = math.length(rvoAgent.velocity);
            var rvoVelocity = new float3(rvoAgent.velocity.x, previousVelocityNormed.y * speed, rvoAgent.velocity.y);
            var rvoVelocityNormed = math.normalizesafe(rvoVelocity);
            // var fwd = transform.Forward();
            // float angle = math.atan2(body.Velocity.x, body.Velocity.y);
            // Debug.Log(angle);
            var angle = math.atan2(rvoVelocityNormed.x, rvoVelocityNormed.z);
            // angle = math.atan2(math.sin(angle), math.cos(angle));
            // speed *= math.saturate(1f / (angle * actorBlob.turningSpeedImpact));
            var lerpedVelocity = math.lerp(previousVelocity, rvoVelocity, actorBlob.acceleration * deltaTime);
            // var lerpedVelocity = math.lerp(previousVelocity, rvoVelocity, actorBlob.acceleration * deltaTime);


            if ((runtime.flags & ActorRuntime.Flag.AllowMove) != 0)
            {
                runtime.velocity = lerpedVelocity;
                transform.Position += lerpedVelocity * deltaTime;
            }
            else
            {
                runtime.velocity=float3.zero;
            }
            // transform.Rotation = math.nlerp(transform.Rotation, quaternion.LookRotationSafe(rvoVelocityNormed, transform.Up()), actorBlob.rotationSpeed * deltaTime);
            if ((runtime.flags & ActorRuntime.Flag.AllowTurning) != 0)
                transform.Rotation = math.nlerp(transform.Rotation, quaternion.RotateY(angle), actorBlob.rotationSpeed * deltaTime);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ActorMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ActorFollowToDirectionJob() { deltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
        }
    }
}