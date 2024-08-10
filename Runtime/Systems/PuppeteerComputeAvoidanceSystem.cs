using Core.Runtime;
using GameReady.Runtime;
using Introvert.RVO2;
using SpatialHashing.Uniform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine.Experimental.AI;
using Xacce.BlobActor.Runtime;
using Xacce.Introvert.Runtime.PolyObstacle;
using Xacce.Introvert.Runtime.RVO2;

namespace Xacce.Puppeteer.Runtime.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(PuppetterComputeNavMeshMovementSystem))]
    public partial struct PuppeteerComputeAvoidanceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            _navMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 128);
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<UniformSpatialDatabase>();
            _lookups = new Lookups(ref state);
        }

        public void OnDestroy(ref SystemState state)
        {
            _navMeshQuery.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _lookups.Update(ref state);
            // var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
            state.Dependency = new IntrovertRvo2Job()
            {
                actorFlagsRo = _lookups.actorFlagsRo,
                actorLimitsRo = _lookups.actorLimitsRo,
                dynamicRo = _lookups.dynamicObjectVelocityRo,
                timeStep = SystemAPI.Time.DeltaTime,
                rvoAgentRw = _lookups.rvo2AgentRw,
                localToWorldRo = _lookups.localToWorldRo,
                obstacleLineRo = _lookups.polyObstacleRo,
                actorLookupRo = _lookups.actorRo,
                bridge = new UniformSpatialDatabaseReadonlyBridge()
                {
                    entity = SystemAPI.GetSingletonEntity<UniformSpatialDatabase>(),
                    uniformSpatialDatabaseRo = _lookups.uniformSpatialDatabaseRo,
                    uniformSpatialElementRo = _lookups.uniformSpatialElementRo,
                    uniformSpatialCellRo = _lookups.uniformSpatialCellRo,
                },
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        internal struct Lookups
        {
            [ReadOnly] public ComponentLookup<LocalToWorld> localToWorldRo;
            [ReadOnly] public ComponentLookup<UniformSpatialDatabase> uniformSpatialDatabaseRo;
            [ReadOnly] public BufferLookup<UniformSpatialElement> uniformSpatialElementRo;
            [ReadOnly] public BufferLookup<UniformSpatialCell> uniformSpatialCellRo;
            [ReadOnly] public ComponentLookup<PolyObstacle> polyObstacleRo;
            [ReadOnly] public ComponentLookup<BlobActor.Runtime.BlobActor> actorRo;
            [ReadOnly] public ComponentLookup<BlobActorFlags> actorFlagsRo;
            [ReadOnly] public ComponentLookup<BlobActorLimits> actorLimitsRo;
            [ReadOnly] public ComponentLookup<DynamicObjectVelocity> dynamicObjectVelocityRo;
            [ReadOnly] public ComponentLookup<Dead> deadRo;


            public ComponentLookup<IntrovertAgent> rvo2AgentRw;

            public Lookups(ref SystemState state) : this()
            {
                rvo2AgentRw = state.GetComponentLookup<IntrovertAgent>(false);
                localToWorldRo = state.GetComponentLookup<LocalToWorld>(true);
                uniformSpatialCellRo = state.GetBufferLookup<UniformSpatialCell>(true);
                uniformSpatialElementRo = state.GetBufferLookup<UniformSpatialElement>(true);
                uniformSpatialDatabaseRo = state.GetComponentLookup<UniformSpatialDatabase>(true);
                polyObstacleRo = state.GetComponentLookup<PolyObstacle>(true);
                actorFlagsRo = state.GetComponentLookup<BlobActorFlags>(true);
                actorLimitsRo = state.GetComponentLookup<BlobActorLimits>(true);
                dynamicObjectVelocityRo = state.GetComponentLookup<DynamicObjectVelocity>(true);
                actorRo = state.GetComponentLookup<BlobActor.Runtime.BlobActor>(true);
                deadRo = state.GetComponentLookup<Dead>(true);
            }


            [BurstCompile]
            public void Update(ref SystemState state)
            {
                rvo2AgentRw.Update(ref state);
                localToWorldRo.Update(ref state);
                uniformSpatialCellRo.Update(ref state);
                uniformSpatialElementRo.Update(ref state);
                uniformSpatialDatabaseRo.Update(ref state);
                polyObstacleRo.Update(ref state);
                actorRo.Update(ref state);
                deadRo.Update(ref state);
                actorFlagsRo.Update(ref state);
                actorLimitsRo.Update(ref state);
                dynamicObjectVelocityRo.Update(ref state);
            }
        }

        private Lookups _lookups;
        private NavMeshQuery _navMeshQuery;
    }
}