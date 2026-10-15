using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Local joint rotations that override the cooked rest pose of a skinned mesh. The animation
/// runtime writes here and the frame snapshot copies the component into the frame's pose array, so
/// render preparation can build a skin palette without reaching back into the ECS. Translations and
/// scales stay on the cooked rest pose until the clip runtime needs to drive them.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SkeletonPoseComponent : IComponent
{
    public const int MaxJoints = 32;

    public int JointCount;
    private fixed float m_LocalRotations[MaxJoints * 4];

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
}
