using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArisenEngine.Threading;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A container that manages ECS systems and executes them using a TaskGraph.
/// </summary>
public class SystemContainer
    : IDisposable
{
    private class SystemMetadata
    {
        public ISystem System = null!;
        public Type SystemType = null!;
        public HashSet<Type> ReadComponents = new();
        public HashSet<Type> WriteComponents = new();
        public HashSet<Type> ExecuteBefore = new();
        public HashSet<Type> ExecuteAfter = new();
        public TaskNode? TaskNode;
    }

    private class SystemTaskNode : TaskNode
    {
        public ISystem System = null!;
        public EntityManager? EntityManager;
        public EntityCommandBuffer? CommandBuffer; // Used for thread-safe structural changes
        public float DeltaTime;

        public override void Execute()
        {
            if (EntityManager != null && CommandBuffer != null)
            {
                System.Execute(EntityManager, CommandBuffer, DeltaTime);
            }
        }
    }

    private readonly List<SystemMetadata> m_Systems = new();
    private readonly ITaskGraph m_TaskGraph;
    private ITaskSchedule? m_Schedule;
    private bool m_IsDirty = true;
    private bool m_Disposed;

    public SystemContainer(ITaskGraph taskGraph)
    {
        m_TaskGraph = taskGraph ?? throw new ArgumentNullException(nameof(taskGraph));
    }

    public void AddSystem(ISystem system)
    {
        ObjectDisposedException.ThrowIf(m_Disposed, this);
        ArgumentNullException.ThrowIfNull(system);

        var meta = new SystemMetadata
        {
            System = system,
            SystemType = system.GetType(),
            TaskNode = new SystemTaskNode 
            { 
                System = system, 
                Name = system.Name,
                CommandBuffer = new EntityCommandBuffer() // Each system gets its own ECB
            }
        };

        // Extract attributes
        var attributes = meta.SystemType.GetCustomAttributes(true);
        foreach (var attr in attributes)
        {
            if (attr is ReadComponentAttribute read) meta.ReadComponents.Add(read.ComponentType);
            else if (attr is WriteComponentAttribute write) meta.WriteComponents.Add(write.ComponentType);
            else if (attr is ExecuteBeforeAttribute before) meta.ExecuteBefore.Add(before.OtherSystemType);
            else if (attr is ExecuteAfterAttribute after) meta.ExecuteAfter.Add(after.OtherSystemType);
        }

        m_Systems.Add(meta);
        m_IsDirty = true;
    }

    public void Execute(EntityManager em, float dt)
    {
        ObjectDisposedException.ThrowIf(m_Disposed, this);
        ArgumentNullException.ThrowIfNull(em);

        if (m_IsDirty)
        {
            RebuildGraph();
        }

        if (m_Schedule == null) return;

        // Update context for all systems before execution
        foreach (var meta in m_Systems)
        {
            var node = (SystemTaskNode)meta.TaskNode!;
            node.EntityManager = em;
            node.DeltaTime = dt;
        }

        try
        {
            m_Schedule.Execute();
        }
        catch
        {
            ClearCommandBuffers();
            throw;
        }

        // 3. Playback Phase (Sequential)
        // Now that the parallel simulation phase is complete, we apply all 
        // structural changes (Create/Destroy/Add/Remove) to the EntityManager.
        try
        {
            for (int i = 0; i < m_Systems.Count; i++)
            {
                var node = (SystemTaskNode)m_Systems[i].TaskNode!;
                node.CommandBuffer?.Playback(em);
            }
        }
        catch
        {
            ClearCommandBuffers();
            throw;
        }
    }

    private void RebuildGraph()
    {
        m_Schedule?.Dispose();
        m_Schedule = null;

        if (m_Systems.Count == 0)
        {
            m_IsDirty = false;
            return;
        }

        var tasks = new TaskNode[m_Systems.Count];
        for (int i = 0; i < m_Systems.Count; i++)
        {
            tasks[i] = m_Systems[i].TaskNode!;
        }

        var dependencies = new List<TaskDependency>();
        for (int i = 0; i < m_Systems.Count; i++)
        {
            for (int j = 0; j < m_Systems.Count; j++)
            {
                if (i == j) continue;

                var sysA = m_Systems[i];
                var sysB = m_Systems[j];

                bool needsDependency = false;

                // Data Conflict: A writes, B reads/writes -> A must run before B
                // Note: To be deterministic, we follow registration order for conflicts.
                if (i < j)
                {
                    bool aWrites = sysA.WriteComponents.Overlaps(sysB.ReadComponents) || 
                                   sysA.WriteComponents.Overlaps(sysB.WriteComponents);
                    
                    bool bWrites = sysB.WriteComponents.Overlaps(sysA.ReadComponents);

                    if (aWrites || bWrites) needsDependency = true;
                }

                // Explicit Ordering
                if (sysA.ExecuteBefore.Contains(sysB.SystemType) || 
                    sysB.ExecuteAfter.Contains(sysA.SystemType))
                {
                    needsDependency = true;
                }

                if (needsDependency)
                {
                    dependencies.Add(new TaskDependency(sysA.TaskNode!, sysB.TaskNode!));
                }
            }
        }

        m_Schedule = m_TaskGraph.CreateSchedule(tasks, dependencies);
        m_IsDirty = false;
    }

    private void ClearCommandBuffers()
    {
        for (int i = 0; i < m_Systems.Count; i++)
        {
            var node = (SystemTaskNode)m_Systems[i].TaskNode!;
            node.CommandBuffer?.Clear();
        }
    }

    public void Dispose()
    {
        if (m_Disposed) return;

        m_Disposed = true;
        ClearCommandBuffers();
        m_Schedule?.Dispose();
        m_Schedule = null;
    }
}
