namespace ProxyBase
{
    /// <summary>
    /// Server -> client opcodes, recovered from the Darkages.exe protocol RE.
    /// Names follow the reverse-engineered handler/deserializer meanings. This is a
    /// documented reference for writing handlers and is intentionally not exhaustive.
    /// The encoding class per opcode (raw / static-key / dynamic-key) is decided in
    /// <see cref="ServerPacket.ShouldEncrypt"/> and <see cref="ServerPacket.UseDefaultKey"/>.
    /// </summary>
    public enum ServerOpcode : byte
    {
        Redirect = 0x03,            // server transfer (login -> world); rewritten to loopback
        SelfLocation = 0x04,        // self id / location
        PlayerId = 0x05,            // self id / world id
        AddObject = 0x07,           // add object(s) to the viewport
        RemoveObject = 0x08,        // remove object from the viewport
        SystemMessage = 0x0A,       // system / server message (type, length, text)
        SelfProfile = 0x0B,         // self profile / location
        CreatureWalk = 0x0C,        // a creature/player walks (id, x, y, dir)
        BodyAnimation = 0x0D,       // body / creature animation
        PublicChat = 0x0E,          // public chat / overhead speech bubble
        RemoveInventoryItem = 0x0F,
        AddInventoryItem = 0x10,
        CreatureTurn = 0x11,        // set creature facing direction
        HealthBar = 0x13,           // creature HP / health bar
        MapInfo = 0x15,             // load map
        AddSpell = 0x17,
        RemoveSpell = 0x18,
        PlaySound = 0x19,           // play sound / music
        SpellAnimation = 0x1A,      // spell / skill animation
        MapData = 0x20,             // map tile rows
        DisplayEquipment = 0x29,    // equipment / aisling detail
        AddSkill = 0x2C,
        RemoveSkill = 0x2D,
        Dialog = 0x2F,              // dialog / menu
        DialogControl = 0x30,       // dialog / board control
        WorldObjectState = 0x32,    // door / world object state
        DisplayUser = 0x33,         // display a player (appearance)
        SelfProfileFull = 0x39,     // player profile (self)
        TradeWindow = 0x4A,         // trade / exchange window
        CancelTrade = 0x4B,
        MapEffect = 0x64,           // map effect / metafile
        MapWorldList = 0x68,
        Metafile = 0x6B,
    }

    /// <summary>
    /// Client -> server opcodes, recovered from the Darkages.exe send dispatcher.
    /// Meanings are cross-referenced to the public DA protocol; the encoding class
    /// (raw / static-key / dynamic-key) is authoritative from the binary and is decided
    /// in <see cref="ClientPacket.ShouldEncrypt"/> and <see cref="ClientPacket.UseDefaultKey"/>.
    /// Intentionally not exhaustive.
    /// </summary>
    public enum ClientOpcode : byte
    {
        Version = 0x00,             // version / connect (raw)
        LoginRequest = 0x02,        // login request
        Credentials = 0x03,         // login credentials
        Walk = 0x06,                // request to walk (direction, step)
        PublicChat = 0x0E,          // public chat (type, text)
        EncryptionKey = 0x10,       // encryption key / seed exchange (raw) -- see ClientJoin handler
        Turn = 0x11,                // turn to face a direction
        Assail = 0x13,              // assail (spacebar)
        MapChangeRequest = 0x1B,
        Refresh = 0x38,             // refresh / redraw request
        DialogResponse = 0x3A,      // response to an NPC dialog
        KeepAlive = 0x45,           // heartbeat
        Cast = 0x4D,                // chant / cast a spell
        NpcMenu = 0x57,             // NPC menu interaction
        MetafileRequest = 0x7B,
    }
}
