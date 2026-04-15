using System;
using System.Numerics;
using Arisen.Native.RHI;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Memory;
using ArisenEngine.Core.RHI;
using ArisenEngine.ECS.Lifecycle;
using ArisenKernel.Lifecycle;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A system that gathers all visible mesh renderers and prepares a Draw List for the RenderSubsystem.
/// No longer depends on the Rendering package.
/// </summary>
public sealed class MeshSystem : ISystem
{
    public string Name => "MeshSystem";

    public void Execute(EntityManager em, EntityCommandBuffer ecb, float deltaTime)
    {
        var meshPool = em.GetPool<MeshRendererComponent>();
        var transformPool = em.GetPool<TransformComponent>();
        
        int count = meshPool.Count;
        if (count == 0) return;

        // 1. Allocate space in the FrameArena for this frame's draw commands
        var drawList = FrameArena.Instance.Alloc<MeshDrawCommand>(count);
        
        var meshComponents = meshPool.GetRawComponentArray();
        var meshEntities = meshPool.GetRawEntityArray();

        int drawCount = 0;
        for (int i = 0; i < count; i++)
        {
            Entity entity = meshEntities[i];
            if (transformPool.Has(entity))
            {
                ref var meshComp = ref meshComponents[i];
                if (!meshComp.IsValid) continue;

                ref var transComp = ref transformPool.GetRef(entity);
                ref var cmd = ref drawList[drawCount];

                // 2. Prepare the LocalToWorld matrix
                // Arisen follows a Row-Major convention for CPU math (System.Numerics default)
                cmd.LocalToWorld = Matrix4x4.CreateScale(transComp.Scale) * 
                                   Matrix4x4.CreateFromQuaternion(transComp.Rotation) * 
                                   Matrix4x4.CreateTranslation(transComp.Position);

                // 3. Extract RHI handles directly from the component (DOD path)
                cmd.VertexBuffer = meshComp.VertexBuffer;
                cmd.IndexBuffer = meshComp.IndexBuffer;
                cmd.IndexCount = meshComp.IndexCount;
                cmd.IndexType = meshComp.IndexType;

                drawCount++;
            }
        }

        // 4. Update the SceneSubsystem with the final list of commands to be rendered
        var scene = EngineKernel.Instance.GetSubsystem<SceneSubsystem>();
        if (scene != null)
        {
            scene.UpdateDrawList(drawList.Slice(0, drawCount));
        }
    }
}
