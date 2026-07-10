using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Asset-facing mesh renderer data authored by simulation/game code.
/// Render setup resolves these stable asset references into prepared RHI draw commands.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MeshRendererComponent : IComponent
{
    public Guid MeshGuid;
    public Guid MaterialGuid;
    public int FirstSubmeshIndex;
    public int SubmeshCount;
    public Vector3 BoundsCenter;
    public Vector3 BoundsExtents;
    public byte Visible;

    public bool IsVisible => Visible != 0;
    public bool IsValid => MeshGuid != Guid.Empty && IsVisible;

    public static MeshRendererComponent Create(Guid meshGuid, Guid materialGuid = default)
    {
        return new MeshRendererComponent
        {
            MeshGuid = meshGuid,
            MaterialGuid = materialGuid,
            FirstSubmeshIndex = 0,
            SubmeshCount = -1,
            BoundsCenter = Vector3.Zero,
            BoundsExtents = Vector3.Zero,
            Visible = 1
        };
    }
}
