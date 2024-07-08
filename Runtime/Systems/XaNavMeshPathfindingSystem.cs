using Jobs;
using NavMeshDots.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Experimental.AI;

namespace Susanin.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(NavMeshDotsManagedSystem))]
    [BurstCompile]
    public unsafe partial struct XaNavMeshPathfindingSystem : ISystem
    {
        private const int MAX_PATH_NODES_COUNT = 4096;
        private NativeQueue<int> _pool;
        private UnsafeList<NavMeshQuery>* _queries;
        private NativeList<NavMeshQueryData> _data;
        private NavMeshWorld _navMeshWorld;
        private Entity _requestsEntity;
        private NativeList<JobHandle> _handles;
        private NavMeshQuery _baseQuery;

        public void OnCreate(ref SystemState state)
        {
            _data = new NativeList<NavMeshQueryData>(Allocator.Persistent);
            _handles = new NativeList<JobHandle>(Allocator.Persistent);
            _navMeshWorld = NavMeshWorld.GetDefaultWorld();
            _pool = new NativeQueue<int>(Allocator.Persistent);
            _queries = UnsafeList<NavMeshQuery>.Create(0, Allocator.Persistent);
            _requestsEntity = state.EntityManager.CreateSingletonBuffer<NavMeshSearchPath>();
            _baseQuery = new NavMeshQuery(_navMeshWorld, Allocator.Persistent, MAX_PATH_NODES_COUNT);
           
        }

        public void OnDestroy(ref SystemState state)
        {
            _baseQuery.Dispose();
            if (_data.IsCreated) _data.Dispose();
            if (_handles.IsCreated) _handles.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
            if (_queries->IsCreated)
            {
                for (int i = 0; i < _queries->Length; i++)
                {
                    _queries->ElementAt(i).Dispose();
                }
                UnsafeList<NavMeshQuery>.Destroy(_queries);
            }
            if (!_requestsEntity.Equals(Entity.Null)) state.EntityManager.DestroyEntity(_requestsEntity);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
            var requests = SystemAPI.GetSingletonBuffer<NavMeshSearchPath>();
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (_pool.TryDequeue(out var index))
                {
                    ref var queryData = ref _data.ElementAt(index);
                    NavMeshQueryData.Reset(ref queryData, request);
                }
                else
                {
                    var queryData = new NavMeshQueryData();
                    NavMeshQueryData.Reset(ref queryData, request);
                    _data.Add(queryData);
                    _queries->Add(new NavMeshQuery(_navMeshWorld, Allocator.Persistent, MAX_PATH_NODES_COUNT));
                }
            }
            requests.Clear();
            _handles.Clear();
            var ecbWriter = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            var poolWriter = _pool.AsParallelWriter();
            for (int i = 0; i < _data.Length; i++)
            {
                ref var queryData = ref _data.ElementAt(i);
                if (queryData._status == NavMeshQueryData._Status.Idle) continue;

                var query = _queries->ElementAt(i);
                _handles.Add(
                    new ProcessQueriesJob()
                    {
                        ecb = ecbWriter,
                        max = MAX_ITERATIONS,
                        data = _data,
                        queryIndex = i,
                        query = query,
                        pool = poolWriter,
                    }.Schedule(state.Dependency));
            }
            state.Dependency = JobHandle.CombineDependencies(_handles.AsArray());
            state.Dependency = new SeeekerJob()
            {
                query = _baseQuery,
                pathRequestEntity = _requestsEntity,
                ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                deltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);

            _navMeshWorld.AddDependency(state.Dependency);

        }
        private const int MAX_ITERATIONS = 1028;
    }

}