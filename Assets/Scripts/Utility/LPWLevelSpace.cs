using UnityEngine;

/// <summary>
/// Put this on the level root that gets moved by <see cref="OrbitAroundTarget"/>.
/// The "Level Space" Low Poly Wind shaders then calculate the wind relative to this transform
/// instead of world space, so orbiting the level no longer speeds up the wind animation.
/// Only one active instance is supported (the values are global shader properties).
/// </summary>
[ExecuteAlways]
public class LPWLevelSpace : MonoBehaviour
{
    private static readonly int WorldToLocalId = Shader.PropertyToID("_LPWLevelWorldToLocal");
    private static readonly int LocalToWorldId = Shader.PropertyToID("_LPWLevelLocalToWorld");
    private static readonly int EnabledId = Shader.PropertyToID("_LPWLevelSpaceEnabled");

    private void OnEnable()
    {
        UpdateShaderGlobals();
    }

    // LateUpdate runs after OrbitAroundTarget has moved the level in Update.
    private void LateUpdate()
    {
        UpdateShaderGlobals();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
    }

    private void UpdateShaderGlobals()
    {
        // Position and rotation only: a scaled level root should not change the wind phase or speed.
        Matrix4x4 localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Shader.SetGlobalMatrix(LocalToWorldId, localToWorld);
        Shader.SetGlobalMatrix(WorldToLocalId, localToWorld.inverse);
        Shader.SetGlobalFloat(EnabledId, 1f);
    }
}
