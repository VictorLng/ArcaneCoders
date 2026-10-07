using System;

namespace ArcaneCode.Core
{
    // Programming belongs to a character class, independently of its equipment.
    [Serializable]
    public sealed class CharacterProgramState
    {
        public string ClassId;
        public string Code = "", Draft = "";
    }

    [Serializable]
    public sealed class SavedCharacterProgram
    {
        public string Name, ClassId, Code;
    }
}
