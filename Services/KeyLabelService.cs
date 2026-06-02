using KeyboardTracker.Helpers;

namespace KeyboardTracker.Services;

public static class KeyLabelService
{
    private static readonly Dictionary<uint, string> KnownKeys = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter",
        [0x10] = "Shift", [0x11] = "Ctrl", [0x12] = "Alt",
        [0x13] = "Pause", [0x14] = "CapsLock", [0x1B] = "Esc",
        [0x20] = "Space", [0x21] = "PageUp", [0x22] = "PageDown",
        [0x23] = "End", [0x24] = "Home",
        [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down",
        [0x2C] = "PrintScreen", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x5B] = "LWin", [0x5C] = "RWin", [0x5D] = "Menu",
        [0x70] = "F1", [0x71] = "F2", [0x72] = "F3", [0x73] = "F4",
        [0x74] = "F5", [0x75] = "F6", [0x76] = "F7", [0x77] = "F8",
        [0x78] = "F9", [0x79] = "F10", [0x7A] = "F11", [0x7B] = "F12",
        [0x90] = "NumLock", [0x91] = "ScrollLock",
        [0xA0] = "LShift", [0xA1] = "RShift",
        [0xA2] = "LCtrl", [0xA3] = "RCtrl",
        [0xA4] = "LAlt", [0xA5] = "RAlt",
    };

    private static readonly Dictionary<uint, string> NumberRowMap = new()
    {
        [0x30] = "0", [0x31] = "1", [0x32] = "2", [0x33] = "3", [0x34] = "4",
        [0x35] = "5", [0x36] = "6", [0x37] = "7", [0x38] = "8", [0x39] = "9",
    };

    private static readonly Dictionary<uint, string> LetterMap = new()
    {
        [0x41] = "A", [0x42] = "B", [0x43] = "C", [0x44] = "D", [0x45] = "E",
        [0x46] = "F", [0x47] = "G", [0x48] = "H", [0x49] = "I", [0x4A] = "J",
        [0x4B] = "K", [0x4C] = "L", [0x4D] = "M", [0x4E] = "N", [0x4F] = "O",
        [0x50] = "P", [0x51] = "Q", [0x52] = "R", [0x53] = "S", [0x54] = "T",
        [0x55] = "U", [0x56] = "V", [0x57] = "W", [0x58] = "X", [0x59] = "Y",
        [0x5A] = "Z",
    };

    public static string GetLabel(uint vkCode, bool isExtended)
    {
        // Check known named keys first
        if (KnownKeys.TryGetValue(vkCode, out var name))
            return name;

        // Number row
        if (NumberRowMap.TryGetValue(vkCode, out var num))
            return num;

        // Letters
        if (LetterMap.TryGetValue(vkCode, out var letter))
            return letter;

        // Numpad keys 0-9
        if (vkCode is >= 0x60 and <= 0x69)
            return $"Num{vkCode - 0x60}";

        // Numpad operators
        if (vkCode == 0x6A) return "Num*";
        if (vkCode == 0x6B) return "Num+";
        if (vkCode == 0x6D) return "Num-";
        if (vkCode == 0x6E) return "Num.";
        if (vkCode == 0x6F) return "Num/";

        // OEM keys — try GetKeyNameText
        var label = GetKeyNameTextW(vkCode, isExtended);
        if (!string.IsNullOrEmpty(label))
            return label;

        return $"VK_{vkCode:X2}";
    }

    private static string GetKeyNameTextW(uint vkCode, bool isExtended)
    {
        int scanCode = MapVkToScanCode(vkCode, isExtended);
        int lParam = scanCode << 16;
        if (isExtended) lParam |= 1 << 24;

        char[] buffer = new char[32];
        int len = NativeMethods.GetKeyNameTextW(lParam, buffer, buffer.Length);
        if (len > 0)
            return new string(buffer, 0, len);

        return string.Empty;
    }

    private static int MapVkToScanCode(uint vkCode, bool isExtended)
    {
        return vkCode switch
        {
            0x21 => 0x0149, // PageUp
            0x22 => 0x0151, // PageDown
            0x23 => 0x014F, // End
            0x24 => 0x0147, // Home
            0x25 => 0x014B, // Left
            0x26 => 0x0148, // Up
            0x27 => 0x014D, // Right
            0x28 => 0x0150, // Down
            0x2D => 0x0152, // Insert
            0x2E => 0x0153, // Delete
            _ => isExtended ? (int)(vkCode | 0xE000) : (int)vkCode,
        };
    }
}
