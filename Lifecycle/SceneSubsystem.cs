using ArisenKernel.Lifecycle;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.RHI;
using ArisenEngine.Threading;
using System;
using System.Numerics;

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
    private Quaternion[] m_SkinJointRotations = new Quaternion[64];
    private int m_SkinJointRotationCount = 0;
    private Vector3[] m_SkinJointTranslations = new Vector3[64];
    private int m_SkinJointTranslationCount = 0;

    /// <summary>
    /// Gets the list of mesh draw commands processed during the current frame.
    /// </summary>
    public ReadOnlySpan<MeshDrawCommand> GetCurrentDrawList() => new(m_DrawListBuffer, 0, m_DrawCommandCount);

    public ReadOnlySpan<StaticMeshRenderItem> GetCurrentStaticMeshItems() => new(m_StaticMeshItems, 0, m_StaticMeshItemCount);

    public ReadOnlySpan<Quaternion> GetCurrentSkinJointRotations() => new(m_SkinJointRotations, 0, m_SkinJointRotationCount);

    public ReadOnlySpan<Vector3> GetCurrentSkinJointTranslations() => new(m_SkinJointTranslations, 0, m_SkinJointTranslationCount);

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

    public void UpdateStaticMeshItems(
        ReadOnlySpan<StaticMeshRenderItem> items,
        ReadOnlySpan<Quaternion> skinJointRotations,
        ReadOnlySpan<Vector3> skinJointTranslations)
    {
        if (items.Length > m_StaticMeshItems.Length)
        {
            m_StaticMeshItems = new StaticMeshRenderItem[items.Length * 2];
        }

        if (skinJointRotations.Length > m_SkinJointRotations.Length)
        {
            m_SkinJointRotations = new Quaternion[skinJointRotations.Length * 2];
        }

        if (skinJointTranslations.Length > m_SkinJointTranslations.Length)
        {
            m_SkinJointTranslations = new Vector3[skinJointTranslations.Length * 2];
        }

        items.CopyTo(m_StaticMeshItems);
        m_StaticMeshItemCount = items.Length;
        skinJointRotations.CopyTo(m_SkinJointRotations);
        m_SkinJointRotationCount = skinJointRotations.Length;
        skinJointTranslations.CopyTo(m_SkinJointTranslations);
        m_SkinJointTranslationCount = skinJointTranslations.Length;
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

