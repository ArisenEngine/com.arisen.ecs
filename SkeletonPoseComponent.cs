using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Local joint rotations and translations that override the cooked rest pose of a skinned mesh. The
/// animation runtime writes here - a writer supplies a complete pose, so both channels are set
/// together - and the frame snapshot copies the component into the frame's pose arrays, so render
/// preparation can build a skin palette without reaching back into the ECS. Scales stay on the
/// cooked rest pose: the cooked skeleton stores unit-scale joints.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SkeletonPoseComponent : IComponent
{
    // The pose is a fixed buffer inside the component, so this bound is what one pool slot may carry.
    // Thirty-two was the hand-authored figure's skeleton; an imported character's reach further once
    // hands, feet, and its IK controls join the skin (the gallery's character rig cooks 41 joints, a
    // Mixamo-style rig around 65), so the bound leaves room for a real rig while the cooker's own
    // 255-joint bound stays the outer limit.
    public const int MaxJoints = 128;

    public int JointCount;
    private fixed float m_LocalRotations[MaxJoints * 4];
    private fixed float m_LocalTranslations[MaxJoints * 3];

    public bool IsValid => JointCount is > 0 and <= MaxJoints;

    public static SkeletonPoseComponent Create(int jointCount)
    {
        if (jointCount <= 0 || jointCount > MaxJoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jointCount),
                $"[SkeletonPoseComponent] Joint count '{jointCount}' is outside 1..{MaxJoints}.");
        }

        var component = default(SkeletonPoseComponent);
        component.JointCount = jointCount;
        for (int jointIndex = 0; jointIndex < jointCount; jointIndex++)
        {
            component.SetLocalRotation(jointIndex, Quaternion.Identity);
        }

        return component;
    }

    public Quaternion GetLocalRotation(int jointIndex)
    {
        if ((uint)jointIndex >= (uint)JointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        return new Quaternion(
            m_LocalRotations[jointIndex * 4],
            m_LocalRotations[jointIndex * 4 + 1],
            m_LocalRotations[jointIndex * 4 + 2],
            m_LocalRotations[jointIndex * 4 + 3]);
    }

    public void SetLocalRotation(int jointIndex, Quaternion rotation)
    {
        if ((uint)jointIndex >= (uint)JointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        var lengthSquared = rotation.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared < 1.0e-8f)
        {
            throw new ArgumentException(
                $"[SkeletonPoseComponent] Joint {jointIndex} rotation must be a non-zero quaternion.",
                nameof(rotation));
        }

        var normalized = Quaternion.Normalize(rotation);
        m_LocalRotations[jointIndex * 4] = normalized.X;
        m_LocalRotations[jointIndex * 4 + 1] = normalized.Y;
        m_LocalRotations[jointIndex * 4 + 2] = normalized.Z;
        m_LocalRotations[jointIndex * 4 + 3] = normalized.W;
    }

    public Vector3 GetLocalTranslation(int jointIndex)
    {
        if ((uint)jointIndex >= (uint)JointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        return new Vector3(
            m_LocalTranslations[jointIndex * 3],
            m_LocalTranslations[jointIndex * 3 + 1],
            m_LocalTranslations[jointIndex * 3 + 2]);
    }

    public void SetLocalTranslation(int jointIndex, Vector3 translation)
    {
        if ((uint)jointIndex >= (uint)JointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        if (!float.IsFinite(translation.X) ||
            !float.IsFinite(translation.Y) ||
            !float.IsFinite(translation.Z))
        {
            throw new ArgumentException(
                $"[SkeletonPoseComponent] Joint {jointIndex} translation must be finite.",
                nameof(translation));
        }

        m_LocalTranslations[jointIndex * 3] = translation.X;
        m_LocalTranslations[jointIndex * 3 + 1] = translation.Y;
        m_LocalTranslations[jointIndex * 3 + 2] = translation.Z;
    }
}
