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
        public ISystem System;
        public EntityManager? EntityManager;
        public float DeltaTime;

        public override void Execute()
        {
            if (EntityManager != null)
            {
                System.Execute(EntityManager, DeltaTime);
            }
        }
    }

    private readonly List<SystemMetadata> m_Systems = new();
    private TaskGraph? m_TaskGraph;
    private bool m_IsDirty = true;

    public void AddSystem(ISystem system)
    {
        var meta = new SystemMetadata
        {
            System = system,
            SystemType = system.GetType(),
            TaskNode = new SystemTaskNode { System = system, Name = system.Name }
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
        if (m_IsDirty)
        {
            RebuildGraph();
        }

        if (m_TaskGraph == null) return;

        // Update context for all systems before execution
        foreach (var meta in m_Systems)
        {
            var node = (SystemTaskNode)meta.TaskNode!;
            node.EntityManager = em;
            node.DeltaTime = dt;
        }

        m_TaskGraph.Execute();
    }

    private void RebuildGraph()
    {
        m_TaskGraph?.Dispose();
        m_TaskGraph = new TaskGraph();

        // 1. Add all nodes to the graph
        foreach (var meta in m_Systems)
        {
            m_TaskGraph.AddTask(meta.TaskNode!);
        }

        // 2. Resolve dependencies
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
                    m_TaskGraph.AddDependency(sysA.TaskNode!, sysB.TaskNode!);
                }
            }
        }

        m_IsDirty = false;
    }
}
