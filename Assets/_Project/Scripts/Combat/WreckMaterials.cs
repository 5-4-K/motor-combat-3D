using UnityEngine;
using UnityEngine.Rendering;

namespace MotorCombat.Combat
{
    /// <summary>
    /// URP Lit materials are opaque, and an opaque material cannot be faded by
    /// changing its colour's alpha. This switches a (cloned) material to URP's
    /// transparent surface, which is what the material inspector does.
    /// </summary>
    public static class WreckMaterials
    {
        public static void MakeTransparent(Material material)
        {
            if (material == null || !material.HasProperty("_Surface")) return;

            material.SetFloat("_Surface", 1f);   // Transparent
            material.SetFloat("_Blend", 0f);     // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
