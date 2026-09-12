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
    }
}
