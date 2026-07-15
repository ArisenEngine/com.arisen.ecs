using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Scene-authored spot light data. Position and cone direction come from TransformComponent.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SpotLightComponent : IComponent
{
    public Vector3 Color;
    public float Intensity;
    public float Range;
    public float InnerConeAngleDegrees;
    public float OuterConeAngleDegrees;
    public byte Enabled;

    public bool IsEnabled =>
        Enabled != 0 &&
        Intensity > 0.0f &&
        Range > 0.0f &&
        OuterConeAngleDegrees > 0.0f;

    public static SpotLightComponent Default => new()
    {
        Color = Vector3.One,
        Intensity = 1.0f,
        Range = 4.0f,
        InnerConeAngleDegrees = 18.0f,
        OuterConeAngleDegrees = 28.0f,
        Enabled = 1
    };
}
