using System;
using System.Collections.Generic;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Records structural changes to the ECS world to be played back later.
/// This allows parallel systems to queue actions without thread-safety concerns.
/// </summary>
public class EntityCommandBuffer
{
    private interface ICommand
    {
        void Execute(EntityManager em);
    }

    private struct CreateEntityCommand : ICommand
    {
        public void Execute(EntityManager em) => em.CreateEntity();
    }

    private struct DestroyEntityCommand : ICommand
    {
        public Entity Entity;
        public void Execute(EntityManager em) => em.DestroyEntity(Entity);
    }

    private struct AddComponentCommand<T> : ICommand where T : struct, IComponent
    {
        public Entity Entity;
        public T Component;
        public void Execute(EntityManager em) => em.AddComponent(Entity, Component);
    }

    private struct RemoveComponentCommand<T> : ICommand where T : struct, IComponent
    {
        public Entity Entity;
        public void Execute(EntityManager em) => em.RemoveComponent<T>(Entity);
    }

    private readonly List<ICommand> m_Commands = new();
    private int m_RecordingThreadId = -1;

    private void EnsureThreadSafety()
    {
        int currentThread = Environment.CurrentManagedThreadId;
        if (m_RecordingThreadId == -1)
        {
            m_RecordingThreadId = currentThread;
        }
        else if (m_RecordingThreadId != currentThread)
        {
            throw new InvalidOperationException(
                $"EntityCommandBuffer was created for thread {m_RecordingThreadId} " +
                $"but is being mutated by thread {currentThread}. " +
                "ECBs are not thread-safe and must be per-system/per-thread.");
        }
    }

    public void CreateEntity()
    {
        EnsureThreadSafety();
        m_Commands.Add(new CreateEntityCommand());
    }

    public void DestroyEntity(Entity entity)
    {
        EnsureThreadSafety();
        m_Commands.Add(new DestroyEntityCommand { Entity = entity });
    }

    public void AddComponent<T>(Entity entity, in T component = default) where T : struct, IComponent
    {
        EnsureThreadSafety();
        m_Commands.Add(new AddComponentCommand<T> { Entity = entity, Component = component });
    }

    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        EnsureThreadSafety();
        m_Commands.Add(new RemoveComponentCommand<T> { Entity = entity });
    }

    /// <summary>
    /// Executes all recorded commands against the provided EntityManager.
    /// This should be called sequentially on the main thread.
    /// </summary>
    public void Playback(EntityManager em)
    {
        try
        {
            for (int i = 0; i < m_Commands.Count; i++)
            {
                m_Commands[i].Execute(em);
            }
        }
        finally
        {
            Clear();
        }
    }

    public void Clear()
    {
        m_Commands.Clear();
        m_RecordingThreadId = -1;
    }
}
