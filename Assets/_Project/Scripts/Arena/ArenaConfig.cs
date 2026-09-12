using UnityEngine;

namespace MotorCombat.Arena
{
    [CreateAssetMenu(menuName = "Motor Combat/Arena Config", fileName = "ArenaConfig")]
    public class ArenaConfig : ScriptableObject
    {
        [Tooltip("Arena radius expressed in car lengths. Actual radius = this * CarDefinition.length.")]
        public float radiusInCarLengths = 10f;

        public float wallHeight = 2.5f;

        [Tooltip("Segments around the circle. Higher is smoother and costs more triangles.")]
        [Range(12, 256)]
        public int segments = 96;

        [Header("Appearance")]
        [Tooltip("Ground material. Leave empty to generate the placeholder calibration grid.")]
        public Material groundMaterial;

        [Tooltip("Wall material. Leave empty to generate the placeholder stripes.")]
        public Material wallMaterial;

        [Tooltip("World size of one texture repeat, in CAR LENGTHS rather than metres. " +
                 "Car lengths because the arena radius is already expressed that way, so changing " +
                 "car length rescales the whole arena coherently instead of silently shifting how " +
                 "big a texture looks. The placeholder grid subdivides each repeat into one cell " +
                 "per car length, so major grid lines land every this-many cars.")]
        public float tileSizeInCarLengths = 5f;
    }
}
