using NUnit.Framework;
using UnityEngine;

namespace ArcaneCode.Tests
{
    public sealed class MageArtTests
    {
        [Test]
        public void ImportedMageKeepsPurpleAndLastFacingWhenStopped()
        {
            var root = new GameObject("Mage art test");
            try
            {
                var actor = WorldArt.Actor(root.transform, "mage", Vector2.zero, Color.red, 1.25f);
                Assert.That(actor.Directional, Is.True, "Imported sheet must be available through Resources");
                Assert.That(actor.Body.color, Is.EqualTo(Color.white), "Element tint must not recolor the purple art");
                Vector2[] directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
                string[] names = { "mage-front", "mage-back", "mage-left", "mage-right" };
                for (int i = 0; i < 4; i++)
                {
                    actor.Face(directions[i]);
                    Assert.That(actor.Body.sprite.name, Is.EqualTo(names[i]));
                    Assert.That(actor.Body.sprite.rect.size, Is.EqualTo(new Vector2(128,160)));
                    actor.Face(Vector2.zero);
                    Assert.That(actor.Body.sprite.name, Is.EqualTo(names[i]));
                }
                WorldArt.Sort(actor, 2, true);
                Assert.That(actor.Body.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(actor.Body.sortingOrder, Is.GreaterThan(actor.Shadow.sortingOrder));
                Assert.That(actor.Body.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
