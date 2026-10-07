using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneCode
{
    public static class EnemySprites
    {
        static readonly Dictionary<string,Sprite[]> Poses = new Dictionary<string,Sprite[]>();
        static readonly HashSet<string> Missing = new HashSet<string>();

        public static bool Available(string id)
        {
            Load(id); return Poses.ContainsKey(id);
        }
        public static Sprite Facing(string id, Vector2 direction)
        {
            if (!Load(id)) return null;
            int index = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? (direction.x < 0 ? 2 : 3) : (direction.y > 0 ? 1 : 0);
            return Poses[id][index];
        }
        static bool Load(string id)
        {
            if (Poses.ContainsKey(id)) return true;
            if (Missing.Contains(id)) return false;
            Texture2D texture=Resources.Load<Texture2D>("Characters/Enemies/"+id+"-directions");
            if (texture==null) { Missing.Add(id); return false; }
            int width=texture.width/4;
            float pixelsPerUnit=texture.height>200 ? 512 : 128;
            var poses=new Sprite[4];
            for (int i=0;i<4;i++)
            {
                poses[i]=Sprite.Create(texture,new Rect(i*width,0,width,texture.height),new Vector2(.5f,.05f),pixelsPerUnit);
                poses[i].name=id+"-"+new[] {"front","back","left","right"}[i];
            }
            Poses[id]=poses; return true;
        }
    }
}
