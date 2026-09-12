using UnityEngine;

namespace MotorCombat.Arena
{
    [CreateAssetMenu(menuName = "Motor Combat/Arena Config", fileName = "ArenaConfig")]
    public class ArenaConfig : ScriptableObject
    {
        [Tooltip("Arena radius in metres. Absolute, not car-relative: the arena is a place, " +
                 "and its size should not move when the car is retuned.")]
        public float radiusMetres = 45f;

        public float wallHeight = 2.5f;

        [Tooltip("Segments around the circle. Higher is smoother and costs more triangles.")]
        [Range(12, 256)]
        public int segments = 96;

        [Header("Appearance")]
        [Tooltip("Ground material. Leave empty to generate the placeholder calibration grid.")]
        public Material groundMaterial;

        [Tooltip("Wall material. Leave empty to generate the placeholder stripes.")]
        public Material wallMaterial;

        [Tooltip("Metres of world space per texture repeat on the floor. A texture's scale is a " +
                 "physical property of the material -- a paving slab is the same size whatever " +
                 "drives over it -- so this is absolute.")]
        public float groundTileMetres = 4f;

        [Tooltip("Metres per texture repeat on the wall. Kept separate from the floor because the " +
                 "wall is only wallHeight tall: set it equal to wallHeight and the wall shows " +
                 "exactly one full tile from base to top.")]
        public float wallTileMetres = 2.5f;

        [Header("Placeholder only")]
        [Tooltip("Size of one calibration-grid cell, in CAR LENGTHS. The one thing here that is " +
                 "deliberately car-relative: the grid is a measuring tool whose whole job is to " +
                 "let you count distance and drift in cars. Ignored once a real ground material " +
                 "is assigned.")]
        public float gridCellInCarLengths = 1f;
    }
}
