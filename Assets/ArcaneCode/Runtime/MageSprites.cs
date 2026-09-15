using UnityEngine;

namespace ArcaneCode
{
    public static class MageSprites
    {
        static Sprite[] poses;
        public static Sprite Facing(Vector2 direction)
        {
            if (poses == null)
            {
                var texture = Resources.Load<Texture2D>("Characters/Mage/mage-directions");
                if (texture == null) return null;
                poses = new Sprite[4];
                int width = texture.width / 4;
                for (int i = 0; i < 4; i++)
                {
                    poses[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height), new Vector2(.5f, .05f), 128);
                    poses[i].name = new[] { "mage-front", "mage-back", "mage-left", "mage-right" }[i];
                }
            }
            int index = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? (direction.x < 0 ? 2 : 3) : (direction.y > 0 ? 1 : 0);
            return poses[index];
        }
    }
}
