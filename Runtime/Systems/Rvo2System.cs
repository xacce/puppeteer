using BlobActor.Runtime;
using GameReady.Runtime;
using Introvert.RVO2;
using Jobs;
using SpatialHashing.Uniform;
using Susanin.Systems;
using Troupe.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine.Experimental.AI;

namespace Introvert.Systems
{

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(XaNavMeshPathfindingSystem))]
    public partial struct Rvo2System : ISystem
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
            var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
            state.Dependency = new LazyFormationNavMeshInitializeJob()
            {
                ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                actorRo = _lookups.actorRo,
                navMeshQuery = _navMeshQuery,
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new PredifinedFormationJob()
            {
                deltaTime = SystemAPI.Time.DeltaTime,
                localToWorldRo = _lookups.localToWorldRo,
                informationLookupRw = _lookups.inFormationRw,
                deadRo = _lookups.deadRo,
                ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new IntrovertRvo2Job()
            {
                actorRuntimeLookupRo = _lookups.actorRuntimeRo,
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
            [ReadOnly] public ComponentLookup<PolyObstacle.PolyObstacle> polyObstacleRo;
            [ReadOnly] public ComponentLookup<Actor> actorRo;

            [ReadOnly] public ComponentLookup<ActorRuntime> actorRuntimeRo;

            [ReadOnly] public ComponentLookup<Dead> deadRo;


            public ComponentLookup<InFormation> inFormationRw;


            public ComponentLookup<IntrovertRvoAgent> rvo2AgentRw;

            public Lookups(ref SystemState state) : this()
            {
                rvo2AgentRw = state.GetComponentLookup<IntrovertRvoAgent>(false);
                localToWorldRo = state.GetComponentLookup<LocalToWorld>(true);
                uniformSpatialCellRo = state.GetBufferLookup<UniformSpatialCell>(true);
                uniformSpatialElementRo = state.GetBufferLookup<UniformSpatialElement>(true);
                uniformSpatialDatabaseRo = state.GetComponentLookup<UniformSpatialDatabase>(true);
                polyObstacleRo = state.GetComponentLookup<PolyObstacle.PolyObstacle>(true);
                actorRuntimeRo = state.GetComponentLookup<ActorRuntime>(true);
                actorRo = state.GetComponentLookup<Actor>(true);
                inFormationRw = state.GetComponentLookup<InFormation>(false);
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
                actorRuntimeRo.Update(ref state);
                actorRo.Update(ref state);
                inFormationRw.Update(ref state);
                deadRo.Update(ref state);
            }
        }

        private Lookups _lookups;
        private NavMeshQuery _navMeshQuery;
    }
}