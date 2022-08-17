﻿using UnityEngine;

namespace AvatarImageReader.Enums
{
    public enum Platform
    {
        Android,
        PC
    }

    public enum DataMode
    {
        [InspectorName("UTF16 (Obsolete)")]
        UTF16,
        UTF8,
        [InspectorName("ASCII (Not supported yet)")]
        ASCII = 1,
        [InspectorName("Binary (Not supported yet)")]
        Binary = 1
    }
}
