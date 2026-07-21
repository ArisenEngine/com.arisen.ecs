using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A zero-allocation, purely blittable component to store origin-relative world position,
/// rotation, and scale. World descriptors and streaming policy retain double coordinates;
/// the resources-owned origin service shifts this compact active-world data at frame boundaries.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TransformComponent : IComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    public static TransformComponent Identity => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        Scale = Vector3.One
    };
}


