using System.Collections.Generic;
using UnityEngine;

namespace ArcaneCode
{
    public static class MageSprites
    {
        static Sprite[] poses, attackPoses;
        static readonly Dictionary<string, Sprite[]> AnimationFrames = new Dictionary<string, Sprite[]>();
        static readonly HashSet<Texture2D> CleanedAttackSheets = new HashSet<Texture2D>();
        public static Sprite Facing(Vector2 direction)
        {
            if (poses == null)
            {
                var texture = Resources.Load<Texture2D>("Characters/Mage/mage-directions");
                if (texture == null) return null;
                poses = Slice(texture, "mage", 128);
            }
            return poses[DirectionIndex(direction)];
        }

        public static Sprite Attacking(Vector2 direction)
        {
            if (attackPoses == null)
            {
                var texture = Resources.Load<Texture2D>("Characters/Mage/mage-attack");
                if (texture == null) return null;
                attackPoses = Slice(texture, "mage-attack", texture.height / 1.25f);
            }
            return attackPoses[DirectionIndex(direction)];
        }

        public static Sprite Walking(Vector2 direction, int frame) => Animated("walk", direction, 5, 2, 10, frame) ?? Facing(direction);

        public static Sprite AttackFrame(Vector2 direction, int frame) => Animated("attack", direction, 8, 3, 24, frame) ?? Attacking(direction);

        static int DirectionIndex(Vector2 direction) => Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? (direction.x < 0 ? 2 : 3) : (direction.y > 0 ? 1 : 0);

        static Sprite[] Slice(Texture2D texture, string prefix, float pixelsPerUnit)
        {
            var result = new Sprite[4];
            int width = texture.width / 4;
            for (int i = 0; i < 4; i++)
            {
                int cellWidth = i == 3 ? texture.width - width * i : width;
                result[i] = Sprite.Create(texture, new Rect(i * width, 0, cellWidth, texture.height), new Vector2(.5f, .05f), pixelsPerUnit);
                result[i].name = prefix + "-" + new[] { "front", "back", "left", "right" }[i];
            }
            return result;
        }

        static Sprite Animated(string animation, Vector2 direction, int columns, int rows, int frameCount, int frame)
        {
            string directionName = new[] { "front", "back", "left", "right" }[DirectionIndex(direction)];
            string key = animation + "-" + directionName;
            if (!AnimationFrames.TryGetValue(key, out Sprite[] frames))
            {
                Texture2D texture = Resources.Load<Texture2D>("Characters/Mage/Animations/mage-" + key);
                if (texture == null) return null;
                // Enquanto o Editor termina um reimport, a animação ainda deve tocar.
                // Sem Read/Write o ataque fica com o fundo original, mas não quebra o cast.
                if (animation == "attack" && texture.isReadable) ClearPurpleBackdrop(texture);
                frames = SliceGrid(texture, key, columns, rows);
                AnimationFrames[key] = frames;
            }
            return frames[Mathf.Clamp(frame, 0, frameCount - 1)];
        }

        static Sprite[] SliceGrid(Texture2D texture, string prefix, int columns, int rows)
        {
            var result = new Sprite[columns * rows];
            float width = texture.width / (float)columns;
            float height = texture.height / (float)rows;
            float pixelsPerUnit = height / 1.25f;
            for (int frame = 0; frame < result.Length; frame++)
            {
                int column = frame % columns;
                int row = frame / columns;
                result[frame] = Sprite.Create(texture, new Rect(column * width, texture.height - (row + 1) * height, width, height), new Vector2(.5f, .05f), pixelsPerUnit);
                result[frame].name = prefix + "-" + frame;
            }
            return result;
        }

        // The generated long attack sheets contain a purple studio backdrop. Remove only the edge-connected backdrop;
        // black sprite outlines keep the mage and the bright spell details intact.
        static void ClearPurpleBackdrop(Texture2D texture)
        {
            if (!CleanedAttackSheets.Add(texture)) return;
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width, height = texture.height;
            var clear = new bool[pixels.Length];
            var queue = new Queue<int>();
            void Add(int index)
            {
                if (!clear[index] && PurpleBackdrop(pixels[index])) { clear[index] = true; queue.Enqueue(index); }
            }
            for (int x = 0; x < width; x++) { Add(x); Add((height - 1) * width + x); }
            for (int y = 1; y < height - 1; y++) { Add(y * width); Add(y * width + width - 1); }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                if (x > 0) Add(index - 1);
                if (x < width - 1) Add(index + 1);
                if (index >= width) Add(index - width);
                if (index < pixels.Length - width) Add(index + width);
            }
            for (int i = 0; i < pixels.Length; i++) if (clear[i]) pixels[i].a = 0;
            texture.SetPixels32(pixels); texture.Apply(false, false);
        }

        static bool PurpleBackdrop(Color32 pixel) => pixel.a > 0 && pixel.r > 50 && pixel.b > 45 && pixel.r > pixel.g * 1.16f && pixel.b > pixel.g * 1.05f;
    }
}
