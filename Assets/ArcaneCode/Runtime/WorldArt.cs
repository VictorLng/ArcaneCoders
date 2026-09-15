using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ArcaneCode
{
    public sealed class ActorArt : MonoBehaviour
    {
        public SpriteRenderer Shadow { get; private set; }
        public SpriteRenderer Body { get; private set; }
        public bool Directional { get; private set; }
        public void Initialize(SpriteRenderer shadow, SpriteRenderer body) { Shadow = shadow; Body = body; }
        public void EnableDirections()
        {
            Sprite pose = MageSprites.Facing(Vector2.down);
            if (pose == null) return;
            Directional = true; Body.sprite = pose; Body.color = Color.white;
            Body.transform.localPosition = Vector3.zero;
        }
        public void Face(Vector2 direction)
        {
            if (Directional && direction.sqrMagnitude > .001f) Body.sprite = MageSprites.Facing(direction);
        }
    }

    // Procedural environment/enemies; the mage uses an imported directional sheet.
    public static class WorldArt
    {
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static Material material;
        static Color Ink = new Color(.055f,.07f,.11f);
        public static Sprite Texture(string name)
        {
            if (Sprites.TryGetValue(name, out Sprite found)) return found;
            int size = name == "ring" ? 64 : 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Arcane_" + name, filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                float r = Vector2.Distance(new Vector2(u,v), new Vector2(.5f,.5f));
                Color c = Color.clear;
                switch (name)
                {
                    case "square": c = Color.white; break;
                    case "disc": c = r < .48f ? Color.white : Color.clear; break;
                    case "ring": c = r > .435f && r < .485f ? Color.white : Color.clear; break;
                    case "diamond": c = Math.Abs(u-.5f)+Math.Abs(v-.5f) < .48f ? Color.white : Color.clear; break;
                    case "floor":
                        c = x == 0 || y == 0 ? new Color(.07f,.085f,.13f) : new Color(.13f,.15f,.20f);
                        if ((x * 13 + y * 7) % 59 == 0) c *= 1.18f;
                        if (x == 1 || y == 30) c *= 1.16f;
                        break;
                    case "mage":
                        if (v > .12f && v < .56f && Math.Abs(u-.48f) < .12f + (.56f-v)*.38f) c = Color.white;
                        if (v > .18f && v < .50f && u > .48f && u < .59f) c = new Color(.6f,.65f,.85f);
                        if (v > .49f && v < .65f && u > .36f && u < .64f) c = new Color(.99f,.84f,.67f);
                        if (v > .61f && v < .93f && Math.Abs(u-.49f) < (.96f-v)*.65f) c = Color.white;
                        if (v > .59f && v < .65f && u > .22f && u < .77f) c = Color.white;
                        if (v > .05f && v < .16f && (u > .29f && u < .43f || u > .54f && u < .69f)) c = Ink;
                        if (v > .12f && v < .67f && u > .81f && u < .87f) c = new Color(.6f,.36f,.19f);
                        if (Vector2.Distance(new Vector2(u,v),new Vector2(.84f,.73f)) < .075f) c = new Color(1,.94f,.64f);
                        if (v > .54f && v < .58f && (u > .4f && u < .45f || u > .55f && u < .6f)) c = Ink;
                        break;
                    case "skeleton": case "archer": case "boss":
                        bool skull = ((u-.5f)*(u-.5f)/.046f + (v-.74f)*(v-.74f)/.028f) < 1;
                        if (skull) c = new Color(.87f,.87f,.75f);
                        if (v > .60f && v < .68f && u > .36f && u < .64f) c = new Color(.8f,.8f,.68f);
                        if (v > .22f && v < .59f && u > .45f && u < .54f) c = Color.white;
                        if (v > .30f && v < .56f && (y % 4 < 2) && u > .31f && u < .68f) c = new Color(.8f,.8f,.7f);
                        if (v > .1f && v < .29f && (u > .31f && u < .4f || u > .6f && u < .69f)) c = new Color(.78f,.77f,.65f);
                        if (v > .24f && v < .54f && (u > .23f && u < .29f || u > .72f && u < .78f)) c = new Color(.8f,.8f,.7f);
                        if (v > .72f && v < .79f && (u > .35f && u < .46f || u > .55f && u < .66f)) c = name == "boss" ? new Color(1,.22f,.28f) : Ink;
                        if (name == "archer" && u > .8f && u < .88f && v > .2f && v < .61f) c = new Color(.66f,.38f,.22f);
                        if (name == "boss" && v > .86f && v < .97f && u > .27f && u < .73f && (v < .91f || x % 5 < 3)) c = new Color(1,.72f,.24f);
                        break;
                    case "pillar":
                        if (u > .1f && u < .9f && v > .06f && v < .9f) c = new Color(.30f,.34f,.43f);
                        if (u > .2f && u < .8f && v > .12f && v < .94f) c = new Color(.40f,.44f,.54f);
                        if (u > .25f && u < .35f && v > .16f && v < .85f) c = new Color(.49f,.54f,.64f);
                        if (v > .85f && v < .97f && u > .05f && u < .95f) c = new Color(.51f,.56f,.64f);
                        break;
                }
                tex.SetPixel(x,y,c);
            }
            tex.Apply();
            found = Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(.5f,.5f), size);
            Sprites[name] = found; return found;
        }
        public static SpriteRenderer Draw(Transform parent, string shape, Vector2 position, Vector2 scale, Color color, int order = 0, bool lit = true)
        {
            var obj = new GameObject(shape); obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = Texture(shape); renderer.color = color; renderer.sortingOrder = order;
            if (lit)
            {
                if (material == null) material = Resources.Load<Material>("WorldMaterial");
                if (material != null) renderer.sharedMaterial = material;
            }
            return renderer;
        }
        public static ActorArt Actor(Transform parent, string shape, Vector2 position, Color color, float size)
        {
            var root = new GameObject(shape).transform; root.SetParent(parent); root.position = position;
            SpriteRenderer shadow=Draw(root,"disc",Vector2.zero,new Vector2(size*.68f,size*.22f),new Color(0,0,0,.4f),1,false);
            SpriteRenderer body=Draw(root,shape,new Vector2(0,size*.34f),Vector2.one*size,color,2);
            var art=root.gameObject.AddComponent<ActorArt>(); art.Initialize(shadow,body);
            if (shape == "mage") art.EnableDirections();
            return art;
        }
        public static void Sort(ActorArt actor, float phase, bool moving)
        {
            int order = 100 - Mathf.RoundToInt(actor.transform.position.y * 15);
            actor.Shadow.sortingOrder=order; actor.Body.sortingOrder=order+1;
            if (actor.Directional) return;
            Transform body=actor.Body.transform;
            Vector3 p=body.localPosition; p.y=body.localScale.y*.34f+(moving?Mathf.Sin(phase)*.045f:Mathf.Sin(phase*.4f)*.014f); body.localPosition=p;
        }
        public static void Lamp(Transform parent, Vector2 at, Color color, float radius, float intensity)
        {
            var root = new GameObject("Luz arcana"); root.transform.SetParent(parent); root.transform.localPosition = at;
            var light = root.AddComponent<Light2D>(); light.lightType = Light2D.LightType.Point; light.color = color;
            light.pointLightOuterRadius = radius; light.pointLightInnerRadius = .2f; light.intensity = intensity;
        }
    }
}
