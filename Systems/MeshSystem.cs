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
        var posePool = em.GetPool<SkeletonPoseComponent>();
        var scene = EngineKernel.Instance.GetSubsystem<SceneSubsystem>();

        int count = meshPool.Count;
        if (count == 0)
        {
            scene?.UpdateStaticMeshItems(
                ReadOnlySpan<StaticMeshRenderItem>.Empty,
                ReadOnlySpan<Quaternion>.Empty,
                ReadOnlySpan<Vector3>.Empty);
            return;
        }

        // Frame-scoped: SceneSubsystem publishes these items for this frame's render extraction,
        // and MemoryManager reclaims the arena at the frame boundary.
        var renderItems = MemoryManager.FrameArena.Alloc<StaticMeshRenderItem>(count);
        Span<Quaternion> skinJointRotations = posePool.Count > 0
            ? MemoryManager.FrameArena.Alloc<Quaternion>(posePool.Count * SkeletonPoseComponent.MaxJoints)
            : Span<Quaternion>.Empty;
        Span<Vector3> skinJointTranslations = posePool.Count > 0
            ? MemoryManager.FrameArena.Alloc<Vector3>(posePool.Count * SkeletonPoseComponent.MaxJoints)
            : Span<Vector3>.Empty;

        var meshComponents = meshPool.GetRawComponentArray();
        var meshEntities = meshPool.GetRawEntityArray();

        int itemCount = 0;
        int skinJointRotationCount = 0;
        int skinJointTranslationCount = 0;
        for (int i = 0; i < count; i++)
        {
            Entity entity = meshEntities[i];
            if (transformPool.Has(entity))
            {
                ref var meshComp = ref meshComponents[i];
                if (!meshComp.IsValid) continue;

                ref var transComp = ref transformPool.GetRef(entity);
                if (!IsFinite(transComp)) continue;
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
                item.SkinJointOffset = 0;
                item.SkinJointCount = 0;
                item.SkinTranslationOffset = 0;
                item.SkinTranslationCount = 0;

                if (posePool.Has(entity))
                {
                    ref var pose = ref posePool.GetRef(entity);
                    if (pose.IsValid)
                    {
                        item.SkinJointOffset = skinJointRotationCount;
                        item.SkinJointCount = pose.JointCount;
                        item.SkinTranslationOffset = skinJointTranslationCount;
                        item.SkinTranslationCount = pose.JointCount;
                        for (int jointIndex = 0; jointIndex < pose.JointCount; jointIndex++)
                        {
                            skinJointRotations[skinJointRotationCount++] = pose.GetLocalRotation(jointIndex);
                            skinJointTranslations[skinJointTranslationCount++] = pose.GetLocalTranslation(jointIndex);
                        }
                    }
                }

                itemCount++;
            }
        }

        if (scene != null)
        {
            scene.UpdateStaticMeshItems(
                renderItems.Slice(0, itemCount),
                skinJointRotations.Slice(0, skinJointRotationCount),
                skinJointTranslations.Slice(0, skinJointTranslationCount));
        }
    }

    private static bool IsFinite(in TransformComponent transform) =>
        IsFinite(transform.Position) &&
        float.IsFinite(transform.Rotation.X) &&
        float.IsFinite(transform.Rotation.Y) &&
        float.IsFinite(transform.Rotation.Z) &&
        float.IsFinite(transform.Rotation.W) &&
        IsFinite(transform.Scale);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}
