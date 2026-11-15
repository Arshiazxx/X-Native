namespace XNative.Core.Input
{
    /// <summary>
    /// A modest, platform-independent set of keyboard keys. This is a
    /// deliberately small vocabulary — the keys a launcher UI and a
    /// compatibility layer need to name — not a full scan-code table.
    /// Platform backends map their native key codes onto these values
    /// and drop keys that have no equivalent.
    /// </summary>
    public enum KeyboardKey
    {
        /// <summary>No key.</summary>
        None = 0,

        /// <summary>The A key.</summary>
        A = 1,

        /// <summary>The B key.</summary>
        B = 2,

        /// <summary>The C key.</summary>
        C = 3,

        /// <summary>The D key.</summary>
        D = 4,

        /// <summary>The E key.</summary>
        E = 5,

        /// <summary>The F key.</summary>
        F = 6,

        /// <summary>The G key.</summary>
        G = 7,

        /// <summary>The H key.</summary>
        H = 8,

        /// <summary>The I key.</summary>
        I = 9,

        /// <summary>The J key.</summary>
        J = 10,

        /// <summary>The K key.</summary>
        K = 11,

        /// <summary>The L key.</summary>
        L = 12,

        /// <summary>The M key.</summary>
        M = 13,

        /// <summary>The N key.</summary>
        N = 14,

        /// <summary>The O key.</summary>
        O = 15,

        /// <summary>The P key.</summary>
        P = 16,

        /// <summary>The Q key.</summary>
        Q = 17,

        /// <summary>The R key.</summary>
        R = 18,

        /// <summary>The S key.</summary>
        S = 19,

        /// <summary>The T key.</summary>
        T = 20,

        /// <summary>The U key.</summary>
        U = 21,

        /// <summary>The V key.</summary>
        V = 22,

        /// <summary>The W key.</summary>
        W = 23,

        /// <summary>The X key.</summary>
        X = 24,

        /// <summary>The Y key.</summary>
        Y = 25,

        /// <summary>The Z key.</summary>
        Z = 26,

        /// <summary>The 0 digit key.</summary>
        D0 = 27,

        /// <summary>The 1 digit key.</summary>
        D1 = 28,

        /// <summary>The 2 digit key.</summary>
        D2 = 29,

        /// <summary>The 3 digit key.</summary>
        D3 = 30,

        /// <summary>The 4 digit key.</summary>
        D4 = 31,

        /// <summary>The 5 digit key.</summary>
        D5 = 32,

        /// <summary>The 6 digit key.</summary>
        D6 = 33,

        /// <summary>The 7 digit key.</summary>
        D7 = 34,

        /// <summary>The 8 digit key.</summary>
        D8 = 35,

        /// <summary>The 9 digit key.</summary>
        D9 = 36,

        /// <summary>The up arrow key.</summary>
        Up = 37,

        /// <summary>The down arrow key.</summary>
        Down = 38,

        /// <summary>The left arrow key.</summary>
        Left = 39,

        /// <summary>The right arrow key.</summary>
        Right = 40,

        /// <summary>The space bar.</summary>
        Space = 41,

        /// <summary>The Enter / Return key.</summary>
        Enter = 42,

        /// <summary>The Escape key.</summary>
        Escape = 43,

        /// <summary>Either Shift key (backends do not distinguish left/right).</summary>
        Shift = 44,

        /// <summary>Either Control key (backends do not distinguish left/right).</summary>
        Control = 45,

        /// <summary>Either Alt key (backends do not distinguish left/right).</summary>
        Alt = 46,

        /// <summary>The Tab key.</summary>
        Tab = 47,

        /// <summary>The Backspace key.</summary>
        Backspace = 48,
    }
}
