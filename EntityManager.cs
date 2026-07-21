using System;
using System.Collections.Generic;
using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// The central Registry for the ECS. It issues new Entity IDs and routes component payloads
/// to their respective contiguous ComponentPools.
/// </summary>
public class EntityManager : IEntityManager
{
    private const int InitialEntityCapacity = 128;

    private int m_NextEntityId;
    private readonly List<int> m_FreeIds = new();
    private readonly Dictionary<Type, IComponentPool> m_ComponentPools = new();
    private int[] m_Generations = new int[InitialEntityCapacity];
    private bool[] m_ActiveEntities = new bool[InitialEntityCapacity];

    public int EntityCount { get; private set; }

    public int AllocatedSlotCount => m_NextEntityId;

    public int FreeSlotCount => m_FreeIds.Count;

    /// <summary>
    /// Creates a new Entity, optionally reusing an ID from a destroyed Entity.
    /// </summary>
    public Entity CreateEntity()
    {
        int id;
        if (m_FreeIds.Count > 0)
        {
            id = m_FreeIds[^1];
            m_FreeIds.RemoveAt(m_FreeIds.Count - 1);
        }
        else
        {
            id = m_NextEntityId++;
        }

        EnsureEntityCapacity(id);
        if (m_Generations[id] == 0)
        {
            m_Generations[id] = 1;
        }

        m_ActiveEntities[id] = true;
        EntityCount++;
        return new Entity(id, m_Generations[id]);
    }

    /// <summary>
    /// Creates a new Entity with a specific ID. Useful for deserialization to maintain relationships.
    /// </summary>
    public Entity CreateEntity(int id)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        EnsureEntityCapacity(id);
        if (m_ActiveEntities[id])
        {
            throw new InvalidOperationException($"Entity slot {id} is already active.");
        }

        if (id >= m_NextEntityId)
        {
            for (int freeId = id - 1; freeId >= m_NextEntityId; freeId--)
            {
                EnsureEntityCapacity(freeId);
                if (m_Generations[freeId] == 0)
                {
                    m_Generations[freeId] = 1;
                }
                m_FreeIds.Add(freeId);
            }
            m_NextEntityId = id + 1;
        }
        else
        {
            m_FreeIds.Remove(id);
        }

        if (m_Generations[id] == 0)
        {
            m_Generations[id] = 1;
        }

        m_ActiveEntities[id] = true;
        EntityCount++;
        return new Entity(id, m_Generations[id]);
    }

    public bool IsAlive(Entity entity)
    {
        return entity.IsValid
            && entity.Id < m_NextEntityId
            && entity.Id < m_ActiveEntities.Length
            && m_ActiveEntities[entity.Id]
            && m_Generations[entity.Id] == entity.Generation;
    }

    /// <summary>
    /// Returns all currently active entities in the world.
    /// </summary>
    public IEnumerable<Entity> GetAllEntities()
    {
        for (int id = 0; id < m_NextEntityId; id++)
        {
            if (m_ActiveEntities[id])
            {
                yield return new Entity(id, m_Generations[id]);
            }
        }
    }

    /// <summary>
    /// Destroys the entity and removes all associated components across all pools.
    /// </summary>
    public void DestroyEntity(Entity entity)
    {
        if (!TryDestroyEntity(entity))
        {
            throw new InvalidOperationException($"Cannot destroy stale or inactive entity {entity}.");
        }
    }

    public bool TryDestroyEntity(Entity entity)
    {
        if (!IsAlive(entity))
        {
            return false;
        }

        foreach (var pool in m_ComponentPools.Values)
        {
            pool.Remove(entity);
        }

        ReleaseEntitySlot(entity.Id);
        return true;
    }

    public void DestroyEntities(ReadOnlySpan<Entity> entities)
    {
        if (entities.IsEmpty)
        {
            return;
        }

        var uniqueIds = new HashSet<int>(entities.Length);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            if (!IsAlive(entity))
            {
                throw new InvalidOperationException(
                    $"Cannot bulk-destroy stale or inactive entity {entity}.");
            }

            if (!uniqueIds.Add(entity.Id))
            {
                throw new InvalidOperationException(
                    $"Cannot bulk-destroy duplicate entity {entity}.");
            }
        }

        foreach (var pool in m_ComponentPools.Values)
        {
            pool.RemoveEntities(entities);
        }

        for (int i = 0; i < entities.Length; i++)
        {
            ReleaseEntitySlot(entities[i].Id);
        }
    }

    private void ReleaseEntitySlot(int id)
    {
        m_ActiveEntities[id] = false;
        m_Generations[id] = NextGeneration(m_Generations[id]);
        EntityCount--;
        m_FreeIds.Add(id);
    }

    /// <summary>
    /// Adds or updates a component onto the given entity.
    /// </summary>
    public ref T AddComponent<T>(Entity entity, in T component = default) where T : struct, IComponent
    {
        EnsureAlive(entity);
        var pool = GetOrCreatePool<T>();
        return ref pool.Add(entity, component);
    }

    void IEntityManager.AddComponent<T>(Entity entity, in T component)
    {
        AddComponent(entity, component);
    }

    /// <summary>
    /// Retrieves a contiguous reference to a component for raw mutation.
    /// </summary>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        EnsureAlive(entity);
        return ref GetOrCreatePool<T>().Get(entity);
    }

    /// <summary>
    /// Checks if the entity has the given component type.
    /// </summary>
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (!IsAlive(entity))
        {
            return false;
        }

        if (m_ComponentPools.TryGetValue(typeof(T), out var pool))
        {
            return pool.Has(entity);
        }
        return false;
    }

    /// <summary>
    /// Removes the component from the entity.
    /// </summary>
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        EnsureAlive(entity);
        if (m_ComponentPools.TryGetValue(typeof(T), out var pool))
        {
            pool.Remove(entity);
        }
    }

    /// <summary>
    /// Fetches the raw ComponentPool Sparse Set logic directly for ultra-fast Parallel iteration blocks.
    /// </summary>
    public ComponentPool<T> GetPool<T>() where T : struct, IComponent
    {
        return GetOrCreatePool<T>();
    }

    /// <summary>
    /// Checks if a pool exists for the given component type.
    /// </summary>
    public bool HasPool<T>() where T : struct, IComponent
    {
        return m_ComponentPools.ContainsKey(typeof(T));
    }

    /// <summary>
    /// Returns all component pools that contain the given entity.
    /// Useful for Inspector-style discovery.
    /// </summary>
    public IEnumerable<IComponentPool> GetEntityComponentPools(Entity entity)
    {
        if (!IsAlive(entity))
        {
            yield break;
        }

        foreach (var kvp in m_ComponentPools)
        {
            if (kvp.Value.Has(entity))
            {
                yield return kvp.Value;
            }
        }
    }

    /// <summary>
    /// Returns all registered component pools.
    /// Useful for serialization, inspection, and tools that need to iterate all components.
    /// </summary>
    public IReadOnlyDictionary<Type, IComponentPool> GetAllPools()
    {
        return m_ComponentPools;
    }

    private ComponentPool<T> GetOrCreatePool<T>() where T : struct, IComponent
    {
        var type = typeof(T);
        if (!m_ComponentPools.TryGetValue(type, out var pool))
        {
            pool = new ComponentPool<T>();
            m_ComponentPools[type] = pool;
        }

        return (ComponentPool<T>)pool;
    }

    private void EnsureAlive(Entity entity)
    {
        if (!IsAlive(entity))
        {
            throw new InvalidOperationException($"Entity handle {entity} is stale or inactive.");
        }
    }

    private void EnsureEntityCapacity(int entityId)
    {
        if (entityId < m_Generations.Length)
        {
            return;
        }

        int newSize = System.Math.Max(m_Generations.Length * 2, entityId + 1);
        Array.Resize(ref m_Generations, newSize);
        Array.Resize(ref m_ActiveEntities, newSize);
    }

    private static int NextGeneration(int generation)
    {
        return generation == int.MaxValue ? 1 : generation + 1;
    }
}

