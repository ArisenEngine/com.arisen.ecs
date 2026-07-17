using ArisenKernel.Lifecycle;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.RHI;
using ArisenEngine.Threading;
using System;

namespace ArisenEngine.ECS.Lifecycle;

/// <summary>
/// Manages the active ECS world, entities, and systemic updates.
/// </summary>
public class SceneSubsystem : ITickableSubsystem
{
    public int Priority => 50; // Execute before Rendering (100)
    public EnginePhase InitPhase => EnginePhase.Init;

    public EntityManager ActiveEntityManager { get; private set; } = null!;
    
    // Internal buffer for draw calls, reallocated only when needed.
    private MeshDrawCommand[] m_DrawListBuffer = new MeshDrawCommand[64];
    private int m_DrawCommandCount = 0;
    private StaticMeshRenderItem[] m_StaticMeshItems = new StaticMeshRenderItem[64];
    private int m_StaticMeshItemCount = 0;

    /// <summary>
    /// Gets the list of mesh draw commands processed during the current frame.
    /// </summary>
    public ReadOnlySpan<MeshDrawCommand> GetCurrentDrawList() => new(m_DrawListBuffer, 0, m_DrawCommandCount);

    public ReadOnlySpan<StaticMeshRenderItem> GetCurrentStaticMeshItems() => new(m_StaticMeshItems, 0, m_StaticMeshItemCount);

    /// <summary>
    /// Updates the internal draw list. Called by MeshSystem.
    /// </summary>
    public void UpdateDrawList(ReadOnlySpan<MeshDrawCommand> drawList)
    {
        if (drawList.Length > m_DrawListBuffer.Length)
        {
            m_DrawListBuffer = new MeshDrawCommand[drawList.Length * 2];
        }

        drawList.CopyTo(m_DrawListBuffer);
        m_DrawCommandCount = drawList.Length;
    }

    public void UpdateStaticMeshItems(ReadOnlySpan<StaticMeshRenderItem> items)
    {
        if (items.Length > m_StaticMeshItems.Length)
        {
            m_StaticMeshItems = new StaticMeshRenderItem[items.Length * 2];
        }

        items.CopyTo(m_StaticMeshItems);
        m_StaticMeshItemCount = items.Length;
    }

    private SystemContainer? m_Systems;

    public void Initialize()
    {
        var taskGraph = EngineKernel.Instance.Services.GetService<ITaskGraph>();
        m_Systems = new SystemContainer(taskGraph);
        ActiveEntityManager = new EntityManager();
    }

    public void Tick(float deltaTime)
    {
        m_Systems?.Execute(ActiveEntityManager, deltaTime);
    }

    public void RegisterSystem(ISystem system)
    {
        var systems = m_Systems
            ?? throw new InvalidOperationException("SceneSubsystem must be initialized before systems are registered.");
        systems.AddSystem(system);
    }

    /// <summary>
    /// Atomically replaces the active ECS world after a scene has been fully validated and loaded.
    /// </summary>
    public void ActivateEntityManager(EntityManager entityManager)
    {
        ActiveEntityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        m_DrawCommandCount = 0;
        m_StaticMeshItemCount = 0;
    }

    public void Shutdown()
    {
        m_Systems?.Dispose();
        m_Systems = null;
        ActiveEntityManager = null!;
        m_DrawCommandCount = 0;
        m_StaticMeshItemCount = 0;
    }

    public void Dispose() => Shutdown();
}

