using System;
using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Memory;
using ArisenEngine.Core.RHI;
using ArisenEngine.ECS.Lifecycle;
using ArisenKernel.Lifecycle;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Gathers visible mesh renderer components into asset-facing render items.
/// Render setup resolves the asset references into prepared RHI draw commands.
/// </summary>
public sealed class MeshSystem : ISystem
{
    public string Name => "MeshSystem";

    public void Execute(EntityManager em, EntityCommandBuffer ecb, float deltaTime)
    {
        var meshPool = em.GetPool<MeshRendererComponent>();
        var transformPool = em.GetPool<TransformComponent>();
        var scene = EngineKernel.Instance.GetSubsystem<SceneSubsystem>();
        
        int count = meshPool.Count;
        if (count == 0)
        {
            scene?.UpdateStaticMeshItems(ReadOnlySpan<StaticMeshRenderItem>.Empty);
            return;
        }

        var renderItems = FrameArena.Instance.Alloc<StaticMeshRenderItem>(count);
        
        var meshComponents = meshPool.GetRawComponentArray();
        var meshEntities = meshPool.GetRawEntityArray();

        int itemCount = 0;
        for (int i = 0; i < count; i++)
        {
            Entity entity = meshEntities[i];
            if (transformPool.Has(entity))
            {
                ref var meshComp = ref meshComponents[i];
                if (!meshComp.IsValid) continue;

                ref var transComp = ref transformPool.GetRef(entity);
                ref var item = ref renderItems[itemCount];

                // Arisen follows a Row-Major convention for CPU math (System.Numerics default)
                item.LocalToWorld = Matrix4x4.CreateScale(transComp.Scale) *
                                    Matrix4x4.CreateFromQuaternion(transComp.Rotation) *
                                    Matrix4x4.CreateTranslation(transComp.Position);
                item.MeshGuid = meshComp.MeshGuid;
                item.MaterialGuid = meshComp.MaterialGuid;
                item.FirstSubmeshIndex = meshComp.FirstSubmeshIndex;
                item.SubmeshCount = meshComp.SubmeshCount;
                item.BoundsCenter = meshComp.BoundsCenter;
                item.BoundsExtents = meshComp.BoundsExtents;
                item.Visible = meshComp.Visible;

                itemCount++;
            }
        }

        if (scene != null)
        {
            scene.UpdateStaticMeshItems(renderItems.Slice(0, itemCount));
        }
    }
}
