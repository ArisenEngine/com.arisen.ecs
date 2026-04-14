using ArisenKernel.Lifecycle;
using ArisenEngine.Core.ECS;

namespace ArisenEngine.ECS.Lifecycle;

/// <summary>
/// Manages the active ECS world, entities, and systemic updates.
/// </summary>
public class SceneSubsystem : ITickableSubsystem
{
    public int Priority => 50; // Execute before Rendering (100)
    public EnginePhase InitPhase => EnginePhase.Init;

    public EntityManager ActiveEntityManager { get; private set; }
    private readonly SystemContainer m_Systems = new();

    public void Initialize()
    {
        ActiveEntityManager = new EntityManager();
    }

    public void Tick(float deltaTime)
    {
        m_Systems.Execute(ActiveEntityManager, deltaTime);
    }

    public void RegisterSystem(ISystem system)
    {
        m_Systems.AddSystem(system);
    }

    public void Shutdown()
    {
        ActiveEntityManager = null;
    }

    public void Dispose() => Shutdown();
}

