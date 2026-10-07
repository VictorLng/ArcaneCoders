using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneCode
{
    [Serializable]
    public sealed class SpellDefinition
    {
        public string Id, Label;
        public float Damage, Speed, Radius;
        public bool Area, Ice;
        public Color Color;
    }
    [Serializable]
    public sealed class EnemyDefinition
    {
        public string Label;
        public float Health, Speed, Damage, AttackInterval;
        public int Experience, Coins;
    }
    [CreateAssetMenu(menuName = "Arcane Code/Configuração")]
    public sealed class GameConfig : ScriptableObject
    {
        public float PlayerHealth = 100, PlayerSpeed = 4.4f, Invulnerability = .65f;
        public int InitialBudget = 10, InitialEnergy = 5;
        public float SpellInterval = .6f, ChargeSeconds = .35f;
        public List<SpellDefinition> Spells = new List<SpellDefinition>
        {
            new SpellDefinition { Id = "fireball", Label = "Bola de fogo", Damage = 23, Speed = 10, Radius = .22f, Color = new Color(1,.4f,.12f) },
            new SpellDefinition { Id = "flameWave", Label = "Onda de chamas", Damage = 18, Radius = 3.4f, Area = true, Color = new Color(1,.6f,.15f) },
            new SpellDefinition { Id = "icebolt", Label = "Lança de gelo", Damage = 20, Speed = 11, Radius = .19f, Ice = true, Color = new Color(.3f,.85f,1) },
            new SpellDefinition { Id = "frostNova", Label = "Nova congelante", Damage = 14, Radius = 3.7f, Area = true, Ice = true, Color = new Color(.55f,.8f,1) }
        };
        public EnemyDefinition Melee = new EnemyDefinition { Label = "Esqueleto", Health = 54, Speed = 1.6f, Damage = 12, AttackInterval = 1, Experience = 12, Coins = 3 };
        public EnemyDefinition Bat = new EnemyDefinition { Label = "Morcego", Health = 30, Speed = 3.2f, Damage = 7, AttackInterval = .8f, Experience = 10, Coins = 2 };
        public EnemyDefinition Goblin = new EnemyDefinition { Label = "Goblin", Health = 50, Speed = 1.8f, Damage = 10, AttackInterval = 1, Experience = 13, Coins = 3 };
        public EnemyDefinition Rat = new EnemyDefinition { Label = "Rato", Health = 32, Speed = 2.6f, Damage = 7, AttackInterval = .75f, Experience = 10, Coins = 2 };
        public EnemyDefinition MiniMage = new EnemyDefinition { Label = "Mini mago", Health = 46, Speed = 1.3f, Damage = 12, AttackInterval = 1.65f, Experience = 20, Coins = 5 };
        public EnemyDefinition Spider = new EnemyDefinition { Label = "Aranha", Health = 62, Speed = 2.1f, Damage = 11, AttackInterval = .9f, Experience = 18, Coins = 4 };
        public EnemyDefinition DarkKnight = new EnemyDefinition { Label = "Cavaleiro negro", Health = 120, Speed = 1.45f, Damage = 18, AttackInterval = 1.1f, Experience = 30, Coins = 7 };
        public EnemyDefinition HeadlessKnight = new EnemyDefinition { Label = "Cavaleiro sem cabeça", Health = 138, Speed = 1.55f, Damage = 21, AttackInterval = 1.15f, Experience = 34, Coins = 8 };
        public EnemyDefinition BasiliskFrog = new EnemyDefinition { Label = "Sapo basilisco", Health = 86, Speed = 1.55f, Damage = 15, AttackInterval = 1.7f, Experience = 25, Coins = 6 };
        // Kept so older GameConfig assets still deserialize cleanly; Mini mago and basilisco now use ranged attacks.
        public EnemyDefinition Archer = new EnemyDefinition { Label = "Arqueiro", Health = 42, Speed = 1.1f, Damage = 10, AttackInterval = 1.9f, Experience = 16, Coins = 4 };
        public EnemyDefinition Boss = new EnemyDefinition { Label = "O Rei sem Nome", Health = 620, Speed = .8f, Damage = 18, AttackInterval = 2.5f, Experience = 100, Coins = 45 };
    }
}
