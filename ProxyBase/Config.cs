namespace ProxyBase
{
    /// <summary>
    /// Central configuration for the proxy.
    ///
    /// Edit these values to point the proxy at your own server and client.
    /// The memory-patch offsets below are specific to ONE Darkages.exe build
    /// (this base was made for client 4.21) — a different client version will
    /// require different offsets.
    /// </summary>
    public static class Config
    {
        // ---- Network --------------------------------------------------------

        /// <summary>IP of the real game server the proxy connects out to.</summary>
        public const string RemoteServerIp = "52.88.55.94";

        /// <summary>Port of the real game server.</summary>
        public const int RemoteServerPort = 2610;

        /// <summary>
        /// Local loopback port the patched client is redirected to and that the
        /// proxy listens on. Used by the listener, the client memory patch, and
        /// the server redirect rewrite, so they always stay in sync.
        /// </summary>
        public const int LocalListenPort = 2610;

        // ---- Game client ----------------------------------------------------

        /// <summary>Full path to the Dark Ages client to launch and patch.</summary>
        public const string ClientPath = @"C:\KRU\Dark Ages\Darkages.exe";

        // ---- Client memory patch (SPECIFIC TO ONE Darkages.exe BUILD) -------
        // These offsets redirect the launched client to 127.0.0.1:LocalListenPort.

        /// <summary>Force the client past its server-selection branch (writes 0xEB).</summary>
        public const long PatchForceJump = 0x004333A2;

        /// <summary>Overwrite the connect() IP arguments with 127.0.0.1.</summary>
        public const long PatchConnectIp = 0x004333C2;

        /// <summary>Overwrite the connect() port with LocalListenPort.</summary>
        public const long PatchConnectPort = 0x004333E4;

        /// <summary>Second branch bypass (writes 0xEB).</summary>
        public const long PatchSecondJump = 0x0057A7D9;
    }
}
