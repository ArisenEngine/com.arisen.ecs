using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Scene-authored environment colors used by the first procedural sky and ambient-lighting path.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SceneEnvironmentComponent : IComponent
{
    public Vector3 SkyColor;
    public Vector3 HorizonColor;
    public Vector3 GroundColor;
    public Vector3 AmbientColor;
    public float SkyIntensity;
    public float AmbientIntensity;
    public byte Enabled;

    public bool IsEnabled =>
        Enabled != 0 &&
        (SkyIntensity > 0.0f || AmbientIntensity > 0.0f);

    public static SceneEnvironmentComponent Default => new()
    {
        SkyColor = new Vector3(0.07f, 0.19f, 0.38f),
        HorizonColor = new Vector3(0.62f, 0.73f, 0.82f),
        GroundColor = new Vector3(0.035f, 0.045f, 0.07f),
        AmbientColor = new Vector3(0.52f, 0.62f, 0.78f),
        SkyIntensity = 0.85f,
        AmbientIntensity = 0.32f,
        Enabled = 1
    };
}
