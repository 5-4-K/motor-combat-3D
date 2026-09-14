using UnityEngine;

namespace MotorCombat.Core
{
    public static class HurtboxRules
    {
        /// <summary>
        /// True when at least one box's vertical range, measured from the car's
        /// floor, contains <paramref name="height"/>. The car root is the centre
        /// of its collision box, so the floor is at local y = −carBoxHeight / 2.
        /// </summary>
        public static bool SpansHeight(HurtboxBox[] boxes, float carBoxHeight, float height)
        {
            if (boxes == null) return false;

            float floor = -carBoxHeight * 0.5f;
            for (int i = 0; i < boxes.Length; i++)
            {
                float half = Mathf.Abs(boxes[i].size.y) * 0.5f;
                float bottom = boxes[i].centre.y - half - floor;
                float top = boxes[i].centre.y + half - floor;
                if (height >= bottom && height <= top) return true;
            }

            return false;
        }
    }
}
