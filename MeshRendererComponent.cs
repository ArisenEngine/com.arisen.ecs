using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A zero-allocation, purely blittable component to describe mesh rendering data.
/// Removed dependency on ArisenEngine.Rendering to resolve circularity.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MeshRendererComponent : IComponent
{
    public RHIBufferHandle VertexBuffer;
    public RHIBufferHandle IndexBuffer;
    public uint IndexCount;
    public EIndexType IndexType;
    
    // Placeholder for material logic
    public uint MaterialID;

    public bool IsValid => VertexBuffer.IsValid;
}
