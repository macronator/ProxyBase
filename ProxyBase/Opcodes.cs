namespace ProxyBase
{
    /// <summary>
    /// Server -> client opcodes. Names are the cross-verified set from the protocol RE
    /// (reconciled against the Darkages.exe binary plus the Chaos, pyda and da.js
    /// projects); most are independently confirmed, a few are RE-only best-guesses.
    /// The encoding class per opcode (raw / static-key / dynamic-key) is decided in
    /// <see cref="ServerPacket.ShouldEncrypt"/> and <see cref="ServerPacket.UseDefaultKey"/>.
    /// </summary>
    public enum ServerOpcode : byte
    {
        ConnectionInfo = 0x00,      // encryption/connection info (raw)
        LoginResult = 0x02,         // login result message
        Redirect = 0x03,            // server transfer (login -> world); rewritten to loopback
        Location = 0x04,            // self location
        PlayerId = 0x05,            // self id / world id
        DisplayNpc = 0x07,          // add visible entities to the viewport
        Statistics = 0x08,          // self attributes / statistics
        SystemMessage = 0x0A,       // system / server message (type, length, text)
        MoveClient = 0x0B,          // move self / relocate
        CreatureWalk = 0x0C,        // a creature/player walks (id, x, y, dir)  [RE: MoveCharacter]
        Chat = 0x0D,                // public chat / overhead speech
        RemoveCharacter = 0x0E,     // remove an entity from the viewport
        AddItem = 0x0F,             // add item to an inventory pane
        RemoveItem = 0x10,          // remove item from an inventory pane
        CharacterTurn = 0x11,       // set a creature's facing direction
        HpBar = 0x13,               // creature HP / health bar
        MapInfo = 0x15,             // load map
        AddSpell = 0x17,            // add spell to pane
        RemoveSpell = 0x18,         // remove spell from pane
        SoundEffect = 0x19,         // play sound / music
        BodyAnimation = 0x1A,       // body / creature animation
        NewMap = 0x1F,              // map change complete
        MapData = 0x20,             // map tile rows
        MapDataContinued = 0x21,    // map tile rows (continued)
        SpellAnimation = 0x29,      // spell / skill animation
        AddSkill = 0x2C,            // add skill to pane
        RemoveSkill = 0x2D,         // remove skill from pane
        DisplayWorldMap = 0x2E,     // world map
        DialogueResponse = 0x2F,    // dialog / menu display
        PopupResponse = 0x30,       // popup menu display
        MailMenu = 0x31,            // board / mail menu
        WorldObjectState = 0x32,    // door / world object state
        DisplayPlayer = 0x33,       // display a player (appearance)
        Legend = 0x34,              // other player's profile / legend
        CountryList = 0x36,
        AddAppendage = 0x37,
        RemoveAppendage = 0x38,     // also seen as RefreshResponse
        Profile = 0x39,             // self profile
        SpellBar = 0x3A,
        Heartbeat = 0x3B,           // server heartbeat (client replies 0x45)
        Cooldown = 0x3F,            // ability cooldown
        ExchangeWindow = 0x42,      // trade / exchange window
        ProcessBadGuy = 0x4A,
        CancelTrade = 0x4B,
        LogOffSignal = 0x4C,        // server end / exit signal
        DialogSequence = 0x50,
        DialogSequence2 = 0x51,
        MetafileControl = 0x56,
        ConnectionControl1 = 0x5B,
        Ok = 0x60,                  // login control / acknowledgement
        MetafileControl2 = 0x62,
        GroupRequestPopup = 0x63,
        MapEffect = 0x64,           // map effect / metafile
        ConnectionControl3 = 0x66,
        WorldMapResponse = 0x67,
        SynchronizeTicks = 0x68,    // server ping (client replies 0x75)
        Metafile = 0x6B,
        MiscState = 0x6D,
    }

    /// <summary>
    /// Client -> server opcodes. Names are the cross-verified set from the protocol RE
    /// (binary plus Chaos / pyda / da.js). The encoding class (raw / static-key /
    /// dynamic-key) is authoritative from the binary and is decided in
    /// <see cref="ClientPacket.ShouldEncrypt"/> and <see cref="ClientPacket.UseDefaultKey"/>.
    /// </summary>
    public enum ClientOpcode : byte
    {
        VersionConnect = 0x00,      // version / connect (raw)
        ClientInfo = 0x01,
        CreateCharacterA = 0x02,    // create character, step A
        LogIn = 0x03,               // login credentials
        CreateCharacterB = 0x04,    // create character, step B
        Walk = 0x06,                // request to walk (direction, step)
        Drop = 0x08,                // drop an item (raw)
        LogOut = 0x0B,              // log out / exit
        Speak = 0x0E,               // public chat (type, text)
        UseSpell = 0x0F,            // cast a spell (slot, target, x, y)
        ClientJoin = 0x10,          // encryption key / seed exchange + redirect ack (raw)
        Turn = 0x11,                // turn to face a direction
        Assail = 0x13,              // assail (spacebar)
        DropGold = 0x18,
        Whisper = 0x1B,
        UseItem = 0x1C,
        Message = 0x23,
        RequestProfile = 0x2D,
        Group = 0x2E,
        SwapSlots = 0x30,
        Refresh = 0x38,             // refresh / redraw request
        DialogueSelect = 0x39,      // select an NPC dialog option
        PopupSelect = 0x3A,         // select a popup menu option
        BoardInteract = 0x3B,
        UseSkill = 0x3E,
        WorldMapSelect = 0x3F,
        ClickCharacter = 0x43,
        UnequipGear = 0x44,
        PongA = 0x45,               // heartbeat reply (to server 0x3B)
        RaiseStat = 0x47,
        Exchange = 0x4A,
        SpellLines = 0x4D,          // chant lines
        SkillCaption = 0x4E,
        NpcMenu = 0x57,             // NPC menu interaction
        MetafileRequest = 0x62,
        SocialStatus = 0x73,
        PongB = 0x75,               // ping reply (to server 0x68)
        MetafileRequest2 = 0x7B,
    }
}
